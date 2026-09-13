using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BattleCity.Characters;
using BattleCity.Core;
using BattleCity.Rendering;
using Godot;

namespace BattleCity.Diagnostics;

/// <summary>
/// Pipeline smoke test (asset-list.md §0, architecture.md §6). Loads the three
/// first-delivery assets if they exist, checks scale, collision, rig, mount and
/// clips against the contract, and prints a report. Missing assets are skipped
/// so the scene runs on a fresh clone; contract violations print
/// <c>SmokeTest FAIL</c> lines, which CI treats as a failure. It also loads the
/// shared toon and hologram materials (issue #27), applies the toon material to
/// every <c>toon_*</c> surface and floats three hologram cards at <see cref="CardSpot"/>;
/// a shader that fails to compile prints an <c>ERROR</c> line, which CI catches
/// even headless.
/// </summary>
public partial class SmokeTestScene : Node3D
{
    private const string FailPrefix = "SmokeTest FAIL";
    private const float CharacterHeight = 1.7f;
    private const float HeightTolerance = 0.1f;
    private const int CharacterTriangleBudget = 12_000;
    private const int DiskTriangleBudget = 1_500;
    private const string MountBone = "LeftLowerArm";
    private const int PoseCheckFrame = 20;

    private static readonly string[] _requiredHumanoidBones =
    {
        "Hips", "Spine", "Chest", "Neck", "Head",
        "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
        "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
        "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
        "RightUpperLeg", "RightLowerLeg", "RightFoot",
    };

    private static readonly string[] _smokeTestClips = { "idle", "walk" };


    [ExportGroup("Asset paths (asset-list.md §0)")]
    [Export]
    public string CubePath { get; set; } = Paths.SmokeCube;

    [Export]
    public string CharacterBodyPath { get; set; } = Paths.SmokeCharacterBody;

    [Export]
    public string CharacterAnimsPath { get; set; } = Paths.CharacterAnims;

    [Export]
    public string DuelDiskPath { get; set; } = Paths.DuelDiskModel;

    [Export]
    public string BodyType { get; set; } = "a";

    [ExportGroup("Scene wiring")]
    [Export]
    public Node3D? CubeSpot { get; set; }

    [Export]
    public Node3D? CharacterSpot { get; set; }

    [Export]
    public Node3D? DiskSpot { get; set; }

    [Export]
    public AnimationPlayer? CharacterAnimations { get; set; }

    [Export]
    public AnimationTree? CharacterTree { get; set; }

    [Export]
    public Label? ReportLabel { get; set; }

    [Export]
    public Camera3D? GameplayCamera { get; set; }

    [Export]
    public Camera3D? InspectCamera { get; set; }

    /// <summary>Where the three sample hologram cards float (player attack, opponent defence, face-down).</summary>
    [Export]
    public Node3D? CardSpot { get; set; }

    [ExportGroup("Playback")]
    [Export(PropertyHint.Range, "0,30,0.5")]
    public float AutoCycleSeconds { get; set; } = 4.0f;

    private readonly List<string> _reportLines = new();
    private int _passCount;
    private int _warnCount;
    private int _failCount;
    private int _skipCount;
    private readonly List<string> _availableClips = new();
    private AnimationNodeStateMachinePlayback? _playback;
    private Skeleton3D? _skeleton;
    private int _framesSincePlay;
    private bool _poseChecked;
    private double _cycleTimer;
    private int _clipIndex;

    public override void _Ready()
    {
        if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--inspect") >= 0 && InspectCamera is not null)
        {
            InspectCamera.Current = true;
        }

        CheckShaders();
        CheckSunShadows();
        Node? cube = LoadAndPlace(CubePath, CubeSpot, "metric cube");
        if (cube is not null)
        {
            CheckCube(cube);
        }

        Node? character = LoadAndPlace(CharacterBodyPath, CharacterSpot, "neutral character");
        Skeleton3D? skeleton = null;
        if (character is not null)
        {
            skeleton = CheckCharacter(character);
        }

        Node? anims = LoadScene(CharacterAnimsPath, "character animations");
        if (anims is not null && skeleton is not null)
        {
            CheckAndWireAnimations(anims, skeleton);
            anims.QueueFree();
        }
        else if (anims is not null)
        {
            Skip("animations: character body missing, clips not wired");
            anims.QueueFree();
        }

        Node? disk = LoadAndPlace(DuelDiskPath, DiskSpot, "duel disk blockout");
        if (disk is not null)
        {
            CheckDisk(disk);
            if (skeleton is not null)
            {
                MountDisk(skeleton);
            }
            else
            {
                Skip("disk mount: character body missing");
            }
        }

        PrintSummary();
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed(InputActions.Interact))
        {
            NextClip();
        }

        if (Input.IsActionJustPressed(InputActions.Menu) && GameplayCamera is not null && InspectCamera is not null)
        {
            if (GameplayCamera.Current)
            {
                InspectCamera.MakeCurrent();
            }
            else
            {
                GameplayCamera.MakeCurrent();
            }
        }

        CheckSkeletonMoves();
        if (_playback is not null && AutoCycleSeconds > 0.0f)
        {
            _cycleTimer += delta;
            if (_cycleTimer >= AutoCycleSeconds)
            {
                _cycleTimer = 0.0;
                NextClip();
            }
        }
    }

    /// <summary>Target of the method tracks injected from data/rig/animation_events.json.</summary>
    public void OnAnimationEvent(string name) => LogEvent(name);

    private void LogEvent(string name)
    {
        string clip = _playback?.GetCurrentNode().ToString() ?? "?";
        GD.Print($"SmokeTest event: {name} (clip {clip})");
    }

    private Node? LoadAndPlace(string path, Node3D? spot, string label)
    {
        Node? instance = LoadScene(path, label);
        if (instance is null)
        {
            return null;
        }

        Node parent = spot ?? this;
        parent.AddChild(instance);
        return instance;
    }

    private Node? LoadScene(string path, string label)
    {
        if (!ResourceLoader.Exists(path))
        {
            Skip($"{label}: not delivered yet ({path})");
            return null;
        }

        var packed = ResourceLoader.Load<PackedScene>(path);
        if (packed is null)
        {
            Fail($"{label}: {path} exists but did not import as a scene");
            return null;
        }

        Node instance = packed.Instantiate();
        Pass($"{label}: loaded {path}");
        return instance;
    }

    private void CheckCube(Node cube)
    {
        Aabb? bounds = LocalBounds(cube);
        if (bounds is null)
        {
            Fail("cube: no mesh found");
            return;
        }

        Aabb b = bounds.Value;
        Check(Approximately(b.Size, Vector3.One, 0.01f), $"cube: size {Fmt(b.Size)} (expected 1 × 1 × 1 m)", fail: true);
        Check(Approximately(b.Position, Vector3.Zero, 0.01f),
            $"cube: origin at bounds-min corner, min {Fmt(b.Position)} (kit pieces, architecture §6.1)", fail: false);

        int shapes = CountNodes<CollisionShape3D>(cube);
        Check(shapes > 0, $"cube: {shapes} collision shape(s) from the -col suffix", fail: true);
        CheckToonMaterials(cube, "cube");
    }

    private Skeleton3D? CheckCharacter(Node character)
    {
        Skeleton3D? skeleton = FindNode<Skeleton3D>(character);
        if (skeleton is null)
        {
            Fail("character: no Skeleton3D in the body file");
            return null;
        }

        var missing = new List<string>();
        foreach (string bone in _requiredHumanoidBones)
        {
            if (skeleton.FindBone(bone) < 0)
            {
                missing.Add(bone);
            }
        }

        Check(missing.Count == 0,
            missing.Count == 0
                ? $"character: {skeleton.GetBoneCount()} bones, SkeletonProfileHumanoid names present"
                : $"character: humanoid bones missing: {string.Join(", ", missing)}",
            fail: true);
        Check(skeleton.FindBone(MountBone) >= 0, $"character: {MountBone} bone present for the disk mount", fail: true);

        Aabb? bounds = LocalBounds(character);
        if (bounds is null)
        {
            Fail("character: no mesh found");
        }
        else
        {
            float top = bounds.Value.End.Y;
            float feet = bounds.Value.Position.Y;
            Check(Mathf.Abs(top - CharacterHeight) <= HeightTolerance,
                FormattableString.Invariant($"character: height {top:F2} m (expected {CharacterHeight:F1} ± {HeightTolerance:F1})"), fail: true);
            Check(Mathf.Abs(feet) <= 0.05f, FormattableString.Invariant($"character: feet at y = {feet:F2} (origin at the feet)"), fail: false);
        }

        int tris = CountTriangles(character);
        Check(tris <= CharacterTriangleBudget, $"character: {tris} triangles (budget {CharacterTriangleBudget})", fail: false);
        CheckToonMaterials(character, "character");
        return skeleton;
    }

    private void CheckAndWireAnimations(Node anims, Skeleton3D skeleton)
    {
        AnimationPlayer? source = FindNode<AnimationPlayer>(anims);
        if (source is null)
        {
            Fail("animations: no AnimationPlayer in character_anims.glb");
            return;
        }

        if (CharacterAnimations is null || CharacterTree is null)
        {
            Fail("animations: scene is missing CharacterAnimations or CharacterTree");
            return;
        }

        AnimationLibrary library = AnimationRetarget.CopyClips(source, GetPathTo(skeleton));
        foreach (StringName clip in library.GetAnimationList())
        {
            _availableClips.Add(clip.ToString());
        }

        CharacterAnimations.AddAnimationLibrary("", library);
        Pass($"animations: clips {string.Join(", ", _availableClips)}");

        bool allPresent = true;
        foreach (string clip in _smokeTestClips)
        {
            if (!library.HasAnimation(clip))
            {
                allPresent = false;
                Fail($"animations: clip '{clip}' missing (asset-list §0.2)");
                continue;
            }

            Animation animation = library.GetAnimation(clip);
            animation.LoopMode = Animation.LoopModeEnum.Linear;
            float expected = clip == "idle" ? 4.0f : 1.0f;
            Check(Mathf.Abs(animation.Length - expected) <= 0.25f,
                FormattableString.Invariant($"animations: '{clip}' length {animation.Length:F2} s (asset-list §2.9 says {expected:F0} s loop)"), fail: false);
        }

        // glTF has no method tracks, so clips arrive without events; they are
        // injected from data (systems.md §3.2) exactly as Character.tscn does.
        List<string> injected = AnimationRetarget.InjectEvents(
            library, AnimationRetarget.LoadEventTable(Paths.AnimationEventsData));
        Check(injected.Count > 0, injected.Count > 0
            ? $"animations: events injected from data: {string.Join(", ", injected)}"
            : $"animations: no events injected ({Paths.AnimationEventsData} has none for the delivered clips)", fail: false);

        if (!allPresent)
        {
            return;
        }

        CharacterTree.Active = true;
        _playback = CharacterTree.Get("parameters/playback").As<AnimationNodeStateMachinePlayback>();
        if (_playback is null)
        {
            Fail("animations: AnimationTree has no state machine playback");
            return;
        }

        _playback.Start("idle");
        _skeleton = skeleton;
        Pass("animations: AnimationTree active, playing idle (interact toggles idle/walk)");
    }

    private void CheckDisk(Node disk)
    {
        int tris = CountTriangles(disk);
        Check(tris <= DiskTriangleBudget, $"disk: {tris} triangles (budget {DiskTriangleBudget})", fail: false);

        AnimationPlayer? player = FindNode<AnimationPlayer>(disk);
        var clips = new List<string>();
        if (player is not null)
        {
            foreach (StringName libraryName in player.GetAnimationLibraryList())
            {
                foreach (StringName name in player.GetAnimationLibrary(libraryName).GetAnimationList())
                {
                    clips.Add(AnimationRetarget.ClipName(name.ToString()));
                }
            }
        }

        Info(clips.Count == 0
            ? "disk: no animations (folded/deployed poses can come later, asset-list §3.2)"
            : $"disk: animations {string.Join(", ", clips)}");
        CheckToonMaterials(disk, "disk");
    }

    private void MountDisk(Skeleton3D skeleton)
    {
        if (skeleton.FindBone(MountBone) < 0)
        {
            Skip($"disk mount: {MountBone} missing on the character");
            return;
        }

        var packed = ResourceLoader.Load<PackedScene>(DuelDiskPath);
        if (packed is null)
        {
            return;
        }

        var attachment = new BoneAttachment3D { Name = "DuelDiskMount", BoneName = MountBone };
        skeleton.AddChild(attachment);
        Node mounted = packed.Instantiate();
        attachment.AddChild(mounted);
        if (mounted is Node3D mounted3D)
        {
            mounted3D.Transform = LoadMountOffset();
            ToonMaterials.Apply(mounted3D);
        }

        Pass($"disk mount: attached to {MountBone} with the {BodyType} offset from {Paths.DiskMountData}");
    }

    private Transform3D LoadMountOffset()
    {
        if (!FileAccess.FileExists(Paths.DiskMountData))
        {
            Warn($"disk mount: {Paths.DiskMountData} missing, using identity offset");
        }

        return AnimationRetarget.LoadDiskMountOffset(Paths.DiskMountData, BodyType);
    }

    private void CheckToonMaterials(Node root, string label)
    {
        int toon = 0;
        int other = 0;
        foreach (MeshInstance3D mesh in FindAll<MeshInstance3D>(root))
        {
            if (mesh.Mesh is null)
            {
                continue;
            }

            for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                Material? material = mesh.Mesh.SurfaceGetMaterial(surface);
                if (material is not null && ToonMaterials.IsToonNamed(material))
                {
                    toon++;
                }
                else
                {
                    other++;
                }
            }
        }

        int converted = ToonMaterials.Apply(root);
        Check(toon > 0, $"{label}: {toon} toon_ surface(s), {other} other (toon_ prefix gets the shared toon material, architecture §6.4)",
            fail: false);
        Check(converted == toon, $"{label}: {converted} surface(s) now use shaders/toon.gdshader with the outline pass", fail: true);
    }

    /// <summary>The scene's sun gets the shared shadow setup, as levels do in Game.LoadLevel.</summary>
    private void CheckSunShadows()
    {
        int suns = SunShadows.Apply(this);
        Check(suns >= 1, $"sun shadows: {suns} directional light(s) configured (SunShadows, orthogonal {SunShadows.MaxDistance:0} m)", fail: true);
        var sun = GetNodeOrNull<DirectionalLight3D>("Sun");
        Check(sun is { DirectionalShadowMode: DirectionalLight3D.ShadowMode.Orthogonal }, "sun shadows: Sun uses orthogonal mode after Apply", fail: true);
    }

    /// <summary>
    /// Loads the shared materials (issue #27), checks the uniforms code relies on
    /// exist, and floats three sample cards so the hologram look can be reviewed
    /// next to the character. Shader compile errors surface as ERROR lines.
    /// </summary>
    private void CheckShaders()
    {
        ShaderMaterial? toon = ToonMaterials.Base;
        Check(toon?.Shader is not null, $"shaders: {Paths.ToonMaterial} loads with its shader", fail: true);
        Check(toon?.NextPass is ShaderMaterial, $"shaders: toon material has the outline pass ({Paths.OutlineMaterial})", fail: true);
        if (toon?.Shader is Shader toonShader)
        {
            CheckUniforms(toonShader, "toon", ToonParameters.Albedo, ToonParameters.AlbedoTexture, "bands", "shade_floor", "rim_strength");
        }

        ShaderMaterial? player = HologramCards.SideMaterial(HologramSide.Player);
        ShaderMaterial? opponent = HologramCards.SideMaterial(HologramSide.Opponent);
        Check(player?.Shader is not null && opponent?.Shader is not null,
            "shaders: hologram_player.tres and hologram_opponent.tres load with shaders/hologram.gdshader", fail: true);
        if (player?.Shader is Shader hologram)
        {
            CheckUniforms(hologram, "hologram", "face_texture", "back_texture", "edge_color", "edge_width", "scanline_density");
            CheckInstanceUniforms(hologram, "hologram",
                HologramCards.HoverPhase, HologramCards.Selected, HologramCards.Reveal, HologramCards.Dissolve);
        }

        if (CardSpot is null)
        {
            Skip("hologram cards: no CardSpot in the scene");
            return;
        }

        Texture2D? normal = LoadTexture($"{Paths.CardFrames}/frame_normal.png");
        Texture2D? effect = LoadTexture($"{Paths.CardFrames}/frame_effect.png");
        MeshInstance3D attack = HologramCards.Create(HologramSide.Player, normal, 0.0f);
        attack.Position = new Vector3(-0.3f, 0.0f, 0.0f);
        MeshInstance3D defence = HologramCards.Create(HologramSide.Opponent, effect, 0.33f);
        defence.Position = Vector3.Zero;
        defence.RotationDegrees = new Vector3(0.0f, 0.0f, 90.0f);
        MeshInstance3D faceDown = HologramCards.Create(HologramSide.Opponent, effect, 0.66f);
        faceDown.Position = new Vector3(0.3f, 0.0f, 0.0f);
        faceDown.RotationDegrees = new Vector3(0.0f, 180.0f, 0.0f);
        HologramCards.SetSelected(attack, true);
        CardSpot.AddChild(attack);
        CardSpot.AddChild(defence);
        CardSpot.AddChild(faceDown);
        Info(FormattableString.Invariant(
            $"hologram cards: 3 at CardSpot, {HologramCards.Width:F2} × {HologramCards.Height:F2} m (attack selected, defence, face-down), frames {(normal is null ? "missing" : "from assets/cards/frames")}"));
    }

    private void CheckUniforms(Shader shader, string label, params string[] expected)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Godot.Collections.Dictionary uniform in shader.GetShaderUniformList())
        {
            names.Add(uniform["name"].AsString());
        }

        var missing = new List<string>();
        foreach (string name in expected)
        {
            if (!names.Contains(name))
            {
                missing.Add(name);
            }
        }

        Check(missing.Count == 0, missing.Count == 0
            ? $"shaders: {label} exposes {string.Join(", ", expected)}"
            : $"shaders: {label} is missing uniform(s) {string.Join(", ", missing)}", fail: true);
    }

    /// <summary>Instance uniforms are not in the uniform list (they live per instance), so the source is checked.</summary>
    private void CheckInstanceUniforms(Shader shader, string label, params string[] expected)
    {
        var missing = new List<string>();
        foreach (string name in expected)
        {
            if (!shader.Code.Contains($"instance uniform float {name}", StringComparison.Ordinal))
            {
                missing.Add(name);
            }
        }

        Check(missing.Count == 0, missing.Count == 0
            ? $"shaders: {label} exposes instance uniforms {string.Join(", ", expected)}"
            : $"shaders: {label} is missing instance uniform(s) {string.Join(", ", missing)}", fail: true);
    }

    private static Texture2D? LoadTexture(string path) =>
        ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;

    /// <summary>Bounds of every mesh under <paramref name="root"/>, in the root's local space.</summary>
    private static Aabb? LocalBounds(Node root)
    {
        if (root is not Node3D root3D)
        {
            return null;
        }

        Transform3D inverse = root3D.GlobalTransform.AffineInverse();
        Aabb? result = null;
        foreach (MeshInstance3D mesh in FindAll<MeshInstance3D>(root))
        {
            if (mesh.Mesh is null)
            {
                continue;
            }

            Aabb local = (inverse * mesh.GlobalTransform) * mesh.GetAabb();
            result = result is null ? local : result.Value.Merge(local);
        }

        return result;
    }

    private static int CountTriangles(Node root)
    {
        int triangles = 0;
        foreach (MeshInstance3D instance in FindAll<MeshInstance3D>(root))
        {
            Mesh? mesh = instance.Mesh;
            if (mesh is null)
            {
                continue;
            }

            for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                Godot.Collections.Array arrays = mesh.SurfaceGetArrays(surface);
                Variant index = arrays[(int)Mesh.ArrayType.Index];
                if (index.VariantType != Variant.Type.Nil && index.AsInt32Array().Length > 0)
                {
                    triangles += index.AsInt32Array().Length / 3;
                }
                else
                {
                    triangles += arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length / 3;
                }
            }
        }

        return triangles;
    }

    private static int CountNodes<T>(Node root) where T : Node
    {
        int count = 0;
        foreach (T _ in FindAll<T>(root))
        {
            count++;
        }

        return count;
    }

    private static T? FindNode<T>(Node root) where T : Node
    {
        foreach (T node in FindAll<T>(root))
        {
            return node;
        }

        return null;
    }

    private static IEnumerable<T> FindAll<T>(Node root) where T : Node
    {
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            Node current = stack.Pop();
            if (current is T match)
            {
                yield return match;
            }

            foreach (Node child in current.GetChildren())
            {
                stack.Push(child);
            }
        }
    }

    /// <summary>
    /// A few frames into the walk clip, at least one bone must have left its
    /// rest pose; otherwise the clips did not reach this skeleton.
    /// </summary>
    private void CheckSkeletonMoves()
    {
        if (_poseChecked || _playback is null || _skeleton is null)
        {
            return;
        }

        _framesSincePlay++;
        if (_framesSincePlay == 2)
        {
            _playback.Travel("walk");
        }

        if (_framesSincePlay < PoseCheckFrame)
        {
            return;
        }

        _poseChecked = true;
        bool moved = false;
        for (int bone = 0; bone < _skeleton.GetBoneCount() && !moved; bone++)
        {
            Quaternion pose = _skeleton.GetBonePoseRotation(bone);
            Quaternion rest = _skeleton.GetBoneRest(bone).Basis.GetRotationQuaternion();
            moved = !pose.IsEqualApprox(rest);
        }

        Check(moved, $"animations: skeleton pose changed after {PoseCheckFrame} frames of 'walk'", fail: true);
        PrintSummary();
    }

    private void NextClip()
    {
        if (_playback is null)
        {
            return;
        }

        _clipIndex = (_clipIndex + 1) % _smokeTestClips.Length;
        _playback.Travel(_smokeTestClips[_clipIndex]);
        _cycleTimer = 0.0;
    }

    private static bool Approximately(Vector3 a, Vector3 b, float tolerance)
    {
        return Mathf.Abs(a.X - b.X) <= tolerance && Mathf.Abs(a.Y - b.Y) <= tolerance && Mathf.Abs(a.Z - b.Z) <= tolerance;
    }

    private static string Fmt(Vector3 v) => FormattableString.Invariant($"({v.X:F2}, {v.Y:F2}, {v.Z:F2})");

    private void Check(bool ok, string message, bool fail)
    {
        if (ok)
        {
            Pass(message);
        }
        else if (fail)
        {
            Fail(message);
        }
        else
        {
            Warn(message);
        }
    }

    private void Pass(string message)
    {
        _passCount++;
        Report("PASS", message);
    }

    private void Warn(string message)
    {
        _warnCount++;
        Report("WARN", message);
        GD.PushWarning($"SmokeTest: {message}");
    }

    private void Fail(string message)
    {
        _failCount++;
        Report("FAIL", message);
        GD.PrintErr($"{FailPrefix}: {message}");
    }

    private void Skip(string message)
    {
        _skipCount++;
        Report("SKIP", message);
    }

    private void Info(string message) => Report("INFO", message);

    private void Report(string status, string message)
    {
        _reportLines.Add($"[{status}] {message}");
        GD.Print($"SmokeTest {status}: {message}");
    }

    private void PrintSummary()
    {
        string summary = $"SmokeTest summary: {_passCount} pass, {_warnCount} warn, {_failCount} fail, {_skipCount} skipped";
        GD.Print(summary);
        if (ReportLabel is not null)
        {
            var text = new StringBuilder();
            text.AppendLine(summary);
            foreach (string line in _reportLines)
            {
                text.AppendLine(line);
            }

            ReportLabel.Text = text.ToString();
        }
    }
}
