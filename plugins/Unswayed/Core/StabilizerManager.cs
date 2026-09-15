namespace Unswayed.Core;

using Unswayed.Configuration;
using UnityEngine;

public static class StabilizerManager
{
    public static float LastCameraDistance { get; set; } = 4.0f;
    public static bool IsTightSpace { get; set; } = false;
    public static float OriginalBaseFov { get; set; } = 65f;
    public static float CurrentFovOffset { get; set; } = 0f;

    public static bool IsInDungeonOrTightSpace(GameCamera? camera)
    {
        if (camera == null) return false;
        return IsTightSpace || (camera.m_distance < PluginConfig.DungeonProximityThreshold.Value);
    }

    public static void ToggleStabilization(out string message)
    {
        PluginConfig.IsModEnabled.Value = !PluginConfig.IsModEnabled.Value;
        message = $"[Unswayed] Camera stabilization is now {(PluginConfig.IsModEnabled.Value ? "ENABLED" : "DISABLED")}.";
    }

    public static void ResetFov(GameCamera camera)
    {
        if (camera != null && camera.m_camera != null && CurrentFovOffset > 0.01f)
        {
            camera.m_camera.fieldOfView = OriginalBaseFov;
            CurrentFovOffset = 0f;
        }
    }
}
