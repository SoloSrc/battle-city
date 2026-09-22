using System.Linq;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;
using Xunit;

namespace BattleCity.Duel.Core.Tests;

public class BattleTests
{
    [Fact]
    public void EnteringTheBattlePhaseReachesTheBattleStep()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);

        Scenario.EnterBattle(engine);

        Assert.Equal(Phase.Battle, engine.State.Phase);
        Assert.Equal(BattleStep.Battle, engine.State.BattleStep);
        Assert.True(engine.State.BattlePhaseUsed);
        Assert.Equal(new[] { BattleStep.Start, BattleStep.Battle }, engine.Events.OfType<BattleStepChanged>().Select(e => e.Step));
    }

    [Fact]
    public void AttackDestroysAWeakerAttackPositionMonsterAndDealsTheDifference()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        Scenario.EnterBattle(engine);

        Scenario.Attack(engine, elf, weakling);

        Assert.Equal(Location.Graveyard, weakling.Loc);
        Assert.Equal(Location.MonsterZone, elf.Loc);
        Assert.Equal(7600, engine.State.Player(1).LifePoints);
        Assert.Equal(8000, engine.State.Player(0).LifePoints);
        Assert.True(elf.AttackedThisTurn);
        Assert.Equal(BattleStep.Battle, engine.State.BattleStep);
        Assert.Equal(0, engine.State.Priority);
        Assert.Contains(engine.Events, e => e is MonsterDestroyed { Reason: DestroyReason.Battle } d && d.Card == weakling.Id);
    }

    [Fact]
    public void AttackingAStrongerMonsterDestroysTheAttacker()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance weakling = Scenario.Place(engine, 0, Cards.Weakling, Position.FaceUpAttack);
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        Scenario.EnterBattle(engine);

        Scenario.Attack(engine, weakling, elf);

        Assert.Equal(Location.Graveyard, weakling.Loc);
        Assert.Equal(Location.MonsterZone, elf.Loc);
        Assert.Equal(7600, engine.State.Player(0).LifePoints);
        Assert.Equal(8000, engine.State.Player(1).LifePoints);
    }

    [Fact]
    public void EqualAttackDestroysBothWithoutDamage()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance a = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance b = Scenario.Place(engine, 1, Cards.ArchfiendSoldier, Position.FaceUpAttack);
        Scenario.EnterBattle(engine);

        Scenario.Attack(engine, a, b);

        Assert.Equal(Location.Graveyard, a.Loc);
        Assert.Equal(Location.Graveyard, b.Loc);
        Assert.Equal(8000, engine.State.Player(0).LifePoints);
        Assert.Equal(8000, engine.State.Player(1).LifePoints);
    }

    [Fact]
    public void AttackingHigherDefenseDamagesTheAttackersController()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance wall = Scenario.Place(engine, 1, Cards.Wall, Position.FaceUpDefense);
        Scenario.EnterBattle(engine);

        Scenario.Attack(engine, elf, wall);

        Assert.Equal(Location.MonsterZone, elf.Loc);
        Assert.Equal(Location.MonsterZone, wall.Loc);
        Assert.Equal(7900, engine.State.Player(0).LifePoints);
        Assert.Equal(8000, engine.State.Player(1).LifePoints);
    }

    [Fact]
    public void AttackingLowerDefenseDestroysWithoutDamage()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpDefense);
        Scenario.EnterBattle(engine);

        Scenario.Attack(engine, elf, weakling);

        Assert.Equal(Location.Graveyard, weakling.Loc);
        Assert.Equal(8000, engine.State.Player(1).LifePoints);
    }

    [Fact]
    public void FaceDownTargetFlipsBeforeDamageCalculation()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance wall = Scenario.Place(engine, 1, Cards.Wall, Position.FaceDownDefense);
        Scenario.EnterBattle(engine);

        Scenario.Attack(engine, elf, wall);

        Assert.Equal(Position.FaceUpDefense, wall.Pos);
        Assert.Equal(7900, engine.State.Player(0).LifePoints);
        var order = engine.Events.Where(e => e is DamageSubstepChanged or MonsterFlipped).ToList();
        Assert.Equal(
            new DuelEvent[]
            {
                new DamageSubstepChanged(DamageSubstep.StartDamage),
                new DamageSubstepChanged(DamageSubstep.BeforeCalc),
                new MonsterFlipped(1, wall.Id, "test_wall"),
                new DamageSubstepChanged(DamageSubstep.Calc),
                new DamageSubstepChanged(DamageSubstep.AfterCalc),
                new DamageSubstepChanged(DamageSubstep.EndDamage),
            },
            order);
    }

    [Fact]
    public void DirectAttacksNeedAnEmptyOpposingField()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        Scenario.EnterBattle(engine);

        SubmitResult blocked = engine.Submit(new DeclareAttack(0, elf.Id, null));

        Assert.Equal("direct attacks are only possible when the opponent controls no monsters", blocked.Error);
        Assert.DoesNotContain(engine.LegalActions(0), a => a is DeclareAttack { Target: null });
        Assert.Contains(engine.LegalActions(0), a => a is DeclareAttack d && d.Target == weakling.Id);
    }

    [Fact]
    public void DirectAttackDealsFullAtk()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        Scenario.EnterBattle(engine);

        Scenario.Attack(engine, elf, null);

        Assert.Equal(6100, engine.State.Player(1).LifePoints);
        Assert.Contains(engine.Events, e => e is BattleDamage { Player: 1, Amount: 1900 });
    }

    [Fact]
    public void AMonsterAttacksOncePerTurnAndCannotChangePositionAfterwards()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, elf, null);

        Assert.Equal("Gemini Elf already attacked this turn", engine.Validate(new DeclareAttack(0, elf.Id, null)));

        Scenario.Submit(engine, new Pass(0));

        Assert.Equal(Phase.Main2, engine.State.Phase);
        Assert.Equal("a monster that attacked cannot change position this turn", engine.Validate(new ChangePosition(0, elf.Id)));
        Assert.Equal("the Battle Phase follows Main Phase 1", engine.Validate(new EnterBattlePhase(0)));
    }

    [Fact]
    public void DefensePositionAndSetMonstersCannotAttack()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance wall = Scenario.Place(engine, 0, Cards.Wall, Position.FaceUpDefense);
        Scenario.Place(engine, 0, Cards.Weakling, Position.FaceDownDefense);
        Scenario.EnterBattle(engine);

        Assert.Equal(BattleStep.Battle, engine.State.BattleStep);
        Assert.Equal(new[] { new Pass(0) }, engine.LegalActions(0));
        Assert.Equal("only a face-up Attack Position monster can attack", engine.Validate(new DeclareAttack(0, wall.Id, null)));
    }

    [Fact]
    public void LifePointsReachingZeroEndsTheDuel()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        engine.State.Player(1).LifePoints = 1500;
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        Scenario.EnterBattle(engine);

        Scenario.Attack(engine, elf, null);

        Assert.True(engine.State.IsOver);
        Assert.Equal(0, engine.State.Winner);
        Assert.Equal(DuelOutcome.LifePoints, engine.State.Outcome);
        Assert.Equal(0, engine.State.Player(1).LifePoints);
        Assert.Equal(new DuelEnded(0, DuelOutcome.LifePoints), engine.Events.Last(e => e is DuelEnded));
        Assert.Empty(engine.LegalActions(0));
    }

    [Fact]
    public void SummonedMonsterCanAttackTheSameTurn()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.GeminiElf });
        CardInstance elf = Scenario.InHand(engine, 0, Cards.GeminiElf);
        Scenario.Submit(engine, new NormalSummon(0, elf.Id));
        Scenario.EnterBattle(engine);

        Scenario.Attack(engine, elf, null);

        Assert.Equal(6100, engine.State.Player(1).LifePoints);
    }

    // ----- A monster that leaves the field and comes back is a new monster (issue #204) -----

    /// <summary>The director's duel: the attacker is destroyed by Sakuretsu Armor and revived by Call of the Haunted before the Damage Step.</summary>
    [Fact]
    public void ARevivedAttackerDoesNotCarryOnItsOldAttack()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance call = Scenario.Set(engine, 0, Cards.Real("call_of_the_haunted"));
        CardInstance skull = Scenario.Place(engine, 1, Cards.SummonedSkull, Position.FaceDownDefense);
        CardInstance armor = Scenario.Set(engine, 1, Cards.Real("sakuretsu_armor"));
        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, elf, skull);

        Scenario.Submit(engine, new ActivateTrap(1, armor.Id));
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.Graveyard, elf.Loc);
        Assert.Equal(Window.AttackDeclared, engine.State.Window);
        Scenario.Submit(engine, new ActivateTrap(0, call.Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.MonsterZone, elf.Loc);
        Scenario.PassUntil(engine, s => s.Window == Window.Open);

        // The declared attack ended with the monster that declared it: no flip, no battle, no damage.
        Assert.Contains(engine.Events, e => e is AttackCancelled a && a.Attacker == elf.Id);
        Assert.DoesNotContain(engine.Events, e => e is DamageSubstepChanged);
        Assert.DoesNotContain(engine.Events, e => e is MonsterFlipped);
        Assert.Equal(Location.MonsterZone, skull.Loc);
        Assert.Equal(Position.FaceDownDefense, skull.Pos);
        Assert.Equal(8000, engine.State.Player(1).LifePoints);
        Assert.Equal(BattleStep.Battle, engine.State.BattleStep);

        // The revived Elf is a new monster and may declare one attack of its own, which then plays out.
        Assert.False(elf.AttackedThisTurn);
        Assert.Null(engine.Validate(new DeclareAttack(0, elf.Id, skull.Id)));
        Scenario.Attack(engine, elf, skull);
        Assert.Contains(engine.Events, e => e is MonsterFlipped f && f.Card == skull.Id);
        Assert.Equal(Location.Graveyard, skull.Loc);
        Assert.Equal(8000, engine.State.Player(1).LifePoints);
        Assert.Equal("Gemini Elf already attacked this turn", engine.Validate(new DeclareAttack(0, elf.Id, null)));
    }

    [Fact]
    public void AnAttackEndsWhenItsTargetLeavesTheFieldEvenIfTheTargetComesBack()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        CardInstance ring = Scenario.Set(engine, 1, Cards.Real("ring_of_destruction"));
        CardInstance call = Scenario.Set(engine, 1, Cards.Real("call_of_the_haunted"));
        Scenario.EnterBattle(engine);
        // Ring of Destruction could be activated in the Start Step: player 1 lets it pass.
        Scenario.PassUntil(engine, s => s.BattleStep == BattleStep.Battle && s.Priority == 0);
        Scenario.Attack(engine, elf, weakling);

        Scenario.Submit(engine, new ActivateTrap(1, ring.Id));
        Scenario.Answer(engine, weakling.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.Graveyard, weakling.Loc);
        Assert.Equal(Window.AttackDeclared, engine.State.Window);
        Scenario.Submit(engine, new ActivateTrap(1, call.Id));
        Scenario.Answer(engine, weakling.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.MonsterZone, weakling.Loc);
        Scenario.PassUntil(engine, s => s.Window == Window.Open);

        Assert.Contains(engine.Events, e => e is AttackCancelled a && a.Attacker == elf.Id);
        Assert.DoesNotContain(engine.Events, e => e is DamageSubstepChanged);
        Assert.Equal(Location.MonsterZone, weakling.Loc);
        Assert.Equal(Location.MonsterZone, elf.Loc);
        Assert.All(engine.State.Players, p => Assert.Equal(8000 - 1500, p.LifePoints));
        Assert.True(elf.AttackedThisTurn);
        Assert.Equal(BattleStep.Battle, engine.State.BattleStep);
    }

    [Fact]
    public void LeavingTheFieldStartsANewStay()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        int stay = elf.FieldStay;

        engine.Destroy(elf, DestroyReason.Effect);

        Assert.Equal(stay + 1, elf.FieldStay);
        Assert.Null(engine.State.InMonsterZoneSince(elf.Id, stay));
        Assert.True(engine.SpecialSummon(elf, 0, Position.FaceUpAttack));
        Assert.Null(engine.State.InMonsterZoneSince(elf.Id, stay));
        Assert.Same(elf, engine.State.InMonsterZoneSince(elf.Id, elf.FieldStay));
    }
}
