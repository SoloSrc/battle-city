using System;

namespace BattleCity.Diagnostics;

/// <summary>
/// Godot .NET disposes the managed wrappers it still tracks only after the
/// engine has checked the ObjectDB and the rendering server for leaks, so a
/// wrapper of a Resource that the GC has not collected yet by then (a texture
/// read while applying materials, for example) is reported as a leaked RID and
/// image at exit. The acceptance scenes flush the managed heap when they finish
/// so their exit is clean regardless of how the GC paced the run.
/// </summary>
public static class ManagedWrappers
{
    public static void Flush()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
