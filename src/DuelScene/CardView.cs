using BattleCity.Core;
using BattleCity.Duel.Core.Model;
using BattleCity.Rendering;
using Godot;

namespace BattleCity.DuelScene;

/// <summary>How a card sits on its anchor (systems.md §6.1).</summary>
public enum CardOrientation
{
    /// <summary>Upright, face toward the camera side.</summary>
    Attack,

    /// <summary>Face-up, turned 90° in its plane.</summary>
    Defense,

    /// <summary>Back toward the camera side, upright (the opponent's hand, the deck).</summary>
    FaceDown,

    /// <summary>On a duel disk pile, face to the sky whichever way the marker points (graveyard, banished).</summary>
    PileFaceUp,

    /// <summary>On a duel disk pile, face to the floor so nobody reads it (deck).</summary>
    PileFaceDown,

    /// <summary>Lying flat, face to the ground, long side along the duel axis (Set Spell/Trap).</summary>
    Set,

    /// <summary>Lying flat, face to the ground, turned 90° so the long side runs across the row (Set monster).</summary>
    SetDefense,
}

/// <summary>
/// One card in the 3D duel (systems.md §6.1): a hologram quad from
/// <see cref="HologramCards"/> carrying the face composed by <see cref="CardFaces"/>.
/// It is parented to an anchor by <see cref="AttachTo"/> and tweens its local
/// transform there; orientation, hover, selection, reveal and dissolve go
/// through the shader's instance parameters: the artist's card-state effects
/// (<see cref="DuelEffects"/>: CardMaterialise, CardSelected, Dissolve) drive
/// them when their scenes exist, short tweens otherwise, never both at once.
/// </summary>
public partial class CardView : Node3D
{
    /// <summary><c>duel.card_tween</c> from <c>data/tuning.json</c> (systems.md §10).</summary>
    public static float MoveTime => Tuning.Current.Duel.CardTween;
    public const float RevealTime = 0.3f;
    public const float DissolveTime = 1.0f;

    private MeshInstance3D? _mesh;
    private CollisionShape3D? _pickShape;
    private Tween? _move;
    private Tween? _effect;
    private Node? _selection;

    public CardInstance? Card { get; private set; }

    public HologramSide Side { get; private set; }

    /// <summary>
    /// Whether upright cards turn to face away from the duelist whose anchors they sit on, so both sides read from the
    /// player's camera. It follows the side the card is on, not its owner: a monster taken with Snatch Steal faces like its new side.
    /// </summary>
    public bool Mirrored { get; set; }

    public CardOrientation Orientation { get; private set; } = CardOrientation.FaceDown;

    /// <summary>The anchor this card was last attached to.</summary>
    public Node3D? Anchor { get; private set; }

    /// <summary>Moves so far (attachments that changed the anchor).</summary>
    public int Moves { get; private set; }

    public MeshInstance3D? Mesh => _mesh;

    /// <summary>The effect adapter of the staging; null means the shader tweens stand in.</summary>
    public DuelEffects? Effects { get; set; }

    /// <summary>The local position the last <see cref="AttachTo"/> aimed at (the tween may still be on its way).</summary>
    public Vector3 TargetPosition { get; private set; }

    /// <summary>Where the card comes to rest on its anchor, in world space: the transform effects anchor to.</summary>
    public Transform3D RestTransform =>
        Anchor is null ? GlobalTransform : Anchor.GlobalTransform * new Transform3D(Basis.FromEuler(RotationFor(Orientation)), TargetPosition);

    /// <summary>True while a CardSelected effect (or the fallback uniform) marks this card.</summary>
    public bool IsSelected { get; private set; }

    /// <summary>Builds the quad for <paramref name="card"/>; <paramref name="mirrored"/> for the side whose cards must face away from their owner.</summary>
    public void Setup(CardInstance card, HologramSide side, Texture2D? face, bool mirrored, float hoverPhase)
    {
        Card = card;
        Side = side;
        Mirrored = mirrored;
        Name = $"Card_{card.Def.Id}_{card.Id.ToString()[..8]}";
        _mesh = HologramCards.Create(side, face, hoverPhase);
        AddChild(_mesh);
        // Mouse picking (systems.md §6.3): an area on the card layer the size of the quad; DuelStaging.Pick casts against it.
        var pick = new Area3D
        {
            Name = "Pick",
            CollisionLayer = PhysicsLayers.Card,
            CollisionMask = 0,
            Monitoring = false,
            Monitorable = true,
        };
        _pickShape = new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(HologramCards.Width, HologramCards.Height, 0.01f) } };
        pick.AddChild(_pickShape);
        AddChild(pick);
    }

    /// <summary>Whether the mouse can pick this card; hidden cards (the player's 3D hand under the HUD fan) are not pickable.</summary>
    public void SetPickable(bool pickable)
    {
        if (_pickShape is not null)
        {
            _pickShape.Disabled = !pickable;
        }
    }

    /// <summary>The card view a picked collider belongs to, or null.</summary>
    public static CardView? FromCollider(GodotObject? collider) => (collider as Node)?.GetParent() as CardView;

    /// <summary>Swaps the face texture (a baked face replacing the viewport texture).</summary>
    public void SetFace(Texture2D face)
    {
        if (_mesh?.MaterialOverride is ShaderMaterial material)
        {
            material.SetShaderParameter("face_texture", face);
        }
    }

    /// <summary>Parents the card to <paramref name="anchor"/> and tweens it to <paramref name="localPosition"/> in <paramref name="orientation"/>; immediate when <paramref name="animate"/> is false.</summary>
    public void AttachTo(Node3D anchor, Vector3 localPosition, CardOrientation orientation, bool animate)
    {
        System.ArgumentNullException.ThrowIfNull(anchor);
        bool moved = !ReferenceEquals(anchor, Anchor);
        if (moved)
        {
            if (GetParent() is null)
            {
                anchor.AddChild(this);
            }
            else
            {
                Reparent(anchor, keepGlobalTransform: animate);
            }

            Anchor = anchor;
            Moves++;
        }

        Orientation = orientation;
        TargetPosition = localPosition;
        Vector3 rotation = RotationFor(orientation);
        _move?.Kill();
        if (!animate)
        {
            Position = localPosition;
            Rotation = rotation;
            return;
        }

        _move = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _move.TweenProperty(this, "position", localPosition, MoveTime);
        _move.TweenProperty(this, "rotation", rotation, MoveTime);
    }

    public bool IsMoving => _move is { } move && move.IsValid() && move.IsRunning();

    /// <summary>Selection glow (VFX hook <c>CardSelected</c>): one persistent effect per selected card, stopped when the selection leaves.</summary>
    public void SetSelected(bool selected)
    {
        if (_mesh is null || selected == IsSelected)
        {
            return;
        }

        IsSelected = selected;
        if (Effects is { } effects && effects.Available(DuelEffects.CardSelected))
        {
            if (selected)
            {
                _selection = effects.Spawn(DuelEffects.CardSelected, GlobalTransform, null, Side, _mesh);
            }
            else
            {
                DuelEffects.Stop(_selection);
                _selection = null;
            }

            return;
        }

        HologramCards.SetSelected(_mesh, selected);
    }

    /// <summary>Materialise wipe from hidden to whole (VFX hook <c>CardMaterialise</c>).</summary>
    public void Reveal()
    {
        if (_mesh is null)
        {
            return;
        }

        _effect?.Kill();
        if (Effects is { } effects && effects.Spawn(DuelEffects.CardMaterialise, RestTransform, null, Side, _mesh) is not null)
        {
            return;
        }

        HologramCards.SetReveal(_mesh, 0.0f);
        _effect = CreateTween();
        _effect.TweenMethod(Callable.From<float>(v => HologramCards.SetReveal(_mesh, v)), 0.0f, 1.0f, RevealTime);
    }

    /// <summary>End dissolve (VFX hook <c>Dissolve</c>); the node frees itself afterwards when <paramref name="free"/>.</summary>
    public void Dissolve(bool free)
    {
        if (_mesh is null)
        {
            if (free)
            {
                QueueFree();
            }

            return;
        }

        SetSelected(false);
        SetPickable(false);
        _effect?.Kill();
        if (Effects is { } effects && effects.Spawn(DuelEffects.Dissolve, RestTransform, null, Side, _mesh, free ? QueueFree : null) is not null)
        {
            return;
        }

        _effect = CreateTween();
        _effect.TweenMethod(Callable.From<float>(v => HologramCards.SetDissolve(_mesh, v)), 0.0f, 1.0f, DissolveTime);
        if (free)
        {
            _effect.TweenCallback(Callable.From(QueueFree));
        }
    }

    /// <summary>Whether a pile marker's normal (+Z) points to the floor, so its piles must grow and face the other way.</summary>
    public static bool PointsDown(Node3D? anchor) => anchor is not null && anchor.IsInsideTree() && anchor.GlobalBasis.Z.Y < -0.5f;

    /// <summary>Whether an orientation lies flat on the field plane (the Set cards) instead of standing upright.</summary>
    public static bool IsFlat(CardOrientation orientation) => orientation is CardOrientation.Set or CardOrientation.SetDefense;

    /// <summary>
    /// Euler rotation of an orientation: face-down turns about Y, Defense turns 90° in the card's plane (about Z).
    /// Set cards pitch 90° about X so the face looks at the ground and the back up; a Set monster also yaws 90°
    /// (Godot composes Y·X·Z, so the yaw is about the anchor's vertical).
    /// </summary>
    public Vector3 RotationFor(CardOrientation orientation)
    {
        if (IsFlat(orientation))
        {
            return new Vector3(Mathf.Pi / 2.0f, orientation == CardOrientation.SetDefense ? Mathf.Pi / 2.0f : 0.0f, 0.0f);
        }

        if (orientation is CardOrientation.PileFaceUp or CardOrientation.PileFaceDown)
        {
            // The face looks along the card's +Z. The disk's pile markers point wherever the arm pose
            // leaves them (the rookie disk's point down), so the side is chosen from the marker, not from Mirrored.
            bool faceAlongZ = (orientation == CardOrientation.PileFaceUp) ^ PointsDown(Anchor);
            return new Vector3(0.0f, faceAlongZ ? 0.0f : Mathf.Pi, 0.0f);
        }

        bool faceDown = orientation == CardOrientation.FaceDown;
        bool defense = orientation == CardOrientation.Defense;
        float yaw = (Mirrored ^ faceDown) ? Mathf.Pi : 0.0f;
        float roll = defense ? -Mathf.Pi / 2.0f : 0.0f;
        return new Vector3(0.0f, yaw, roll);
    }
}
