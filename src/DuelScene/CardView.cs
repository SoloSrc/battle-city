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

    /// <summary>Back toward the camera side, upright (Set Spell/Trap, deck).</summary>
    FaceDown,

    /// <summary>Back toward the camera side, turned 90° (Set monster).</summary>
    FaceDownDefense,
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

    /// <summary>The owner's face-up cards face +Z of their anchors (toward the owner); the opponent's face the other way, so both read from the player's camera.</summary>
    public bool Mirrored { get; private set; }

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

    /// <summary>Euler rotation of an orientation: face-down turns about Y, Defense turns 90° in the card's plane (about Z).</summary>
    public Vector3 RotationFor(CardOrientation orientation)
    {
        bool faceDown = orientation is CardOrientation.FaceDown or CardOrientation.FaceDownDefense;
        bool defense = orientation is CardOrientation.Defense or CardOrientation.FaceDownDefense;
        float yaw = (Mirrored ^ faceDown) ? Mathf.Pi : 0.0f;
        float roll = defense ? -Mathf.Pi / 2.0f : 0.0f;
        return new Vector3(0.0f, yaw, roll);
    }
}
