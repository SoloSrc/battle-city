using BattleCity.Characters;
using BattleCity.Core;
using Godot;

namespace BattleCity.World;

/// <summary>
/// Audio zone per area (systems.md §3.4). Reports the player entering and
/// leaving with the music and ambience ids; the audio system crossfades.
/// </summary>
[Tool]
public partial class AmbientZone : Area3D
{
    [Signal]
    public delegate void PlayerEnteredEventHandler(string musicId, string ambienceId);

    [Signal]
    public delegate void PlayerExitedEventHandler(string musicId, string ambienceId);

    private Vector3 _size = new(20.0f, 10.0f, 20.0f);

    [Export]
    public string MusicId { get; set; } = "";

    [Export]
    public string AmbienceId { get; set; } = "";

    [Export(PropertyHint.None, "suffix:m")]
    public Vector3 Size
    {
        get => _size;
        set
        {
            _size = new Vector3(Mathf.Max(value.X, 0.1f), Mathf.Max(value.Y, 0.1f), Mathf.Max(value.Z, 0.1f));
            BoxVolume.SyncShape(this, _size);
        }
    }

    public bool PlayerInside { get; private set; }

    public override void _Ready()
    {
        BoxVolume.SyncShape(this, _size);
        if (Engine.IsEditorHint())
        {
            return;
        }

        AddToGroup(Groups.AmbientZone);
        CollisionLayer = PhysicsLayers.Trigger;
        CollisionMask = PhysicsLayers.Character;
        Monitoring = true;
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is Character && body.IsInGroup(Groups.Player))
        {
            PlayerInside = true;
            EmitSignal(SignalName.PlayerEntered, MusicId, AmbienceId);
        }
    }

    private void OnBodyExited(Node3D body)
    {
        if (body is Character && body.IsInGroup(Groups.Player))
        {
            PlayerInside = false;
            EmitSignal(SignalName.PlayerExited, MusicId, AmbienceId);
        }
    }
}
