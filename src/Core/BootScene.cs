using System;
using System.Collections.Generic;
using Godot;

namespace BattleCity.Core;

/// <summary>
/// Placeholder main scene for the skeleton milestone. It proves the C# assembly
/// loads and the input map is present, then starts a new game through the
/// <see cref="Game"/> autoload; the real boot flow (Boot → MainMenu →
/// AvatarCreator, systems.md §2.2) replaces it in later issues.
/// </summary>
public partial class BootScene : Control
{
    [Export]
    public Label? StatusLabel { get; set; }

    /// <summary>Start a new game as soon as the scene is up (off for diagnostics that only need the status).</summary>
    [Export]
    public bool AutoStart { get; set; } = true;

    public override void _Ready()
    {
        string engine = Engine.GetVersionInfo()["string"].AsString();
        string duelCore = Duel.Core.DuelCoreInfo.Ruleset;
        var actions = new List<string>();
        foreach (StringName action in InputMap.GetActions())
        {
            string name = action.ToString();
            if (!name.StartsWith("ui_", StringComparison.Ordinal))
            {
                actions.Add(name);
            }
        }

        string text = $"Battle City skeleton\nGodot {engine}\nDuel.Core ruleset: {duelCore}\n"
            + $"Input actions: {string.Join(", ", actions)}";
        GD.Print(text);
        if (StatusLabel is not null)
        {
            StatusLabel.Text = text;
        }

        if (AutoStart && Game.Instance is { } game)
        {
            game.LevelLoaded += OnLevelLoaded;
            CallDeferred(MethodName.StartGame);
        }
    }

    private void StartGame()
    {
        Game.Instance?.NewGame();
    }

    private void OnLevelLoaded(string scenePath, string spawnId)
    {
        if (Game.Instance is { } game)
        {
            game.LevelLoaded -= OnLevelLoaded;
        }

        QueueFree();
    }
}
