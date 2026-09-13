namespace Unfaded.Configuration;

using BepInEx.Configuration;
using UnityEngine;

public static class PluginConfig
{
    public static ConfigEntry<bool> IsModEnabled = null!;
    public static ConfigEntry<bool> DisableDeathFade = null!;
    public static ConfigEntry<float> CustomDeathFadeDuration = null!;

    public static ConfigEntry<float> RespawnDelay = null!;
    public static ConfigEntry<bool> EnableManualRespawn = null!;
    public static ConfigEntry<KeyCode> ManualRespawnKey = null!;

    public static ConfigEntry<bool> EnableCameraOrbit = null!;
    public static ConfigEntry<float> CameraOrbitSensitivity = null!;

    public static ConfigEntry<bool> ShowRespawnPrompt = null!;
    public static ConfigEntry<string> RespawnPromptText = null!;

    public static void BindConfig(ConfigFile config)
    {
        IsModEnabled = config.Bind(
            "1 - General",
            "Enabled",
            true,
            "Master toggle for Unfaded mod."
        );

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
            "Show an on-screen HUD prompt indicating the manual respawn hotkey while dead."
        );

        RespawnPromptText = config.Bind(
            "3 - Respawn",
            "RespawnPromptText",
            "Press [{0}] to Respawn",
            "Message format string for the respawn prompt. {0} is replaced by the key name."
        );

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
    }
}
