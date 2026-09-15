namespace Unswayed.Commands;

using HarmonyLib;
using Unswayed.Configuration;
using Unswayed.Core;
using UnityEngine;

[HarmonyPatch(typeof(Terminal), "InitTerminal")]
public static class UnswayedCommand
{
    [HarmonyPostfix]
    public static void AddTerminalCommands()
    {
        new Terminal.ConsoleCommand("unswayed", "Manage Unswayed camera stabilization (toggle, status, height, damping)", (Terminal.ConsoleEventArgs args) =>
        {
            if (args.Length < 2)
            {
                PrintStatus(args.Context);
                return;
            }

            string subCommand = args[1].ToLowerInvariant();
            switch (subCommand)
            {
                case "toggle":
                    StabilizerManager.ToggleStabilization(out string toggleMsg);
                    args.Context.AddString(toggleMsg);
                    break;

                case "status":
                    PrintStatus(args.Context);
                    break;

                case "height":
                    if (args.Length >= 3 && float.TryParse(args[2], out float newHeight))
                    {
                        PluginConfig.StabilizedEyeHeight.Value = Mathf.Clamp(newHeight, 1.2f, 2.2f);
                        args.Context.AddString($"[Unswayed] Stabilized eye height set to: {PluginConfig.StabilizedEyeHeight.Value:F2}m");
                    }
                    else
                    {
                        args.Context.AddString($"[Unswayed] Current height: {PluginConfig.StabilizedEyeHeight.Value:F2}m. Usage: unswayed height <1.2 - 2.2>");
                    }
                    break;

                case "damping":
                    if (args.Length >= 3 && float.TryParse(args[2], out float newDamping))
                    {
                        PluginConfig.VerticalDamping.Value = Mathf.Clamp01(newDamping);
                        args.Context.AddString($"[Unswayed] Vertical damping set to: {PluginConfig.VerticalDamping.Value:F2} (1.0 = stable, 0.0 = vanilla)");
                    }
                    else
                    {
                        args.Context.AddString($"[Unswayed] Current damping: {PluginConfig.VerticalDamping.Value:F2}. Usage: unswayed damping <0.0 - 1.0>");
                    }
                    break;

                case "shake":
                    if (args.Length >= 3 && float.TryParse(args[2], out float newShake))
                    {
                        PluginConfig.CameraShakeScale.Value = Mathf.Clamp01(newShake);
                        args.Context.AddString($"[Unswayed] Camera shake scale set to: {PluginConfig.CameraShakeScale.Value:F2}");
                    }
                    else
                    {
                        args.Context.AddString($"[Unswayed] Current shake scale: {PluginConfig.CameraShakeScale.Value:F2}. Usage: unswayed shake <0.0 - 1.0>");
                    }
                    break;

                default:
                    args.Context.AddString("[Unswayed] Available commands: toggle, status, height <m>, damping <0-1>, shake <0-1>");
                    break;
            }
        });
    }

    private static void PrintStatus(Terminal terminal)
    {
        terminal.AddString("=== [Unswayed Camera Stabilizer v1.0.0] ===");
        terminal.AddString($"  Enabled: {PluginConfig.IsModEnabled.Value} (Hotkey: [{PluginConfig.ToggleHotkey.Value}])");
        terminal.AddString($"  Mode: {PluginConfig.Mode.Value} | Damping: {PluginConfig.VerticalDamping.Value:F2} | Eye Height: {PluginConfig.StabilizedEyeHeight.Value:F2}m");
        terminal.AddString($"  Dungeon Ergonomics: {PluginConfig.EnableDungeonErgonomics.Value} (MinDist: {PluginConfig.MinCameraDistance.Value:F2}m, ShoulderLift: +{PluginConfig.ShoulderLiftAmount.Value:F2}m, FOV Boost: +{PluginConfig.DungeonFovBoost.Value:F0}Â°)");
        terminal.AddString($"  Camera Shake Scale: {PluginConfig.CameraShakeScale.Value:F2} | Disable Ship Tilt: {PluginConfig.DisableShipTilt.Value}");
        terminal.AddString($"  Live Camera Distance: {StabilizerManager.LastCameraDistance:F2}m (Tight Space: {StabilizerManager.IsTightSpace})");
    }
}
