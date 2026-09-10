namespace BattleCity.Core;

/// <summary>
/// Every <c>res://</c> path used from code is written here once (architecture.md §3).
/// </summary>
public static class Paths
{
    public const string BootScene = "res://scenes/Boot.tscn";
    public const string DataRoot = "res://data";
    public const string CardsData = "res://data/cards";
    public const string DecksData = "res://data/decks";
    public const string DuelistsData = "res://data/duelists.json";
    public const string ShopData = "res://data/shop.json";
    public const string TuningData = "res://data/tuning.json";
    public const string AvatarData = "res://data/avatar.json";
    public const string DiskMountData = "res://data/rig/disk_mount.json";
    public const string AnimationEventsData = "res://data/rig/animation_events.json";
    public const string CharacterScene = "res://scenes/characters/Character.tscn";
    public const string PlayerScene = "res://scenes/characters/Player.tscn";
    public const string DuelDiskScene = "res://scenes/characters/DuelDisk.tscn";
    public const string CameraRigScene = "res://scenes/world/CameraRig.tscn";
    public const string CameraBoundsScene = "res://scenes/world/CameraBounds.tscn";

    // Pipeline smoke-test deliverables (asset-list.md §0). Owned by gpt-astra.
    public const string SmokeTestScene = "res://tests/scenes/SmokeTest.tscn";
    public const string CharacterTestScene = "res://tests/scenes/CharacterTest.tscn";
    public const string PlayerTestScene = "res://tests/scenes/PlayerTest.tscn";
    public const string CameraFramingScene = "res://tests/scenes/CameraFraming.tscn";
    public const string SmokeCube = "res://assets/kit/kit_test_cube_1m.glb";
    public const string SmokeCharacterBody = "res://assets/characters/body/char_a_body.glb";
    public const string CharacterAnims = "res://assets/characters/anims/character_anims.glb";
    public const string DuelDiskModel = "res://assets/props/prop_duel_disk.glb";
}
