using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Characters;
using BattleCity.Core;
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
/// themselves drive the transient effects through <see cref="Effects"/>
/// (systems.md §6.4: materialise, selection, summon flash, attack trail, hit
/// pulse, dissolve) and the GDD §8 cues.
/// </summary>
public partial class DuelStaging : Node3D
{
    private const int ZoneCount = 5;

    // Card anchors (systems.md §6.1): anchors.* in data/tuning.json (systems.md §10).
    public float Forward { get; set; } = Tuning.Current.Anchors.Forward;

    public float SpacingX { get; set; } = Tuning.Current.Anchors.SpacingX;

    public float SpacingZ { get; set; } = Tuning.Current.Anchors.SpacingZ;

    public float ChestHeight { get; set; } = Tuning.Current.Anchors.ChestHeight;

    /// <summary>The hand row sits beside the body (the free hand's side) so the camera behind the duelist sees it.</summary>
    public float HandForward { get; set; } = Tuning.Current.Anchors.HandForward;

    public float HandSide { get; set; } = Tuning.Current.Anchors.HandSide;

    public float HandHeight { get; set; } = Tuning.Current.Anchors.HandHeight;

    public float HandSpacing { get; set; } = Tuning.Current.Anchors.HandSpacing;

    /// <summary>Offset between stacked cards along their normal (deck, graveyard, banished).</summary>
    public float StackStep { get; set; } = Tuning.Current.Anchors.StackStep;

    /// <summary>Lunge of an attacking card toward its target, as a fraction of the distance.</summary>
    public float LungeFraction { get; set; } = Tuning.Current.Anchors.LungeFraction;

    // Duel camera (systems.md §4.2): camera.duel.* in data/tuning.json.
    public float CameraPitch { get; set; } = Tuning.Current.Camera.Duel.Pitch;

    public float CameraDistance { get; set; } = Tuning.Current.Camera.Duel.Distance;

    public float CameraFov { get; set; } = Tuning.Current.Camera.Duel.Fov;

    public float CameraBlendTime { get; set; } = Tuning.Current.Camera.Duel.BlendTime;

    public float CameraFocusHeight { get; set; } = Tuning.Current.Camera.Duel.FocusHeight;

    private readonly Dictionary<Guid, CardView> _cards = new();
    private bool _showPlayerHand = true;
    private readonly List<DuelEvent> _pendingEvents = new();
    private CardFaces? _faces;
    private DuelEffects? _effects;
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

    /// <summary>The VFX and SFX adapter (systems.md §6.4); card views delegate their card-state effects to it.</summary>
    public DuelEffects Effects => _effects ??= AddEffects();

    /// <summary>The player's 3D hand row; off while the HUD draws the hand fan (systems.md §6.3).</summary>
    public bool ShowPlayerHand
    {
        get => _showPlayerHand;
        set
        {
            if (_showPlayerHand == value)
            {
                return;
            }

            _showPlayerHand = value;
            Sync(animate: false);
        }
    }

    /// <summary>The card under <paramref name="screen"/> as seen by <paramref name="camera"/>, cast against the card layer; null when there is none.</summary>
    public CardView? Pick(Camera3D camera, Vector2 screen)
    {
        ArgumentNullException.ThrowIfNull(camera);
        Vector3 from = camera.ProjectRayOrigin(screen);
        Vector3 to = from + camera.ProjectRayNormal(screen) * 50.0f;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(from, to, PhysicsLayers.Card);
        query.CollideWithAreas = true;
        query.CollideWithBodies = false;
        Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count == 0 ? null : CardView.FromCollider(hit["collider"].AsGodotObject());
    }

    /// <summary>The screen position of a card's centre for <paramref name="camera"/>, or null when it is behind it.</summary>
    public static Vector2? ScreenPosition(Camera3D camera, CardView view)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(view);
        return camera.IsPositionBehind(view.GlobalPosition) ? null : camera.UnprojectPosition(view.GlobalPosition);
    }

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
                var view = new CardView { Effects = Effects };
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
            view.SetSelected(false);
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
                view.SetPickable(visible);
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

    /// <summary>The transient effects an event triggers (systems.md §6.4, GDD §8 cues); positions come from <see cref="Sync"/>, which ran first.</summary>
    private void ApplyEvent(DuelEvent e)
    {
        EventsApplied++;
        switch (e)
        {
            case CardDrawn drawn when _cards.TryGetValue(drawn.Card, out CardView? view):
                view.Reveal();
                Effects.Play("draw");
                (drawn.Player == 0 ? PlayerCharacter : OpponentCharacter)?.PlayState(Character.DrawCardState);
                break;
            case MonsterSummoned summoned:
                Summon(summoned.Card, summoned.Player);
                break;
            case MonsterSpecialSummoned special:
                Summon(special.Card, special.Player);
                break;
            case MonsterFlipSummoned flip:
                Summon(flip.Card, flip.Player);
                break;
            case TokenCreated token:
                Summon(token.Card, token.Player);
                break;
            case MonsterSet set:
                Effects.Play("set");
                (set.Player == 0 ? PlayerCharacter : OpponentCharacter)?.PlayState(Character.PlayCardState);
                break;
            case SpellTrapSet set:
                Effects.Play("set");
                (set.Player == 0 ? PlayerCharacter : OpponentCharacter)?.PlayState(Character.PlayCardState);
                break;
            case SpellActivated spell:
                Activate(spell.Card, spell.Player);
                break;
            case TrapActivated trap:
                Activate(trap.Card, trap.Player);
                break;
            case EffectActivated effect:
                Activate(effect.Card, effect.Player);
                break;
            case AttackDeclared attack:
                Lunge(attack.Attacker, attack.Target, attack.Player);
                break;
            case BattleDamage damage:
                Hit(damage.Player);
                break;
            case EffectDamage damage:
                Hit(damage.Player);
                break;
            case DuelEnded ended:
                Effects.Play(ended.Winner == 0 ? "win" : "lose");
                PlayerCharacter?.PlayState(ended.Winner == 0 ? Character.WinState : Character.LoseState);
                OpponentCharacter?.PlayState(ended.Winner == 1 ? Character.WinState : Character.LoseState);
                break;
        }
    }

    /// <summary>A monster arriving on the field: materialise, a <c>SummonFlash</c> ring on its zone, the summon cue.</summary>
    private void Summon(Guid card, int player)
    {
        if (_cards.TryGetValue(card, out CardView? view))
        {
            view.Reveal();
            Effects.Spawn(DuelEffects.SummonFlash, view.RestTransform, null, view.Side);
        }

        Effects.Play("summon");
        (player == 0 ? PlayerCharacter : OpponentCharacter)?.PlayState(Character.PlayCardState);
    }

    /// <summary>A Spell, Trap or effect going off: materialise (a Set card turns over) and the activate cue.</summary>
    private void Activate(Guid card, int player)
    {
        if (_cards.TryGetValue(card, out CardView? view))
        {
            view.Reveal();
        }

        Effects.Play("activate");
        (player == 0 ? PlayerCharacter : OpponentCharacter)?.PlayState(Character.PlayCardState);
    }

    /// <summary>Damage to a duelist: a <c>HitPulse</c> at their chest, the hit cue and the flinch.</summary>
    private void Hit(int player)
    {
        Character? character = player == 0 ? PlayerCharacter : OpponentCharacter;
        SideAnchors? side = player == 0 ? PlayerSide : OpponentSide;
        if (character is not null && side is not null)
        {
            var at = new Transform3D(side.Root.GlobalBasis, character.GlobalPosition + Vector3.Up * ChestHeight);
            Effects.Spawn(DuelEffects.HitPulse, at, null, side.Side);
        }

        Effects.Play("hit");
        character?.PlayState(Character.TakeDamageState);
    }

    /// <summary>An attack: the <c>AttackTrail</c> from the attacker to its target (or the defending duelist's chest), the attack cue, and the attacker darting a fraction of the way and back.</summary>
    private void Lunge(Guid attacker, Guid? target, int player)
    {
        if (!_cards.TryGetValue(attacker, out CardView? view))
        {
            return;
        }

        SideAnchors defender = (player == 0 ? OpponentSide : PlayerSide)!;
        Transform3D destination = target is { } id && _cards.TryGetValue(id, out CardView? targetView)
            ? targetView.RestTransform
            : new Transform3D(defender.Root.GlobalBasis, defender.Root.GlobalPosition + Vector3.Up * ChestHeight);
        Effects.Spawn(DuelEffects.AttackTrail, view.RestTransform, destination, view.Side);
        Effects.Play("attack");
        Vector3 to = destination.Origin;
        Vector3 offset = view.ToLocal(to) * LungeFraction;
        Vector3 rest = view.Position;
        Tween tween = view.CreateTween();
        tween.TweenProperty(view, "position", rest + view.Basis * offset, CardView.MoveTime).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(view, "position", rest, CardView.MoveTime * 2.0f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        (player == 0 ? PlayerCharacter : OpponentCharacter)?.PlayState(Character.PlayCardState);
    }

    private CardView CreateToken(CardInstance card)
    {
        var view = new CardView { Effects = Effects };
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
                {
                    CardOrientation orientation = OrientationOf(card.Pos);
                    return (controllerSide.Monsters[Math.Clamp(card.ZoneIndex, 0, ZoneCount - 1)], ZoneOffset(orientation, card.ZoneIndex), orientation, true);
                }

            case Location.SpellTrapZone:
                {
                    CardOrientation orientation = card.IsFaceUp ? CardOrientation.Attack : CardOrientation.Set;
                    return (controllerSide.SpellTraps[Math.Clamp(card.ZoneIndex, 0, ZoneCount - 1)], ZoneOffset(orientation, card.ZoneIndex), orientation, true);
                }

            case Location.FieldZone:
                return (controllerSide.SpellTraps[0], new Vector3(-SpacingX, 0.0f, 0.0f), CardOrientation.Attack, true);
            case Location.Hand:
                {
                    float x = (handIndex++ - (handCount - 1) * 0.5f) * HandSpacing;
                    return (ownerSide.Hand, new Vector3(x, 0.0f, handIndex * 0.001f), owner.Index == 0 ? CardOrientation.Attack : CardOrientation.FaceDown, owner.Index != 0 || _showPlayerHand);
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
            CardPosition.FaceDownDefense => CardOrientation.SetDefense,
            _ => CardOrientation.Set,
        };

    /// <summary>
    /// Set cards lie flat at the foot of the upright cards, like a table under the
    /// holograms, so from the duel camera they never cover the row behind them.
    /// A sideways Set monster is wider than the column spacing, so neighbours
    /// step a hair in height to overlap without z-fighting.
    /// </summary>
    private Vector3 ZoneOffset(CardOrientation orientation, int column) =>
        CardView.IsFlat(orientation) ? new Vector3(0.0f, -HologramCards.Height / 2.0f + column * StackStep, 0.0f) : Vector3.Zero;

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

    private DuelEffects AddEffects()
    {
        var effects = new DuelEffects { Name = "Effects" };
        AddChild(effects);
        return effects;
    }

    /// <summary>The anchors of one side: the grid, the hand row and the three piles (on the disk's markers when it has them).</summary>
    public sealed record SideAnchors(HologramSide Side, Node3D Root, Node3D[] Monsters, Node3D[] SpellTraps, Node3D Hand, Node3D Deck, Node3D Graveyard, Node3D Banished, bool PilesOnDisk);
}
