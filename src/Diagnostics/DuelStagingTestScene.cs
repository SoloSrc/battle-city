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
using BattleCity.DuelScene;
using BattleCity.World;
using Godot;

namespace BattleCity.Diagnostics;

/// <summary>
/// Acceptance scene of issue #60: stages two characters on an encounter site,
/// blends the rig to the duel camera, binds a real <see cref="DuelEngine"/>
/// (the starter against Rookie Beatdown, two heuristic agents) and submits one
/// agent command every <see cref="StepFrames"/> frames while the card views
/// follow the events. After every command the card views are checked against
/// the engine state; at the end the layout contract of §6.2 is checked as
/// numbers. <c>DuelStagingTest FAIL</c> lines fail CI. Run with
/// <c>--fixed-fps 60</c> headless.
/// </summary>
public partial class DuelStagingTestScene : Node3D
{
    private const string FailPrefix = "DuelStagingTest FAIL";
    private const int SetupFrames = 30;
    private const int StepFrames = 15;
    private const int MaxCommands = 150;
    private const int SettleFrames = 90;
    private const ulong Seed = 7;

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
    public Label? ReportLabel { get; set; }

    private readonly List<string> _report = new();
    private DuelEngine? _engine;
    private IDuelAgent? _agent0;
    private IDuelAgent? _agent1;
    private int _frame;
    private int _commands;
    private int _mismatchFrames;
    private int _passCount;
    private int _failCount;
    private bool _done;
    private bool _dealt;
    private int _lastCommandFrame = -1;
    private string? _captureDir;
    private bool _captured;
    private int _captureSideFrame;

    public override void _Ready()
    {
        if (Staging is null || Site is null || Player is null || Opponent is null || Rig is null)
        {
            Fail("scene not wired (Staging, Site, Player, Opponent, Rig)");
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
        Check(Player.GlobalPosition.DistanceTo(Site.StandPointA) < 0.01f && Opponent.GlobalPosition.DistanceTo(Site.StandPointB) < 0.01f, "characters placed on the stand points");
        Check((-Player.GlobalBasis.Z).Dot((Opponent.GlobalPosition - Player.GlobalPosition).Normalized()) > 0.99f, "characters face each other");
        DuelStaging.SideAnchors? side = Staging.PlayerSide;
        Check(side is not null && side.Monsters.Length == 5 && side.SpellTraps.Length == 5, "ten card anchors per side");
        if (side is not null)
        {
            Vector3 m1 = side.Monsters[0].GlobalPosition;
            Vector3 m2 = side.Monsters[1].GlobalPosition;
            Vector3 st1 = side.SpellTraps[0].GlobalPosition;
            Check(Mathf.Abs(m1.DistanceTo(m2) - Staging.SpacingX) < 0.001f, Inv($"monster anchors {m1.DistanceTo(m2):F2} m apart"));
            Check(Mathf.Abs(m1.DistanceTo(st1) - Staging.SpacingZ) < 0.001f, Inv($"spell/trap row {m1.DistanceTo(st1):F2} m behind"));
            Vector3 toOpponent = (Opponent.GlobalPosition - Player.GlobalPosition).Normalized();
            float forward = (side.Monsters[2].GlobalPosition - Player.GlobalPosition).Dot(toOpponent);
            Check(Mathf.Abs(forward - Staging.Forward) < 0.01f, Inv($"front row {forward:F2} m toward the opponent"));
            Report("INFO", side.PilesOnDisk ? "deck, graveyard and banished anchors on the disk markers" : "disk markers not available, piles on fallback anchors");
        }

        Player.PlayState(Character.DuelReadyState);
        Opponent.PlayState(Character.DuelReadyState);
        Player.Disk?.Deploy();
        Opponent.Disk?.Deploy();
        Rig.Target = Player;
        Staging.EnterCamera(Rig);
        Check(Rig.DuelActive, "camera rig holds the duel framing");

        CheckLayoutContract();
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

        if (!_dealt)
        {
            return;
        }

        if (_lastCommandFrame >= 0 && _frame == _lastCommandFrame + 1)
        {
            int mismatches = Staging!.Mismatches();
            if (mismatches > 0)
            {
                _mismatchFrames++;
                if (_mismatchFrames <= 3)
                {
                    Fail(Inv($"{mismatches} card view(s) disagree with the engine after command {_commands}"));
                }
            }
        }

        if (_captureDir is not null && !_captured && _commands >= 12 && _frame == _lastCommandFrame + 10)
        {
            Capture();
            _captureSideFrame = _frame + 20;
            Vector3 focus = Player!.GlobalPosition + Vector3.Up * 1.1f;
            Rig!.EnterDuel(focus, 90.0f, 25.0f, 2.2f, 40.0f, 0.0f);
        }

        if (_captureSideFrame > 0 && _frame == _captureSideFrame && _captureDir is not null)
        {
            _captureSideFrame = 0;
            GetViewport().GetTexture().GetImage().SavePng($"{_captureDir}/staging_side.png");
            Staging!.EnterCamera(Rig!);
        }

        if (_engine is { State.IsOver: false } && _commands < MaxCommands && _frame % StepFrames == 0)
        {
            Step();
            return;
        }

        bool finished = _engine is null || _engine.State.IsOver || _commands >= MaxCommands;
        if (finished && _frame >= _lastCommandFrame + SettleFrames)
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

        DuelistDefinition nico = data.Duelists["d1"];
        Deck starter = data.Decks["starter"].ToDeck(data.Cards);
        Deck rookie = data.Decks[nico.DeckId].ToDeck(data.Cards);
        _engine = DuelEngine.Start(starter, rookie, new DuelOptions { Seed = Seed, FirstPlayer = 0 });
        _agent0 = new HeuristicAgent(data.Duelists["d2"].Profile, Seed);
        _agent1 = new HeuristicAgent(nico.Profile, Seed + 100);
        Report("INFO", Inv($"engine started: starter vs {nico.DeckId}, seed {Seed}, {_engine.State.Player(0).AllCards.Count()} + {_engine.State.Player(1).AllCards.Count()} cards"));
    }

    private void Deal()
    {
        _dealt = true;
        if (_engine is null || Staging is null)
        {
            return;
        }

        Staging.Bind(_engine);
        int views = Staging.Cards.Count;
        int cards = _engine.State.Player(0).AllCards.Count() + _engine.State.Player(1).AllCards.Count();
        Check(views == cards, Inv($"one card view per card ({views} of {cards})"));
        Check(Staging.Mismatches() == 0, "every card view starts on the anchor its state says (deck, opening hands)");
        Check(_engine.State.Player(0).Hand.Count == DuelCoreInfo.OpeningHandSize + 1, "player drew the opening hand and the turn 1 card");
        Report("INFO", Inv($"faces: {Staging.Faces.PendingCount} viewports pending, {CardFaces.BakedCount} baked"));
        _lastCommandFrame = _frame;
    }

    private void Step()
    {
        if (_engine is null || _agent0 is null || _agent1 is null)
        {
            return;
        }

        int player = _engine.ActingPlayer;
        IReadOnlyList<PlayerCommand> legal = _engine.LegalActions(player);
        if (legal.Count == 0)
        {
            Fail(Inv($"player {player} has priority but no legal action ({_engine.State.Phase})"));
            _commands = MaxCommands;
            return;
        }

        PlayerCommand command = (player == 0 ? _agent0 : _agent1).Choose(_engine, player, legal);
        SubmitResult result = _engine.Submit(command);
        if (!result.Accepted)
        {
            Fail($"agent command rejected: {command} ({result.Error})");
            _commands = MaxCommands;
            return;
        }

        _commands++;
        _lastCommandFrame = _frame;
        Staging!.Sync();
    }

    /// <summary><c>-- --capture &lt;dir&gt;</c>: saves the screen and the baked faces of a few cards of each kind for review (not headless).</summary>
    private void Capture()
    {
        _captured = true;
        if (_captureDir is null || CardFaces.IsHeadless)
        {
            return;
        }

        DirAccess.MakeDirRecursiveAbsolute(_captureDir);
        Image screen = GetViewport().GetTexture().GetImage();
        screen.SavePng($"{_captureDir}/staging.png");
        int saved = 0;
        foreach (CardView view in Staging!.Cards.Values)
        {
            if (view.Card is null || !CardFaces.TryGetBaked(view.Card.Def.Id, out Texture2D? face) || face is null)
            {
                continue;
            }

            string path = $"{_captureDir}/face_{view.Card.Def.Id}.png";
            if (!FileAccess.FileExists(path))
            {
                face.GetImage().SavePng(path);
                saved++;
            }
        }

        Report("INFO", $"captured staging.png and {saved} faces to {_captureDir}");
    }

    private void CheckLayoutContract()
    {
        (int left1, int width1) = CardFaces.StarRow(1);
        (int left12, int width12) = CardFaces.StarRow(12);
        Check(left1 + width1 / 2 == CardFaces.StarRowCenterX, Inv($"level 1 star centered at x={left1 + width1 / 2}"));
        Check(left12 == 26 && left12 + width12 == CardFaces.StarRowRightMax, Inv($"level 12 row spans x={left12}..{left12 + width12}"));
        Check(CardFaces.AttributeX - CardFaces.AttributeSize / 2 - (left12 + width12) == 24, "24 px between the level 12 row and the attribute");
        Check(CardFaces.Width == 590 && CardFaces.Height == 860, "face viewport is 590 × 860");
        foreach (string frame in new[] { "frame_normal", "frame_effect", "frame_fusion", "frame_ritual", "frame_spell", "frame_trap", "card_back" })
        {
            Check(ResourceLoader.Exists($"{Paths.CardFrames}/{frame}.png"), $"frame texture {frame}.png present");
        }

        Check(ResourceLoader.Exists($"{Paths.CardIcons}/star.png") && ResourceLoader.Exists($"{Paths.CardIcons}/attr_dark.png") && ResourceLoader.Exists($"{Paths.CardIcons}/st_spell.png"), "star, attribute and badge icons present");
        // Layout fixtures, not card definitions: a level 12 monster and a Ritual monster compose without error.
        SubViewport fixture12 = CardFaces.Compose(CardDefinition.Vanilla("fixture_level_12", "Fixture", "Dragon", MonsterAttribute.Light, 12, 4000, 4000));
        SubViewport ritual = CardFaces.Compose(CardDefinition.EffectMonster("fixture_ritual", "Fixture", "Warrior", MonsterAttribute.Dark, 8, 3000, 2500, MonsterCategory.Ritual, 4));
        Check(fixture12.GetChild(0).GetChildCount() >= 12 + 3, "level 12 fixture composes twelve stars, attribute and stats");
        Check(ritual.GetChild(0).FindChild("Frame", recursive: true, owned: false) is TextureRect r && r.Texture is not null, "ritual fixture takes the ritual frame");
        fixture12.Free();
        ritual.Free();
    }

    private void Finish()
    {
        _done = true;
        if (_engine is not null && Staging is not null && Rig is not null)
        {
            DuelState s = _engine.State;
            Report("INFO", Inv($"{_commands} commands, turn {s.TurnNumber}, {s.Phase}, LP {s.Player(0).LifePoints}/{s.Player(1).LifePoints}, over: {s.IsOver}"));
            Check(_commands >= 40, Inv($"{_commands} agent commands played"));
            Check(Staging.Moves >= 30, Inv($"{Staging.Moves} card moves driven by the engine"));
            Check(Staging.EventsApplied >= _commands, Inv($"{Staging.EventsApplied} engine events applied"));
            Check(_mismatchFrames == 0, Inv($"card views matched the engine after every command ({_mismatchFrames} frames off)"));
            int onField = 0;
            foreach (CardView view in Staging.Cards.Values)
            {
                if (view.Card is { IsOnField: true })
                {
                    onField++;
                }
            }

            Check(onField > 0 || s.IsOver, Inv($"{onField} cards on the field at the end"));
            Check(Mathf.Abs(Rig.CurrentPitch - Staging.CameraPitch) < 0.5f && Mathf.Abs(Rig.CurrentDistance - Staging.CameraDistance) < 0.05f && Mathf.Abs(Rig.CurrentFov - Staging.CameraFov) < 0.5f,
                Inv($"duel camera reached pitch {Rig.CurrentPitch:F1}°, {Rig.CurrentDistance:F2} m, fov {Rig.CurrentFov:F1}°"));
            Report(CardFaces.BakedCount > 0 ? "PASS" : "INFO", Inv($"{CardFaces.BakedCount} faces baked, {Staging.Faces.PendingCount} viewports kept (headless keeps them all)"));
            Check(Player is not null && Player.CurrentState is not Character.LocomotionState, $"player in a duel state ({Player?.CurrentState})");
        }

        string summary = $"DuelStagingTest summary: {_passCount} pass, {_failCount} fail";
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
        GD.Print($"DuelStagingTest {status}: {message}");
    }
}
