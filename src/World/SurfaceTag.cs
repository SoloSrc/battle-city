using Godot;

namespace BattleCity.World;

/// <summary>
/// Footstep surface for a piece of collision (systems.md §3.4). Attach to a
/// <c>StaticBody3D</c>; untagged collision counts as <see cref="Surface.Stone"/>.
/// </summary>
public partial class SurfaceTag : StaticBody3D
{
    public enum Surface
    {
        Stone,
        Grass,
        Wood,
    }

    [Export]
    public Surface Kind { get; set; } = Surface.Stone;

    /// <summary>Surface of a collider hit by a ray or floor query.</summary>
    public static Surface Of(GodotObject? collider) => collider is SurfaceTag tag ? tag.Kind : Surface.Stone;
}
