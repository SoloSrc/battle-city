using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BattleCity.Characters;
using BattleCity.Core;
using BattleCity.Data;
using BattleCity.Duel.Core;
using BattleCity.Duel.Core.Ai;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Data;
using BattleCity.Duel.Core.Model;
using BattleCity.Duel.Core.Presentation;
using BattleCity.DuelScene;
using BattleCity.World;
using Godot;

namespace BattleCity.Diagnostics;

/// <summary>
/// Acceptance scene of issue #61: a real duel where the human seat is played
/// through <see cref="DuelUi"/> by a scripted player. Every few frames the
/// script asks a heuristic agent what it would do, then reaches that command
/// only through the HUD: grid moves and interact for the cursor and the lists,
/// the mouse (hover and click on the 3D cards and the hand fan) every other
/// time. The command the engine receives must be the one the script aimed
/// for. <c>DuelUiTest FAIL</c> lines fail CI. Run with <c>--fixed-fps 60</c>.
/// </summary>
public partial class DuelUiTestScene : Node3D
{
    private const string FailPrefix = "DuelUiTest FAIL";
    private const int SetupFrames = 30;
    private const int TickFrames = 3;
    private const int MaxHumanCommands = 60;
    private const int MaxFrames = 5400;
    private const int SettleFrames = 60;
    private const int StuckTicks = 60;
    private const ulong Seed = 5;

    [Export]
    public DuelStaging? Staging { get; set; }

    [Export]
    public EncounterSite? Site { get; set; }

    [Export]
    public Character? Player { get; set; }

    [Export]
    public Character? Opponent { get; set; }

    [Export]
    public CameraRig? Rig { get; set; }

    [Export]
    public DuelSession? Session { get; set; }

    [Export]
    public DuelUi? Ui { get; set; }

    [Export]
    public Label? ReportLabel { get; set; }

    private readonly List<string> _report = new();
    private readonly List<string> _pressed = new();
    private readonly HashSet<DuelUiMode> _modesSeen = new();
    private DuelEngine? _engine;
    private IDuelAgent? _script;
    private PlayerCommand? _target;
    private int _targetTicks;
    private int _frame;
    private int _passCount;
    private int _failCount;
    private bool _done;
    private bool _dealt;
    private int _lastCommandFrame = -1;
    private int _mismatchFrames;
    private int _wrongCommands;
    private int _illegalEntries;
    private int _chainMismatches;
    private int _handMismatches;
    private int _mouseHits;
    private int _mouseMisses;
    private int _mouseTry;
    private bool _mousePending;
    private Vector2 _mousePosition;
    private (int Row, int Column) _mouseCell;
    private int _human;
    private string? _captureDir;
    private readonly HashSet<string> _captured = new();

    public override void _Ready()
    {
        if (Staging is null || Site is null || Player is null || Opponent is null || Rig is null || Session is null || Ui is null)
        {
            Fail("scene not wired (Staging, Site, Player, Opponent, Rig, Session, Ui)");
            _done = true;
            return;
        }

        string[] args = OS.GetCmdlineUserArgs();
        int capture = Array.IndexOf(args, "--capture");
        if (capture >= 0 && capture + 1 < args.Length)
        {
            _captureDir = args[capture + 1];
        }

        Staging.Stage(Site, Player, Opponent);
        Player.PlayState(Character.DuelReadyState);
        Opponent.PlayState(Character.DuelReadyState);
        Player.Disk?.Deploy();
        Opponent.Disk?.Deploy();
        Rig.Target = Player;
        Staging.EnterCamera(Rig);
        foreach (string action in new[] { InputActions.DuelPhase, InputActions.DuelGraveyard, InputActions.DuelBanished, InputActions.DuelLog })
        {
            Check(InputMap.HasAction(action), $"input action {action} declared");
        }

        StartDuel();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_done)
        {
            return;
        }

        _frame++;
        if (_frame == SetupFrames)
        {
            Deal();
        }

        if (!_dealt || _engine is null)
        {
            return;
        }

        // Commands land in a process step; the staging syncs in the next one, so check two physics frames later.
        if (_lastCommandFrame >= 0 && _frame == _lastCommandFrame + 2 && Staging!.Mismatches() is > 0 and int mismatches)
        {
            _mismatchFrames++;
            if (_mismatchFrames <= 3)
            {
                Fail(Inv($"{mismatches} card view(s) disagree with the engine after command {Session!.Commands}"));
            }
        }

        bool finished = _engine.State.IsOver || Session!.HumanCommands >= MaxHumanCommands || _frame >= MaxFrames;
        if (!finished)
        {
            if (_frame % TickFrames == 0)
            {
                Drive();
            }

            return;
        }

        if (_frame >= _lastCommandFrame + SettleFrames)
        {
            Finish();
        }
    }

    private void StartDuel()
    {
        string root = ProjectSettings.GlobalizePath(Paths.DataRoot);
        GameData data;
        try
        {
            data = GameData.Load(root);
        }
        catch (Exception e) when (e is DataException or CardDataException)
        {
            Fail($"data did not load: {e.Message}");
            return;
        }

        DuelistDefinition mara = data.Duelists["d2"];
        Deck goat = data.Decks["goat_control"].ToDeck(data.Cards);
        Deck theirs = data.Decks[mara.DeckId].ToDeck(data.Cards);
        _engine = DuelEngine.Start(goat, theirs, new DuelOptions { Seed = Seed, FirstPlayer = 0 });
        _script = new HeuristicAgent(data.Duelists["d3"].Profile, Seed + 7);
        Session!.AiDelay = 0.1f;
        Report("INFO", Inv($"engine started: goat_control (scripted player) vs {mara.DeckId} ({mara.Profile.Name}), seed {Seed}"));
    }

    private void Deal()
    {
        _dealt = true;
        if (_engine is null || Staging is null || Session is null || Ui is null)
        {
            return;
        }

        Staging.Bind(_engine);
        Session.Begin(_engine, new HeuristicAgent(AiProfile.Mara, Seed + 100), humanPlayer: 0);
        Ui.Bind(Session, Staging);
        // After the HUD's own subscription, so the checks read the refreshed HUD.
        Session.Changed += OnChanged;
        _human = Session.HumanPlayer;
        _lastCommandFrame = _frame;
        Check(Ui.Mode == DuelUiMode.Free, $"HUD starts free on the player's turn ({Ui.Mode})");
        Check(Ui.HandCount == _engine.State.Player(0).Hand.Count, Inv($"hand fan shows {Ui.HandCount} cards"));
        Check(Staging.Cards.Values.Where(v => v.Card is { Loc: Location.Hand, Owner: 0 }).All(v => !v.Visible), "the player's 3D hand row is hidden under the fan");
        Check(Staging.Mismatches() == 0, "card views match the engine after the deal");
        Check(Ui.Banner.StartsWith("Your turn", StringComparison.Ordinal), $"banner reads the turn ({Ui.Banner})");
        Report("INFO", Inv($"viewport {GetViewport().GetVisibleRect().Size}, camera {(GetViewport().GetCamera3D() is null ? "missing" : "present")}"));
    }

    private void OnChanged()
    {
        if (_engine is null || Session is null || Ui is null)
        {
            return;
        }

        _lastCommandFrame = _frame;
        _modesSeen.Add(Ui.Mode);
        if (Session.LastCommand is { } last && last.Player == _human)
        {
            if (_target is null || !ActionCatalog.Same(_target, last))
            {
                _wrongCommands++;
                Fail($"the HUD submitted {DuelText.Describe(last, _engine.State, _human)} while the script aimed for {(_target is null ? "nothing" : DuelText.Describe(_target, _engine.State, _human))}");
            }

            _target = null;
            _targetTicks = 0;
        }

        if (_engine.State.Chain.Count > 0 && Ui.ChainShown != _engine.State.Chain.Count)
        {
            _chainMismatches++;
        }

        if (Ui.HandCount != _engine.State.Player(_human).Hand.Count)
        {
            _handMismatches++;
        }
    }

    /// <summary>One step of the scripted player toward <see cref="_target"/> through the HUD.</summary>
    private void Drive()
    {
        if (_engine is null || Ui is null || Session is null || _script is null)
        {
            return;
        }

        if (_pressed.Count > 0)
        {
            foreach (string action in _pressed)
            {
                Input.ActionRelease(action);
            }

            _pressed.Clear();
            return;
        }

        if (_mousePending)
        {
            _mousePending = false;
            bool moved = Ui.CursorRow == _mouseCell.Row && Ui.CursorColumn == _mouseCell.Column;
            if (moved)
            {
                _mouseHits++;
                Click(_mousePosition);
            }
            else
            {
                _mouseMisses++;
            }

            return;
        }

        _modesSeen.Add(Ui.Mode);
        Capture(Ui.Mode);
        if (Ui.Mode is DuelUiMode.Free or DuelUiMode.Picker or DuelUiMode.Response && !EnsureTarget())
        {
            return;
        }

        if (Ui.Mode is DuelUiMode.Menu or DuelUiMode.Targets or DuelUiMode.Response)
        {
            foreach (DuelListEntry entry in Ui.List.Entries)
            {
                IEnumerable<PlayerCommand> commands = entry.Tag switch
                {
                    CardAction action => action.Commands,
                    PlayerCommand command => new[] { command },
                    _ => Array.Empty<PlayerCommand>(),
                };
                foreach (PlayerCommand command in commands)
                {
                    if (_engine.Validate(command) is { } error)
                    {
                        _illegalEntries++;
                        Fail($"menu offered {DuelText.Describe(command, _engine.State, _human)}: {error}");
                    }
                }
            }
        }

        switch (Ui.Mode)
        {
            case DuelUiMode.Waiting:
            case DuelUiMode.Ended:
            case DuelUiMode.Unbound:
                return;
            case DuelUiMode.Free:
                DriveFree();
                break;
            case DuelUiMode.Menu:
                DriveList(entry => entry.Tag is CardAction action && _target is not null && action.Commands.Any(c => ActionCatalog.Same(c, _target)));
                break;
            case DuelUiMode.Targets:
            case DuelUiMode.Response:
                DriveList(entry => entry.Tag is PlayerCommand command && _target is not null && ActionCatalog.Same(command, _target));
                break;
            case DuelUiMode.Picker:
                DrivePicker();
                break;
            case DuelUiMode.Pile:
                Press(InputActions.Cancel);
                break;
        }
    }

    /// <summary>Asks the script what to do next when nothing is being pursued; false when the player has no legal action yet.</summary>
    private bool EnsureTarget()
    {
        if (_target is not null)
        {
            return true;
        }

        IReadOnlyList<PlayerCommand> legal = _engine!.LegalActions(_human);
        if (legal.Count == 0)
        {
            return false;
        }

        _target = _script!.Choose(_engine, _human, legal);
        _targetTicks = 0;
        return true;
    }

    private void DriveFree()
    {
        if (_engine is null || Ui is null || _target is null)
        {
            return;
        }

        if (++_targetTicks > StuckTicks)
        {
            Fail($"stuck trying to reach {DuelText.Describe(_target, _engine.State, _human)} (mode {Ui.Mode}, cursor {Ui.CursorRow}/{Ui.CursorColumn})");
            _target = null;
            return;
        }

        if (_target is Pass or EnterBattlePhase)
        {
            Press(InputActions.DuelPhase);
            return;
        }

        if (ActionCatalog.CardOf(_target) is not { } card || Ui.CellOf(card) is not { } cell)
        {
            Fail($"no cursor cell for {DuelText.Describe(_target, _engine.State, _human)}");
            _target = null;
            return;
        }

        if (Ui.CursorRow == cell.Row && Ui.CursorColumn == cell.Column)
        {
            Press(InputActions.Interact);
            return;
        }

        if (_mouseTry++ % 2 == 0 && TryMouseTo(card, cell))
        {
            return;
        }

        int dy = Math.Sign(cell.Row - Ui.CursorRow);
        if (dy != 0)
        {
            Press(dy > 0 ? InputActions.MoveDown : InputActions.MoveUp);
            return;
        }

        Press(cell.Column > Ui.CursorColumn ? InputActions.MoveRight : InputActions.MoveLeft);
    }

    private void DriveList(Func<DuelListEntry, bool> wanted)
    {
        if (_engine is null || Ui is null)
        {
            return;
        }

        int index = Ui.List.Entries.ToList().FindIndex(e => wanted(e));
        if (index < 0)
        {
            Fail($"no list entry for {(_target is null ? "nothing" : DuelText.Describe(_target, _engine.State, _human))} in {Ui.Mode} ({string.Join(" | ", Ui.List.Entries.Select(e => e.Label))})");
            _target = null;
            Press(InputActions.Cancel);
            return;
        }

        MoveListTo(index);
    }

    private void DrivePicker()
    {
        if (_engine is null || Ui is null || _target is null)
        {
            return;
        }

        if (++_targetTicks > StuckTicks)
        {
            Fail($"stuck in the picker aiming for {DuelText.Describe(_target, _engine.State, _human)}");
            _target = null;
            return;
        }

        IReadOnlyList<Guid> required = _target switch
        {
            NormalSummon s => s.Tributes,
            SetMonster s => s.Tributes,
            AnswerChoice a => a.Selected,
            _ => Array.Empty<Guid>(),
        };
        IReadOnlyList<DuelListEntry> entries = Ui.List.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Card is not { } card)
            {
                continue;
            }

            bool want = required.Contains(card);
            bool have = Ui.Picked.Contains(card);
            if (want != have)
            {
                MoveListTo(i);
                return;
            }
        }

        int decline = entries.ToList().FindIndex(e => e.Tag is "decline");
        int confirm = entries.ToList().FindIndex(e => e.Tag is "confirm");
        MoveListTo(required.Count == 0 && decline >= 0 ? decline : confirm);
    }

    private void MoveListTo(int index)
    {
        if (Ui is null || index < 0)
        {
            return;
        }

        if (Ui.List.Index == index)
        {
            Press(InputActions.Interact);
            return;
        }

        int count = Ui.List.Entries.Count;
        int down = ((index - Ui.List.Index) % count + count) % count;
        Press(down <= count / 2 ? InputActions.MoveDown : InputActions.MoveUp);
    }

    /// <summary>Hovers the mouse over the card's 3D view or its fan card; the next tick checks the cursor followed and clicks.</summary>
    private bool TryMouseTo(Guid card, (int Row, int Column) cell)
    {
        if (Ui is null || Staging is null || GetViewport().GetCamera3D() is not { } camera)
        {
            return false;
        }

        Vector2? position = cell.Row == DuelUi.HandRow
            ? Ui.HandCardCenter(cell.Column)
            : Staging.Cards.TryGetValue(card, out CardView? view) ? DuelStaging.ScreenPosition(camera, view) : null;
        if (position is not { } p)
        {
            return false;
        }

        _mousePosition = p;
        _mouseCell = cell;
        _mousePending = true;
        if (_mouseTry <= 2)
        {
            Report("INFO", Inv($"mouse probe: cell {cell.Row}/{cell.Column} at {p} (viewport), window {GetWindow().Size}, stretch {GetViewport().GetFinalTransform()}"));
        }

        Input.ParseInputEvent(new InputEventMouseMotion { Position = WindowPosition(p), GlobalPosition = WindowPosition(p) });
        return true;
    }

    /// <summary>Injected events carry window pixels; the viewport maps them through the stretch transform, so pre-apply it.</summary>
    private Vector2 WindowPosition(Vector2 viewportPosition) => GetViewport().GetFinalTransform() * viewportPosition;

    private void Click(Vector2 position)
    {
        Vector2 p = WindowPosition(position);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = p, GlobalPosition = p });
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = p, GlobalPosition = p });
    }

    private void Press(string action)
    {
        Input.ActionPress(action);
        _pressed.Add(action);
    }

    /// <summary><c>-- --capture &lt;dir&gt;</c>: saves the screen the first time each kind of prompt is open (not headless).</summary>
    private void Capture(DuelUiMode mode)
    {
        if (_captureDir is null || CardFaces.IsHeadless || mode is DuelUiMode.Waiting or DuelUiMode.Unbound or DuelUiMode.Ended)
        {
            return;
        }

        if (mode == DuelUiMode.Free && _frame < SetupFrames + 20)
        {
            return;
        }

        string name = mode.ToString().ToLowerInvariant();
        if (!_captured.Add(name))
        {
            return;
        }

        DirAccess.MakeDirRecursiveAbsolute(_captureDir);
        GetViewport().GetTexture().GetImage().SavePng($"{_captureDir}/ui_{name}.png");
        Report("INFO", $"captured ui_{name}.png");
    }

    private void Finish()
    {
        ManagedWrappers.Flush();
        _done = true;
        if (_engine is null || Session is null || Ui is null || Staging is null)
        {
            Print();
            return;
        }

        DuelState s = _engine.State;
        Report("INFO", Inv($"{Session.Commands} commands ({Session.HumanCommands} through the HUD), turn {s.TurnNumber}, {s.Phase}, LP {s.Player(0).LifePoints}/{s.Player(1).LifePoints}, over: {s.IsOver}, {_frame} frames"));
        Check(Session.HumanCommands >= 25, Inv($"{Session.HumanCommands} commands played through the HUD"));
        Check(_wrongCommands == 0, Inv($"every HUD command was the one the script aimed for ({_wrongCommands} wrong)"));
        Check(Session.Rejected == 0, Inv($"the engine rejected {Session.Rejected} HUD commands"));
        Check(_illegalEntries == 0, Inv($"{_illegalEntries} menu entries were illegal"));
        Check(_mismatchFrames == 0, Inv($"card views matched the engine after every command ({_mismatchFrames} frames off)"));
        Check(_chainMismatches == 0, Inv($"chain display matched the chain ({_chainMismatches} off)"));
        Check(_handMismatches == 0, Inv($"hand fan matched the hand after every command ({_handMismatches} off)"));
        Check(_modesSeen.Contains(DuelUiMode.Menu), "card action menus were used");
        Report(_modesSeen.Contains(DuelUiMode.Targets) ? "PASS" : "INFO", $"attack target list {(_modesSeen.Contains(DuelUiMode.Targets) ? "used" : "not reached in this seed")}");
        Report(_modesSeen.Contains(DuelUiMode.Response) ? "PASS" : "INFO", $"response prompt {(_modesSeen.Contains(DuelUiMode.Response) ? "used" : "not reached in this seed")}");
        Report(_modesSeen.Contains(DuelUiMode.Picker) ? "PASS" : "INFO", $"choice picker {(_modesSeen.Contains(DuelUiMode.Picker) ? "used" : "not reached in this seed")}");
        Check(_mouseHits >= 3, Inv($"mouse picking moved the cursor {_mouseHits} times ({_mouseMisses} misses)"));
        Check(Ui.LifePointsShown(0) == s.Player(0).LifePoints && Ui.LifePointsShown(1) == s.Player(1).LifePoints, Inv($"Life Point counters settled at {Ui.LifePointsShown(0)}/{Ui.LifePointsShown(1)}"));
        Check(Ui.Log.Count is > 0 and <= DuelUi.LogLines, Inv($"log holds {Ui.Log.Count} lines"));
        Check(Staging.Cards.Values.Where(v => v.Card is { Loc: Location.Hand, Owner: 0 }).All(v => !v.Visible), "the player's 3D hand stays hidden");
        if (Ui.Mode == DuelUiMode.Free)
        {
            Ui.TogglePile(Location.Graveyard);
            int expected = s.Player(0).Graveyard.Count + s.Player(1).Graveyard.Count;
            Check(Ui.Mode == DuelUiMode.Pile && Ui.List.Entries.Count(e => e.Card is not null) == expected, Inv($"graveyard list shows {expected} cards"));
            Capture(DuelUiMode.Pile);
            Ui.Cancel();
            Check(Ui.Mode == DuelUiMode.Free, "cancel closes the pile list");
        }
        else
        {
            Report("INFO", $"pile list not checked (mode {Ui.Mode})");
        }

        Ui.ToggleLog();
        Check(Ui.LogVisible, "log panel toggles");
        Print();
    }

    private void Print()
    {
        string summary = $"DuelUiTest summary: {_passCount} pass, {_failCount} fail";
        GD.Print(summary);
        if (ReportLabel is not null)
        {
            ReportLabel.Text = summary + "\n" + string.Join("\n", _report);
        }
    }

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    private void Check(bool ok, string message)
    {
        if (ok)
        {
            _passCount++;
            Report("PASS", message);
        }
        else
        {
            Fail(message);
        }
    }

    private void Fail(string message)
    {
        _failCount++;
        Report("FAIL", message);
        GD.PrintErr($"{FailPrefix}: {message}");
    }

    private void Report(string status, string message)
    {
        _report.Add($"[{status}] {message}");
        GD.Print($"DuelUiTest {status}: {message}");
    }
}
