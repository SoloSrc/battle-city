using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BattleCity.Characters;
using BattleCity.Data;
using BattleCity.Duel.Core.Data;
using BattleCity.Duel.Core.Rng;
using BattleCity.DuelScene;
using BattleCity.Rendering;
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
    public const ulong DefaultSeed = 123456;
    public const string DefaultSavePath = "user://save.json";
    private const string TransitionLock = "transition";

    public static Game? Instance { get; private set; }

    public GameMode Mode { get; private set; } = GameMode.Boot;

    public IReadOnlySet<string> Flags => _flags;

    public int Coins { get; set; } = StartingCoins;

    /// <summary>The save's seed (systems.md §9); <see cref="Rng"/> restarts from it on New Game.</summary>
    public ulong Seed { get; private set; } = DefaultSeed;

    /// <summary>The game-side RNG: duel seeds and booster draws come from it, so a run replays from the seed.</summary>
    public DuelRng Rng { get; private set; } = new(DefaultSeed);

    /// <summary>The player's cards (systems.md §8); the starter deck until the deck editor lands.</summary>
    public Collection? Collection { get; private set; }

    /// <summary>Everything under <c>data/</c>, loaded on first use; null when it failed to load (already reported).</summary>
    public GameData? Data => _data ??= LoadData();

    /// <summary>The duel nodes; created with the world on the first level load.</summary>
    public DuelDirector? Duels { get; private set; }

    /// <summary>The autosave slot (systems.md §9); diagnostics point it elsewhere so runs never touch a real save.</summary>
    public string SavePath { get; set; } = DefaultSavePath;

    /// <summary>The spawn the player last arrived at; the save resumes there.</summary>
    public string SpawnId { get; private set; } = PlayerSpawn.ArrivalId;

    /// <summary>The player's display name (GDD §1.1); the avatar creator sets it later.</summary>
    public string PlayerName { get; set; } = "Duelist";

    /// <summary>Set when the last autosave failed or was skipped; the message is in the log.</summary>
    public string? LastSaveError { get; private set; }

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
    private GameData? _data;
    private bool _dataFailed;

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

    /// <summary>Resets progression, coins, the collection and the RNG, deletes the autosave, then loads the starting room at its <c>arrival</c> spawn.</summary>
    public void NewGame(ulong seed = DefaultSeed)
    {
        _flags.Clear();
        Coins = StartingCoins;
        Seed = seed;
        Rng = new DuelRng(seed);
        Collection = Data is { } data ? Collection.Starter(data) : null;
        ClearSave();
        Transition(Paths.StartRoomScene, PlayerSpawn.ArrivalId);
    }

    /// <summary>True when <see cref="SavePath"/> holds a save to continue.</summary>
    public bool HasSave => FileAccess.FileExists(SavePath);

    /// <summary>
    /// Loads the autosave and resumes at its level and spawn with its flags, coins, collection and RNG position (systems.md §9).
    /// Returns false, leaving the game untouched, when there is no save or it does not parse; card ids the library no longer has are dropped with a warning.
    /// </summary>
    public bool Continue()
    {
        if (Data is not { } data || !HasSave)
        {
            return false;
        }

        LoadResult loaded;
        try
        {
            using FileAccess file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read)
                ?? throw new DataException($"{SavePath}: {FileAccess.GetOpenError()}");
            loaded = SaveCodec.Parse(file.GetAsText(), data.Cards, SavePath);
        }
        catch (DataException e)
        {
            GD.PushError($"Game: save did not load: {e.Message}");
            return false;
        }

        if (loaded.Dropped.Count > 0)
        {
            GD.PushWarning($"Game: dropped unknown cards from the save: {string.Join(", ", loaded.Dropped)}");
        }

        SaveData save = loaded.Save;
        _flags.Clear();
        _flags.UnionWith(SaveCodec.JoinFlags(save));
        Coins = save.Coins;
        Seed = save.Seed;
        Rng = new DuelRng(save.Seed);
        Rng.Skip(save.RngDraws);
        PlayerName = save.Name;
        Collection = Collection.FromSave(save);
        GD.Print($"Game: continuing from {SavePath} at {save.Level} '{save.Spawn}'");
        Transition(save.Level, save.Spawn);
        return true;
    }

    /// <summary>Writes the autosave (after every duel and transition, systems.md §9). Failures are reported, never thrown.</summary>
    public void Save()
    {
        LastSaveError = null;
        if (Collection is null || LevelPath.Length == 0)
        {
            LastSaveError = "nothing to save yet";
            return;
        }

        (List<string> defeated, List<string> flags) = SaveCodec.SplitFlags(_flags);
        var save = new SaveData(Seed, Rng.Draws, PlayerName, new Dictionary<string, string>(StringComparer.Ordinal), LevelPath, SpawnId, Coins, Collection.Owned, Collection.Deck, Collection.FusionDeck, defeated, flags);
        using FileAccess? file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            LastSaveError = $"{SavePath}: {FileAccess.GetOpenError()}";
            GD.PushError($"Game: autosave failed: {LastSaveError}");
            return;
        }

        file.StoreString(SaveCodec.Serialize(save));
    }

    /// <summary>Deletes the autosave (New Game).</summary>
    public void ClearSave()
    {
        if (HasSave)
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SavePath));
        }
    }

    /// <summary>The next seed for a duel, drawn from <see cref="Rng"/>.</summary>
    public ulong NextSeed() => Rng.NextUInt64();

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
        Save();
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
        ToonMaterials.Apply(Level);
        SunShadows.Apply(Level);
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
        SpawnId = spawn?.Id ?? spawnId;
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
        Duels = new DuelDirector { Name = "Duels" };
        _world.AddChild(Duels);
    }

    private GameData? LoadData()
    {
        if (_dataFailed)
        {
            return null;
        }

        try
        {
            return GameData.Load(ProjectSettings.GlobalizePath(Paths.DataRoot));
        }
        catch (Exception e) when (e is DataException or CardDataException)
        {
            _dataFailed = true;
            GD.PushError($"Game: data did not load: {e.Message}");
            return null;
        }
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
