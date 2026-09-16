using System;
using BattleCity.Characters;
using BattleCity.Duel.Core;
using BattleCity.Duel.Core.Ai;
using BattleCity.World;
using Godot;

namespace BattleCity.DuelScene;

/// <summary>
/// Owns the three duel nodes of the running game (systems.md §4.3 steps 4–6):
/// <see cref="DuelStaging"/> in the 3D world, <see cref="DuelSession"/> and
/// <see cref="DuelUi"/>. <see cref="Begin"/> stages the duelists, starts the
/// engine, binds the views and blends the camera; <see cref="End"/> dissolves
/// the cards, hides the HUD and blends the camera back. The encounter system
/// decides when; this node only wires.
/// </summary>
public partial class DuelDirector : Node3D
{
    public DuelStaging Staging { get; private set; } = null!;

    public DuelSession Session { get; private set; } = null!;

    public DuelUi Ui { get; private set; } = null!;

    /// <summary>Once per duel, from <see cref="DuelSession.Finished"/>: the winner seat (null for a draw).</summary>
    public event Action<int?>? Finished;

    public bool IsRunning => Session.IsRunning;

    /// <summary>The seat the human plays; the opponent sits in the other one.</summary>
    public const int HumanSeat = 0;

    private CameraRig? _rig;

    public override void _Ready()
    {
        Staging = new DuelStaging { Name = "DuelStaging" };
        AddChild(Staging);
        Session = new DuelSession { Name = "DuelSession" };
        AddChild(Session);
        Ui = new DuelUi { Name = "DuelUi" };
        AddChild(Ui);
        Session.Finished += winner => Finished?.Invoke(winner);
    }

    /// <summary>Starts a duel between <paramref name="player"/> (seat 0) and <paramref name="opponent"/> on the stand points.</summary>
    public void Begin(Character player, Character opponent, Vector3 playerStand, Vector3 opponentStand, Deck playerDeck, Deck opponentDeck, IDuelAgent agent, ulong seed, bool tutorial, CameraRig? rig)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(opponent);
        ArgumentNullException.ThrowIfNull(playerDeck);
        ArgumentNullException.ThrowIfNull(opponentDeck);
        ArgumentNullException.ThrowIfNull(agent);
        End();
        Staging.Stage(player, opponent, playerStand, opponentStand);
        player.Disk?.Deploy();
        opponent.Disk?.Deploy();
        DuelEngine engine = DuelEngine.Start(playerDeck, opponentDeck, new DuelOptions { Seed = seed });
        Staging.Bind(engine);
        Session.Begin(engine, agent, HumanSeat);
        Ui.Tutorial = tutorial;
        Ui.Bind(Session, Staging);
        _rig = rig;
        if (rig is not null)
        {
            Staging.EnterCamera(rig);
        }
    }

    /// <summary>Tears the duel down: HUD off, cards dissolve, camera back. Safe to call when nothing runs.</summary>
    public void End()
    {
        Ui.Unbind();
        Staging.DissolveAll();
        Session.End();
        if (_rig is not null)
        {
            Staging.ExitCamera(_rig);
            _rig = null;
        }

        Staging.PlayerCharacter?.Disk?.Fold();
        Staging.OpponentCharacter?.Disk?.Fold();
    }
}
