namespace BattleCity.Data;

/// <summary>Player locomotion (systems.md §10 <c>player.*</c>).</summary>
public sealed record PlayerTuning(float WalkSpeed, float RunSpeed);

/// <summary>One camera profile: pitch and fov in degrees, distance in metres.</summary>
public sealed record CameraProfileTuning(float Pitch, float Distance, float Fov);

/// <summary>The duel camera (systems.md §4.2): profile plus the blend into it and the focus height above the field midpoint.</summary>
public sealed record DuelCameraTuning(float Pitch, float Distance, float Fov, float BlendTime, float FocusHeight);

/// <summary><c>camera.*</c>: the overworld profile and the duel profile.</summary>
public sealed record CameraTuning(CameraProfileTuning Overworld, DuelCameraTuning Duel);

/// <summary><c>encounter.*</c>: the detection cone, the stand distance and the beat timings (systems.md §4.3).</summary>
public sealed record EncounterTuning(float ConeRange, float ConeAngle, float StandDistance, float RevealTime, float RevealBlend, float WalkSpeed, float ResultTime, float DisarmTime);

/// <summary><c>duel.*</c>: the match rules handed to <c>DuelOptions</c>, the card move tween and the AI's think delay.</summary>
public sealed record DuelTuning(int StartLp, int HandLimit, int OpeningHand, float CardTween, float AiDelay);

/// <summary><c>anchors.*</c>: where the holographic cards sit around each duelist (systems.md §6.1), in metres.</summary>
public sealed record AnchorTuning(float Forward, float SpacingX, float SpacingZ, float ChestHeight, float HandForward, float HandSide, float HandHeight, float HandSpacing, float StackStep, float LungeFraction);

/// <summary><c>data/tuning.json</c>: the single source for the systems.md §10 numbers.</summary>
public sealed record TuningDefinition(PlayerTuning Player, CameraTuning Camera, EncounterTuning Encounter, DuelTuning Duel, AnchorTuning Anchors)
{
    /// <summary>The shipped values, used when the file cannot be read so a broken file degrades to the defaults after an error.</summary>
    public static TuningDefinition Default { get; } = new(
        new PlayerTuning(2.2f, 4.5f),
        new CameraTuning(new CameraProfileTuning(57.0f, 12.0f, 35.0f), new DuelCameraTuning(40.0f, 9.0f, 40.0f, 1.2f, 0.8f)),
        new EncounterTuning(8.0f, 60.0f, 7.0f, 1.4f, 0.6f, 2.2f, 2.0f, 3.0f),
        new DuelTuning(8000, 6, 5, 0.15f, 0.7f),
        new AnchorTuning(1.8f, 0.92f, 1.2f, 1.2f, 0.25f, 0.62f, 0.95f, 0.11f, 0.0015f, 0.35f));
}
