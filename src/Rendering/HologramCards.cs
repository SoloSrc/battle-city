using BattleCity.Core;
using Godot;

namespace BattleCity.Rendering;

/// <summary>Which duelist a hologram belongs to; picks the edge colour (direction.md palette).</summary>
public enum HologramSide
{
    Player,
    Opponent,
}

/// <summary>
/// Builds the floating card quads that DuelStaging parents to its card anchors
/// (systems.md §6.1, issue #27). Each card is a 0.20 × 0.29 m quad (590:860)
/// facing +Z with <c>shaders/hologram.gdshader</c>; the two side materials in
/// <c>shaders/materials/</c> are duplicated per card only to carry the face
/// texture, and per-card state goes through instance shader parameters.
/// </summary>
public static class HologramCards
{
    public const float Width = 0.20f;
    public const float Height = Width * 860.0f / 590.0f;

    /// <summary>Instance shader parameters every card exposes (see shaders/README.md).</summary>
    public const string HoverPhase = "hover_phase";
    public const string Selected = "selected";
    public const string Reveal = "reveal";
    public const string Dissolve = "dissolve";

    private static ShaderMaterial? _player;
    private static ShaderMaterial? _opponent;

    /// <summary>The shared material of a side, or null when the resource is missing.</summary>
    public static ShaderMaterial? SideMaterial(HologramSide side)
    {
        string path = side == HologramSide.Player ? Paths.HologramPlayerMaterial : Paths.HologramOpponentMaterial;
        ref ShaderMaterial? slot = ref side == HologramSide.Player ? ref _player : ref _opponent;
        if (slot is null && ResourceLoader.Exists(path))
        {
            slot = ResourceLoader.Load<ShaderMaterial>(path);
        }

        return slot;
    }

    /// <summary>
    /// A card quad for <paramref name="side"/> showing <paramref name="face"/> (the
    /// composed card face; null keeps the material's default) with the back on the
    /// reverse. <paramref name="hoverPhase"/> (0–1) offsets the bob per card.
    /// </summary>
    public static MeshInstance3D Create(HologramSide side, Texture2D? face = null, float hoverPhase = 0.0f)
    {
        ShaderMaterial? shared = SideMaterial(side);
        Material? material = null;
        if (shared is not null)
        {
            var copy = (ShaderMaterial)shared.Duplicate();
            if (face is not null)
            {
                copy.SetShaderParameter("face_texture", face);
            }

            material = copy;
        }
        else
        {
            GD.PushError($"HologramCards: material for {side} is missing, card drawn unlit");
        }

        var card = new MeshInstance3D
        {
            Name = "HologramCard",
            Mesh = new QuadMesh { Size = new Vector2(Width, Height) },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        card.SetInstanceShaderParameter(HoverPhase, hoverPhase);
        return card;
    }

    public static void SetSelected(MeshInstance3D card, bool selected) =>
        card.SetInstanceShaderParameter(Selected, selected ? 1.0f : 0.0f);

    /// <summary>Materialise wipe: 0 hides the card, 1 shows all of it (VFX hook 6.2).</summary>
    public static void SetReveal(MeshInstance3D card, float reveal) =>
        card.SetInstanceShaderParameter(Reveal, Mathf.Clamp(reveal, 0.0f, 1.0f));

    /// <summary>End dissolve: 0 intact, 1 gone (VFX hook 6.7).</summary>
    public static void SetDissolve(MeshInstance3D card, float dissolve) =>
        card.SetInstanceShaderParameter(Dissolve, Mathf.Clamp(dissolve, 0.0f, 1.0f));
}
