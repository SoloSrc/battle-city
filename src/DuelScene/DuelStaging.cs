using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Characters;
using BattleCity.Duel.Core;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;
using BattleCity.Rendering;
using BattleCity.World;
using Godot;
using CardPosition = BattleCity.Duel.Core.Model.Position;

namespace BattleCity.DuelScene;

/// <summary>
/// Owns everything 3D during a duel (systems.md §6.1). <see cref="Stage"/> puts
/// both characters on the stand points facing each other and builds the card
/// anchors of each side from the stand point and axis (never from the disk
/// mesh): the 2 × 5 grid, a hand row, and deck, graveyard and banished
/// anchors on the <see cref="DuelDisk"/> markers when the prop is loaded.
/// <see cref="Bind"/> creates a <see cref="CardView"/> per card of the engine
/// and, after every batch of engine events, <see cref="Sync"/> moves each card
/// to the anchor its <see cref="CardInstance"/> says it is in; the events
/// themselves drive the transient effects (attack lunge, hit, dissolve).
/// </summary>
public partial class DuelStaging : Node3D
{
    private const int ZoneCount = 5;

    [ExportGroup("Card anchors (systems.md §6.1)")]
    [Export(PropertyHint.Range, "0.5,2,0.05,suffix:m")]
    public float Forward { get; set; } = 1.0f;

    [Export(PropertyHint.Range, "0.1,0.5,0.01,suffix:m")]
    public float SpacingX { get; set; } = 0.22f;

    [Export(PropertyHint.Range, "0.1,0.5,0.01,suffix:m")]
    public float SpacingZ { get; set; } = 0.28f;

    [Export(PropertyHint.Range, "0.5,2,0.05,suffix:m")]
    public float ChestHeight { get; set; } = 1.2f;

    /// <summary>The hand row sits beside the body (the free hand's side) so the camera behind the duelist sees it.</summary>
    [Export(PropertyHint.Range, "0,1,0.05,suffix:m")]
    public float HandForward { get; set; } = 0.25f;

    [Export(PropertyHint.Range, "-1,1,0.05,suffix:m")]
    public float HandSide { get; set; } = 0.62f;

    [Export(PropertyHint.Range, "0.5,1.6,0.05,suffix:m")]
    public float HandHeight { get; set; } = 0.95f;

    [Export(PropertyHint.Range, "0.02,0.3,0.01,suffix:m")]
    public float HandSpacing { get; set; } = 0.11f;

    /// <summary>Offset between stacked cards along their normal (deck, graveyard, banished).</summary>
    [Export(PropertyHint.Range, "0,0.01,0.0005,suffix:m")]
    public float StackStep { get; set; } = 0.0015f;

    [ExportGroup("Duel camera (systems.md §4.2)")]
    [Export(PropertyHint.Range, "5,30,1,suffix:°")]
    public float CameraPitch { get; set; } = 15.0f;

    [Export(PropertyHint.Range, "3,10,0.1,suffix:m")]
    public float CameraDistance { get; set; } = 5.5f;

    [Export(PropertyHint.Range, "20,70,1,suffix:°")]
    public float CameraFov { get; set; } = 40.0f;

    /// <summary><c>camera.duel.blend_time</c> until <c>data/tuning.json</c> lands (#63).</summary>
    [Export(PropertyHint.Range, "0,3,0.1,suffix:s")]
    public float CameraBlendTime { get; set; } = 1.2f;

    [Export(PropertyHint.Range, "0,2,0.05,suffix:m")]
    public float CameraFocusHeight { get; set; } = 1.1f;

    /// <summary>Lunge of an attacking card toward its target, as a fraction of the distance.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")]
    public float LungeFraction { get; set; } = 0.35f;

    private readonly Dictionary<Guid, CardView> _cards = new();
    private readonly List<DuelEvent> _pendingEvents = new();
    private CardFaces? _faces;
    private DuelEngine? _engine;
    private bool _dirty;
    private bool _animateSync = true;

    public Character? PlayerCharacter { get; private set; }

    public Character? OpponentCharacter { get; private set; }

    public SideAnchors? PlayerSide { get; private set; }

    public SideAnchors? OpponentSide { get; private set; }

    public DuelEngine? Engine => _engine;

    public IReadOnlyDictionary<Guid, CardView> Cards => _cards;

    public bool IsStaged => PlayerSide is not null && OpponentSide is not null;

    /// <summary>Card moves since <see cref="Bind"/> (anchor changes, the initial deal excluded).</summary>
    public int Moves => _cards.Values.Sum(c => c.Moves - 1);

    /// <summary>Events applied so far.</summary>
    public int EventsApplied { get; private set; }

    public CardFaces Faces => _faces ??= AddFaces();

    /// <summary>Places the characters on the site's stand points (player on A) and builds the anchors.</summary>
    public void Stage(EncounterSite site, Character player, Character opponent)
    {
        ArgumentNullException.ThrowIfNull(site);
        Stage(player, opponent, site.StandPointA, site.StandPointB);
    }

    public void Stage(Character player, Character opponent, Vector3 playerStand, Vector3 opponentStand)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(opponent);
        PlayerCharacter = player;
        OpponentCharacter = opponent;
        Place(player, playerStand, opponentStand);
        Place(opponent, opponentStand, playerStand);
        PlayerSide?.Root.QueueFree();
        OpponentSide?.Root.QueueFree();
        PlayerSide = BuildSide("PlayerSide", player, playerStand, opponentStand, HologramSide.Player);
        OpponentSide = BuildSide("OpponentSide", opponent, opponentStand, playerStand, HologramSide.Opponent);
    }

    /// <summary>The duel framing: behind the player's shoulder, looking down the axis at the opponent.</summary>
    public void EnterCamera(CameraRig rig)
    {
        ArgumentNullException.ThrowIfNull(rig);
        if (PlayerSide is null || OpponentSide is null)
        {
            return;
        }

        Vector3 toOpponent = OpponentSide.Root.GlobalPosition - PlayerSide.Root.GlobalPosition;
        float yaw = Mathf.RadToDeg(Mathf.Atan2(-toOpponent.X, -toOpponent.Z));
        Vector3 focus = PlayerSide.Root.GlobalPosition + Vector3.Up * CameraFocusHeight;
        rig.EnterDuel(focus, yaw, CameraPitch, CameraDistance, CameraFov, CameraBlendTime);
    }

    public void ExitCamera(CameraRig rig)
    {
        ArgumentNullException.ThrowIfNull(rig);
        rig.ExitDuel(CameraBlendTime);
    }

    /// <summary>Creates a card view per card of <paramref name="engine"/> on its deck anchor and follows the engine's events from now on.</summary>
    public void Bind(DuelEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (!IsStaged)
        {
            throw new InvalidOperationException("Stage the duelists before binding an engine.");
        }

        Unbind();
        _engine = engine;
        engine.EventRaised += OnEvent;
        int index = 0;
        foreach (PlayerState p in engine.State.Players)
        {
            foreach (CardInstance card in p.AllCards)
            {
                var view = new CardView();
                bool opponent = card.Owner == 1;
                view.Setup(card, opponent ? HologramSide.Opponent : HologramSide.Player, Faces.Get(card.Def), opponent, (index++ % 10) / 10.0f);
                _cards[card.Id] = view;
            }
        }

        Faces.FaceBaked += OnFaceBaked;
        Sync(animate: false);
        foreach (DuelEvent e in engine.Events)
        {
            ApplyEvent(e);
        }
    }

    public void Unbind()
    {
        if (_engine is not null)
        {
            _engine.EventRaised -= OnEvent;
            _engine = null;
        }

        if (_faces is not null)
        {
            _faces.FaceBaked -= OnFaceBaked;
        }

        foreach (CardView view in _cards.Values)
        {
            view.QueueFree();
        }

        _cards.Clear();
        _pendingEvents.Clear();
        EventsApplied = 0;
    }

    /// <summary>Dissolves every card (end of the duel, VFX hook 6.7) and forgets the engine.</summary>
    public void DissolveAll()
    {
        if (_engine is not null)
        {
            _engine.EventRaised -= OnEvent;
            _engine = null;
        }

        foreach (CardView view in _cards.Values)
        {
            view.Dissolve(free: true);
        }

        _cards.Clear();
    }

    /// <summary>Moves every card to the anchor its state says; tokens that left the duel dissolve, new tokens appear.</summary>
    public void Sync(bool animate = true)
    {
        if (_engine is null || PlayerSide is null || OpponentSide is null)
        {
            return;
        }

        _dirty = false;
        var seen = new HashSet<Guid>();
        foreach (PlayerState p in _engine.State.Players)
        {
            var stacks = new Dictionary<Node3D, int>();
            int handIndex = 0;
            int handCount = p.Hand.Count;
            foreach (CardInstance card in p.AllCards)
            {
                seen.Add(card.Id);
                CardView view = _cards.TryGetValue(card.Id, out CardView? existing) ? existing : CreateToken(card);
                (Node3D anchor, Vector3 local, CardOrientation orientation, bool visible) = Placement(card, p, stacks, ref handIndex, handCount);
                view.Visible = visible;
                view.AttachTo(anchor, local, orientation, animate && view.Anchor is not null);
            }
        }

        foreach ((Guid id, CardView view) in _cards.Where(pair => !seen.Contains(pair.Key)).ToList())
        {
            _cards.Remove(id);
            view.Dissolve(free: true);
        }
    }

    /// <summary>Card views whose anchor disagrees with the engine state (a diagnostic invariant; 0 after <see cref="Sync"/>).</summary>
    public int Mismatches()
    {
        if (_engine is null || PlayerSide is null || OpponentSide is null)
        {
            return 0;
        }

        int mismatches = 0;
        foreach (PlayerState p in _engine.State.Players)
        {
            var stacks = new Dictionary<Node3D, int>();
            int handIndex = 0;
            foreach (CardInstance card in p.AllCards)
            {
                if (!_cards.TryGetValue(card.Id, out CardView? view))
                {
                    mismatches++;
                    continue;
                }

                (Node3D anchor, _, CardOrientation orientation, _) = Placement(card, p, stacks, ref handIndex, p.Hand.Count);
                if (!ReferenceEquals(view.Anchor, anchor) || view.Orientation != orientation)
                {
                    mismatches++;
                }
            }
        }

        return mismatches;
    }

    public override void _Process(double delta)
    {
        if (_dirty)
        {
            Sync(_animateSync);
        }

        if (_pendingEvents.Count > 0)
        {
            foreach (DuelEvent e in _pendingEvents)
            {
                ApplyEvent(e);
            }

            _pendingEvents.Clear();
        }
    }

    private void OnEvent(DuelEvent e)
    {
        _dirty = true;
        _animateSync = true;
        _pendingEvents.Add(e);
    }

    private void OnFaceBaked(string cardId, Texture2D texture)
    {
        foreach (CardView view in _cards.Values)
        {
            if (view.Card?.Def.Id == cardId)
            {
                view.SetFace(texture);
            }
        }
    }

    /// <summary>The transient effects an event triggers; positions come from <see cref="Sync"/>.</summary>
    private void ApplyEvent(DuelEvent e)
    {
        EventsApplied++;
        switch (e)
        {
            case CardDrawn drawn when _cards.TryGetValue(drawn.Card, out CardView? view):
                view.Reveal();
                (drawn.Player == 0 ? PlayerCharacter : OpponentCharacter)?.PlayState(Character.DrawCardState);
                break;
            case MonsterSummoned summoned:
                Flash(summoned.Card, summoned.Player);
                break;
            case MonsterSpecialSummoned special:
                Flash(special.Card, special.Player);
                break;
            case SpellActivated spell:
                Flash(spell.Card, spell.Player);
                break;
            case TrapActivated trap:
                Flash(trap.Card, trap.Player);
                break;
            case EffectActivated effect:
                Flash(effect.Card, effect.Player);
                break;
            case AttackDeclared attack:
                Lunge(attack.Attacker, attack.Target, attack.Player);
                break;
            case BattleDamage damage:
                (damage.Player == 0 ? PlayerCharacter : OpponentCharacter)?.PlayState(Character.TakeDamageState);
                break;
            case EffectDamage damage:
                (damage.Player == 0 ? PlayerCharacter : OpponentCharacter)?.PlayState(Character.TakeDamageState);
                break;
            case DuelEnded ended:
                PlayerCharacter?.PlayState(ended.Winner == 0 ? Character.WinState : Character.LoseState);
                OpponentCharacter?.PlayState(ended.Winner == 1 ? Character.WinState : Character.LoseState);
                break;
        }
    }

    private void Flash(Guid card, int player)
    {
        if (_cards.TryGetValue(card, out CardView? view))
        {
            view.Reveal();
        }

        (player == 0 ? PlayerCharacter : OpponentCharacter)?.PlayState(Character.PlayCardState);
    }

    /// <summary>Attack trail stand-in (VFX hook 6.4): the attacker darts a fraction of the way to its target and back.</summary>
    private void Lunge(Guid attacker, Guid? target, int player)
    {
        if (!_cards.TryGetValue(attacker, out CardView? view))
        {
            return;
        }

        Vector3 to = target is { } id && _cards.TryGetValue(id, out CardView? targetView)
            ? targetView.GlobalPosition
            : (player == 0 ? OpponentSide : PlayerSide)!.Root.GlobalPosition + Vector3.Up * ChestHeight;
        Vector3 offset = view.ToLocal(to) * LungeFraction;
        Vector3 rest = view.Position;
        Tween tween = view.CreateTween();
        tween.TweenProperty(view, "position", rest + view.Basis * offset, CardView.MoveTime).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(view, "position", rest, CardView.MoveTime * 2.0f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        (player == 0 ? PlayerCharacter : OpponentCharacter)?.PlayState(Character.PlayCardState);
    }

    private CardView CreateToken(CardInstance card)
    {
        var view = new CardView();
        bool opponent = card.Owner == 1;
        view.Setup(card, opponent ? HologramSide.Opponent : HologramSide.Player, Faces.Get(card.Def), opponent, 0.5f);
        _cards[card.Id] = view;
        return view;
    }

    private (Node3D Anchor, Vector3 Local, CardOrientation Orientation, bool Visible) Placement(CardInstance card, PlayerState owner, Dictionary<Node3D, int> stacks, ref int handIndex, int handCount)
    {
        SideAnchors ownerSide = owner.Index == 0 ? PlayerSide! : OpponentSide!;
        SideAnchors controllerSide = card.Controller == 0 ? PlayerSide! : OpponentSide!;
        switch (card.Loc)
        {
            case Location.MonsterZone:
                return (controllerSide.Monsters[Math.Clamp(card.ZoneIndex, 0, ZoneCount - 1)], Vector3.Zero, OrientationOf(card.Pos), true);
            case Location.SpellTrapZone:
                return (controllerSide.SpellTraps[Math.Clamp(card.ZoneIndex, 0, ZoneCount - 1)], Vector3.Zero, card.IsFaceUp ? CardOrientation.Attack : CardOrientation.FaceDown, true);
            case Location.FieldZone:
                return (controllerSide.SpellTraps[0], new Vector3(-SpacingX, 0.0f, 0.0f), CardOrientation.Attack, true);
            case Location.Hand:
                {
                    float x = (handIndex++ - (handCount - 1) * 0.5f) * HandSpacing;
                    return (ownerSide.Hand, new Vector3(x, 0.0f, handIndex * 0.001f), owner.Index == 0 ? CardOrientation.Attack : CardOrientation.FaceDown, true);
                }

            case Location.Graveyard:
                return (ownerSide.Graveyard, Stack(ownerSide.Graveyard, stacks), CardOrientation.Attack, true);
            case Location.Banished:
                return (ownerSide.Banished, Stack(ownerSide.Banished, stacks), CardOrientation.Attack, true);
            case Location.FusionDeck:
                return (ownerSide.Deck, Vector3.Zero, CardOrientation.FaceDown, false);
            default:
                return (ownerSide.Deck, Stack(ownerSide.Deck, stacks), CardOrientation.FaceDown, true);
        }
    }

    private Vector3 Stack(Node3D anchor, Dictionary<Node3D, int> stacks)
    {
        int index = stacks.TryGetValue(anchor, out int count) ? count : 0;
        stacks[anchor] = index + 1;
        return new Vector3(0.0f, 0.0f, index * StackStep);
    }

    private static CardOrientation OrientationOf(CardPosition position) =>
        position switch
        {
            CardPosition.FaceUpAttack => CardOrientation.Attack,
            CardPosition.FaceUpDefense => CardOrientation.Defense,
            CardPosition.FaceDownDefense => CardOrientation.FaceDownDefense,
            _ => CardOrientation.FaceDown,
        };

    private static void Place(Character character, Vector3 stand, Vector3 facing)
    {
        character.GlobalPosition = stand;
        Vector3 flat = new(facing.X, stand.Y, facing.Z);
        if (flat.DistanceSquaredTo(stand) > 1e-6f)
        {
            character.LookAt(flat, Vector3.Up);
        }

        character.Velocity = Vector3.Zero;
    }

    private SideAnchors BuildSide(string name, Character character, Vector3 stand, Vector3 facing, HologramSide side)
    {
        var root = new Node3D { Name = name };
        AddChild(root);
        root.GlobalPosition = stand;
        Vector3 flat = new(facing.X, stand.Y, facing.Z);
        if (flat.DistanceSquaredTo(stand) > 1e-6f)
        {
            root.LookAt(flat, Vector3.Up);
        }

        var grid = new Node3D { Name = "CardAnchors" };
        root.AddChild(grid);
        var monsters = new Node3D[ZoneCount];
        var spellTraps = new Node3D[ZoneCount];
        for (int column = 0; column < ZoneCount; column++)
        {
            monsters[column] = Marker(grid, $"m{column + 1}", new Vector3((column - 2) * SpacingX, ChestHeight, -Forward));
            spellTraps[column] = Marker(grid, $"st{column + 1}", new Vector3((column - 2) * SpacingX, ChestHeight, -(Forward + SpacingZ)));
        }

        Node3D hand = Marker(root, "hand", new Vector3(HandSide, HandHeight, -HandForward));
        DuelDisk? disk = character.Disk;
        Node3D deck = disk?.GetMarker("deck") ?? Marker(root, "deck", new Vector3(-0.6f, ChestHeight - 0.1f, -0.5f));
        Node3D graveyard = disk?.GetMarker("graveyard") ?? Marker(root, "graveyard", new Vector3(-0.75f, ChestHeight - 0.1f, -0.5f));
        Node3D banished = disk?.GetMarker("banished") ?? Marker(root, "banished", new Vector3(-0.9f, ChestHeight - 0.1f, -0.5f));
        return new SideAnchors(side, root, monsters, spellTraps, hand, deck, graveyard, banished, disk is not null && disk.GetMarker("deck") is not null);
    }

    private static Node3D Marker(Node3D parent, string name, Vector3 position)
    {
        var marker = new Marker3D { Name = name, Position = position };
        parent.AddChild(marker);
        return marker;
    }

    private CardFaces AddFaces()
    {
        var faces = new CardFaces { Name = "CardFaces" };
        AddChild(faces);
        return faces;
    }

    /// <summary>The anchors of one side: the grid, the hand row and the three piles (on the disk's markers when it has them).</summary>
    public sealed record SideAnchors(HologramSide Side, Node3D Root, Node3D[] Monsters, Node3D[] SpellTraps, Node3D Hand, Node3D Deck, Node3D Graveyard, Node3D Banished, bool PilesOnDisk);
}
