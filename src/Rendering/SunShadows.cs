using Godot;

namespace BattleCity.Rendering;

/// <summary>
/// Applies one directional-shadow setup to every shadow-casting
/// <see cref="DirectionalLight3D"/> in a level when it loads, the way
/// <see cref="ToonMaterials"/> applies the cel material: the levels keep only
/// the sun's direction and energy, and the shadow quality is tuned here for
/// the fixed 57° / 12 m overworld camera. Orthogonal mode gives the whole
/// visible range the full atlas instead of the coarse far split of the
/// PSSM default, which is what drew stair-stepped edges under the arcade
/// windows and around the characters (walkthrough feedback, 2026-09-13).
/// </summary>
public static class SunShadows
{
    /// <summary>The camera sees about 20 m of ground; shadows fade a little beyond that.</summary>
    public const float MaxDistance = 25.0f;

    public const float Blur = 1.0f;

    public const float Bias = 0.1f;

    public const float NormalBias = 2.0f;

    /// <summary>Angular size of the sun in degrees; a small value softens the penumbra without going blurry.</summary>
    public const float AngularDistance = 0.5f;

    /// <summary>Configures every shadow-casting directional light under <paramref name="root"/>; returns how many were touched.</summary>
    public static int Apply(Node root)
    {
        int count = 0;
        if (root is DirectionalLight3D { ShadowEnabled: true } light)
        {
            Configure(light);
            count++;
        }

        foreach (Node child in root.GetChildren())
        {
            count += Apply(child);
        }

        return count;
    }

    public static void Configure(DirectionalLight3D light)
    {
        light.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal;
        light.DirectionalShadowMaxDistance = MaxDistance;
        light.ShadowBlur = Blur;
        light.ShadowBias = Bias;
        light.ShadowNormalBias = NormalBias;
        light.LightAngularDistance = AngularDistance;
    }
}
