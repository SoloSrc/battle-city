using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Characters;
using BattleCity.Core;
using BattleCity.Duel.Core;
using BattleCity.Duel.Core.Ai;
using BattleCity.Duel.Core.Commands;
using BattleCity.DuelScene;
using BattleCity.World;
using Godot;

namespace BattleCity.Diagnostics;

/// <summary>
/// Plays the issue #23 and #62 acceptance path through the real <see cref="Game"/>
/// autoload: New Game → starting room → exit door → Plaza arrival → Nico's
/// cone starts the tutorial encounter (the player keeps control while Nico
/// walks over) → the real duel. Scripted mode surrenders the first duel (lose
/// line, return to the meeting spot, no coin loss, tutorial hints shown once,
/// 3 s disarm with no retrigger), challenges Nico and wins with a heuristic
/// agent in the player's seat (flag, coins, booster cards in the collection,
/// Park gate opens, Mara unlocks), checks the cone stays off after the victory,
/// and goes back into the room through the door. Phases advance on conditions
/// with a timeout each.
/// </summary>
public partial class DistrictTestScene : Node
{
    private const string FailPrefix = "DistrictTest FAIL";

    /// <summary>Game seed of the scripted run; the rematch below is a known win for the scripted player with it.</summary>
    private const ulong Seed = 7;

    private enum Phase
    {
        Boot,
        WaitRoom,
        WalkToExit,
        WaitDistrict,
        WaitEncounter,
        WaitDialogue,
        WaitDuel,
        WaitLoseLine,
        WaitLoss,
        HoldInCone,
        WalkToNico,
        WaitRematchDialogue,
        WaitRematchDuel,
        PlayRematch,
        WaitWinLine,
        WaitRewardLine,
        WaitWin,
        WalkAway,
        WalkBack,
        WalkToRoomDoor,
        WaitRoomAgain,
        Done,
    }

    [Export]
    public Label? ReportLabel { get; set; }

    [Export]
    public bool Scripted { get; set; }

    private readonly List<string> _report = new();
    private Phase _phase = Phase.Boot;
    private int _phaseFrames;
    private int _frame;
    private int _pass;
    private int _fail;
    private bool _interactQueued;
    private bool _cancelQueued;
    private Vector3 _arrival;
    private Vector3 _encounterOrigin;
    private int _spottedCount;
    private bool _rearmedSeen;
    private int _idleFramesAfterRearm;
    private int _messageOpenFrames;
    private string _lastMessage = string.Empty;
    private Duelist? _nico;
    private Duelist? _mara;
    private Duelist? _owner;
    private ProgressionGate? _parkGate;
    private Door? _exitDoor;
    private float _standDistance;
    private bool _inputFreeDuringApproach;
    private int _hintsFirstDuel;
    private int _collectionBefore;
    private HeuristicAgent? _agent;
    private int _rematchCommands;
    private string? _captureDir;
    private readonly HashSet<string> _captured = new();

    private static Game G => Game.Instance!;

    public override void _Ready()
    {
        Scripted = Scripted || DisplayServer.GetName() == "headless"
            || Array.IndexOf(OS.GetCmdlineUserArgs(), "--scripted") >= 0;
        if (Game.Instance is null)
        {
            Fail("Game autoload missing");
            _phase = Phase.Done;
            return;
        }

        string[] args = OS.GetCmdlineUserArgs();
        int capture = Array.IndexOf(args, "--capture");
        if (capture >= 0 && capture + 1 < args.Length)
        {
            _captureDir = args[capture + 1];
        }

        G.LevelLoaded += OnLevelLoaded;
        Report("INFO", Scripted ? "scripted: room → plaza → Nico (surrender, rematch, win) → gate → room" : "live: play from the starting room; the report only fills in scripted mode");
        G.NewGame(Seed);
    }

    private void OnLevelLoaded(string scenePath, string spawnId)
    {
        Report("INFO", $"level loaded: {scenePath.Substring(scenePath.LastIndexOf('/') + 1)} at '{spawnId}'");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Scripted || _phase == Phase.Done)
        {
            return;
        }

        _frame++;
        _phaseFrames++;
        // count frames the current box has been open; a new text (dialogue → duel) restarts the count
        _messageOpenFrames = G.Messages.IsOpen && G.Messages.Text == _lastMessage ? _messageOpenFrames + 1 : G.Messages.IsOpen ? 1 : 0;
        _lastMessage = G.Messages.IsOpen ? G.Messages.Text : string.Empty;
        ReleaseQueued();
        Step();
    }

    private void Step()
    {
        Character? player = G.Player;
        switch (_phase)
        {
            case Phase.Boot:
                Next(Phase.WaitRoom);
                break;

            case Phase.WaitRoom:
                if (Settled() && G.LevelPath == Paths.StartRoomScene)
                {
                    PlayerSpawn? spawn = PlayerSpawn.Find(GetTree(), PlayerSpawn.ArrivalId);
                    Check(G.Mode == GameMode.Interior, $"New Game loaded the starting room in Interior mode ({G.Mode})");
                    Check(spawn is not null && player is not null && player.GlobalPosition.DistanceTo(spawn.GlobalPosition) < 0.5f, "player placed at the room's 'arrival' spawn");
                    Check(G.Camera is { ActiveBounds.Interior: true }, "camera rig picked the interior bounds");
                    _exitDoor = FindDoor();
                    Check(_exitDoor is not null, "room has an exit door");
                    if (_exitDoor is not null)
                    {
                        G.Controller?.WalkTo(_exitDoor.GlobalPosition + _exitDoor.GlobalTransform.Basis.Z * 1.0f);
                    }

                    Next(Phase.WalkToExit);
                }

                Timeout(240, "starting room did not settle");
                break;

            case Phase.WalkToExit:
                if (G.Controller is { IsAutoWalking: false, CurrentInteractable: Door })
                {
                    Check(G.Prompt.Text.Contains("Go outside", StringComparison.Ordinal), $"door prompt shows the door's verb ('{G.Prompt.Text}')");
                    QueueInteract();
                    Next(Phase.WaitDistrict);
                }

                Timeout(400, "player did not reach the exit door");
                break;

            case Phase.WaitDistrict:
                if (G.LevelPath == Paths.DistrictScene && player is not null && !G.IsTransitioning)
                {
                    _arrival = player.GlobalPosition;
                    FindDistrictNodes();
                    Check(G.Mode is GameMode.Overworld or GameMode.Duel, $"door transition reached the district ({G.Mode})");
                    Check(_arrival.DistanceTo(new Vector3(58.0f, 0.0f, 98.0f)) < 0.5f, Inv($"arrived at the Plaza spawn {_arrival}"));
                    Check(_parkGate is { IsOpen: false, Blocker: StaticBody3D { CollisionLayer: PhysicsLayers.World } }, "Park gate closed and solid before Nico is beaten");
                    Check(_mara is { Locked: true } && _owner is { Locked: true }, "Mara and the Arcade Owner are locked before their flags");
                    Check(_nico is not null && _nico.CanInteract(player), "Nico is challengeable");
                    if (_nico is not null)
                    {
                        _nico.PlayerSpotted += _ => _spottedCount++;
                    }

                    Next(Phase.WaitEncounter);
                }

                Timeout(400, "district did not load after the door");
                break;

            case Phase.WaitEncounter:
                if (G.Encounters.State != EncounterSystem.EncounterState.Idle)
                {
                    Check(G.Encounters.Current == _nico, "first Plaza entry started Nico's encounter from the cone");
                    Check(G.InputEnabled, "player keeps control when spotted");
                    Check(G.Collection is { Deck.Count: 40 }, Inv($"collection holds the starter deck ({G.Collection?.Deck.Count ?? 0} cards)"));
                    _collectionBefore = G.Collection?.Total ?? 0;
                    _inputFreeDuringApproach = true;
                    Next(Phase.WaitDialogue);
                }

                Timeout(240, "Nico's cone did not trigger on arrival");
                break;

            case Phase.WaitDialogue:
                if (G.Encounters.State == EncounterSystem.EncounterState.Approach && !G.InputEnabled)
                {
                    _inputFreeDuringApproach = false;
                }

                if (G.Encounters.State == EncounterSystem.EncounterState.Approach && _phaseFrames >= 60)
                {
                    Capture("approach");
                }

                if (G.Encounters.State == EncounterSystem.EncounterState.Dialogue && MessageReady())
                {
                    _encounterOrigin = G.Encounters.Origin;
                    _standDistance = player!.GlobalPosition.DistanceTo(_nico!.Character!.GlobalPosition);
                    Check(_inputFreeDuringApproach, "input stayed enabled while Nico walked over");
                    Check(!G.InputEnabled, "input locked once Nico arrived");
                    Check(_encounterOrigin.DistanceTo(_arrival) < 0.5f, Inv($"meeting spot is where the player stood ({_encounterOrigin.DistanceTo(_arrival):F2} m from arrival)"));
                    Check(G.Encounters.Site is { Id: "nico" }, $"encounter uses the 'nico' site ({G.Encounters.Site?.Id})");
                    Check(Mathf.Abs(_standDistance - 7.0f) < 0.6f, Inv($"both walked to the stand points, {_standDistance:F2} m apart"));
                    Check(G.Messages.Text.StartsWith("Nico:", StringComparison.Ordinal), $"challenge line shown ('{G.Messages.Text}')");
                    Capture("challenge");
                    QueueInteract();
                    Next(Phase.WaitDuel);
                }

                Timeout(900, "approach did not reach the dialogue");
                break;

            case Phase.WaitDuel:
                if (G.Encounters.State == EncounterSystem.EncounterState.Duel && G.Duels is { IsRunning: true } duels && _phaseFrames >= 100)
                {
                    DuelUi ui = duels.Ui;
                    Check(G.Mode == GameMode.Duel, "mode is Duel");
                    Check(player!.CurrentState is Character.DuelReadyState or Character.DuelIdleState or Character.DrawCardState or Character.PlayCardState, $"player is in the duel states ({player.CurrentState})");
                    Check(duels.Staging.IsStaged && duels.Staging.Cards.Count == 80 && duels.Staging.Mismatches() == 0, Inv($"staging holds both decks ({duels.Staging.Cards.Count} card views, {duels.Staging.Mismatches()} mismatches)"));
                    Check(ui.Mode != DuelUiMode.Unbound && ui.HandCount == duels.Session.Engine!.State.Player(0).Hand.Count, Inv($"HUD bound with the hand fan ({ui.Mode}, {ui.HandCount} cards)"));
                    Check(G.Camera is { DuelActive: true }, "camera holds the duel framing");
                    Check(ui.Tutorial && ui.HintsShown > 0 && ui.Hint.Length > 0, Inv($"tutorial hint shown in the first duel ({ui.HintsShown} so far: '{ui.Hint}')"));
                    _hintsFirstDuel = ui.HintsShown;
                    Capture("duel");
                    SubmitResult result = duels.Session.Submit(new Surrender(DuelDirector.HumanSeat));
                    Check(result.Accepted, $"scripted player surrenders the tutorial duel ({result.Error ?? "accepted"})");
                    Next(Phase.WaitLoseLine);
                }

                Timeout(300, "dialogue did not lead to the duel");
                break;

            case Phase.WaitLoseLine:
                if (G.Encounters.State == EncounterSystem.EncounterState.Lines && MessageReady())
                {
                    Check(G.Messages.Text.StartsWith("Nico: Heh.", StringComparison.Ordinal), $"lose line shown ('{G.Messages.Text}')");
                    Check(G.Duels is { IsRunning: false } && G.Duels.Ui.Mode == DuelUiMode.Unbound && G.Duels.Staging.Cards.Count == 0, "duel torn down before the line: HUD off, cards dissolved");
                    Check(G.Camera is { DuelActive: false }, "camera blending back to the overworld");
                    QueueInteract();
                    Next(Phase.WaitLoss);
                }

                Timeout(400, "surrender did not lead to the lose line");
                break;

            case Phase.WaitLoss:
                if (Settled() && G.Encounters.State == EncounterSystem.EncounterState.Idle)
                {
                    Check(!G.Encounters.LastWon && !G.HasFlag(Flags.Defeated("d1")), "loss recorded, no flag set");
                    Check(G.HasFlag(Flags.TutorialDone), "'tutorial_done' set after the first duel");
                    Check(player!.GlobalPosition.DistanceTo(_encounterOrigin) < 0.5f, Inv($"loss returned the player to the meeting spot ({player.GlobalPosition.DistanceTo(_encounterOrigin):F2} m off)"));
                    Check(G.Coins == Game.StartingCoins && G.Collection?.Total == _collectionBefore, Inv($"no coin loss, collection unchanged ({G.Coins} coins, {G.Collection?.Total} cards)"));
                    Check(_nico is { RearmIn: > 0.0f, IsArmed: false }, Inv($"cone disarmed after the duel ({_nico?.RearmIn:F1} s left)"));
                    Check(G.Mode == GameMode.Overworld && G.InputEnabled, "back to Overworld with input enabled");
                    _spottedCount = 0;
                    Next(Phase.HoldInCone);
                }

                Timeout(400, "loss did not resolve");
                break;

            case Phase.HoldInCone:
                if (_nico is { IsArmed: true })
                {
                    _rearmedSeen = true;
                    _idleFramesAfterRearm++;
                }

                if (_idleFramesAfterRearm >= 90)
                {
                    bool inCone = _nico!.IsInCone(player!.GlobalPosition);
                    Check(_rearmedSeen && G.Encounters.State == EncounterSystem.EncounterState.Idle && _spottedCount == 0,
                        Inv($"cone re-armed after {G.Encounters.DisarmTime:F0} s without retriggering on the standing player (in cone: {inCone})"));
                    G.Controller?.WalkTo(ApproachPoint(_nico.Character!, player));
                    Next(Phase.WalkToNico);
                }

                Timeout(600, "cone never re-armed");
                break;

            case Phase.WalkToNico:
                if (G.Controller is { IsAutoWalking: false, CurrentInteractable: Duelist })
                {
                    QueueInteract();
                    Next(Phase.WaitRematchDialogue);
                }

                Timeout(600, "player did not reach Nico for the challenge");
                break;

            case Phase.WaitRematchDialogue:
                if (G.Encounters.State == EncounterSystem.EncounterState.Dialogue && MessageReady())
                {
                    Check(G.Encounters.Current == _nico, "manual challenge started the encounter");
                    QueueInteract();
                    Next(Phase.WaitRematchDuel);
                }

                Timeout(900, "challenge did not reach the dialogue");
                break;

            case Phase.WaitRematchDuel:
                if (G.Encounters.State == EncounterSystem.EncounterState.Duel && G.Duels is { IsRunning: true } rematch && _phaseFrames >= 20)
                {
                    Check(!rematch.Ui.Tutorial && rematch.Ui.HintsShown == 0, Inv($"no tutorial hints in the second duel ({rematch.Ui.HintsShown})"));
                    rematch.Session.AiDelay = 0.0f;
                    _agent = new HeuristicAgent(AiProfile.ArcadeOwner, Seed);
                    _rematchCommands = 0;
                    Report("INFO", Inv($"rematch: a heuristic agent plays the player's seat (seed {Seed}, opponent {rematch.Session.Engine!.State.Player(1).Deck.Count + rematch.Session.Engine.State.Player(1).Hand.Count} cards)"));
                    Next(Phase.PlayRematch);
                }

                Timeout(300, "rematch dialogue did not lead to the duel");
                break;

            case Phase.PlayRematch:
                if (G.Duels is { IsRunning: true } running && _agent is not null && running.Session.IsHumanTurn)
                {
                    DuelEngine engine = running.Session.Engine!;
                    running.Session.Submit(_agent.Choose(engine, DuelDirector.HumanSeat, engine.LegalActions(DuelDirector.HumanSeat)));
                    _rematchCommands++;
                }

                if (G.Duels is { IsRunning: true } && _phaseFrames >= 75)
                {
                    Capture("rematch");
                }

                if (G.Duels is { IsRunning: false } over && G.Encounters.State != EncounterSystem.EncounterState.Duel)
                {
                    Report("INFO", Inv($"rematch over after {_rematchCommands} scripted commands in {_phaseFrames} frames: {over.Session.Engine?.State.Outcome.ToString() ?? "no engine"}, winner {over.Session.Engine?.State.Winner?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}"));
                    Next(Phase.WaitWinLine);
                }

                Timeout(6000, "rematch duel did not finish");
                break;

            case Phase.WaitWinLine:
                if (G.Encounters.State == EncounterSystem.EncounterState.Lines && MessageReady())
                {
                    Check(G.Encounters.LastWon, "scripted player won the rematch");
                    Check(G.Messages.Text.StartsWith("Nico: Not bad.", StringComparison.Ordinal), $"win line shown ('{G.Messages.Text}')");
                    QueueInteract();
                    Next(Phase.WaitRewardLine);
                }

                Timeout(400, "rematch did not lead to the win line");
                break;

            case Phase.WaitRewardLine:
                if (G.Encounters.State == EncounterSystem.EncounterState.Lines && MessageReady() && G.Messages.Text.StartsWith("Reward", StringComparison.Ordinal))
                {
                    Check(G.Messages.Text.Contains("600 coins", StringComparison.Ordinal) && G.Messages.Text.Contains("Street Pack", StringComparison.Ordinal), $"reward line names the coins and the pack ('{G.Messages.Text}')");
                    Capture("reward");
                    QueueInteract();
                    Next(Phase.WaitWin);
                }

                Timeout(400, "win line did not lead to the reward line");
                break;

            case Phase.WaitWin:
                if (Settled() && G.Encounters.State == EncounterSystem.EncounterState.Idle)
                {
                    Check(G.Encounters.LastWon && G.HasFlag(Flags.Defeated("d1")), "win set 'defeated:d1'");
                    Check(_parkGate is { IsOpen: true, Blocker: StaticBody3D { CollisionLayer: 0 } }, "Park gate opened and dropped its collision");
                    Check(_mara is { Locked: false } && _owner is { Locked: true }, "Mara unlocked, Arcade Owner still locked");
                    Check(_nico is { Defeated: true, IsArmed: false } && _nico.CanInteract(player!), "Nico defeated: cone off, rematch by interacting stays available");
                    Check(G.Coins == Game.StartingCoins + 600 && G.Encounters.LastRewardCoins == 600, Inv($"first win pays 600 coins ({G.Coins})"));
                    Check(G.Encounters.LastRewardCards.Count == 5 && G.Collection?.Total == _collectionBefore + 5 && G.Encounters.LastRewardCards.All(id => G.Data!.Cards.Contains(id)), Inv($"one Street Pack of five library cards joined the collection ({G.Collection?.Total} cards: {string.Join(", ", G.Encounters.LastRewardCards)})"));
                    Check(_hintsFirstDuel > 0 && G.Duels is { Ui.HintsShown: 0 }, Inv($"tutorial hints only in the first duel ({_hintsFirstDuel} then 0)"));
                    _spottedCount = 0;
                    Vector3 away = _nico!.Character!.GlobalPosition + _nico.Facing * 11.0f;
                    G.Controller?.WalkTo(away);
                    Next(Phase.WalkAway);
                }

                Timeout(400, "win did not resolve");
                break;

            case Phase.WalkAway:
                if (G.Controller is { IsAutoWalking: false } || _phaseFrames > 420)
                {
                    Vector3 back = _nico!.Character!.GlobalPosition + _nico.Facing * 3.0f;
                    G.Controller?.WalkTo(back);
                    Next(Phase.WalkBack);
                }

                break;

            case Phase.WalkBack:
                if (G.Controller is { IsAutoWalking: false } || _phaseFrames > 420)
                {
                    bool inCone = _nico!.IsInCone(player!.GlobalPosition);
                    Check(inCone && _spottedCount == 0 && G.Encounters.State == EncounterSystem.EncounterState.Idle, Inv($"walking back into the cone after the victory does not retrigger (in cone: {inCone})"));
                    Door? roomDoor = FindDoor(Paths.StartRoomScene);
                    Check(roomDoor is not null, "district has the room door");
                    if (roomDoor is not null)
                    {
                        G.Controller?.WalkTo(roomDoor.GlobalPosition + roomDoor.GlobalTransform.Basis.Z * 1.2f);
                    }

                    Next(Phase.WalkToRoomDoor);
                }

                break;

            case Phase.WalkToRoomDoor:
                if (G.Controller is { IsAutoWalking: false, CurrentInteractable: Door })
                {
                    QueueInteract();
                    Next(Phase.WaitRoomAgain);
                }

                Timeout(900, "player did not reach the room door");
                break;

            case Phase.WaitRoomAgain:
                if (Settled() && G.LevelPath == Paths.StartRoomScene)
                {
                    PlayerSpawn? spawn = PlayerSpawn.Find(GetTree(), "door");
                    Check(spawn is not null && player!.GlobalPosition.DistanceTo(spawn.GlobalPosition) < 0.5f, "returned into the room at its 'door' spawn");
                    Check(G.Mode == GameMode.Interior, "mode back to Interior");
                    Finish();
                }

                Timeout(400, "did not get back into the room");
                break;
        }
    }

    private bool Settled() => !G.IsTransitioning && G.InputEnabled && !G.Messages.IsOpen;

    /// <summary>The box ignores input on the frame it opens; dwell a few frames so renders of the run show it.</summary>
    private bool MessageReady() => G.Messages.IsOpen && _messageOpenFrames >= 20;

    private void Next(Phase phase)
    {
        _phase = phase;
        _phaseFrames = 0;
    }

    private void Timeout(int frames, string message)
    {
        if (_phaseFrames > frames)
        {
            Fail(Inv($"{message} (phase {_phase}, frame {_frame})"));
            Finish();
        }
    }

    private void QueueInteract()
    {
        Input.ActionPress(InputActions.Interact);
        _interactQueued = true;
    }

    private void QueueCancel()
    {
        Input.ActionPress(InputActions.Cancel);
        _cancelQueued = true;
    }

    private void ReleaseQueued()
    {
        if (_interactQueued)
        {
            Input.ActionRelease(InputActions.Interact);
            _interactQueued = false;
        }

        if (_cancelQueued)
        {
            Input.ActionRelease(InputActions.Cancel);
            _cancelQueued = false;
        }
    }

    private static Vector3 ApproachPoint(Character npc, Character player)
    {
        Vector3 from = player.GlobalPosition - npc.GlobalPosition;
        from.Y = 0.0f;
        from = from.LengthSquared() > 0.001f ? from.Normalized() : Vector3.Back;
        return npc.GlobalPosition + from * 1.2f;
    }

    private Door? FindDoor(string? target = null)
    {
        foreach (Node node in GetTree().GetNodesInGroup(Groups.Interactable))
        {
            if (node is Door door && (target is null || door.TargetScene == target))
            {
                return door;
            }
        }

        return null;
    }

    private void FindDistrictNodes()
    {
        foreach (Node node in GetTree().GetNodesInGroup(Groups.Duelist))
        {
            if (node is Duelist d)
            {
                switch (d.DuelistId)
                {
                    case "d1":
                        _nico = d;
                        break;
                    case "d2":
                        _mara = d;
                        break;
                    case "d3":
                        _owner = d;
                        break;
                }
            }
        }

        foreach (Node node in GetTree().GetNodesInGroup(Groups.Gate))
        {
            if (node is ProgressionGate gate && gate.RequiredFlag == Flags.Defeated("d1"))
            {
                _parkGate = gate;
            }
        }
    }

    private void Finish()
    {
        ManagedWrappers.Flush();
        _phase = Phase.Done;
        string summary = Inv($"DistrictTest summary: {_pass} pass, {_fail} fail ({_frame} frames)");
        GD.Print(summary);
        if (ReportLabel is not null)
        {
            ReportLabel.Text = summary + "\n" + string.Join("\n", _report);
        }
    }

    /// <summary>Windowed with <c>-- --capture &lt;dir&gt;</c>: saves <c>district_&lt;name&gt;.png</c> once per name.</summary>
    private void Capture(string name)
    {
        if (_captureDir is null || DisplayServer.GetName() == "headless" || !_captured.Add(name))
        {
            return;
        }

        DirAccess.MakeDirRecursiveAbsolute(_captureDir);
        GetViewport().GetTexture().GetImage().SavePng($"{_captureDir}/district_{name}.png");
        Report("INFO", $"captured district_{name}.png");
    }

    private static string Inv(FormattableString text) => FormattableString.Invariant(text);

    private void Check(bool ok, string message)
    {
        if (ok)
        {
            _pass++;
            Report("PASS", message);
        }
        else
        {
            Fail(message);
        }
    }

    private void Fail(string message)
    {
        _fail++;
        Report("FAIL", message);
        GD.PrintErr($"{FailPrefix}: {message}");
    }

    private void Report(string status, string message)
    {
        string line = Inv($"{message} (frame {_frame})");
        _report.Add($"[{status}] {line}");
        GD.Print($"DistrictTest {status}: {line}");
    }
}
