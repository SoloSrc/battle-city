using System;
using System.Collections.Generic;
using BattleCity.Characters;
using BattleCity.Core;
using BattleCity.World;
using Godot;

namespace BattleCity.Diagnostics;

/// <summary>
/// Exercises every level marker (architecture.md §7.1) in one small level:
/// the player walks into an AmbientZone, reads a Sign through the interaction
/// probe, is spotted by a Duelist's cone and challenges it, a ProgressionGate
/// opens on its flag, the Door / ShopCounter / TalkNpc contracts fire, and
/// LevelBounds puts the player back after running off the built area. The
/// navmesh is baked at load so tools/level_check.gd can run on this scene too.
/// </summary>
public partial class MarkersTestScene : Node3D
{
    private const string FailPrefix = "MarkersTest FAIL";
    private const int WalkEnd = 120;
    private const int ReadFrame = 130;
    private const int RunEnd = 260;
    private const int ApproachEnd = 290;
    private const int ChallengeFrame = 300;
    private const int ContractsFrame = 320;
    private const int BoundsStart = 340;
    private const int CheckFrame = 470;

    [Export]
    public Character? Player { get; set; }

    [Export]
    public NavigationRegion3D? Nav { get; set; }

    [Export]
    public PlayerSpawn? Spawn { get; set; }

    [Export]
    public Sign? Sign { get; set; }

    [Export]
    public Duelist? Duelist { get; set; }

    [Export]
    public EncounterSite? Site { get; set; }

    [Export]
    public AmbientZone? Zone { get; set; }

    [Export]
    public ProgressionGate? Gate { get; set; }

    [Export]
    public Door? Door { get; set; }

    [Export]
    public ShopCounter? Shop { get; set; }

    [Export]
    public TalkNpc? Npc { get; set; }

    [Export]
    public LevelBounds? Bounds { get; set; }

    [Export]
    public SurfaceTag? Ground { get; set; }

    [Export]
    public Label? ReportLabel { get; set; }

    [Export]
    public bool Scripted { get; set; }

    private readonly List<string> _report = new();
    private int _frame;
    private int _pass;
    private int _fail;
    private bool _done;
    private string _signText = "";
    private string _zoneEntered = "";
    private bool _spotted;
    private bool _challenged;
    private string _doorTarget = "";
    private string _shopId = "";
    private string _dialogueId = "";
    private bool _gateOpened;
    private Vector3 _recoveredFrom;
    private float _maxX;

    public override void _Ready()
    {
        Scripted = Scripted || DisplayServer.GetName() == "headless"
            || Array.IndexOf(OS.GetCmdlineUserArgs(), "--scripted") >= 0;
        if (Nav is not null && (Nav.NavigationMesh is null || Nav.NavigationMesh.GetPolygonCount() == 0))
        {
            Nav.NavigationMesh ??= new NavigationMesh();
            Nav.BakeNavigationMesh(false);
        }

        if (Player is null)
        {
            Fail("Player not wired");
            _done = true;
            return;
        }

        if (Spawn is not null)
        {
            Player.GlobalPosition = Spawn.GlobalPosition;
        }

        if (Sign is not null)
        {
            Sign.Read += (text, _) => _signText = text;
        }

        if (Zone is not null)
        {
            Zone.PlayerEntered += (music, _) => _zoneEntered = music;
        }

        if (Duelist is not null)
        {
            Duelist.PlayerSpotted += _ => _spotted = true;
            Duelist.Challenged += _ => _challenged = true;
        }

        if (Gate is not null)
        {
            Gate.Opened += _ => _gateOpened = true;
        }

        if (Door is not null)
        {
            Door.TransitionRequested += (scene, spawn, _) => _doorTarget = scene + "#" + spawn;
        }

        if (Shop is not null)
        {
            Shop.ShopRequested += (id, _) => _shopId = id;
        }

        if (Npc is not null)
        {
            Npc.TalkRequested += (id, _) => _dialogueId = id;
        }

        if (Bounds is not null)
        {
            Bounds.PlayerRecovered += (from, _) => _recoveredFrom = from;
        }

        Report("INFO", Scripted ? "scripted: zone, sign, cone, challenge, gate, contracts, bounds" : "live: walk around; the report only fills in scripted mode");
        int polygons = Nav?.NavigationMesh?.GetPolygonCount() ?? 0;
        Report(polygons > 0 ? "INFO" : "WARN", Inv($"navmesh baked at load: {polygons} polygons"));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_done || !Scripted || Player is null)
        {
            return;
        }

        _frame++;
        Drive();
        _maxX = Mathf.Max(_maxX, Player.GlobalPosition.X);
        if (_frame == ContractsFrame)
        {
            RunContracts();
        }

        if (_frame >= CheckFrame)
        {
            Finish();
        }
    }

    private void Drive()
    {
        Input.ActionRelease(InputActions.MoveUp);
        Input.ActionRelease(InputActions.MoveRight);
        if (_frame < WalkEnd)
        {
            Input.ActionPress(InputActions.MoveUp, 0.5f);
        }
        else if (_frame >= ReadFrame + 10 && _frame < RunEnd)
        {
            Input.ActionPress(InputActions.MoveUp, 1.0f);
        }
        else if (_frame >= RunEnd && _frame < ApproachEnd)
        {
            Input.ActionPress(InputActions.MoveUp, 0.5f);
        }
        else if (_frame >= BoundsStart && _frame < CheckFrame)
        {
            Input.ActionPress(InputActions.MoveRight, 1.0f);
        }

        if (_frame == ReadFrame || _frame == ChallengeFrame)
        {
            Input.ActionPress(InputActions.Interact);
        }
        else if (_frame == ReadFrame + 1 || _frame == ChallengeFrame + 1)
        {
            Input.ActionRelease(InputActions.Interact);
        }
    }

    private void RunContracts()
    {
        Gate?.ApplyFlags(new HashSet<string> { Flags.Defeated("d1") });
        Door?.Interact(Player!);
        Shop?.Interact(Player!);
        Npc?.Interact(Player!);
    }

    private void Finish()
    {
        _done = true;
        Vector3 p = Player!.GlobalPosition;
        Check(Spawn is { Id: PlayerSpawn.ArrivalId } && PlayerSpawn.Find(GetTree(), PlayerSpawn.ArrivalId) == Spawn, "PlayerSpawn 'arrival' found by id");
        Check(_zoneEntered == "plaza", $"AmbientZone reported the player entering (music '{_zoneEntered}')");
        Check(_signText.Length > 0, $"Sign read through the interaction probe ('{_signText}')");
        Check(_spotted, Inv($"Duelist cone ({Duelist?.ConeRange:F0} m, {Duelist?.ConeAngle:F0}°) spotted the player"));
        Check(_challenged, "Duelist challenged by interacting");
        Check(Site is not null && Site.StandPointA.DistanceTo(Site.StandPointB) is > 6.99f and < 7.01f, Inv($"EncounterSite stand points {Site?.StandPointA.DistanceTo(Site.StandPointB):F2} m apart"));
        Check(_gateOpened && Gate is { IsOpen: true, Blocker: StaticBody3D { CollisionLayer: 0 } }, "ProgressionGate opened on 'defeated:d1' and dropped its collision");
        Check(Gate?.Blocker is not null && Gate.Blocker.Position.Y < 0.0f, Inv($"gate blocker sank to y = {Gate?.Blocker?.Position.Y:F2}"));
        Check(_doorTarget.EndsWith("#arrival", StringComparison.Ordinal), $"Door requested a transition ({_doorTarget})");
        Check(_shopId == "card_shop", $"ShopCounter requested shop '{_shopId}'");
        Check(_dialogueId == "greeter", $"TalkNpc requested dialogue '{_dialogueId}'");
        float edge = Bounds is null ? 0.0f : Bounds.GlobalPosition.X + Bounds.Size.X * 0.5f;
        Check(Bounds is { Recoveries: > 0 } && _maxX < edge + 0.5f, Inv($"LevelBounds recovered the player {Bounds?.Recoveries} times running at the x = {edge:F0} edge (furthest x = {_maxX:F2}, back to {p.X:F2})"));
        Check(SurfaceTag.Of(Ground) == SurfaceTag.Surface.Grass && SurfaceTag.Of(null) == SurfaceTag.Surface.Stone, "SurfaceTag reports grass for the ground and stone by default");
        string summary = $"MarkersTest summary: {_pass} pass, {_fail} fail";
        GD.Print(summary);
        if (ReportLabel is not null)
        {
            ReportLabel.Text = summary + "\n" + string.Join("\n", _report);
        }
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
        GD.Print($"MarkersTest {status}: {message}");
    }
}
