using System;
using System.Collections.Generic;
using BattleCity.Characters;
using BattleCity.Core;
using BattleCity.World;
using Godot;

namespace BattleCity.Diagnostics;

/// <summary>
/// Plays the issue #23 acceptance path through the real <see cref="Game"/>
/// autoload: New Game → starting room → exit door → Plaza arrival → Nico's
/// cone starts the tutorial encounter → placeholder duel. Scripted mode loses
/// the first duel (return to the encounter spot, no coin loss, 3 s disarm with
/// no retrigger), challenges Nico and wins (flag, Park gate opens, Mara
/// unlocks), checks the cone stays off after the victory, and goes back into
/// the room through the door. Phases advance on conditions with a timeout each.
/// </summary>
public partial class DistrictTestScene : Node
{
    private const string FailPrefix = "DistrictTest FAIL";

    private enum Phase
    {
        Boot,
        WaitRoom,
        WalkToExit,
        WaitDistrict,
        WaitEncounter,
        WaitDialogue,
        WaitDuel,
        WaitLoss,
        HoldInCone,
        WalkToNico,
        WaitRematchDialogue,
        WaitRematchDuel,
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

        G.LevelLoaded += OnLevelLoaded;
        Report("INFO", Scripted ? "scripted: room → plaza → Nico (lose, rematch, win) → gate → room" : "live: play from the starting room; the report only fills in scripted mode");
        G.NewGame();
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
                    _encounterOrigin = G.Encounters.Origin;
                    Check(G.Encounters.Current == _nico, "first Plaza entry started Nico's encounter from the cone");
                    Check(G.Encounters.Site is { Id: "nico" }, $"encounter uses the 'nico' site ({G.Encounters.Site?.Id})");
                    Check(!G.InputEnabled, "player input frozen for the encounter");
                    Next(Phase.WaitDialogue);
                }

                Timeout(240, "Nico's cone did not trigger on arrival");
                break;

            case Phase.WaitDialogue:
                if (G.Encounters.State == EncounterSystem.EncounterState.Dialogue && MessageReady())
                {
                    _standDistance = player!.GlobalPosition.DistanceTo(_nico!.Character!.GlobalPosition);
                    Check(Mathf.Abs(_standDistance - 7.0f) < 0.6f, Inv($"both walked to the stand points, {_standDistance:F2} m apart"));
                    Check(G.Messages.Text.StartsWith("Nico:", StringComparison.Ordinal), $"dialogue line shown ('{G.Messages.Text}')");
                    QueueInteract();
                    Next(Phase.WaitDuel);
                }

                Timeout(900, "approach did not reach the dialogue");
                break;

            case Phase.WaitDuel:
                if (G.Encounters.State == EncounterSystem.EncounterState.Duel && MessageReady())
                {
                    Check(G.Mode == GameMode.Duel, "mode is Duel for the placeholder duel");
                    Check(player!.CurrentState == Character.DuelReadyState || player.CurrentState == Character.DuelIdleState, $"player plays duel_ready ({player.CurrentState})");
                    QueueCancel();
                    Next(Phase.WaitLoss);
                }

                Timeout(240, "dialogue did not lead to the duel");
                break;

            case Phase.WaitLoss:
                if (Settled() && G.Encounters.State == EncounterSystem.EncounterState.Idle)
                {
                    Check(!G.Encounters.LastWon && !G.HasFlag(Flags.Defeated("d1")), "loss recorded, no flag set");
                    Check(player!.GlobalPosition.DistanceTo(_encounterOrigin) < 0.5f, Inv($"loss returned the player to the encounter spot ({player.GlobalPosition.DistanceTo(_encounterOrigin):F2} m off)"));
                    Check(G.Coins == Game.StartingCoins, Inv($"no coin loss ({G.Coins})"));
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
                if (G.Encounters.State == EncounterSystem.EncounterState.Duel && MessageReady())
                {
                    QueueInteract();
                    Next(Phase.WaitWin);
                }

                Timeout(240, "rematch dialogue did not lead to the duel");
                break;

            case Phase.WaitWin:
                if (Settled() && G.Encounters.State == EncounterSystem.EncounterState.Idle)
                {
                    Check(G.Encounters.LastWon && G.HasFlag(Flags.Defeated("d1")), "win set 'defeated:d1'");
                    Check(_parkGate is { IsOpen: true, Blocker: StaticBody3D { CollisionLayer: 0 } }, "Park gate opened and dropped its collision");
                    Check(_mara is { Locked: false } && _owner is { Locked: true }, "Mara unlocked, Arcade Owner still locked");
                    Check(_nico is { Defeated: true, IsArmed: false } && _nico.CanInteract(player!), "Nico defeated: cone off, rematch by interacting stays available");
                    Check(G.Coins == Game.StartingCoins, Inv($"coins unchanged by the placeholder duel ({G.Coins})"));
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
        _phase = Phase.Done;
        string summary = Inv($"DistrictTest summary: {_pass} pass, {_fail} fail ({_frame} frames)");
        GD.Print(summary);
        if (ReportLabel is not null)
        {
            ReportLabel.Text = summary + "\n" + string.Join("\n", _report);
        }
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
