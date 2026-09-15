namespace Unfaded.Core;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Unfaded.Configuration;
using UnityEngine;

public static class RecordingManager
{
    public static bool IsRecording { get; private set; }
    public static DateTime RecordingStartTime { get; private set; }
    public static DateTime RecordingStopTime { get; private set; }
    public static string LastRecordedFile { get; private set; } = string.Empty;
    public static string LastRecordedDirectory { get; private set; } = string.Empty;
    public static double LastDurationSeconds { get; private set; }
    public static long LastFileSizeBytes { get; private set; }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const byte VK_LWIN = 0x5B;
    private const byte VK_LMENU = 0x12; // Alt
    private const byte VK_R = 0x52;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static string GetActiveOutputDirectory()
    {
        string? customDir = PluginConfig.RecordOutputDirectory?.Value;
        if (!string.IsNullOrWhiteSpace(customDir) && Directory.Exists(customDir))
        {
            return customDir!;
        }

        string captures = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Captures");
        if (Directory.Exists(captures))
        {
            return captures;
        }

        string myVideos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        if (Directory.Exists(myVideos))
        {
            return myVideos;
        }

        if (Directory.Exists(@"E:\BF6-Highlights\raw"))
        {
            return @"E:\BF6-Highlights\raw";
        }

        return Directory.GetCurrentDirectory();
    }

    public static List<string> GetCandidateDirectories()
    {
        var dirs = new List<string>();
        string? customDir = PluginConfig.RecordOutputDirectory?.Value;
        if (!string.IsNullOrWhiteSpace(customDir) && Directory.Exists(customDir))
        {
            dirs.Add(customDir!);
        }

        string captures = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Captures");
        if (Directory.Exists(captures) && !dirs.Contains(captures)) dirs.Add(captures);

        string myVideos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        if (Directory.Exists(myVideos) && !dirs.Contains(myVideos)) dirs.Add(myVideos);

        if (Directory.Exists(@"E:\BF6-Highlights\raw") && !dirs.Contains(@"E:\BF6-Highlights\raw")) dirs.Add(@"E:\BF6-Highlights\raw");

        return dirs;
    }

    public static void TriggerHardwareCaptureShortcut()
    {
        if (PluginConfig.EnableGameBarTrigger?.Value != true) return;

        try
        {
            keybd_event(VK_LWIN, 0, 0, UIntPtr.Zero);
            keybd_event(VK_LMENU, 0, 0, UIntPtr.Zero);
            keybd_event(VK_R, 0, 0, UIntPtr.Zero);
            keybd_event(VK_R, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_LMENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
        catch (Exception ex)
        {
            ZLog.LogWarning($"[Unfaded.RecordingManager] Failed to dispatch capture shortcut: {ex.Message}");
        }
    }

    public static bool StartRecording(out string message)
    {
        if (IsRecording)
        {
            double elapsed = (DateTime.Now - RecordingStartTime).TotalSeconds;
            message = $"<color=#FFAA00>[Record]</color> Already recording since <b>{RecordingStartTime:HH:mm:ss}</b> ({elapsed:F0}s elapsed).\nType <color=#00FFAA>'record stop'</color> to complete.";
            return false;
        }

        RecordingStartTime = DateTime.Now;
        IsRecording = true;

        TriggerHardwareCaptureShortcut();

        string outDir = GetActiveOutputDirectory();
        ShowHudMessage($"<color=#FF4444>🔴 REC</color> Started at {RecordingStartTime:HH:mm:ss}");

        message = $"<color=#00FFAA>[Record]</color> <b>RECORDING STARTED</b> at <b>{RecordingStartTime:HH:mm:ss}</b>\n" +
                  $" • Target Directory : <b>{outDir}</b>\n" +
                  $" • Hotkey Dispatched : <b>[Win + Alt + R]</b> (Windows Game Bar / OBS)\n" +
                  $" • Finish Command   : Type <color=#00FFAA>'record stop'</color> (or press {PluginConfig.RecordHotkey?.Value})";
        return true;
    }

    public static bool StopRecording(out string message)
    {
        if (!IsRecording)
        {
            message = "<color=#FFAA00>[Record]</color> No active recording in progress. Type <color=#00FFAA>'record start'</color> to begin.";
            return false;
        }

        RecordingStopTime = DateTime.Now;
        IsRecording = false;

        TriggerHardwareCaptureShortcut();

        double elapsedSeconds = (RecordingStopTime - RecordingStartTime).TotalSeconds;
        LastDurationSeconds = elapsedSeconds;

        FileInfo? newestFile = FindNewestVideoFile(RecordingStartTime.AddSeconds(-3));

        if (newestFile != null)
        {
            LastRecordedFile = newestFile.FullName;
            LastRecordedDirectory = newestFile.DirectoryName ?? string.Empty;
            LastFileSizeBytes = newestFile.Length;

            double sizeMB = LastFileSizeBytes / (1024.0 * 1024.0);
            string durStr = FormatDuration(elapsedSeconds);

            ShowHudMessage($"<color=#00FFAA>⏹ REC SAVED</color> [{durStr}] ({sizeMB:F1} MB)");

            message = "==================================================\n" +
                      "   <b><color=#00FFAA>Unfaded :: Recording Completed & Saved</color></b>\n" +
                      "==================================================\n" +
                      $" • Start Time : <b>{RecordingStartTime:HH:mm:ss}</b>\n" +
                      $" • Stop Time  : <b>{RecordingStopTime:HH:mm:ss}</b>\n" +
                      $" • Duration   : <b>{durStr}</b> ({elapsedSeconds:F1}s)\n" +
                      $" • File Name  : <b><color=#00FFAA>{newestFile.Name}</color></b>\n" +
                      $" • File Size  : <b>{sizeMB:F2} MB</b>\n" +
                      $" • Directory  : <b>{LastRecordedDirectory}</b>\n" +
                      "==================================================";
            return true;
        }
        else
        {
            string outDir = GetActiveOutputDirectory();
            string durStr = FormatDuration(elapsedSeconds);
            ShowHudMessage($"<color=#00FFAA>⏹ REC STOPPED</color> [{durStr}]");

            message = "==================================================\n" +
                      "   <b><color=#00FFAA>Unfaded :: Recording Stopped</color></b>\n" +
                      "==================================================\n" +
                      $" • Start Time : <b>{RecordingStartTime:HH:mm:ss}</b>\n" +
                      $" • Stop Time  : <b>{RecordingStopTime:HH:mm:ss}</b>\n" +
                      $" • Duration   : <b>{durStr}</b> ({elapsedSeconds:F1}s)\n" +
                      $" • Directory  : <b>{outDir}</b>\n" +
                      " • Status     : Finalizing write buffer in capture folder.\n" +
                      "==================================================";
            return true;
        }
    }

    public static string GetStatus()
    {
        if (IsRecording)
        {
            double elapsed = (DateTime.Now - RecordingStartTime).TotalSeconds;
            return $"<color=#00FFAA>[Record]</color> State: <b><color=#FF4444>● RECORDING ACTIVE</color></b> ({FormatDuration(elapsed)} elapsed, started {RecordingStartTime:HH:mm:ss})\n" +
                   $" • Target Directory: {GetActiveOutputDirectory()}\n" +
                   $" • Finish Command  : Type <color=#00FFAA>'record stop'</color>";
        }

        string lastInfo = !string.IsNullOrEmpty(LastRecordedFile)
            ? $"\n • Last File : {Path.GetFileName(LastRecordedFile)} ({FormatDuration(LastDurationSeconds)}, {LastFileSizeBytes / (1024.0 * 1024.0):F1} MB)\n • Directory : {LastRecordedDirectory}"
            : "";

        return $"<color=#00FFAA>[Record]</color> State: <b>IDLE</b>\n" +
               $" • Capture Directory : {GetActiveOutputDirectory()}\n" +
               $" • Hotkey            : [{PluginConfig.RecordHotkey?.Value}] (toggle start/stop){lastInfo}\n" +
               $" • Usage             : 'record start' | 'record stop' | 'record status' | 'record dir <path>'";
    }

    private static FileInfo? FindNewestVideoFile(DateTime sinceTime)
    {
        var validExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".mov", ".avi", ".webm" };
        FileInfo? bestCandidate = null;

        foreach (string dir in GetCandidateDirectories())
        {
            if (!Directory.Exists(dir)) continue;

            try
            {
                var dirInfo = new DirectoryInfo(dir);
                var candidates = dirInfo.GetFiles("*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => validExtensions.Contains(f.Extension) && f.LastWriteTime >= sinceTime)
                    .OrderByDescending(f => f.LastWriteTime);

                var newest = candidates.FirstOrDefault();
                if (newest != null)
                {
                    if (bestCandidate == null || newest.LastWriteTime > bestCandidate.LastWriteTime)
                    {
                        bestCandidate = newest;
                    }
                }
            }
            catch (Exception ex)
            {
                ZLog.LogWarning($"[Unfaded.RecordingManager] Error scanning {dir}: {ex.Message}");
            }
        }

        return bestCandidate;
    }

    public static string FormatDuration(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private static void ShowHudMessage(string text)
    {
        try
        {
            if (MessageHud.instance != null)
            {
                MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, text);
            }
            else if (Player.m_localPlayer != null)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, text);
            }
        }
        catch
        {
            // Graceful fallback if HUD uninitialized
        }
    }
}
