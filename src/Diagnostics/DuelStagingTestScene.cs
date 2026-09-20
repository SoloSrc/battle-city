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
using BattleCity.Duel.Core.Events;
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
/// numbers, and the six VFX scenes and eight cues of issue #64 must have
/// been instantiated by the events, selection must stop, finite effects must
/// clean up and the end dissolve must free every card. <c>DuelStagingTest
/// FAIL</c> lines fail CI. Run with <c>--fixed-fps 60</c> headless.
/// </summary>
public partial class DuelStagingTestScene : Node3D
{
    private const string FailPrefix = "DuelStagingTest FAIL";
    private const int SetupFrames = 30;
    private const int StepFrames = 15;
    private const int MaxCommands = 150;
    private const int SettleFrames = 90;
    private const int TeardownFrames = 90;
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
    private int _setCardsSeen;
    private int _setMonstersSeen;
    private int _setCardsWrong;
    private int _pileCardsSeen;
    private int _pileCardsWrong;
    private int _faceUpSeen;
    private int _faceUpWrong;
    private SubViewport[]? _reviewViews;
    private int _reviewFrame;
    private int _reviewEvents;
    private int _reviewEventsNext;
    private int _passCount;
    private int _failCount;
    private bool _done;
    private bool _dealt;
    private bool _tornDown;
    private int _teardownFrame;
    private int _cardsBeforeTeardown;
    private int _movesBeforeTeardown;
    private int _dissolvesBeforeTeardown;
    private bool _selectionChecked;
    private int _lastCommandFrame = -1;
    private string? _captureDir;
    private bool _captured;
    private int _captureSideFrame;
    private int _attacksSeen;
    private int _hitsSeen;
    private int _attackCaptureFrame;
    private int _hitCaptureFrame;
    private int _showcaseFrame;
    private int _showcaseCaptureFrame;
    private int _showcased;

    public override void _Ready()
    {
        // Evidence captures must contain only the committed generated artwork.
        CardArtwork.UseLocalArt = false;
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

        if (_lastCommandFrame >= 0 && _frame == _lastCommandFrame + 1)
        {
            CountSetCards();
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
            _showcaseFrame = _frame + 90;
        }

        CaptureEffects();
        CaptureSetReview();

        if (_engine is { State.IsOver: false } && _commands < MaxCommands && _frame % StepFrames == 0)
        {
            Step();
            return;
        }

        if (!_selectionChecked && _commands >= 6)
        {
            _selectionChecked = true;
            CheckSelection();
        }

        bool finished = _engine is null || _engine.State.IsOver || _commands >= MaxCommands;
        if (finished && !_tornDown && _frame >= _lastCommandFrame + SettleFrames)
        {
            BeginTeardown();
            return;
        }

        if (_tornDown && _frame >= _teardownFrame + TeardownFrames)
        {
            Finish();
        }
    }

    /// <summary>
    /// Set cards lie flat, face to the ground, at the foot of the upright cards so they
    /// never cover the row behind them: a Set Spell/Trap with its long side along the
    /// duel axis, a Set monster with it across the row.
    /// </summary>
    private void CountSetCards()
    {
        foreach (PlayerState p in _engine!.State.Players)
        {
            DuelStaging.SideAnchors side = p.Index == 0 ? Staging!.PlayerSide! : Staging!.OpponentSide!;
            if (side.PilesOnDisk)
            {
                // Nobody may read a deck: every card faces the floor and the pile grows upward. Graveyards face the sky.
                CheckPile(p.Deck, faceUp: false);
                CheckPile(p.Graveyard, faceUp: true);
            }

            foreach (CardInstance card in p.AllCards.Where(c => c.IsOnField && c.IsFaceUp))
            {
                // Face-up cards read from the player's camera whoever controls them (a monster taken by Snatch Steal too).
                if (Staging!.Cards.TryGetValue(card.Id, out CardView? faceUp) && faceUp.Anchor is not null)
                {
                    _faceUpSeen++;
                    Vector3 toPlayerSeat = (Player!.GlobalPosition - Opponent!.GlobalPosition).Normalized();
                    if (faceUp.RestTransform.Basis.Z.Dot(toPlayerSeat) < 0.9f)
                    {
                        _faceUpWrong++;
                    }
                }
            }

            foreach (CardInstance card in p.AllCards.Where(c => c.IsOnField && c.IsFaceDown))
            {
                if (!Staging!.Cards.TryGetValue(card.Id, out CardView? view) || view.Anchor is null)
                {
                    continue;
                }

                Transform3D rest = view.RestTransform;
                Basis anchor = view.Anchor.GlobalBasis;
                bool monster = card.Loc == Location.MonsterZone;
                bool flat = rest.Basis.Z.Dot(Vector3.Up) < -0.99f;
                bool along = Mathf.Abs(rest.Basis.Y.Dot(monster ? anchor.X : anchor.Z)) > 0.99f;
                bool low = Mathf.Abs(view.Anchor.GlobalPosition.Y - rest.Origin.Y - Rendering.HologramCards.Height / 2.0f) < 0.02f;
                _setCardsSeen++;
                if (monster)
                {
                    _setMonstersSeen++;
                }

                if (!(flat && along && low))
                {
                    _setCardsWrong++;
                }
            }
        }
    }

    private void CheckPile(IReadOnlyList<CardInstance> pile, bool faceUp)
    {
        float last = float.NegativeInfinity;
        foreach (CardInstance card in pile)
        {
            if (!Staging!.Cards.TryGetValue(card.Id, out CardView? view) || view.Anchor is null)
            {
                continue;
            }

            Transform3D rest = view.RestTransform;
            float up = rest.Basis.Z.Dot(Vector3.Up);
            _pileCardsSeen++;
            if ((faceUp ? up < 0.5f : up > -0.5f) || rest.Origin.Y < last)
            {
                _pileCardsWrong++;
            }

            last = rest.Origin.Y;
        }
    }

    /// <summary>CardSelected is one persistent effect per card that stops when the selection leaves (vfx/README.md).</summary>
    private void CheckSelection()
    {
        if (Staging is null)
        {
            return;
        }

        CardView? view = Staging.Cards.Values.FirstOrDefault(v => v.Card is { IsOnField: true }) ?? Staging.Cards.Values.First();
        int before = Staging.Effects.Active;
        view.SetSelected(true);
        view.SetSelected(true);
        Check(view.IsSelected && Staging.Effects.Active == before + 1 && Staging.Effects.Spawned.GetValueOrDefault(DuelEffects.CardSelected) == 1, Inv($"selecting a card spawns one CardSelected ({Staging.Effects.Active} active)"));
        Check(view.Mesh?.GetInstanceShaderParameter(Rendering.HologramCards.Selected).AsSingle() == 1.0f, "CardSelected drives the card's 'selected' uniform to 1");
        view.SetSelected(false);
        Check(!view.IsSelected && Staging.Effects.Active == before && Staging.Effects.Finished.GetValueOrDefault(DuelEffects.CardSelected) == 1, "deselecting stops it and the effect finishes");
        Check(view.Mesh?.GetInstanceShaderParameter(Rendering.HologramCards.Selected).AsSingle() == 0.0f, "'selected' uniform back to 0");
    }

    /// <summary>End of the duel: every card dissolves through the Dissolve scene and frees itself.</summary>
    private void BeginTeardown()
    {
        _tornDown = true;
        _teardownFrame = _frame;
        if (Staging is null)
        {
            return;
        }

        CheckEffects();
        _cardsBeforeTeardown = Staging.Cards.Count;
        _movesBeforeTeardown = Staging.Moves;
        _dissolvesBeforeTeardown = Staging.Effects.Spawned.GetValueOrDefault(DuelEffects.Dissolve);
        Staging.DissolveAll();
        Check(Staging.Effects.Spawned.GetValueOrDefault(DuelEffects.Dissolve) == _dissolvesBeforeTeardown + _cardsBeforeTeardown, Inv($"DissolveAll spawned one Dissolve per card ({_cardsBeforeTeardown})"));
    }

    private void CheckEffects()
    {
        if (Staging is null || _engine is null)
        {
            return;
        }

        DuelEffects effects = Staging.Effects;
        foreach (string effect in DuelEffects.Effects)
        {
            Check(effects.Available(effect), $"vfx/{effect}.tscn loads");
        }

        int draws = _engine.Events.Count(e => e is CardDrawn);
        int summons = _engine.Events.Count(e => e is MonsterSummoned or MonsterSpecialSummoned or MonsterFlipSummoned or TokenCreated);
        int attacks = _engine.Events.Count(e => e is AttackDeclared);
        int hits = _engine.Events.Count(e => e is BattleDamage or EffectDamage);
        int activations = _engine.Events.Count(e => e is SpellActivated or TrapActivated or EffectActivated);
        int sets = _engine.Events.Count(e => e is MonsterSet or SpellTrapSet);
        Check(draws > 0 && effects.Spawned.GetValueOrDefault(DuelEffects.CardMaterialise) >= draws + summons, Inv($"CardMaterialise spawned for every draw and summon ({effects.Spawned.GetValueOrDefault(DuelEffects.CardMaterialise)} for {draws} draws, {summons} summons)"));
        Check(summons > 0 && effects.Spawned.GetValueOrDefault(DuelEffects.SummonFlash) == summons + _showcased, Inv($"SummonFlash spawned per summon ({summons})"));
        Check(attacks > 0 && effects.Spawned.GetValueOrDefault(DuelEffects.AttackTrail) == attacks + _showcased, Inv($"AttackTrail spawned per attack ({attacks})"));
        Check(hits > 0 && effects.Spawned.GetValueOrDefault(DuelEffects.HitPulse) == hits + _showcased, Inv($"HitPulse spawned per damage ({hits})"));
        Check(effects.SoundsPlayed.GetValueOrDefault("draw") == draws && effects.SoundsPlayed.GetValueOrDefault("summon") == summons && effects.SoundsPlayed.GetValueOrDefault("attack") == attacks && effects.SoundsPlayed.GetValueOrDefault("hit") == hits,
            Inv($"draw, summon, attack and hit cues played per event ({draws}/{summons}/{attacks}/{hits})"));
        Check(effects.SoundsPlayed.GetValueOrDefault("activate") == activations && effects.SoundsPlayed.GetValueOrDefault("set") == sets, Inv($"activate and set cues played per event ({activations}/{sets})"));
        Report(activations > 0 && sets > 0 ? "PASS" : "INFO", Inv($"activations {activations}, sets {sets} in this seed"));
        int ended = _engine.State.IsOver ? 1 : 0;
        Check(effects.SoundsPlayed.GetValueOrDefault("win") + effects.SoundsPlayed.GetValueOrDefault("lose") == ended, Inv($"win/lose stinger {(ended == 1 ? "played once" : "not played, duel not over")}"));
        int finite = effects.Spawned.GetValueOrDefault(DuelEffects.SummonFlash) + effects.Spawned.GetValueOrDefault(DuelEffects.AttackTrail) + effects.Spawned.GetValueOrDefault(DuelEffects.HitPulse);
        int finiteDone = effects.Finished.GetValueOrDefault(DuelEffects.SummonFlash) + effects.Finished.GetValueOrDefault(DuelEffects.AttackTrail) + effects.Finished.GetValueOrDefault(DuelEffects.HitPulse);
        Check(finite == finiteDone, Inv($"every finite effect finished and freed itself ({finiteDone} of {finite})"));
        Check(effects.Active == 0, Inv($"no effect left running after the settle ({effects.Active} active)"));
        Check(effects.GetChildren().All(c => c is AudioStreamPlayer), Inv($"effect nodes gone from the tree ({effects.GetChildCount()} children, all cue players)"));
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

    /// <summary>With <c>--capture</c>, saves the frame a few ticks into the first AttackTrail and the first HitPulse after the main capture, for the artist's review through the duel camera.</summary>
    private void CaptureEffects()
    {
        if (_captureDir is null || !_captured || _captureSideFrame > 0 || _engine is null || CardFaces.IsHeadless)
        {
            return;
        }

        int attacks = _engine.Events.Count(e => e is AttackDeclared);
        int hits = _engine.Events.Count(e => e is BattleDamage or EffectDamage);
        if (_attackCaptureFrame == 0 && attacks > _attacksSeen)
        {
            _attackCaptureFrame = _frame + 8;
        }

        if (_hitCaptureFrame == 0 && hits > _hitsSeen)
        {
            _hitCaptureFrame = _frame + 6;
        }

        _attacksSeen = attacks;
        _hitsSeen = hits;
        if (_showcaseFrame > 0 && _frame == _showcaseFrame && Staging?.PlayerSide is { } ps && Staging.OpponentSide is { } os)
        {
            // A staged showcase of the three geometry effects at known anchors once the duel camera is back, so the review picture does not depend on the seed's timing.
            _showcaseFrame = -1;
            _showcaseCaptureFrame = _frame + 8;
            _showcased = 1;
            Transform3D playerZone = ps.Monsters[2].GlobalTransform;
            Transform3D opponentZone = os.Monsters[2].GlobalTransform;
            Staging.Effects.Spawn(DuelEffects.SummonFlash, playerZone, null, Rendering.HologramSide.Player);
            Staging.Effects.Spawn(DuelEffects.AttackTrail, playerZone, opponentZone, Rendering.HologramSide.Player);
            Staging.Effects.Spawn(DuelEffects.HitPulse, new Transform3D(os.Root.GlobalBasis, Opponent!.GlobalPosition + Vector3.Up * Staging.ChestHeight), null, Rendering.HologramSide.Opponent);
        }

        if (_showcaseCaptureFrame > 0 && _frame == _showcaseCaptureFrame)
        {
            GetViewport().GetTexture().GetImage().SavePng($"{_captureDir}/staging_effects.png");
            _showcaseCaptureFrame = -1;
            Report("INFO", "captured staging_effects.png (staged SummonFlash, AttackTrail, HitPulse)");
        }

        if (_attackCaptureFrame > 0 && _frame == _attackCaptureFrame)
        {
            GetViewport().GetTexture().GetImage().SavePng($"{_captureDir}/staging_attack.png");
            _attackCaptureFrame = -1;
            Report("INFO", "captured staging_attack.png");
        }

        if (_hitCaptureFrame > 0 && _frame == _hitCaptureFrame)
        {
            GetViewport().GetTexture().GetImage().SavePng($"{_captureDir}/staging_hit.png");
            _hitCaptureFrame = -1;
            Report("INFO", "captured staging_hit.png");
        }
    }

    /// <summary>
    /// <c>-- --capture</c>: the first time the opponent has a Set Spell/Trap in the column of one of their face-up monsters
    /// and a Set monster, saves <c>staging_set_duel.png</c> (the duel camera), <c>staging_set_field.png</c> (their field
    /// from above the player's side) and <c>staging_set_piles.png</c> (the player's disk). The pictures come from
    /// review viewports on the same world, so the rig and the other captures are not disturbed.
    /// </summary>
    private void CaptureSetReview()
    {
        if (_captureDir is null || _engine is null || CardFaces.IsHeadless || _reviewFrame < 0 || Staging?.OpponentSide is not { } os || Staging.PlayerSide is not { } ps)
        {
            return;
        }

        if (_reviewViews is null)
        {
            if (_frame != SetupFrames + 60 || Rig?.Camera is not { } duel)
            {
                return;
            }

            Vector3 toPlayer = (Player!.GlobalPosition - Opponent!.GlobalPosition).Normalized();
            Vector3 field = Opponent.GlobalPosition + toPlayer * (Staging.Forward + Staging.SpacingZ * 0.5f) + Vector3.Up * (Staging.ChestHeight - 0.1f);
            Vector3 piles = ps.Deck.GlobalPosition.Lerp(ps.Graveyard.GlobalPosition, 0.5f);
            _reviewViews = new[]
            {
                ReviewView("duel", duel.GlobalTransform, duel.Fov),
                ReviewView("field", Looking(field + toPlayer * 1.3f + Vector3.Up * 0.9f, field), 40.0f),
                ReviewView("piles", Looking(piles + toPlayer * 0.7f + Vector3.Up * 1.0f, piles), 40.0f),
            };
            return;
        }

        if (_reviewFrame == 0)
        {
            PlayerState opponent = _engine.State.Players[1];
            bool covered = opponent.SpellTraps.Any(st => st is { IsFaceDown: true } && opponent.Monsters.Any(m => m is { IsFaceUp: true } && m.ZoneIndex == st.ZoneIndex));
            bool setMonster = opponent.Monsters.Any(m => m is { IsFaceDown: true });
            if (_frame == _lastCommandFrame + 1)
            {
                // Not while a lunge is in flight: no attack among the events of the last two commands.
                bool quiet = !_engine.Events.Skip(_reviewEvents).Any(e => e is AttackDeclared);
                _reviewEvents = _reviewEventsNext;
                _reviewEventsNext = _engine.Events.Count;
                if (covered && setMonster && quiet)
                {
                    _reviewFrame = _frame + 12;
                }
            }

            return;
        }

        if (_frame == _reviewFrame)
        {
            foreach (SubViewport view in _reviewViews)
            {
                view.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
            }
        }
        else if (_frame == _reviewFrame + 2)
        {
            foreach (SubViewport view in _reviewViews)
            {
                view.GetTexture().GetImage().SavePng($"{_captureDir}/staging_set_{view.Name}.png");
                view.QueueFree();
            }

            _reviewFrame = -1;
            Report("INFO", "captured staging_set_duel.png, staging_set_field.png, staging_set_piles.png");
        }
    }

    private static Transform3D Looking(Vector3 from, Vector3 at) => new Transform3D(Basis.Identity, from).LookingAt(at, Vector3.Up);

    private SubViewport ReviewView(string name, Transform3D camera, float fov)
    {
        var view = new SubViewport { Name = name, Size = new Vector2I(1280, 720), RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled, Msaa3D = Viewport.Msaa.Msaa4X };
        AddChild(view);
        var eye = new Camera3D { Fov = fov, Current = true };
        view.AddChild(eye);
        eye.GlobalTransform = camera;
        return view;
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
        Staging?.Effects.Silence();
        ManagedWrappers.Flush();
        _done = true;
        if (_engine is not null && Staging is not null && Rig is not null)
        {
            DuelState s = _engine.State;
            Report("INFO", Inv($"{_commands} commands, turn {s.TurnNumber}, {s.Phase}, LP {s.Player(0).LifePoints}/{s.Player(1).LifePoints}, over: {s.IsOver}"));
            Check(_commands >= 40, Inv($"{_commands} agent commands played"));
            Check(_movesBeforeTeardown >= 30, Inv($"{_movesBeforeTeardown} card moves driven by the engine"));
            Check(Staging.EventsApplied >= _commands, Inv($"{Staging.EventsApplied} engine events applied"));
            Check(_mismatchFrames == 0, Inv($"card views matched the engine after every command ({_mismatchFrames} frames off)"));
            Check(_faceUpSeen > 0 && _faceUpWrong == 0, Inv($"face-up field cards face the player's camera whoever controls them ({_faceUpSeen} seen, {_faceUpWrong} wrong)"));
            Check(_pileCardsWrong == 0, Inv($"decks face the floor, graveyards face the sky and both piles grow upward ({_pileCardsSeen} seen, {_pileCardsWrong} wrong)"));
            Check(_setCardsSeen > 0 && _setCardsWrong == 0, Inv($"Set cards lie flat, face down, at the foot of their zone ({_setCardsSeen} seen, {_setMonstersSeen} monsters sideways, {_setCardsWrong} wrong)"));
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
            Check(Staging.Cards.Count == 0 && Staging.Effects.Active == 0 && Staging.Effects.Finished.GetValueOrDefault(DuelEffects.Dissolve) == _dissolvesBeforeTeardown + _cardsBeforeTeardown, Inv($"end dissolve finished for all {_cardsBeforeTeardown} cards, nothing active"));
            int cardNodes = 0;
            foreach (Node node in Staging.FindChildren("Card_*", recursive: true, owned: false))
            {
                cardNodes += node.IsQueuedForDeletion() ? 0 : 1;
            }

            Check(cardNodes == 0, Inv($"card views freed after the dissolve ({cardNodes} left)"));
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
