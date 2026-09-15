namespace Unswayed.Patches;

using HarmonyLib;
using Unswayed.Configuration;
using Unswayed.Core;
using UnityEngine;

[HarmonyPatch(typeof(GameCamera))]
public static class CameraStabilizerPatches
{
    // 1. CORE HEAD-BOB SUPPRESSION (Decouple Camera From Animated Head Bone)
    [HarmonyPostfix]
    [HarmonyPatch("GetCameraBaseOffset")]
    public static void GetCameraBaseOffsetPostfix(GameCamera __instance, Player player, ref Vector3 __result)
    {
        if (!PluginConfig.IsModEnabled.Value || !PluginConfig.EnableBobSuppression.Value)
        {
            return;
        }

        if (player.InBed() || player.IsAttached() || player.IsSitting())
        {
            return;
        }

        // Mode filtering
        switch (PluginConfig.Mode.Value)
        {
            case SuppressionMode.RunningOnly:
                if (!player.IsRunning() && !player.IsWalking())
                {
                    return;
                }
                break;

            case SuppressionMode.DungeonsOnly:
                if (!StabilizerManager.IsInDungeonOrTightSpace(__instance))
                {
                    return;
                }
                break;
        }

        // Stable offset relative to root transform (eliminates vertical cervical bobbing)
        Vector3 stableOffset = Vector3.up * PluginConfig.StabilizedEyeHeight.Value;
        float damping = Mathf.Clamp01(PluginConfig.VerticalDamping.Value);

        __result = Vector3.Lerp(__result, stableOffset, damping);
    }

    // 2. ADAPTIVE DUNGEON POSITIONING & POINT-BLANK RELIEF
    [HarmonyPostfix]
    [HarmonyPatch("GetCameraPosition")]
    public static void GetCameraPositionPostfix(GameCamera __instance, float dt, ref Vector3 pos, ref Quaternion rot)
    {
        if (!PluginConfig.IsModEnabled.Value)
        {
            return;
        }

        Player localPlayer = Player.m_localPlayer;
        if (!localPlayer) return;

        Vector3 eyePos = localPlayer.m_eye ? localPlayer.m_eye.position : (localPlayer.transform.position + Vector3.up * 1.6f);
        float currentDist = Vector3.Distance(eyePos, pos);

        StabilizerManager.LastCameraDistance = currentDist;
        bool isTight = currentDist < PluginConfig.DungeonProximityThreshold.Value && currentDist > 0.01f;
        StabilizerManager.IsTightSpace = isTight;

        if (!PluginConfig.EnableDungeonErgonomics.Value)
        {
            return;
        }

        // Enforce Minimum Distance Clamp to prevent screen-filling Viking mesh
        if (PluginConfig.EnableMinDistanceClamp.Value && currentDist < PluginConfig.MinCameraDistance.Value && currentDist > 0.05f)
        {
            Vector3 backDir = (pos - eyePos).normalized;
            if (backDir == Vector3.zero)
            {
                backDir = -localPlayer.transform.forward;
            }
            pos = eyePos + backDir * PluginConfig.MinCameraDistance.Value;
            currentDist = PluginConfig.MinCameraDistance.Value;
        }

        // Adaptive Shoulder Lift: softly elevate camera over shoulders in narrow crypts
        if (isTight && PluginConfig.EnableCryptShoulderLift.Value)
        {
            float threshold = PluginConfig.DungeonProximityThreshold.Value;
            float proximityRatio = Mathf.Clamp01(1.0f - (currentDist / threshold));
            float lift = proximityRatio * PluginConfig.ShoulderLiftAmount.Value;
            pos += Vector3.up * lift;
        }

        // Disable ship camera tilt if requested
        if (PluginConfig.DisableShipTilt.Value && __instance.m_shipCameraTilt)
        {
            // Keep rotation level with horizon
            Vector3 euler = rot.eulerAngles;
            euler.z = 0f;
            rot = Quaternion.Euler(euler);
        }
    }

    // 3. DYNAMIC DUNGEON FOV EXPANSION (Wide peripheral horizon in crypts)
    [HarmonyPostfix]
    [HarmonyPatch("UpdateFOV")]
    public static void UpdateFOVPostfix(GameCamera __instance)
    {
        if (!PluginConfig.IsModEnabled.Value || !PluginConfig.EnableDungeonFovBoost.Value)
        {
            StabilizerManager.ResetFov(__instance);
            return;
        }

        if (StabilizerManager.IsTightSpace && PluginConfig.DungeonFovBoost.Value > 0.1f)
        {
            float targetOffset = PluginConfig.DungeonFovBoost.Value;
            StabilizerManager.CurrentFovOffset = Mathf.MoveTowards(StabilizerManager.CurrentFovOffset, targetOffset, Time.deltaTime * 45f);
            __instance.m_camera.fieldOfView += StabilizerManager.CurrentFovOffset;
        }
        else if (StabilizerManager.CurrentFovOffset > 0.01f)
        {
            StabilizerManager.CurrentFovOffset = Mathf.MoveTowards(StabilizerManager.CurrentFovOffset, 0f, Time.deltaTime * 30f);
            __instance.m_camera.fieldOfView += StabilizerManager.CurrentFovOffset;
        }
    }

    // 4. CAMERA SHAKE MULTIPLIER (Fine-Grained Shake Attenuation)
    [HarmonyPrefix]
    [HarmonyPatch("UpdateCameraShake")]
    public static void UpdateCameraShakePrefix(GameCamera __instance)
    {
        if (!PluginConfig.IsModEnabled.Value)
        {
            return;
        }

        float scale = Mathf.Clamp01(PluginConfig.CameraShakeScale.Value);
        if (scale <= 0.001f)
        {
            __instance.m_shakeIntensity = 0f;
        }
        else if (scale < 0.999f)
        {
            __instance.m_shakeIntensity *= scale;
        }
    }
}
