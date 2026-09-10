using BattleCity.Core;
using Godot;

namespace BattleCity.World;

/// <summary>
/// Where the player appears (architecture.md §7.1). The node's −Z is the
/// facing. <c>arrival</c> is the New Game spawn; doors name their return spawn.
/// </summary>
public partial class PlayerSpawn : Marker3D
{
    public const string ArrivalId = "arrival";

    [Export]
    public string Id { get; set; } = ArrivalId;

    public Vector3 Facing => -GlobalTransform.Basis.Z;

    public override void _Ready()
    {
        AddToGroup(Groups.Spawn);
    }

    /// <summary>Finds the spawn with <paramref name="id"/> in the current scene, or null.</summary>
    public static PlayerSpawn? Find(SceneTree tree, string id)
    {
        foreach (Node node in tree.GetNodesInGroup(Groups.Spawn))
        {
            if (node is PlayerSpawn spawn && spawn.Id == id)
            {
                return spawn;
            }
        }

        return null;
    }
}
