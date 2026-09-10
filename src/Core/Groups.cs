namespace BattleCity.Core;

/// <summary>
/// Node group names used by code to discover scene contents (architecture.md §4.5).
/// Levels place marker scenes; behaviour is looked up by group and id at runtime.
/// </summary>
public static class Groups
{
    public const string Player = "player";
    public const string Npc = "npc";
    public const string Duelist = "duelist";
    public const string Interactable = "interactable";
    public const string EncounterSite = "encounter_site";
    public const string CameraBounds = "camera_bounds";
    public const string Spawn = "spawn";
    public const string Gate = "gate";
    public const string AmbientZone = "ambient_zone";
}
