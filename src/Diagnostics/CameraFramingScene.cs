using System;
using System.Collections.Generic;
using BattleCity.Characters;
using BattleCity.Core;
using BattleCity.World;
using Godot;

namespace BattleCity.Diagnostics;

/// <summary>
/// Readability diagnostic for the overworld camera (systems.md §4.2). Shows the
/// player, a duelist at the 7 m stand distance and 1 m reference cubes on the
/// grid through <see cref="CameraRig"/>, with two <see cref="CameraBounds"/>
/// volumes (a plaza and an interior) to exercise clamping and the interior
/// override. The HUD reports the live framing values and the avatar's pixel
/// height, which is an outcome to look at, not a target (director decision
/// 2026-09-08). Scripted mode (headless or <c>-- --scripted</c>) runs the
/// player east through the seam and past the edge and checks the rig.
/// </summary>
public partial class CameraFramingScene : Node3D
{
    private const string FailPrefix = "CameraFraming FAIL";
    private const float AvatarHeight = 1.7f;
    private const int SettleEnd = 60;
    private const int TrackCheck = 200;
    private const int InteriorCheck = 310;
    private const int EdgeCheck = 480;
    private const int CornerCheck = 600;

    private static readonly (string Name, float Pitch, float Distance, float Fov)[] _presets =
    {
        ("Overworld (GDD)", 57.0f, 12.0f, 35.0f),
        ("GDD min", 55.0f, 12.0f, 35.0f),
        ("GDD max", 60.0f, 14.0f, 35.0f),
        ("Far", 57.0f, 14.0f, 35.0f),
    };

    [Export]
    public Character? Player { get; set; }

    [Export]
    public PlayerController? Controller { get; set; }

    [Export]
    public CameraRig? Rig { get; set; }

    [Export]
    public CameraBounds? Plaza { get; set; }

    [Export]
    public CameraBounds? Interior { get; set; }

    [Export]
    public Node3D? Cubes { get; set; }

    [Export]
    public Label? ReportLabel { get; set; }

    [Export]
    public bool Scripted { get; set; }

    private readonly List<string> _report = new();
    private readonly List<MeshInstance3D> _boundsMeshes = new();
    private int _frame;
    private int _pass;
    private int _fail;
    private int _preset;
    private bool _done;
    private float _maxLag;
    private float _avatarPx1080;

    public override void _Ready()
    {
        Scripted = Scripted || DisplayServer.GetName() == "headless"
            || Array.IndexOf(OS.GetCmdlineUserArgs(), "--scripted") >= 0;
        SpawnCubes();
        BuildBoundsMeshes();
        if (Player is null || Controller is null || Rig is null)
        {
            Fail("Player, Controller or Rig not wired");
            _done = true;
            return;
        }

        Report("INFO", Scripted
            ? "scripted: run east through the plaza/interior seam, then past the edge and the corner"
            : "live: WASD / stick moves, menu cycles framing presets, cancel toggles bounds volumes");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Player is null || Controller is null || Rig is null)
        {
            return;
        }

        if (!Scripted)
        {
            HandleLiveInput();
            UpdateHud();
            return;
        }

        if (_done)
        {
            return;
        }

        _frame++;
        DriveScript();
        Measure();
        UpdateHud();
    }

    private void HandleLiveInput()
    {
        if (Input.IsActionJustPressed(InputActions.Menu) && Rig is not null)
        {
            _preset = (_preset + 1) % _presets.Length;
            (string name, float pitch, float distance, float fov) = _presets[_preset];
            Rig.Pitch = pitch;
            Rig.Distance = distance;
            Rig.Fov = fov;
            Report("INFO", Inv($"preset {name}: pitch {pitch:F0}°, distance {distance:F1} m, fov {fov:F0}°"));
        }

        if (Input.IsActionJustPressed(InputActions.Cancel))
        {
            foreach (MeshInstance3D mesh in _boundsMeshes)
            {
                mesh.Visible = !mesh.Visible;
            }
        }
    }

    private void DriveScript()
    {
        if (_frame < SettleEnd)
        {
            Input.ActionRelease(InputActions.MoveRight);
            Input.ActionRelease(InputActions.MoveUp);
        }
        else if (_frame < EdgeCheck)
        {
            Input.ActionPress(InputActions.MoveRight, 1.0f);
        }
        else
        {
            Input.ActionRelease(InputActions.MoveRight);
            Input.ActionPress(InputActions.MoveUp, 1.0f);
        }

        Vector3 focus = Player!.GlobalPosition + Vector3.Up * Rig!.FocusHeight;
        Vector2 rigXZ = new(Rig.GlobalPosition.X, Rig.GlobalPosition.Z);
        Vector2 playerXZ = new(focus.X, focus.Z);
        if (_frame > SettleEnd && _frame < TrackCheck)
        {
            _maxLag = Mathf.Max(_maxLag, rigXZ.DistanceTo(playerXZ));
        }

        switch (_frame)
        {
            case SettleEnd:
                Check(rigXZ.DistanceTo(playerXZ) < 0.05f, Inv($"rig snapped to the player at start (offset {rigXZ.DistanceTo(playerXZ):F3} m)"));
                Check(Mathf.Abs(Rig.CurrentPitch - 57.0f) < 0.01f && Mathf.Abs(Rig.CurrentDistance - 12.0f) < 0.01f
                    && Mathf.Abs(Rig.CurrentFov - 35.0f) < 0.01f, "overworld profile is the GDD baseline (57°, 12 m, 35° fov)");
                Check(Rig.ActiveBounds == Plaza, $"player starts inside '{Rig.ActiveBounds?.Name}'");
                Check(_avatarPx1080 > 0.0f, Inv($"avatar ≈ {_avatarPx1080:F0} px tall at 1080p (diagnostic only, not a target)"));
                break;
            case TrackCheck:
                Check(_maxLag < 0.9f && _maxLag > 0.2f, Inv($"follow lag while running peaked at {_maxLag:F2} m (0.15 s smoothing at 4.5 m/s)"));
                Check(!Rig.IsClamped, Inv($"not clamped inside the plaza at x = {focus.X:F1}"));
                break;
            case InteriorCheck:
                Check(Rig.ActiveBounds == Interior, Inv($"interior volume took over at x = {focus.X:F1} (active '{Rig.ActiveBounds?.Name}')"));
                Check(Mathf.Abs(Rig.CurrentPitch - Interior!.Pitch) < 0.5f && Mathf.Abs(Rig.CurrentDistance - Interior.Distance) < 0.3f,
                    Inv($"blended to the interior profile: pitch {Rig.CurrentPitch:F1}°, distance {Rig.CurrentDistance:F2} m"));
                break;
            case EdgeCheck:
                {
                    float edge = Interior!.GlobalPosition.X + Interior.Size.X * 0.5f - Rig.SoftMargin;
                    Check(Rig.IsClamped && Mathf.Abs(Rig.GlobalPosition.X - edge) < 0.05f,
                        Inv($"clamped at the east edge: rig x = {Rig.GlobalPosition.X:F2}, player x = {focus.X:F2}, limit {edge:F2}"));
                    Check(Rig.ActiveBounds is null, "no volume contains the player past the edge");
                    break;
                }

            case CornerCheck:
                {
                    float edgeX = Interior!.GlobalPosition.X + Interior.Size.X * 0.5f - Rig.SoftMargin;
                    float edgeZ = Interior.GlobalPosition.Z - Interior.Size.Z * 0.5f + Rig.SoftMargin;
                    Check(Mathf.Abs(Rig.GlobalPosition.X - edgeX) < 0.05f && Mathf.Abs(Rig.GlobalPosition.Z - edgeZ) < 0.05f,
                        Inv($"held the north-east corner ({Rig.GlobalPosition.X:F2}, {Rig.GlobalPosition.Z:F2}) while the player left toward ({focus.X:F1}, {focus.Z:F1})"));
                    Check(Mathf.Abs(Rig.CurrentPitch - Rig.Pitch) < 0.5f, Inv($"profile returned to overworld outside all volumes (pitch {Rig.CurrentPitch:F1}°)"));
                    Finish();
                    break;
                }
        }
    }

    private void Measure()
    {
        if (Rig?.Camera is null || Player is null)
        {
            return;
        }

        Vector3 feet = Player.GlobalPosition;
        Vector2 feetPx = Rig.Camera.UnprojectPosition(feet);
        Vector2 headPx = Rig.Camera.UnprojectPosition(feet + Vector3.Up * AvatarHeight);
        float viewportHeight = GetViewport().GetVisibleRect().Size.Y;
        if (viewportHeight > 0.0f)
        {
            _avatarPx1080 = Mathf.Abs(headPx.Y - feetPx.Y) * 1080.0f / viewportHeight;
        }
    }

    private void UpdateHud()
    {
        if (ReportLabel is null || Rig is null)
        {
            return;
        }

        if (!Scripted)
        {
            Measure();
        }

        string live = Inv($"pitch {Rig.CurrentPitch:F1}°  distance {Rig.CurrentDistance:F2} m  fov {Rig.CurrentFov:F0}°  yaw {Rig.Yaw:F0}°\n")
            + Inv($"rig ({Rig.GlobalPosition.X:F2}, {Rig.GlobalPosition.Z:F2})  bounds {Rig.ActiveBounds?.Name ?? "none"}{(Rig.IsClamped ? "  CLAMPED" : string.Empty)}\n")
            + Inv($"avatar ≈ {_avatarPx1080:F0} px at 1080p (outcome, not a target)");
        ReportLabel.Text = live + "\n" + string.Join("\n", _report);
    }

    private void SpawnCubes()
    {
        if (Cubes is null)
        {
            return;
        }

        PackedScene? kit = ResourceLoader.Exists(Paths.SmokeCube) ? ResourceLoader.Load<PackedScene>(Paths.SmokeCube) : null;
        Vector3[] spots = { new(-3, 0, -3), new(3, 0, -3), new(-3, 0, 3), new(3, 0, 3), new(9, 0, -3), new(16, 0, -3) };
        foreach (Vector3 spot in spots)
        {
            Node3D cube;
            if (kit is not null)
            {
                cube = kit.Instantiate<Node3D>();
            }
            else
            {
                cube = new MeshInstance3D { Mesh = new BoxMesh { Size = Vector3.One }, Position = new Vector3(0.5f, 0.5f, 0.5f) };
                Node3D holder = new();
                holder.AddChild(cube);
                cube = holder;
            }

            cube.Position = spot;
            Cubes.AddChild(cube);
        }
    }

    private void BuildBoundsMeshes()
    {
        foreach (CameraBounds? bounds in new[] { Plaza, Interior })
        {
            if (bounds is null)
            {
                continue;
            }

            StandardMaterial3D material = new()
            {
                AlbedoColor = bounds.Interior ? new Color(0.9f, 0.5f, 0.2f, 0.12f) : new Color(0.2f, 0.6f, 0.9f, 0.12f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            MeshInstance3D mesh = new()
            {
                Mesh = new BoxMesh { Size = bounds.Size, Material = material },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = false,
            };
            bounds.AddChild(mesh);
            _boundsMeshes.Add(mesh);
        }
    }

    private void Finish()
    {
        _done = true;
        string summary = $"CameraFraming summary: {_pass} pass, {_fail} fail";
        GD.Print(summary);
        _report.Insert(0, summary);
    }

    private static string Inv(FormattableString text) => FormattableString.Invariant(text);

    private void Check(bool ok, string message)
    {
        if (ok)
        {
            _pass++;
            Report("PASS", message);
        }
        else
        {
            Fail(message);
        }
    }

    private void Fail(string message)
    {
        _fail++;
        Report("FAIL", message);
        GD.PrintErr($"{FailPrefix}: {message}");
    }

    private void Report(string status, string message)
    {
        _report.Add($"[{status}] {message}");
        GD.Print($"CameraFraming {status}: {message}");
    }
}
