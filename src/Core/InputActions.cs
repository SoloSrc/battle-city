namespace BattleCity.Core;

/// <summary>
/// Names of the input actions declared in <c>project.godot</c> (GDD §1.2).
/// Gamepad is the primary device; keyboard is the fallback.
/// </summary>
public static class InputActions
{
    public const string MoveLeft = "move_left";
    public const string MoveRight = "move_right";
    public const string MoveUp = "move_up";
    public const string MoveDown = "move_down";
    public const string Interact = "interact";
    public const string Cancel = "cancel";
    public const string Menu = "menu";
    public const string RunToggle = "run_toggle";
}
