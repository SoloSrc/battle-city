using System.Collections.Generic;
using BattleCity.Core;
using Godot;

namespace BattleCity.Characters;

/// <summary>
/// The one character scene for the avatar, NPCs and duelists (systems.md §3.1).
/// Loads a body and the shared clip carrier at runtime, keeps a humanoid
/// skeleton available even before art exists, mounts the duel disk on
/// <c>LeftLowerArm</c>, and drives the AnimationTree through named parameters.
/// A controller (issue #20) sets velocity and calls <see cref="SetLocomotion"/>.
/// </summary>
public partial class Character : CharacterBody3D
{
    public const string MountBone = "LeftLowerArm";
    public const string LocomotionState = "Locomotion";
    public const string TalkState = "Talk";
    public const string DuelReadyState = "DuelReady";
    public const string DuelIdleState = "DuelIdle";
    public const string DrawCardState = "DrawCard";
    public const string PlayCardState = "PlayCard";
    public const string TakeDamageState = "TakeDamage";
    public const string WinState = "Win";
    public const string LoseState = "Lose";

    private const string PlaybackParameter = "parameters/playback";
    private const string SpeedParameter = "parameters/Locomotion/blend_position";

    /// <summary>Raised for every call-method event in the clips (systems.md §3.2).</summary>
    [Signal]
    public delegate void AnimationEventEventHandler(string name);

    [ExportGroup("Appearance")]
    [Export]
    public string BodyPath { get; set; } = Paths.SmokeCharacterBody;

    [Export]
    public string AnimationsPath { get; set; } = Paths.CharacterAnims;

    [Export]
    public string BodyType { get; set; } = "a";

    [Export]
    public bool EquipDuelDisk { get; set; } = true;

    [Export]
    public PackedScene? DuelDiskScene { get; set; }

    [ExportGroup("Scene wiring")]
    [Export]
    public Node3D? Rig { get; set; }

    [Export]
    public MeshInstance3D? Placeholder { get; set; }

    [Export]
    public AnimationPlayer? Animations { get; set; }

    [Export]
    public AnimationTree? Tree { get; set; }

    public Skeleton3D? Skeleton { get; private set; }

    public BoneAttachment3D? DuelDiskMount { get; private set; }

    public DuelDisk? Disk { get; private set; }

    public bool HasBody { get; private set; }

    public bool HasClips { get; private set; }

    /// <summary>Clips aliased to a stand-in because they were not delivered yet.</summary>
    public IReadOnlyList<string> ClipAliases => _clipAliases;

    /// <summary>Events injected from data, as "clip:event@seconds".</summary>
    public IReadOnlyList<string> InjectedEvents => _injectedEvents;

    public string CurrentState => _playback?.GetCurrentNode().ToString() ?? string.Empty;

    private readonly List<string> _clipAliases = new();
    private readonly List<string> _injectedEvents = new();
    private AnimationNodeStateMachinePlayback? _playback;

    public override void _Ready()
    {
        if (Rig is null || Animations is null || Tree is null)
        {
            GD.PushError("Character: Rig, Animations and Tree must be wired in the scene");
            return;
        }

        Skeleton = LoadBody() ?? BuildPlaceholderSkeleton();
        if (Placeholder is not null)
        {
            Placeholder.Visible = !HasBody;
        }

        BuildLibrary();
        MountDisk();

        Tree.Active = true;
        _playback = Tree.Get(PlaybackParameter).As<AnimationNodeStateMachinePlayback>();
        _playback?.Start(LocomotionState);
        SetLocomotion(0.0f);
    }

    /// <summary>Blends idle → walk → run by ground speed in m/s (systems.md §4.1 values).</summary>
    public void SetLocomotion(float speed)
    {
        Tree?.Set(SpeedParameter, speed);
    }

    /// <summary>Travels to a state of the AnimationTree state machine (see the *State constants).</summary>
    public void PlayState(string state)
    {
        _playback?.Travel(state);
    }

    /// <summary>Called by injected method tracks; re-emitted as <see cref="AnimationEvent"/>.</summary>
    public void OnAnimationEvent(string name)
    {
        EmitSignal(SignalName.AnimationEvent, name);
    }

    private Skeleton3D? LoadBody()
    {
        if (Rig is null || !ResourceLoader.Exists(BodyPath))
        {
            return null;
        }

        var packed = ResourceLoader.Load<PackedScene>(BodyPath);
        if (packed is null)
        {
            GD.PushError($"Character: {BodyPath} did not import as a scene");
            return null;
        }

        Node body = packed.Instantiate();
        body.Name = "Body";
        Rig.AddChild(body);
        Skeleton3D? skeleton = FindSkeleton(body);
        if (skeleton is null)
        {
            GD.PushError($"Character: {BodyPath} has no Skeleton3D");
            body.QueueFree();
            return null;
        }

        HasBody = true;
        return skeleton;
    }

    /// <summary>
    /// A skeleton with SkeletonProfileHumanoid names and reference poses so the
    /// disk mount and clip retargeting work before a body is delivered.
    /// </summary>
    private Skeleton3D BuildPlaceholderSkeleton()
    {
        var profile = new SkeletonProfileHumanoid();
        var skeleton = new Skeleton3D { Name = "Skeleton3D" };
        for (int i = 0; i < profile.BoneSize; i++)
        {
            skeleton.AddBone(profile.GetBoneName(i));
        }

        for (int i = 0; i < profile.BoneSize; i++)
        {
            int parent = skeleton.FindBone(profile.GetBoneParent(i));
            if (parent >= 0)
            {
                skeleton.SetBoneParent(i, parent);
            }

            skeleton.SetBoneRest(i, profile.GetReferencePose(i));
        }

        skeleton.ResetBonePoses();
        Rig!.AddChild(skeleton);
        return skeleton;
    }

    private void BuildLibrary()
    {
        AnimationLibrary library;
        NodePath skeletonPath = GetPathTo(Skeleton!);
        if (ResourceLoader.Exists(AnimationsPath) && ResourceLoader.Load<PackedScene>(AnimationsPath) is PackedScene carrier)
        {
            // The carrier ships a duplicate mesh for skeleton export; only its clips are used.
            Node instance = carrier.Instantiate();
            AnimationPlayer? source = FindPlayer(instance);
            library = source is null ? new AnimationLibrary() : AnimationRetarget.CopyClips(source, skeletonPath);
            instance.Free();
            HasClips = library.GetAnimationList().Count > 0;
        }
        else
        {
            library = new AnimationLibrary();
        }

        if (!library.HasAnimation(CharacterClips.Idle))
        {
            library.AddAnimation(CharacterClips.Idle, AnimationRetarget.EmptyLoop());
            _clipAliases.Add($"{CharacterClips.Idle}→(empty)");
        }

        _clipAliases.AddRange(AnimationRetarget.FillMissingClips(library, CharacterClips.Required, CharacterClips.Fallbacks));
        _injectedEvents.AddRange(AnimationRetarget.InjectEvents(
            library, AnimationRetarget.LoadEventTable(Paths.AnimationEventsData)));
        Animations!.AddAnimationLibrary("", library);
    }

    private void MountDisk()
    {
        if (Skeleton is null || Skeleton.FindBone(MountBone) < 0)
        {
            GD.PushWarning($"Character: no {MountBone} bone, duel disk not mounted");
            return;
        }

        DuelDiskMount = new BoneAttachment3D { Name = "DuelDiskMount", BoneName = MountBone };
        Skeleton.AddChild(DuelDiskMount);
        if (!EquipDuelDisk)
        {
            return;
        }

        PackedScene? scene = DuelDiskScene ?? ResourceLoader.Load<PackedScene>(Paths.DuelDiskScene);
        if (scene?.Instantiate() is not DuelDisk disk)
        {
            GD.PushError("Character: DuelDisk scene missing or not a DuelDisk");
            return;
        }

        disk.Transform = AnimationRetarget.LoadDiskMountOffset(Paths.DiskMountData, BodyType);
        DuelDiskMount.AddChild(disk);
        Disk = disk;
    }

    private static Skeleton3D? FindSkeleton(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is Skeleton3D skeleton)
            {
                return skeleton;
            }

            Skeleton3D? nested = FindSkeleton(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static AnimationPlayer? FindPlayer(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is AnimationPlayer player)
            {
                return player;
            }

            AnimationPlayer? nested = FindPlayer(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
