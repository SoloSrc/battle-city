using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using BattleCity.Core;
using Godot;

namespace BattleCity.Diagnostics;

/// <summary>
/// Pipeline smoke test (asset-list.md §0, architecture.md §6). Loads the three
/// first-delivery assets if they exist, checks scale, collision, rig, mount and
/// clips against the contract, and prints a report. Missing assets are skipped
/// so the scene runs on a fresh clone; contract violations print
/// <c>SmokeTest FAIL</c> lines, which CI treats as a failure.
/// </summary>
public partial class SmokeTestScene : Node3D
{
    private const string FailPrefix = "SmokeTest FAIL";
    private const float CharacterHeight = 1.7f;
    private const float HeightTolerance = 0.1f;
    private const int CharacterTriangleBudget = 12_000;
    private const int DiskTriangleBudget = 1_500;
    private const string MountBone = "LeftLowerArm";
    private const string ToonMaterialPrefix = "toon_";
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

    private static readonly string[] _knownAnimationEvents =
    {
        "footstep_l", "footstep_r", "disk_deploy", "card_draw", "card_release", "card_to_grave", "hit",
    };

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

    /// <summary>
    /// Material applied to every surface whose glTF material name starts with
    /// <c>toon_</c> (architecture.md §6.4). A placeholder until issue #27 lands
    /// the real toon shader; other materials stay as imported for review.
    /// </summary>
    [Export]
    public Material? ToonMaterial { get; set; }

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

    // Animation call-method events (systems.md §3.2). Method names must match
    // the event names in the clips, so they are snake_case on purpose.
#pragma warning disable IDE1006 // Naming Styles
    public void footstep_l() => LogEvent("footstep_l");

    public void footstep_r() => LogEvent("footstep_r");

    public void disk_deploy() => LogEvent("disk_deploy");

    public void card_draw() => LogEvent("card_draw");

    public void card_release() => LogEvent("card_release");

    public void card_to_grave() => LogEvent("card_to_grave");

    public void hit() => LogEvent("hit");
#pragma warning restore IDE1006

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

        var library = new AnimationLibrary();
        NodePath skeletonPath = GetPathTo(skeleton);
        foreach (StringName libraryName in source.GetAnimationLibraryList())
        {
            AnimationLibrary sourceLibrary = source.GetAnimationLibrary(libraryName);
            foreach (StringName animationName in sourceLibrary.GetAnimationList())
            {
                string clip = ClipName(animationName.ToString());
                var animation = (Animation)sourceLibrary.GetAnimation(animationName).Duplicate(true);
                RetargetTracks(animation, skeletonPath);
                library.AddAnimation(clip, animation);
                _availableClips.Add(clip);
            }
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

        if (library.HasAnimation("walk"))
        {
            // glTF has no method tracks, so exported clips arrive without events.
            // Issue #19 injects them from data (clip → event → time); this only reports.
            List<string> events = MethodEvents(library.GetAnimation("walk"));
            Info(events.Count == 0
                ? "animations: 'walk' carries no call-method events (expected: none from glTF; #19 adds footstep_l/footstep_r from data)"
                : $"animations: 'walk' events {string.Join(", ", events)}");
        }

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
                    clips.Add(ClipName(name.ToString()));
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
            ApplyToonMaterial(mounted3D);
        }

        Pass($"disk mount: attached to {MountBone} with the {BodyType} offset from {Paths.DiskMountData}");
    }

    private Transform3D LoadMountOffset()
    {
        if (!FileAccess.FileExists(Paths.DiskMountData))
        {
            Warn($"disk mount: {Paths.DiskMountData} missing, using identity offset");
            return Transform3D.Identity;
        }

        try
        {
            using var doc = JsonDocument.Parse(FileAccess.GetFileAsString(Paths.DiskMountData));
            if (!doc.RootElement.TryGetProperty(BodyType, out JsonElement body))
            {
                Warn($"disk mount: no entry for body type '{BodyType}', using identity offset");
                return Transform3D.Identity;
            }

            Vector3 position = ReadVector(body, "position");
            Vector3 rotation = ReadVector(body, "rotation_degrees");
            var basis = Basis.FromEuler(new Vector3(
                Mathf.DegToRad(rotation.X), Mathf.DegToRad(rotation.Y), Mathf.DegToRad(rotation.Z)));
            return new Transform3D(basis, position);
        }
        catch (JsonException e)
        {
            Fail($"disk mount: {Paths.DiskMountData} is not valid JSON ({e.Message})");
            return Transform3D.Identity;
        }
    }

    private static Vector3 ReadVector(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out JsonElement array) || array.GetArrayLength() != 3)
        {
            return Vector3.Zero;
        }

        return new Vector3(array[0].GetSingle(), array[1].GetSingle(), array[2].GetSingle());
    }

    /// <summary>
    /// Points bone tracks at the smoke-test character's skeleton and method
    /// tracks at this node, so the shared-rig clips drive whichever body was
    /// loaded (architecture §6.2: one anims file, applied through the library).
    /// </summary>
    private void RetargetTracks(Animation animation, NodePath skeletonPath)
    {
        string skeletonPathText = skeletonPath.ToString();
        for (int i = 0; i < animation.GetTrackCount(); i++)
        {
            NodePath path = animation.TrackGetPath(i);
            switch (animation.TrackGetType(i))
            {
                case Animation.TrackType.Position3D:
                case Animation.TrackType.Rotation3D:
                case Animation.TrackType.Scale3D:
                    if (path.GetSubNameCount() > 0)
                    {
                        string bone = path.GetSubName(path.GetSubNameCount() - 1);
                        animation.TrackSetPath(i, new NodePath($"{skeletonPathText}:{bone}"));
                    }

                    break;
                case Animation.TrackType.Method:
                    animation.TrackSetPath(i, new NodePath("."));
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>Blender action <c>anim_walk</c> becomes clip <c>walk</c> (architecture §6.3).</summary>
    private static string ClipName(string animationName)
    {
        const string prefix = "anim_";
        return animationName.StartsWith(prefix, StringComparison.Ordinal)
            ? animationName[prefix.Length..]
            : animationName;
    }

    private static List<string> MethodEvents(Animation animation)
    {
        var events = new List<string>();
        for (int track = 0; track < animation.GetTrackCount(); track++)
        {
            if (animation.TrackGetType(track) != Animation.TrackType.Method)
            {
                continue;
            }

            for (int key = 0; key < animation.TrackGetKeyCount(track); key++)
            {
                string name = animation.MethodTrackGetName(track, key).ToString();
                if (!events.Contains(name))
                {
                    events.Add(name);
                }
            }
        }

        foreach (string name in events)
        {
            if (Array.IndexOf(_knownAnimationEvents, name) < 0)
            {
                GD.Print($"SmokeTest: unknown animation event '{name}' (systems.md §3.2 lists the engine hooks)");
            }
        }

        return events;
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
                string name = material?.ResourceName ?? string.Empty;
                if (name.StartsWith(ToonMaterialPrefix, StringComparison.Ordinal))
                {
                    toon++;
                    if (ToonMaterial is not null)
                    {
                        mesh.SetSurfaceOverrideMaterial(surface, ToonMaterial);
                    }
                }
                else
                {
                    other++;
                }
            }
        }

        Check(toon > 0, $"{label}: {toon} toon_ surface(s), {other} other (toon_ prefix gets the shared toon material, architecture §6.4)",
            fail: false);
    }

    private void ApplyToonMaterial(Node root)
    {
        if (ToonMaterial is null)
        {
            return;
        }

        foreach (MeshInstance3D mesh in FindAll<MeshInstance3D>(root))
        {
            if (mesh.Mesh is null)
            {
                continue;
            }

            for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                string name = mesh.Mesh.SurfaceGetMaterial(surface)?.ResourceName ?? string.Empty;
                if (name.StartsWith(ToonMaterialPrefix, StringComparison.Ordinal))
                {
                    mesh.SetSurfaceOverrideMaterial(surface, ToonMaterial);
                }
            }
        }
    }

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
