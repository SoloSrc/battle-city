using System;
using BattleCity.Characters;
using BattleCity.Core;
using Godot;

namespace BattleCity.World;

/// <summary>
/// Runs a street encounter (systems.md §4.3): a <see cref="Duelist"/> spots the
/// player or is challenged, an exclamation shows, the duelist walks to the
/// nearer stand point of the closest <see cref="EncounterSite"/> while the
/// controller walks the player to the other one, a dialogue line plays and the
/// duel starts. The duel itself is a placeholder until <c>DuelStaging</c> and
/// <c>Duel.Core</c> land: a choice box decides win or lose. A win sets
/// <c>defeated:&lt;id&gt;</c> (gates open, the cone stays off, rematches stay
/// manual); a loss returns the player to the encounter spot with no coin loss
/// and disarms the cone for <see cref="DisarmTime"/>.
/// </summary>
public partial class EncounterSystem : Node
{
    public enum EncounterState
    {
        Idle,
        Exclaim,
        Approach,
        Dialogue,
        Duel,
        Result,
        Returning,
    }

    [Signal]
    public delegate void EncounterStartedEventHandler(string duelistId, bool manual);

    [Signal]
    public delegate void DuelStartedEventHandler(string duelistId);

    [Signal]
    public delegate void DuelFinishedEventHandler(string duelistId, bool won);

    private const string LockReason = "encounter";
    private const float SiteRange = 10.0f;
    private const float FallbackDistance = 3.5f;
    private const float ArriveRadius = 0.25f;
    private const float SweepStep = 0.5f;
    private const float SweepMax = 3.0f;

    [Export(PropertyHint.Range, "0,3,0.1,suffix:s")]
    public float ExclaimTime { get; set; } = 0.6f;

    [Export(PropertyHint.Range, "0,10,0.1,suffix:m/s")]
    public float WalkSpeed { get; set; } = 2.2f;

    [Export(PropertyHint.Range, "1,30,1,suffix:s")]
    public float ApproachTimeout { get; set; } = 10.0f;

    [Export(PropertyHint.Range, "0,5,0.1,suffix:s")]
    public float ResultTime { get; set; } = 1.0f;

    /// <summary>Cone disarm after a duel ends so a loss cannot retrigger while the player is still inside it.</summary>
    [Export(PropertyHint.Range, "0,10,0.5,suffix:s")]
    public float DisarmTime { get; set; } = 3.0f;

    public EncounterState State { get; private set; }

    public Duelist? Current { get; private set; }

    public EncounterSite? Site { get; private set; }

    /// <summary>Where the player stood when the encounter began; a loss returns them here.</summary>
    public Vector3 Origin { get; private set; }

    public Vector3 DuelistStand { get; private set; }

    public Vector3 PlayerStand { get; private set; }

    public bool LastWon { get; private set; }

    private Character? _player;
    private PlayerController? _controller;
    private bool _manual;
    private float _timer;
    private Vector3[] _path = Array.Empty<Vector3>();
    private int _pathIndex;
    private Label3D? _exclamation;
    private bool _dialogueOpen;
    private bool _duelOpen;
    private float _gravityVelocity;

    /// <summary>Begins an encounter; ignored while one runs, during transitions, or for locked duelists.</summary>
    public void Start(Duelist duelist, Character player, bool manual)
    {
        if (State != EncounterState.Idle || duelist.Locked || duelist.Character is null)
        {
            return;
        }

        if (Game.Instance is { IsTransitioning: true })
        {
            return;
        }

        if (!manual && !duelist.IsArmed)
        {
            return;
        }

        Current = duelist;
        _player = player;
        _controller = player.GetNodeOrNull<PlayerController>("Controller");
        _manual = manual;
        Origin = player.GlobalPosition;
        _gravityVelocity = 0.0f;
        ResolveStands(duelist, player);
        Game.Instance?.LockInput(LockReason);
        _controller?.StopWalk();
        player.Velocity = Vector3.Zero;
        FaceToward(duelist.Character, player.GlobalPosition);
        ShowExclamation(duelist.Character);
        _timer = ExclaimTime;
        State = EncounterState.Exclaim;
        GD.Print($"Encounter: {duelist.DuelistId} {(manual ? "challenged" : "spotted the player")}");
        EmitSignal(SignalName.EncounterStarted, duelist.DuelistId, manual);
    }

    /// <summary>Drops the current encounter without a result (level unload).</summary>
    public void Abort()
    {
        if (State == EncounterState.Idle)
        {
            return;
        }

        HideExclamation();
        Game.Instance?.Messages.Close(false);
        _controller?.StopWalk();
        Finish();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        switch (State)
        {
            case EncounterState.Exclaim:
                _timer -= dt;
                if (_timer <= 0.0f)
                {
                    HideExclamation();
                    BeginApproach();
                }

                break;
            case EncounterState.Approach:
                StepApproach(dt);
                break;
            case EncounterState.Dialogue:
            case EncounterState.Duel:
            case EncounterState.Returning:
                break;
            case EncounterState.Result:
                _timer -= dt;
                if (_timer <= 0.0f)
                {
                    EndResult();
                }

                break;
        }
    }

    private void ResolveStands(Duelist duelist, Character player)
    {
        Character npc = duelist.Character!;
        Site = FindSite(duelist, npc.GlobalPosition);
        Vector3 axis;
        if (Site is not null)
        {
            Vector3 a = Site.StandPointA;
            Vector3 b = Site.StandPointB;
            bool npcTakesA = npc.GlobalPosition.DistanceSquaredTo(a) <= npc.GlobalPosition.DistanceSquaredTo(b);
            DuelistStand = npcTakesA ? a : b;
            PlayerStand = npcTakesA ? b : a;
            axis = Site.AxisWorld;
        }
        else
        {
            Vector3 dir = player.GlobalPosition - npc.GlobalPosition;
            dir.Y = 0.0f;
            dir = dir.LengthSquared() > 0.001f ? dir.Normalized() : npc.GlobalTransform.Basis.Z * -1.0f;
            DuelistStand = npc.GlobalPosition;
            PlayerStand = SnapToNavmesh(npc.GlobalPosition + dir * FallbackDistance);
            axis = dir;
        }

        DuelistStand = ResolveFree(DuelistStand, axis);
        PlayerStand = ResolveFree(PlayerStand, axis);
    }

    private EncounterSite? FindSite(Duelist duelist, Vector3 from)
    {
        EncounterSite? best = null;
        EncounterSite? bestAny = null;
        float bestDistance = float.MaxValue;
        float bestAnyDistance = float.MaxValue;
        foreach (Node node in GetTree().GetNodesInGroup(Groups.EncounterSite))
        {
            if (node is not EncounterSite site)
            {
                continue;
            }

            float distance = site.GlobalPosition.DistanceTo(from);
            if (distance > SiteRange)
            {
                continue;
            }

            if (site.DuelistId == duelist.DuelistId && distance < bestDistance)
            {
                best = site;
                bestDistance = distance;
            }

            if (distance < bestAnyDistance)
            {
                bestAny = site;
                bestAnyDistance = distance;
            }
        }

        return best ?? bestAny;
    }

    /// <summary>Capsule check at <paramref name="point"/>; on a hit, searches outward along the axis in 0.5 m steps for up to 3 m. Never teleports elsewhere.</summary>
    private Vector3 ResolveFree(Vector3 point, Vector3 axis)
    {
        if (_player is null || IsFree(point))
        {
            return point;
        }

        axis.Y = 0.0f;
        if (axis.LengthSquared() < 0.001f)
        {
            return point;
        }

        axis = axis.Normalized();
        for (float step = SweepStep; step <= SweepMax + 0.001f; step += SweepStep)
        {
            Vector3 forward = point + axis * step;
            if (IsFree(forward))
            {
                return forward;
            }

            Vector3 back = point - axis * step;
            if (IsFree(back))
            {
                return back;
            }
        }

        return point;
    }

    private bool IsFree(Vector3 point)
    {
        if (_player is null)
        {
            return true;
        }

        var shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.7f };
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            Transform = new Transform3D(Basis.Identity, point + Vector3.Up * 0.9f),
            CollisionMask = PhysicsLayers.World,
            CollideWithAreas = false,
        };
        return _player.GetWorld3D().DirectSpaceState.IntersectShape(query, 1).Count == 0;
    }

    private Vector3 SnapToNavmesh(Vector3 point)
    {
        if (_player is null)
        {
            return point;
        }

        Rid map = _player.GetWorld3D().NavigationMap;
        Vector3 closest = NavigationServer3D.MapGetClosestPoint(map, point);
        return closest.DistanceTo(point) < 2.0f ? closest : point;
    }

    private void BeginApproach()
    {
        if (Current?.Character is not { } npc || _player is null)
        {
            Finish();
            return;
        }

        Rid map = _player.GetWorld3D().NavigationMap;
        Vector3[] path = NavigationServer3D.MapGetPath(map, npc.GlobalPosition, DuelistStand, true);
        _path = path.Length >= 2 ? path : new[] { npc.GlobalPosition, DuelistStand };
        GD.Print(FormattableString.Invariant($"Encounter: {Current.DuelistId} walks {(path.Length >= 2 ? "a navmesh path" : "a straight line")} of {_path.Length} points to the stand point; player to {PlayerStand}"));
        _pathIndex = 1;
        _controller?.WalkTo(PlayerStand);
        _timer = ApproachTimeout;
        State = EncounterState.Approach;
    }

    private void StepApproach(float dt)
    {
        if (Current?.Character is not { } npc || _player is null)
        {
            Finish();
            return;
        }

        _timer -= dt;
        bool npcArrived = StepDuelist(npc, dt);
        bool playerArrived = _controller is null || !_controller.IsAutoWalking;
        if ((npcArrived && playerArrived) || _timer <= 0.0f)
        {
            if (_timer <= 0.0f)
            {
                GD.PushWarning($"Encounter: approach timed out ({Current.DuelistId}); starting from current positions");
                _controller?.StopWalk();
            }

            npc.Velocity = Vector3.Zero;
            npc.SetLocomotion(0.0f);
            FaceToward(npc, _player.GlobalPosition);
            FaceToward(_player, npc.GlobalPosition);
            BeginDialogue();
        }
    }

    /// <summary>Moves the duelist along its path at walk speed; true once at the stand point.</summary>
    private bool StepDuelist(Character npc, float dt)
    {
        Vector3 target = _pathIndex < _path.Length ? _path[_pathIndex] : DuelistStand;
        Vector3 to = target - npc.GlobalPosition;
        to.Y = 0.0f;
        float distance = to.Length();
        bool last = _pathIndex >= _path.Length - 1;
        if (distance <= ArriveRadius)
        {
            if (!last)
            {
                _pathIndex++;
                return false;
            }

            npc.Velocity = new Vector3(0.0f, npc.Velocity.Y, 0.0f);
            npc.SetLocomotion(0.0f);
            return true;
        }

        Vector3 dir = to / distance;
        float speed = last ? Mathf.Min(WalkSpeed, distance / dt) : WalkSpeed;
        _gravityVelocity = npc.IsOnFloor() ? -0.1f : _gravityVelocity - 9.8f * dt;
        npc.Velocity = new Vector3(dir.X * speed, _gravityVelocity, dir.Z * speed);
        npc.MoveAndSlide();
        npc.SetLocomotion(WalkSpeed);
        npc.Rotation = new Vector3(0.0f, Mathf.Atan2(-dir.X, -dir.Z), 0.0f);
        return false;
    }

    private void BeginDialogue()
    {
        if (Current is null || Game.Instance is not { } game)
        {
            Finish();
            return;
        }

        State = EncounterState.Dialogue;
        _dialogueOpen = true;
        game.Messages.Closed += OnMessageClosed;
        game.Messages.Show($"{Current.DisplayName}: {Current.ChallengeLine}");
    }

    private void OnMessageClosed(bool accepted)
    {
        if (Game.Instance is not { } game)
        {
            return;
        }

        game.Messages.Closed -= OnMessageClosed;
        if (_dialogueOpen)
        {
            _dialogueOpen = false;
            BeginDuel();
        }
        else if (_duelOpen)
        {
            _duelOpen = false;
            EndDuel(accepted);
        }
    }

    private void BeginDuel()
    {
        if (Current?.Character is not { } npc || _player is null || Game.Instance is not { } game)
        {
            Finish();
            return;
        }

        State = EncounterState.Duel;
        npc.PlayState(Character.DuelReadyState);
        _player.PlayState(Character.DuelReadyState);
        game.SetMode(GameMode.Duel);
        EmitSignal(SignalName.DuelStarted, Current.DuelistId);
        _duelOpen = true;
        game.Messages.Closed += OnMessageClosed;
        game.Messages.Show(
            $"Placeholder duel against {Current.DisplayName} ({Current.DuelistId}). Duel.Core and DuelStaging replace this box.",
            choice: true,
            hint: "Interact: win   Cancel: lose");
    }

    private void EndDuel(bool won)
    {
        if (Current?.Character is not { } npc || _player is null || Game.Instance is not { } game)
        {
            Finish();
            return;
        }

        LastWon = won;
        State = EncounterState.Result;
        _timer = ResultTime;
        _player.PlayState(won ? Character.WinState : Character.LoseState);
        npc.PlayState(won ? Character.LoseState : Character.WinState);
        if (won)
        {
            Current.Defeated = true;
            game.SetFlag(Flags.Defeated(Current.DuelistId));
        }

        GD.Print($"Encounter: duel against {Current.DuelistId} {(won ? "won" : "lost")}; coins {game.Coins}");
        EmitSignal(SignalName.DuelFinished, Current.DuelistId, won);
    }

    private void EndResult()
    {
        if (Current?.Character is { } npc)
        {
            npc.ResetToLocomotion();
            npc.Velocity = Vector3.Zero;
            npc.SetLocomotion(0.0f);
            Current.DisarmFor(DisarmTime);
        }

        _player?.ResetToLocomotion();
        if (!LastWon && _player is not null)
        {
            State = EncounterState.Returning;
            ReturnPlayerToOrigin();
            return;
        }

        Finish();
    }

    private async void ReturnPlayerToOrigin()
    {
        if (Game.Instance is not { } game || _player is null)
        {
            Finish();
            return;
        }

        try
        {
            await game.Fade.FadeOutAsync();
            _player.GlobalPosition = Origin;
            _player.Velocity = Vector3.Zero;
            game.Camera?.Snap();
            await game.Fade.FadeInAsync();
        }
        catch (Exception e)
        {
            GD.PushError($"Encounter: loss return failed: {e}");
        }

        Finish();
    }

    private void Finish()
    {
        Game.Instance?.SetMode(Game.Instance.LevelPath.Length > 0 ? Game.ModeFor(Game.Instance.LevelPath) : GameMode.Overworld);
        Game.Instance?.UnlockInput(LockReason);
        State = EncounterState.Idle;
        Current = null;
        Site = null;
        _player = null;
        _controller = null;
        _dialogueOpen = false;
        _duelOpen = false;
    }

    private static void FaceToward(Character who, Vector3 point)
    {
        Vector3 to = point - who.GlobalPosition;
        to.Y = 0.0f;
        if (to.LengthSquared() > 0.001f)
        {
            who.Rotation = new Vector3(0.0f, Mathf.Atan2(-to.X, -to.Z), 0.0f);
        }
    }

    private void ShowExclamation(Character npc)
    {
        HideExclamation();
        _exclamation = new Label3D
        {
            Name = "Exclamation",
            Text = "!",
            FontSize = 160,
            PixelSize = 0.005f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Modulate = new Color(1.0f, 0.85f, 0.2f),
            OutlineSize = 24,
            Position = new Vector3(0.0f, 2.3f, 0.0f),
        };
        npc.AddChild(_exclamation);
    }

    private void HideExclamation()
    {
        if (_exclamation is not null && IsInstanceValid(_exclamation))
        {
            _exclamation.QueueFree();
        }

        _exclamation = null;
    }
}
