using System.Collections.Generic;
using BattleCity.Core;
using Godot;

namespace BattleCity.Characters;

/// <summary>
/// The duel disk prop (asset-list §3): loads <c>prop_duel_disk.glb</c>, plays
/// its deploy/fold clips and exposes the deck, graveyard, banished and bay
/// markers that DuelStaging parents card views to.
/// </summary>
public partial class DuelDisk : Node3D
{
    public const string DeployClip = "disk_deploy";
    public const string FoldClip = "disk_fold";

    [Signal]
    public delegate void DeployFinishedEventHandler();

    [Signal]
    public delegate void FoldFinishedEventHandler();

    [Export]
    public string PropPath { get; set; } = Paths.DuelDiskModel;

    public bool IsLoaded { get; private set; }

    public bool IsDeployed { get; private set; }

    private AnimationPlayer? _player;
    private readonly Dictionary<string, Node3D> _markers = new();

    public override void _Ready()
    {
        if (!ResourceLoader.Exists(PropPath))
        {
            return;
        }

        var packed = ResourceLoader.Load<PackedScene>(PropPath);
        if (packed is null)
        {
            GD.PushError($"DuelDisk: {PropPath} did not import as a scene");
            return;
        }

        Node prop = packed.Instantiate();
        prop.Name = "Prop";
        AddChild(prop);
        IsLoaded = true;
        CollectMarkers(prop);
        _player = FindPlayer(prop);
        if (_player is not null)
        {
            _player.AnimationFinished += OnAnimationFinished;
        }
    }

    /// <summary>Marker node by name (<c>deck</c>, <c>graveyard</c>, <c>banished</c>, <c>bay_1</c>..<c>bay_5</c>), or null.</summary>
    public Node3D? GetMarker(string name)
    {
        return _markers.TryGetValue(name, out Node3D? marker) ? marker : null;
    }

    public IReadOnlyCollection<string> MarkerNames => _markers.Keys;

    public bool HasClip(string clip) => _player is not null && _player.HasAnimation(clip);

    public void Deploy()
    {
        IsDeployed = true;
        if (!Play(DeployClip))
        {
            EmitSignal(SignalName.DeployFinished);
        }
    }

    public void Fold()
    {
        IsDeployed = false;
        if (!Play(FoldClip))
        {
            EmitSignal(SignalName.FoldFinished);
        }
    }

    private bool Play(string clip)
    {
        if (_player is null || !_player.HasAnimation(clip))
        {
            return false;
        }

        _player.Play(clip);
        return true;
    }

    private void OnAnimationFinished(StringName animation)
    {
        if (animation == DeployClip)
        {
            EmitSignal(SignalName.DeployFinished);
        }
        else if (animation == FoldClip)
        {
            EmitSignal(SignalName.FoldFinished);
        }
    }

    private void CollectMarkers(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is Node3D node && child is not MeshInstance3D && child.GetChildCount() == 0)
            {
                _markers[child.Name] = node;
            }

            CollectMarkers(child);
        }
    }

    private static AnimationPlayer? FindPlayer(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is AnimationPlayer player)
            {
                return player;
            }

            AnimationPlayer? nested = FindPlayer(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
