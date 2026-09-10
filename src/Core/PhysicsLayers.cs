namespace BattleCity.Core;

/// <summary>3D physics layers as bit masks (systems.md §4.4; names in project.godot).</summary>
public static class PhysicsLayers
{
    public const uint World = 1u << 0;
    public const uint Character = 1u << 1;
    public const uint Interactable = 1u << 2;
    public const uint Trigger = 1u << 3;
    public const uint Card = 1u << 4;
    public const uint CameraBlocker = 1u << 5;

    public const uint PlayerMask = World | Character;
    public const uint NpcMask = World | Character;
    public const uint CameraMask = World | CameraBlocker;
}
