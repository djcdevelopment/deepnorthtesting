namespace Unfaded.Commands;

using System;
using HarmonyLib;
using Unfaded.Configuration;
using Unfaded.Core;
using UnityEngine;

[HarmonyPatch(typeof(Terminal), "Awake")]
public static class UnfadedCommand
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        new Terminal.ConsoleCommand(
            "unfaded",
            "Unfaded death spectator commands (status, test, delay, slowmo, orbit, killer, freefly)",
            delegate(Terminal.ConsoleEventArgs args)
            {
                if (args.Length < 2 || args[1].Equals("status", StringComparison.OrdinalIgnoreCase) || args[1].Equals("help", StringComparison.OrdinalIgnoreCase))
                {
                    PrintStatus(args.Context);
                    return;
                }

                string sub = args[1].ToLower();

                switch (sub)
                {
                    case "test":
                        DeathStateManager.IsTestSimActive = true;
                        DeathStateManager.TestSimEndTime = Time.time + 6.0f;
                        args.Context?.AddString("<color=#00FFAA>[Unfaded]</color> <b>Spectator Test Mode Active (6s)</b>! Orbit with mouse, press [K] Killer, [F] Free Fly.");
                        break;

                    case "delay":
                        if (args.Length >= 3 && float.TryParse(args[2], out float newDelay))
                        {
                            PluginConfig.RespawnDelay.Value = Mathf.Clamp(newDelay, 0f, 120f);
                            args.Context?.AddString($"<color=#00FFAA>[Unfaded]</color> Respawn delay set to: <b>{PluginConfig.RespawnDelay.Value:F1}s</b>");
                        }
                        else
                        {
                            args.Context?.AddString("<color=#FF6B6B>[Unfaded]</color> Usage: unfaded delay <seconds> (e.g. unfaded delay 5)");
                        }
                        break;

                    case "slowmo":
                        PluginConfig.EnableSlowMotion.Value = !PluginConfig.EnableSlowMotion.Value;
                        args.Context?.AddString($"<color=#00FFAA>[Unfaded]</color> Cinematic Slow-Mo on lethal hit: <b>{(PluginConfig.EnableSlowMotion.Value ? "<color=#00FFAA>ENABLED</color>" : "<color=#FF6B6B>DISABLED</color>")}</b>");
                        break;

                    case "orbit":
                        PluginConfig.EnableCameraOrbit.Value = !PluginConfig.EnableCameraOrbit.Value;
                        args.Context?.AddString($"<color=#00FFAA>[Unfaded]</color> 360° Corpse Orbit: <b>{(PluginConfig.EnableCameraOrbit.Value ? "<color=#00FFAA>ENABLED</color>" : "<color=#FF6B6B>DISABLED</color>")}</b>");
                        break;

                    case "killer":
                        PluginConfig.EnableKillerCam.Value = !PluginConfig.EnableKillerCam.Value;
                        args.Context?.AddString($"<color=#00FFAA>[Unfaded]</color> Killer Focus Cam: <b>{(PluginConfig.EnableKillerCam.Value ? "<color=#00FFAA>ENABLED</color>" : "<color=#FF6B6B>DISABLED</color>")}</b>");
                        break;

                    case "freefly":
                        PluginConfig.EnableFreeFly.Value = !PluginConfig.EnableFreeFly.Value;
                        args.Context?.AddString($"<color=#00FFAA>[Unfaded]</color> Free-Fly Drone Spectator: <b>{(PluginConfig.EnableFreeFly.Value ? "<color=#00FFAA>ENABLED</color>" : "<color=#FF6B6B>DISABLED</color>")}</b>");
                        break;

                    default:
                        args.Context?.AddString("<color=#FF6B6B>[Unfaded]</color> Unknown subcommand. Options: status, test, delay <sec>, slowmo, orbit, killer, freefly");
                        break;
                }
            }
        );
    }

    private static void PrintStatus(Terminal context)
    {
        if (context == null) return;

        context.AddString("==================================================");
        context.AddString("   <b><color=#00FFAA>Unfaded :: Death Spectator & Blackout Bypass</color></b>");
        context.AddString("==================================================");
        context.AddString($" • Blackout Override : <b>{(PluginConfig.DisableDeathFade.Value ? "<color=#00FFAA>SUPPRESSED (Clear View)</color>" : "<color=#FF6B6B>ACTIVE (Vanilla)</color>")}</b>");
        context.AddString($" • Respawn Delay     : <b>{PluginConfig.RespawnDelay.Value:F1}s</b> (Instant on [{PluginConfig.ManualRespawnKey.Value}])");
        context.AddString($" • 360° Corpse Orbit : <b>{(PluginConfig.EnableCameraOrbit.Value ? "ENABLED" : "DISABLED")}</b> (Sensitivity: {PluginConfig.CameraOrbitSensitivity.Value:F1}x)");
        context.AddString($" • Killer Cam [K]    : <b>{(PluginConfig.EnableKillerCam.Value ? "ENABLED" : "DISABLED")}</b>");
        context.AddString($" • Free-Fly Drone [F]: <b>{(PluginConfig.EnableFreeFly.Value ? "ENABLED" : "DISABLED")}</b> (Radius: {PluginConfig.FreeFlyRadius.Value:F0}m)");
        context.AddString($" • Bullet-Time SlowMo: <b>{(PluginConfig.EnableSlowMotion.Value ? $"ENABLED ({PluginConfig.SlowMotionScale.Value:F2}x for {PluginConfig.SlowMotionDuration.Value:F1}s)" : "DISABLED")}</b>");
        context.AddString($" • Death Cause Banner: <b>{(PluginConfig.EnableDeathRecap.Value ? "ENABLED" : "DISABLED")}</b>");
        context.AddString(" Commands: unfaded test | unfaded delay <sec> | unfaded slowmo | unfaded killer | unfaded freefly");
        context.AddString("==================================================");
    }
}
