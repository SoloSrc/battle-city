using System.Collections.Generic;
using BattleCity.Core;
using Godot;

namespace BattleCity.World;

/// <summary>
/// Blocks a route until <see cref="RequiredFlag"/> is set (systems.md §3.4).
/// The <see cref="Blocker"/> slot holds the visible barrier (a placeholder
/// box by default; the level designer may drop a kit piece in). Opening
/// sinks the blocker into the ground over <see cref="OpenTime"/> and
/// disables its collision. <c>Game</c> calls <see cref="ApplyFlags"/> on load
/// and whenever a flag changes.
/// </summary>
public partial class ProgressionGate : Node3D
{
    [Signal]
    public delegate void OpenedEventHandler(string requiredFlag);

    [Export]
    public string RequiredFlag { get; set; } = Flags.Defeated("d1");

    [Export]
    public Node3D? Blocker { get; set; }

    [Export(PropertyHint.Range, "0,3,0.1,suffix:s")]
    public float OpenTime { get; set; } = 0.6f;

    [Export(PropertyHint.Range, "0,10,0.1,suffix:m")]
    public float SinkDepth { get; set; } = 3.2f;

    public bool IsOpen { get; private set; }

    public override void _Ready()
    {
        AddToGroup(Groups.Gate);
        Blocker ??= GetNodeOrNull<Node3D>("Blocker");
    }

    /// <summary>Opens (or stays open) when the required flag is in <paramref name="flags"/>.</summary>
    public void ApplyFlags(IReadOnlySet<string> flags, bool instant = false)
    {
        if (!IsOpen && flags.Contains(RequiredFlag))
        {
            Open(instant);
        }
    }

    public void Open(bool instant = false)
    {
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        if (Blocker is not null)
        {
            SetCollision(Blocker, false);
            Vector3 target = Blocker.Position + Vector3.Down * SinkDepth;
            if (instant || OpenTime <= 0.0f)
            {
                Blocker.Position = target;
            }
            else
            {
                CreateTween().TweenProperty(Blocker, "position", target, OpenTime)
                    .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            }
        }

        EmitSignal(SignalName.Opened, RequiredFlag);
    }

    private static void SetCollision(Node node, bool enabled)
    {
        if (node is CollisionObject3D body)
        {
            body.CollisionLayer = enabled ? PhysicsLayers.World : 0u;
        }

        foreach (Node child in node.GetChildren())
        {
            SetCollision(child, enabled);
        }
    }
}
