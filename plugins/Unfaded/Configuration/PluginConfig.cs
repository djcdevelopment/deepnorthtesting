namespace Unfaded.Configuration;

using BepInEx.Configuration;
using UnityEngine;

public static class PluginConfig
{
    // 1 - General
    public static ConfigEntry<bool> IsModEnabled = null!;

    // 2 - Visuals
    public static ConfigEntry<bool> DisableDeathFade = null!;
    public static ConfigEntry<float> CustomDeathFadeDuration = null!;

    // 3 - Respawn
    public static ConfigEntry<float> RespawnDelay = null!;
    public static ConfigEntry<bool> EnableManualRespawn = null!;
    public static ConfigEntry<KeyCode> ManualRespawnKey = null!;
    public static ConfigEntry<bool> ShowRespawnPrompt = null!;
    public static ConfigEntry<string> RespawnPromptText = null!;

    // 4 - Spectator Camera & Orbit
    public static ConfigEntry<bool> EnableCameraOrbit = null!;
    public static ConfigEntry<float> CameraOrbitSensitivity = null!;

    // 5 - Guess 1: Kill Cam & Death Cause Recap
    public static ConfigEntry<bool> EnableDeathRecap = null!;
    public static ConfigEntry<bool> EnableKillerCam = null!;
    public static ConfigEntry<KeyCode> KillerCamKey = null!;

    // 6 - Guess 2: Cinematic Slow-Motion
    public static ConfigEntry<bool> EnableSlowMotion = null!;
    public static ConfigEntry<float> SlowMotionScale = null!;
    public static ConfigEntry<float> SlowMotionDuration = null!;

    // 7 - Guess 3: Free-Fly Drone Spectator
    public static ConfigEntry<bool> EnableFreeFly = null!;
    public static ConfigEntry<KeyCode> FreeFlyKey = null!;
    public static ConfigEntry<float> FreeFlyRadius = null!;
    public static ConfigEntry<float> FreeFlySpeed = null!;

    // 8 - Video Recording Devcommands
    public static ConfigEntry<string> RecordOutputDirectory = null!;
    public static ConfigEntry<bool> EnableRecordingHotkey = null!;
    public static ConfigEntry<KeyCode> RecordHotkey = null!;
    public static ConfigEntry<bool> EnableGameBarTrigger = null!;

    public static void BindConfig(ConfigFile config)
    {
        // 1 - General
        IsModEnabled = config.Bind(
            "1 - General",
            "Enabled",
            true,
            "Master toggle for Unfaded mod."
        );

        // 2 - Visuals
        DisableDeathFade = config.Bind(
            "2 - Visuals",
            "DisableDeathFade",
            true,
            "Completely eliminate the 9.5-second black screen fadeout on death, keeping the viewport fully visible."
        );

        CustomDeathFadeDuration = config.Bind(
            "2 - Visuals",
            "CustomDeathFadeDuration",
            0.0f,
            "Custom death fade duration in seconds. Set to 0 for no fade. Vanilla Valheim is 9.5 seconds."
        );

        // 3 - Respawn
        RespawnDelay = config.Bind(
            "3 - Respawn",
            "RespawnDelay",
            10.0f,
            new ConfigDescription(
                "Seconds to wait before automatic respawn after death. Vanilla Valheim is 10.0 seconds.",
                new AcceptableValueRange<float>(0.0f, 120.0f)
            )
        );

        EnableManualRespawn = config.Bind(
            "3 - Respawn",
            "EnableManualRespawn",
            true,
            "Allow pressing a key while dead to immediately trigger respawn at your bed/spawnpoint."
        );

        ManualRespawnKey = config.Bind(
            "3 - Respawn",
            "ManualRespawnKey",
            KeyCode.Space,
            "Key that triggers early respawn while dead."
        );

        ShowRespawnPrompt = config.Bind(
            "3 - Respawn",
            "ShowRespawnPrompt",
            true,
            "Show an on-screen HUD prompt indicating spectator controls and the manual respawn hotkey while dead."
        );

        RespawnPromptText = config.Bind(
            "3 - Respawn",
            "RespawnPromptText",
            "[{0}] Respawn • [K] Killer Cam • [F] Free Fly",
            "Status bar text displayed during death spectator mode."
        );

        // 4 - Spectator Camera & Orbit
        EnableCameraOrbit = config.Bind(
            "4 - Spectator Camera",
            "EnableCameraOrbit",
            true,
            "Allow orbiting the camera around your ragdoll using mouse look while dead."
        );

        CameraOrbitSensitivity = config.Bind(
            "4 - Spectator Camera",
            "CameraOrbitSensitivity",
            2.0f,
            new ConfigDescription(
                "Mouse look sensitivity multiplier while spectating your corpse.",
                new AcceptableValueRange<float>(0.1f, 10.0f)
            )
        );

        // 5 - Guess 1: Kill Cam & Death Cause Recap
        EnableDeathRecap = config.Bind(
            "5 - Killer & Recap",
            "EnableDeathRecap",
            true,
            "Display an on-screen Death Recap banner showing the killer's name, star rank, and lethal damage amount."
        );

        EnableKillerCam = config.Bind(
            "5 - Killer & Recap",
            "EnableKillerCam",
            true,
            "Allow toggling the camera focus to the creature that dealt the fatal blow."
        );

        KillerCamKey = config.Bind(
            "5 - Killer & Recap",
            "KillerCamKey",
            KeyCode.K,
            "Key to toggle camera focus between your ragdoll and your killer."
        );

        // 6 - Guess 2: Cinematic Slow-Motion
        EnableSlowMotion = config.Bind(
            "6 - Cinematic Slow-Mo",
            "EnableSlowMotion",
            true,
            "Briefly enter cinematic bullet-time slow motion upon receiving lethal damage so you can watch your Viking ragdoll launch in epic slow-mo."
        );

        SlowMotionScale = config.Bind(
            "6 - Cinematic Slow-Mo",
            "SlowMotionScale",
            0.35f,
            new ConfigDescription(
                "Time scale during the lethal impact (0.1 = super slow, 1.0 = normal speed).",
                new AcceptableValueRange<float>(0.05f, 1.0f)
            )
        );

        SlowMotionDuration = config.Bind(
            "6 - Cinematic Slow-Mo",
            "SlowMotionDuration",
            1.5f,
            new ConfigDescription(
                "Real-time seconds of slow motion before returning to regular game speed.",
                new AcceptableValueRange<float>(0.5f, 5.0f)
            )
        );

        // 7 - Guess 3: Free-Fly Drone Spectator
        EnableFreeFly = config.Bind(
            "7 - Free-Fly Spectator",
            "EnableFreeFly",
            true,
            "Allow untethering the camera to fly freely around the death site for tactical corpse recovery scouting."
        );

        FreeFlyKey = config.Bind(
            "7 - Free-Fly Spectator",
            "FreeFlyKey",
            KeyCode.F,
            "Key to toggle free-fly drone mode on and off while dead."
        );

        FreeFlyRadius = config.Bind(
            "7 - Free-Fly Spectator",
            "FreeFlyRadius",
            60.0f,
            new ConfigDescription(
                "Maximum flight distance in meters from your death location.",
                new AcceptableValueRange<float>(10.0f, 200.0f)
            )
        );

        FreeFlySpeed = config.Bind(
            "7 - Free-Fly Spectator",
            "FreeFlySpeed",
            15.0f,
            new ConfigDescription(
                "Movement speed in meters per second while in free-fly drone mode.",
                new AcceptableValueRange<float>(5.0f, 50.0f)
            )
        );

        // 8 - Video Recording Devcommands
        RecordOutputDirectory = config.Bind(
            "8 - Video Recording",
            "RecordOutputDirectory",
            "",
            "Optional custom directory to scan for recorded video files. If blank, auto-detects Captures and OBS directories."
        );

        EnableRecordingHotkey = config.Bind(
            "8 - Video Recording",
            "EnableRecordingHotkey",
            true,
            "Enable hotkey toggle to start and stop video recording without opening console."
        );

        RecordHotkey = config.Bind(
            "8 - Video Recording",
            "RecordHotkey",
            KeyCode.F9,
            "Hotkey to toggle video recording on/off (starts recording on first press, stops and logs summary on second press)."
        );

        EnableGameBarTrigger = config.Bind(
            "8 - Video Recording",
            "EnableGameBarTrigger",
            true,
            "Automatically dispatch Windows Game Bar capture shortcut (Win + Alt + R) on record start/stop."
        );
    }
}

