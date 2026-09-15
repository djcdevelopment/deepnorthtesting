namespace Unswayed.Configuration;

using BepInEx.Configuration;
using UnityEngine;

public enum SuppressionMode
{
    Always,
    RunningOnly,
    DungeonsOnly
}

public static class PluginConfig
{
    // General
    public static ConfigEntry<bool> IsModEnabled = null!;
    public static ConfigEntry<KeyCode> ToggleHotkey = null!;

    // Head-Bob Suppression (Vertical Motion Damping)
    public static ConfigEntry<bool> EnableBobSuppression = null!;
    public static ConfigEntry<SuppressionMode> Mode = null!;
    public static ConfigEntry<float> StabilizedEyeHeight = null!;
    public static ConfigEntry<float> VerticalDamping = null!;

    // Dungeon & Crypt Ergonomics
    public static ConfigEntry<bool> EnableDungeonErgonomics = null!;
    public static ConfigEntry<float> DungeonProximityThreshold = null!;
    public static ConfigEntry<bool> EnableMinDistanceClamp = null!;
    public static ConfigEntry<float> MinCameraDistance = null!;
    public static ConfigEntry<bool> EnableCryptShoulderLift = null!;
    public static ConfigEntry<float> ShoulderLiftAmount = null!;
    public static ConfigEntry<bool> EnableDungeonFovBoost = null!;
    public static ConfigEntry<float> DungeonFovBoost = null!;

    // Camera Shake & Motion Sickness Toggles
    public static ConfigEntry<float> CameraShakeScale = null!;
    public static ConfigEntry<bool> DisableShipTilt = null!;

    public static void BindConfig(ConfigFile config)
    {
        // General
        IsModEnabled = config.Bind(
            "1 - General",
            "Enabled",
            true,
            "Master toggle for Unswayed camera stabilization."
        );

        ToggleHotkey = config.Bind(
            "1 - General",
            "ToggleHotkey",
            KeyCode.F7,
            "In-game hotkey to quickly toggle camera stabilization on/off."
        );

        // Head-Bob Suppression
        EnableBobSuppression = config.Bind(
            "2 - Head-Bob Suppression",
            "EnableBobSuppression",
            true,
            "Decouples the camera base offset from the animated head bone, completely eliminating the 1.0 vertical running bounce."
        );

        Mode = config.Bind(
            "2 - Head-Bob Suppression",
            "SuppressionMode",
            SuppressionMode.Always,
            "When to suppress head-bobbing: Always, RunningOnly (only when sprinting/running), or DungeonsOnly."
        );

        StabilizedEyeHeight = config.Bind(
            "2 - Head-Bob Suppression",
            "StabilizedEyeHeight",
            1.65f,
            new ConfigDescription(
                "Stable vertical eye pivot height relative to player root transform.",
                new AcceptableValueRange<float>(1.2f, 2.2f)
            )
        );

        VerticalDamping = config.Bind(
            "2 - Head-Bob Suppression",
            "VerticalDamping",
            1.0f,
            new ConfigDescription(
                "Amount of vertical bobbing suppression (1.0 = completely stable horizon, 0.5 = 50% reduced bounce, 0.0 = vanilla bounce).",
                new AcceptableValueRange<float>(0.0f, 1.0f)
            )
        );

        // Dungeon & Crypt Ergonomics
        EnableDungeonErgonomics = config.Bind(
            "3 - Dungeon & Crypt Ergonomics",
            "EnableDungeonErgonomics",
            true,
            "Enables adaptive camera behavior when compressed in tight corridors, burial crypts, and sunken crypts."
        );

        DungeonProximityThreshold = config.Bind(
            "3 - Dungeon & Crypt Ergonomics",
            "DungeonProximityThreshold",
            1.8f,
            new ConfigDescription(
                "Camera distance threshold (meters) below which dungeon/tight-space adaptations activate.",
                new AcceptableValueRange<float>(1.0f, 3.0f)
            )
        );

        EnableMinDistanceClamp = config.Bind(
            "3 - Dungeon & Crypt Ergonomics",
            "EnableMinDistanceClamp",
            true,
            "Prevents the camera collision raycast from crushing point-blank into the player's skull."
        );

        MinCameraDistance = config.Bind(
            "3 - Dungeon & Crypt Ergonomics",
            "MinCameraDistance",
            0.9f,
            new ConfigDescription(
                "Minimum camera distance in tight spaces to prevent the Viking mesh from filling the entire screen.",
                new AcceptableValueRange<float>(0.3f, 2.0f)
            )
        );

        EnableCryptShoulderLift = config.Bind(
            "3 - Dungeon & Crypt Ergonomics",
            "EnableCryptShoulderLift",
            true,
            "Gently elevates the camera over the Viking's shoulders when camera distance drops in tight crypts, preserving forward visibility."
        );

        ShoulderLiftAmount = config.Bind(
            "3 - Dungeon & Crypt Ergonomics",
            "ShoulderLiftAmount",
            0.35f,
            new ConfigDescription(
                "Maximum vertical shoulder lift in tight spaces.",
                new AcceptableValueRange<float>(0.1f, 0.8f)
            )
        );

        EnableDungeonFovBoost = config.Bind(
            "3 - Dungeon & Crypt Ergonomics",
            "EnableDungeonFovBoost",
            true,
            "Dynamically expands FOV when navigating tight crypts to widen peripheral vision and reduce nausea."
        );

        DungeonFovBoost = config.Bind(
            "3 - Dungeon & Crypt Ergonomics",
            "DungeonFovBoost",
            15.0f,
            new ConfigDescription(
                "Additional FOV degrees added in tight crypt spaces (e.g. 65Â° -> 80Â°).",
                new AcceptableValueRange<float>(0.0f, 30.0f)
            )
        );

        // Motion Sickness Master Toggles
        CameraShakeScale = config.Bind(
            "4 - Motion Sickness Toggles",
            "CameraShakeScale",
            0.0f,
            new ConfigDescription(
                "Global camera shake multiplier (0.0 = completely off, 0.2 = subtle rumble, 1.0 = vanilla full shake).",
                new AcceptableValueRange<float>(0.0f, 1.0f)
            )
        );

        DisableShipTilt = config.Bind(
            "4 - Motion Sickness Toggles",
            "DisableShipTilt",
            false,
            "Suppresses camera roll/tilt when sailing on boats (prevents seasickness)."
        );
    }
}
