using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BattleCity.Characters;
using BattleCity.Ui;
using BattleCity.World;
using Godot;

namespace BattleCity.Core;

/// <summary>
/// The top-level autoload (systems.md §2.1): mode, progression flags, coins,
/// scene transitions and the wiring between level markers and systems.
/// Levels contain no scripts (architecture.md §4); everything a marker needs
/// is connected here when the level loads. The player and camera rig are
/// created once and carried across transitions.
/// </summary>
public partial class Game : Node
{
    [Signal]
    public delegate void ModeChangedEventHandler(GameMode mode);

    [Signal]
    public delegate void LevelLoadedEventHandler(string scenePath, string spawnId);

    [Signal]
    public delegate void FlagSetEventHandler(string flag);

    public const int StartingCoins = 500;
    private const string TransitionLock = "transition";

    public static Game? Instance { get; private set; }

    public GameMode Mode { get; private set; } = GameMode.Boot;

    public IReadOnlySet<string> Flags => _flags;

    public int Coins { get; set; } = StartingCoins;

    public string LevelPath { get; private set; } = string.Empty;

    public Node3D? Level { get; private set; }

    public Character? Player { get; private set; }

    public PlayerController? Controller { get; private set; }

    public CameraRig? Camera { get; private set; }

    public EncounterSystem Encounters { get; private set; } = null!;

    public MessageBox Messages { get; private set; } = null!;

    public InteractionPrompt Prompt { get; private set; } = null!;

    public ScreenFade Fade { get; private set; } = null!;

    /// <summary>True while a fade-out, load and fade-in are in progress.</summary>
    public bool IsTransitioning { get; private set; }

    /// <summary>Player input is enabled when nothing holds a lock (transition, message, encounter).</summary>
    public bool InputEnabled => _inputLocks.Count == 0;

    private readonly HashSet<string> _flags = new(StringComparer.Ordinal);
    private readonly HashSet<string> _inputLocks = new(StringComparer.Ordinal);
    private Node3D? _world;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void _Ready()
    {
        Fade = new ScreenFade { Name = "Fade" };
        AddChild(Fade);
        Messages = new MessageBox { Name = "Messages" };
        AddChild(Messages);
        Prompt = new InteractionPrompt { Name = "Prompt" };
        AddChild(Prompt);
        Encounters = new EncounterSystem { Name = "Encounters" };
        AddChild(Encounters);
    }

    /// <summary>Resets progression and loads the starting room at its <c>arrival</c> spawn.</summary>
    public void NewGame()
    {
        _flags.Clear();
        Coins = StartingCoins;
        Transition(Paths.StartRoomScene, PlayerSpawn.ArrivalId);
    }

    /// <summary>Fades out, loads <paramref name="scenePath"/>, places the player at <paramref name="spawnId"/>, fades in.</summary>
    public void Transition(string scenePath, string spawnId)
    {
        RunTransition(scenePath, spawnId);
    }

    public async Task TransitionAsync(string scenePath, string spawnId)
    {
        if (IsTransitioning)
        {
            GD.PushWarning($"Game: transition to {scenePath} ignored, another one is running");
            return;
        }

        IsTransitioning = true;
        LockInput(TransitionLock);
        await Fade.FadeOutAsync();
        LoadLevel(scenePath, spawnId);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await Fade.FadeInAsync();
        UnlockInput(TransitionLock);
        IsTransitioning = false;
    }

    public bool HasFlag(string flag) => _flags.Contains(flag);

    /// <summary>Sets a progression flag and applies it to every gate and duelist in the level.</summary>
    public void SetFlag(string flag)
    {
        if (!_flags.Add(flag))
        {
            return;
        }

        ApplyFlags(instant: false);
        EmitSignal(SignalName.FlagSet, flag);
    }

    public void SetMode(GameMode mode)
    {
        if (Mode == mode)
        {
            return;
        }

        Mode = mode;
        EmitSignal(SignalName.ModeChanged, (int)mode);
    }

    public void LockInput(string reason)
    {
        _inputLocks.Add(reason);
        ApplyInputLock();
    }

    public void UnlockInput(string reason)
    {
        _inputLocks.Remove(reason);
        ApplyInputLock();
    }

    /// <summary>The mode a level belongs to from its path: interiors live under <see cref="Paths.InteriorScenes"/>.</summary>
    public static GameMode ModeFor(string scenePath) =>
        scenePath.StartsWith(Paths.InteriorScenes, StringComparison.Ordinal) ? GameMode.Interior : GameMode.Overworld;

    private async void RunTransition(string scenePath, string spawnId)
    {
        try
        {
            await TransitionAsync(scenePath, spawnId);
        }
        catch (Exception e)
        {
            GD.PushError($"Game: transition to {scenePath} failed: {e}");
            IsTransitioning = false;
            UnlockInput(TransitionLock);
        }
    }

    private void LoadLevel(string scenePath, string spawnId)
    {
        var packed = ResourceLoader.Load<PackedScene>(scenePath);
        if (packed is null)
        {
            GD.PushError($"Game: level not found: {scenePath}");
            return;
        }

        EnsureWorld();
        if (Level is not null)
        {
            Encounters.Abort();
            Messages.Close(false);
            _world!.RemoveChild(Level);
            Level.QueueFree();
            Level = null;
        }

        Level = packed.Instantiate<Node3D>();
        Level.Name = "Level";
        _world!.AddChild(Level);
        BakeNavigation(Level);
        EnsurePlayer();

        PlayerSpawn? spawn = PlayerSpawn.Find(GetTree(), spawnId) ?? PlayerSpawn.Find(GetTree(), PlayerSpawn.ArrivalId);
        if (spawn is null)
        {
            GD.PushError($"Game: {scenePath} has no spawn '{spawnId}' and no '{PlayerSpawn.ArrivalId}'");
        }
        else
        {
            PlacePlayer(spawn);
        }

        Wire(Level);
        ApplyFlags(instant: true);
        LevelPath = scenePath;
        SetMode(ModeFor(scenePath));
        GD.Print($"Game: loaded {scenePath} at spawn '{spawn?.Id ?? "?"}'");
        EmitSignal(SignalName.LevelLoaded, scenePath, spawn?.Id ?? string.Empty);
    }

    private void EnsureWorld()
    {
        if (_world is not null)
        {
            return;
        }

        _world = new Node3D { Name = "World" };
        GetTree().Root.AddChild(_world);
    }

    private void EnsurePlayer()
    {
        if (Player is not null)
        {
            return;
        }

        Player = ResourceLoader.Load<PackedScene>(Paths.PlayerScene).Instantiate<Character>();
        Player.Name = "Player";
        _world!.AddChild(Player);
        Controller = Player.GetNodeOrNull<PlayerController>("Controller");
        Prompt.Bind(Controller);
        ApplyInputLock();

        Camera = ResourceLoader.Load<PackedScene>(Paths.CameraRigScene).Instantiate<CameraRig>();
        Camera.Name = "CameraRig";
        Camera.Target = Player;
        _world.AddChild(Camera);
    }

    private void PlacePlayer(PlayerSpawn spawn)
    {
        if (Player is null)
        {
            return;
        }

        Vector3 facing = spawn.Facing;
        Player.GlobalPosition = spawn.GlobalPosition;
        Player.Rotation = new Vector3(0.0f, Mathf.Atan2(-facing.X, -facing.Z), 0.0f);
        Player.Velocity = Vector3.Zero;
        Controller?.StopWalk();
        Camera?.Snap();
    }

    /// <summary>Levels are committed without baked navigation data; bake at load. The map takes the mesh a few physics frames later, well before the fade-in ends.</summary>
    private static void BakeNavigation(Node3D level)
    {
        foreach (Node child in level.GetChildren())
        {
            if (child is NavigationRegion3D region && (region.NavigationMesh is null || region.NavigationMesh.GetPolygonCount() == 0))
            {
                region.NavigationMesh ??= new NavigationMesh();
                region.BakeNavigationMesh(false);
            }
        }
    }

    /// <summary>Connects every marker in the level to the system that consumes it (systems.md §3.3).</summary>
    private void Wire(Node node)
    {
        switch (node)
        {
            case Door door:
                door.TransitionRequested += (scene, spawn, _) => Transition(scene, spawn);
                break;
            case Duelist duelist:
                duelist.PlayerSpotted += player => Encounters.Start(duelist, player, manual: false);
                duelist.Challenged += player => Encounters.Start(duelist, player, manual: true);
                break;
            case Sign sign:
                sign.Read += (text, _) => Messages.Show(text);
                break;
            case TalkNpc npc:
                npc.TalkRequested += (id, _) => Messages.Show($"({id}) Dialogue lines arrive with the dialogue system.");
                break;
            case ShopCounter counter:
                counter.ShopRequested += (id, _) => Messages.Show($"({id}) The shop opens with the shop UI.");
                break;
            case AmbientZone zone:
                zone.PlayerEntered += (music, ambience) => GD.Print($"Game: ambient zone entered, music '{music}', ambience '{ambience}'");
                break;
        }

        foreach (Node child in node.GetChildren())
        {
            Wire(child);
        }
    }

    private void ApplyFlags(bool instant)
    {
        foreach (Node node in GetTree().GetNodesInGroup(Groups.Gate))
        {
            (node as ProgressionGate)?.ApplyFlags(_flags, instant);
        }

        foreach (Node node in GetTree().GetNodesInGroup(Groups.Duelist))
        {
            (node as Duelist)?.ApplyFlags(_flags);
        }
    }

    private void ApplyInputLock()
    {
        if (Controller is not null)
        {
            Controller.InputEnabled = InputEnabled;
        }
    }
}
