namespace BattleCity.Characters;

/// <summary>
/// Named animation events the engine listens for (systems.md §3.2). They are
/// injected as call-method tracks from <c>data/rig/animation_events.json</c>
/// because glTF cannot carry Godot method tracks; every key calls
/// <see cref="EventMethod"/> on the animation root with the event name.
/// </summary>
public static class AnimationEvents
{
    public const string EventMethod = "OnAnimationEvent";

    public const string FootstepLeft = "footstep_l";
    public const string FootstepRight = "footstep_r";
    public const string DiskDeploy = "disk_deploy";
    public const string CardDraw = "card_draw";
    public const string CardRelease = "card_release";
    public const string CardToGrave = "card_to_grave";
    public const string Hit = "hit";

    public static readonly string[] Known =
    {
        FootstepLeft, FootstepRight, DiskDeploy, CardDraw, CardRelease, CardToGrave, Hit,
    };
}
