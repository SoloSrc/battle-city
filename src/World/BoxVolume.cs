using Godot;

namespace BattleCity.World;

/// <summary>Shared helper for volume markers whose box shape follows an exported size.</summary>
public static class BoxVolume
{
    public static void SyncShape(Node3D owner, Vector3 size)
    {
        CollisionShape3D? shape = owner.GetNodeOrNull<CollisionShape3D>("Shape");
        if (shape is null)
        {
            return;
        }

        if (shape.Shape is not BoxShape3D box)
        {
            box = new BoxShape3D();
            shape.Shape = box;
        }

        box.Size = size;
    }
}
