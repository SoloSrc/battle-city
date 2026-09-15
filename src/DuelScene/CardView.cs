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
/// through the shader's instance parameters and short tweens.
/// </summary>
public partial class CardView : Node3D
{
    public const float MoveTime = 0.15f;
    public const float RevealTime = 0.3f;
    public const float DissolveTime = 1.0f;

    private MeshInstance3D? _mesh;
    private Tween? _move;
    private Tween? _effect;

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

    /// <summary>Builds the quad for <paramref name="card"/>; <paramref name="mirrored"/> for the side whose cards must face away from their owner.</summary>
    public void Setup(CardInstance card, HologramSide side, Texture2D? face, bool mirrored, float hoverPhase)
    {
        Card = card;
        Side = side;
        Mirrored = mirrored;
        Name = $"Card_{card.Def.Id}_{card.Id.ToString()[..8]}";
        _mesh = HologramCards.Create(side, face, hoverPhase);
        AddChild(_mesh);
    }

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

    public void SetSelected(bool selected)
    {
        if (_mesh is not null)
        {
            HologramCards.SetSelected(_mesh, selected);
        }
    }

    /// <summary>Materialise wipe from hidden to whole (VFX hook 6.2).</summary>
    public void Reveal()
    {
        if (_mesh is null)
        {
            return;
        }

        _effect?.Kill();
        HologramCards.SetReveal(_mesh, 0.0f);
        _effect = CreateTween();
        _effect.TweenMethod(Callable.From<float>(v => HologramCards.SetReveal(_mesh, v)), 0.0f, 1.0f, RevealTime);
    }

    /// <summary>End dissolve (VFX hook 6.7); the node frees itself afterwards when <paramref name="free"/>.</summary>
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

        _effect?.Kill();
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
