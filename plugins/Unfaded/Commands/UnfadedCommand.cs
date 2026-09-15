namespace Unfaded.Commands;

using System;
using System.IO;
using System.Linq;
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
        // 1. Standalone 'record' command
        Terminal.ConsoleEvent recordHandler = delegate(Terminal.ConsoleEventArgs args)
        {
            HandleRecordCommand(args);
        };

        new Terminal.ConsoleCommand(
            "record",
            "Video recording devcommands (start, stop, status, dir)",
            recordHandler
        );

        // 2. Shorthand 'rec' alias
        new Terminal.ConsoleCommand(
            "rec",
            "Shorthand alias for record (start, stop, status, dir)",
            recordHandler
        );

        // 3. 'unfaded' command (also handles 'unfaded record ...')
        new Terminal.ConsoleCommand(
            "unfaded",
            "Unfaded death spectator & recording commands (status, record, test, delay, slowmo, orbit, killer, freefly)",
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
                    case "record":
                    case "rec":
                        HandleRecordSubcommand(args);
                        break;

                    case "smite":
                    case "tree":
                    case "die":
                        ExecuteSmite(args, sub == "tree");
                        break;

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
                        args.Context?.AddString("<color=#FF6B6B>[Unfaded]</color> Unknown subcommand. Options: status, record, test, delay <sec>, slowmo, orbit, killer, freefly");
                        break;
                }
            }
        );
    }

    public static void HandleRecordCommand(Terminal.ConsoleEventArgs args)
    {
        if (args.Length < 2 || args[1].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            string status = RecordingManager.GetStatus();
            args.Context?.AddString(status);
            return;
        }

        string action = args[1].ToLower();
        switch (action)
        {
            case "start":
            case "on":
            case "begin":
                RecordingManager.StartRecording(out string startMsg);
                args.Context?.AddString(startMsg);
                break;

            case "stop":
            case "off":
            case "end":
                RecordingManager.StopRecording(out string stopMsg);
                args.Context?.AddString(stopMsg);
                break;

            case "toggle":
                if (RecordingManager.IsRecording)
                {
                    RecordingManager.StopRecording(out string tStopMsg);
                    args.Context?.AddString(tStopMsg);
                }
                else
                {
                    RecordingManager.StartRecording(out string tStartMsg);
                    args.Context?.AddString(tStartMsg);
                }
                break;

            case "dir":
            case "directory":
            case "path":
                if (args.Length >= 3)
                {
                    string newPath = args.FullLine.Substring(args.FullLine.IndexOf(args[2], StringComparison.Ordinal)).Trim();
                    if (Directory.Exists(newPath))
                    {
                        PluginConfig.RecordOutputDirectory.Value = newPath;
                        args.Context?.AddString($"<color=#00FFAA>[Record]</color> Capture directory updated: <b>{newPath}</b>");
                    }
                    else
                    {
                        args.Context?.AddString($"<color=#FF6B6B>[Record]</color> Directory not found: {newPath}");
                    }
                }
                else
                {
                    args.Context?.AddString($"<color=#00FFAA>[Record]</color> Active capture directory: <b>{RecordingManager.GetActiveOutputDirectory()}</b>\n" +
                                            " Candidate folders searched:\n" +
                                            string.Join("\n", RecordingManager.GetCandidateDirectories().Select(d => $"  • {d}")));
                }
                break;

            case "smite":
            case "tree":
                ExecuteSmite(args, action == "tree");
                break;

            default:
                args.Context?.AddString("<color=#FF6B6B>[Record]</color> Usage:\n" +
                                        " • <color=#00FFAA>record start</color>  - Start recording (echos start time, sends Win+Alt+R)\n" +
                                        " • <color=#00FFAA>record stop</color>   - Stop recording (echos duration, filename, path, size)\n" +
                                        " • <color=#00FFAA>record status</color> - View recording status\n" +
                                        " • <color=#00FFAA>record dir [path]</color> - Show or set output directory");
                break;
        }
    }

    private static void HandleRecordSubcommand(Terminal.ConsoleEventArgs args)
    {
        if (args.Length < 3)
        {
            args.Context?.AddString(RecordingManager.GetStatus());
            return;
        }

        string action = args[2].ToLower();
        switch (action)
        {
            case "start":
            case "on":
            case "begin":
                RecordingManager.StartRecording(out string startMsg);
                args.Context?.AddString(startMsg);
                break;

            case "stop":
            case "off":
            case "end":
                RecordingManager.StopRecording(out string stopMsg);
                args.Context?.AddString(stopMsg);
                break;

            case "status":
                args.Context?.AddString(RecordingManager.GetStatus());
                break;

            case "dir":
            case "directory":
            case "path":
                if (args.Length >= 4)
                {
                    string newPath = args.FullLine.Substring(args.FullLine.IndexOf(args[3], StringComparison.Ordinal)).Trim();
                    if (Directory.Exists(newPath))
                    {
                        PluginConfig.RecordOutputDirectory.Value = newPath;
                        args.Context?.AddString($"<color=#00FFAA>[Record]</color> Capture directory updated: <b>{newPath}</b>");
                    }
                    else
                    {
                        args.Context?.AddString($"<color=#FF6B6B>[Record]</color> Directory not found: {newPath}");
                    }
                }
                else
                {
                    args.Context?.AddString($"<color=#00FFAA>[Record]</color> Active capture directory: <b>{RecordingManager.GetActiveOutputDirectory()}</b>");
                }
                break;

            default:
                args.Context?.AddString("<color=#FF6B6B>[Record]</color> Usage: unfaded record start | stop | status | dir [path]");
                break;
        }
    }

    private static void ExecuteSmite(Terminal.ConsoleEventArgs args, bool asTree)
    {
        Player localPlayer = Player.m_localPlayer;
        if (localPlayer == null)
        {
            args.Context?.AddString("<color=#FF6B6B>[Unfaded]</color> Local player not found. Load into a world first.");
            return;
        }

        HitData hit = new HitData
        {
            m_hitType = asTree ? HitData.HitType.Tree : HitData.HitType.Self,
            m_pushForce = 120f,
            m_dir = (localPlayer.transform.forward * -1f + Vector3.up * 0.45f).normalized,
            m_point = localPlayer.transform.position
        };
        hit.m_damage.m_damage = 9999f;
        localPlayer.Damage(hit);

        string reason = asTree ? "Falling Tree / Timber" : "Cinematic Smite";
        args.Context?.AddString($"<color=#00FFAA>[Unfaded]</color> ⚡ Lethal hit dealt ({reason})! Ragdoll launched, spectator active.");
    }

    private static void PrintStatus(Terminal context)
    {
        if (context == null) return;

        context.AddString("==================================================");
        context.AddString("   <b><color=#00FFAA>Unfaded :: Death Spectator & Recording</color></b>");
        context.AddString("==================================================");
        context.AddString($" • Blackout Override : <b>{(PluginConfig.DisableDeathFade.Value ? "<color=#00FFAA>SUPPRESSED (Clear View)</color>" : "<color=#FF6B6B>ACTIVE (Vanilla)</color>")}</b>");
        context.AddString($" • Respawn Delay     : <b>{PluginConfig.RespawnDelay.Value:F1}s</b> (Instant on [{PluginConfig.ManualRespawnKey.Value}])");
        context.AddString($" • 360° Corpse Orbit : <b>{(PluginConfig.EnableCameraOrbit.Value ? "ENABLED" : "DISABLED")}</b> (Sensitivity: {PluginConfig.CameraOrbitSensitivity.Value:F1}x)");
        context.AddString($" • Killer Cam [K]    : <b>{(PluginConfig.EnableKillerCam.Value ? "ENABLED" : "DISABLED")}</b>");
        context.AddString($" • Free-Fly Drone [F]: <b>{(PluginConfig.EnableFreeFly.Value ? "ENABLED" : "DISABLED")}</b> (Radius: {PluginConfig.FreeFlyRadius.Value:F0}m)");
        context.AddString($" • Bullet-Time SlowMo: <b>{(PluginConfig.EnableSlowMotion.Value ? $"ENABLED ({PluginConfig.SlowMotionScale.Value:F2}x for {PluginConfig.SlowMotionDuration.Value:F1}s)" : "DISABLED")}</b>");
        context.AddString($" • Video Recording   : <b>{(RecordingManager.IsRecording ? "<color=#FF4444>ACTIVE</color>" : "<color=#00FFAA>IDLE</color>")}</b> (Hotkey: [{PluginConfig.RecordHotkey?.Value}])");
        context.AddString($" • Capture Directory : <b>{RecordingManager.GetActiveOutputDirectory()}</b>");
        context.AddString(" Recording Commands  : record start | record stop | record status | record dir");
        context.AddString(" Spectator Commands  : unfaded test | unfaded delay <sec> | unfaded slowmo | unfaded killer");
        context.AddString("==================================================");
    }
}
