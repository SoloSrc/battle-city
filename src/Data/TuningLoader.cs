using System.IO;

namespace BattleCity.Data;

/// <summary>Reads <c>data/tuning.json</c> (systems.md §10): every key is required, distances and times are positive, angles stay in range.</summary>
public sealed class TuningLoader
{
    public TuningDefinition LoadFile(string path) => Parse(File.ReadAllText(path), path);

    public TuningDefinition Parse(string json, string source = "tuning")
    {
        TuningDto dto = Json.Parse<TuningDto>(json, source);
        PlayerDto player = Require(dto.Player, source, "player");
        CameraDto camera = Require(dto.Camera, source, "camera");
        ProfileDto overworld = Require(camera.Overworld, source, "camera.overworld");
        DuelCameraDto duelCamera = Require(camera.Duel, source, "camera.duel");
        EncounterDto encounter = Require(dto.Encounter, source, "encounter");
        DuelDto duel = Require(dto.Duel, source, "duel");
        AnchorsDto anchors = Require(dto.Anchors, source, "anchors");

        return new TuningDefinition(
            new PlayerTuning(
                Positive(player.WalkSpeed, source, "player.walk_speed"),
                Positive(player.RunSpeed, source, "player.run_speed")),
            new CameraTuning(
                new CameraProfileTuning(
                    Range(overworld.Pitch, 0.0f, 90.0f, source, "camera.overworld.pitch"),
                    Positive(overworld.Distance, source, "camera.overworld.distance"),
                    Range(overworld.Fov, 1.0f, 179.0f, source, "camera.overworld.fov")),
                new DuelCameraTuning(
                    Range(duelCamera.Pitch, 0.0f, 90.0f, source, "camera.duel.pitch"),
                    Positive(duelCamera.Distance, source, "camera.duel.distance"),
                    Range(duelCamera.Fov, 1.0f, 179.0f, source, "camera.duel.fov"),
                    NonNegative(duelCamera.BlendTime, source, "camera.duel.blend_time"),
                    NonNegative(duelCamera.FocusHeight, source, "camera.duel.focus_height"))),
            new EncounterTuning(
                Positive(encounter.ConeRange, source, "encounter.cone_range"),
                Range(encounter.ConeAngle, 0.0f, 180.0f, source, "encounter.cone_angle"),
                Positive(encounter.StandDistance, source, "encounter.stand_distance"),
                NonNegative(encounter.RevealTime, source, "encounter.reveal_time"),
                NonNegative(encounter.RevealBlend, source, "encounter.reveal_blend"),
                Positive(encounter.WalkSpeed, source, "encounter.walk_speed"),
                NonNegative(encounter.ResultTime, source, "encounter.result_time"),
                NonNegative(encounter.DisarmTime, source, "encounter.disarm_time")),
            new DuelTuning(
                PositiveInt(duel.StartLp, source, "duel.start_lp"),
                PositiveInt(duel.HandLimit, source, "duel.hand_limit"),
                PositiveInt(duel.OpeningHand, source, "duel.opening_hand"),
                NonNegative(duel.CardTween, source, "duel.card_tween"),
                NonNegative(duel.AiDelay, source, "duel.ai_delay")),
            new AnchorTuning(
                Positive(anchors.Forward, source, "anchors.forward"),
                Positive(anchors.SpacingX, source, "anchors.spacing_x"),
                Positive(anchors.SpacingZ, source, "anchors.spacing_z"),
                Positive(anchors.ChestHeight, source, "anchors.chest_height"),
                NonNegative(anchors.HandForward, source, "anchors.hand_forward"),
                Finite(anchors.HandSide, source, "anchors.hand_side"),
                Positive(anchors.HandHeight, source, "anchors.hand_height"),
                Positive(anchors.HandSpacing, source, "anchors.hand_spacing"),
                NonNegative(anchors.StackStep, source, "anchors.stack_step"),
                Range(anchors.LungeFraction, 0.0f, 1.0f, source, "anchors.lunge_fraction")));
    }

    private static T Require<T>(T? value, string source, string key)
        where T : class =>
        value ?? throw new DataException($"{source}: '{key}' is required.");

    private static float Finite(float? value, string source, string key)
    {
        if (value is not { } v || float.IsNaN(v) || float.IsInfinity(v))
        {
            throw new DataException($"{source}: '{key}' must be a number.");
        }

        return v;
    }

    private static float Positive(float? value, string source, string key)
    {
        float v = Finite(value, source, key);
        return v > 0.0f ? v : throw new DataException($"{source}: '{key}' must be positive (got {v}).");
    }

    private static float NonNegative(float? value, string source, string key)
    {
        float v = Finite(value, source, key);
        return v >= 0.0f ? v : throw new DataException($"{source}: '{key}' must be zero or more (got {v}).");
    }

    private static float Range(float? value, float min, float max, string source, string key)
    {
        float v = Finite(value, source, key);
        return v >= min && v <= max ? v : throw new DataException($"{source}: '{key}' must be between {min} and {max} (got {v}).");
    }

    private static int PositiveInt(int? value, string source, string key) =>
        value is > 0 ? value.Value : throw new DataException($"{source}: '{key}' must be a positive integer.");

    private sealed class TuningDto
    {
        public PlayerDto? Player { get; set; }

        public CameraDto? Camera { get; set; }

        public EncounterDto? Encounter { get; set; }

        public DuelDto? Duel { get; set; }

        public AnchorsDto? Anchors { get; set; }
    }

    private sealed class PlayerDto
    {
        public float? WalkSpeed { get; set; }

        public float? RunSpeed { get; set; }
    }

    private sealed class CameraDto
    {
        public ProfileDto? Overworld { get; set; }

        public DuelCameraDto? Duel { get; set; }
    }

    private sealed class ProfileDto
    {
        public float? Pitch { get; set; }

        public float? Distance { get; set; }

        public float? Fov { get; set; }
    }

    private sealed class DuelCameraDto
    {
        public float? Pitch { get; set; }

        public float? Distance { get; set; }

        public float? Fov { get; set; }

        public float? BlendTime { get; set; }

        public float? FocusHeight { get; set; }
    }

    private sealed class EncounterDto
    {
        public float? ConeRange { get; set; }

        public float? ConeAngle { get; set; }

        public float? StandDistance { get; set; }

        public float? RevealTime { get; set; }

        public float? RevealBlend { get; set; }

        public float? WalkSpeed { get; set; }

        public float? ResultTime { get; set; }

        public float? DisarmTime { get; set; }
    }

    private sealed class DuelDto
    {
        public int? StartLp { get; set; }

        public int? HandLimit { get; set; }

        public int? OpeningHand { get; set; }

        public float? CardTween { get; set; }

        public float? AiDelay { get; set; }
    }

    private sealed class AnchorsDto
    {
        public float? Forward { get; set; }

        public float? SpacingX { get; set; }

        public float? SpacingZ { get; set; }

        public float? ChestHeight { get; set; }

        public float? HandForward { get; set; }

        public float? HandSide { get; set; }

        public float? HandHeight { get; set; }

        public float? HandSpacing { get; set; }

        public float? StackStep { get; set; }

        public float? LungeFraction { get; set; }
    }
}
