using System;
using BattleCity.Data;
using Godot;

namespace BattleCity.Core;

/// <summary>
/// The systems.md §10 numbers, read once from <c>data/tuning.json</c> and
/// applied by every node that used to export them. A file that fails to load
/// is reported once and the shipped defaults stand in, so a typo in the data
/// never blanks a scene.
/// </summary>
public static class Tuning
{
    private static TuningDefinition? _current;

    public static TuningDefinition Current => _current ??= Load();

    /// <summary>Forgets the loaded file so the next read parses it again (tests and hot edits).</summary>
    public static void Reload() => _current = null;

    private static TuningDefinition Load()
    {
        string path = ProjectSettings.GlobalizePath(Paths.TuningData);
        try
        {
            return new TuningLoader().LoadFile(path);
        }
        catch (Exception e) when (e is DataException or System.IO.IOException)
        {
            GD.PushError($"Tuning: {path} did not load, using the defaults: {e.Message}");
            return TuningDefinition.Default;
        }
    }
}
