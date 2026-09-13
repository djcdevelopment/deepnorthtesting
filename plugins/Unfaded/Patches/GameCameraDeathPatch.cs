namespace Unfaded.Patches;

using HarmonyLib;
using Unfaded.Configuration;
using UnityEngine;

[HarmonyPatch(typeof(GameCamera))]
public static class GameCameraDeathPatch
{
    private static bool s_wasDead = false;
    private static float s_orbitYaw = 0f;
    private static float s_orbitPitch = 25f;

    [HarmonyPostfix]
    [HarmonyPatch("UpdateCamera")]
    public static void UpdateCameraPostfix(GameCamera __instance, float dt)
    {
        Player localPlayer = Player.m_localPlayer;
        if (localPlayer == null || !localPlayer.IsDead())
        {
            s_wasDead = false;
            return;
        }

        if (!PluginConfig.IsModEnabled.Value || !PluginConfig.EnableCameraOrbit.Value)
        {
            return;
        }

        Ragdoll ragdoll = localPlayer.GetRagdoll();
        if (ragdoll == null)
        {
            return;
        }

        Vector3 targetCenter = ragdoll.GetAverageBodyPosition();

        if (!s_wasDead)
        {
            s_wasDead = true;
            s_orbitYaw = __instance.transform.eulerAngles.y;
            s_orbitPitch = Mathf.Clamp(__instance.transform.eulerAngles.x, 5f, 75f);
        }

        // Apply mouse delta for orbital viewing
        Vector2 mouseDelta = ZInput.GetMouseDelta();
        s_orbitYaw += mouseDelta.x * PluginConfig.CameraOrbitSensitivity.Value;
        s_orbitPitch -= mouseDelta.y * PluginConfig.CameraOrbitSensitivity.Value;
        s_orbitPitch = Mathf.Clamp(s_orbitPitch, -10f, 85f);

        Quaternion rot = Quaternion.Euler(s_orbitPitch, s_orbitYaw, 0f);
        Vector3 dir = -(rot * Vector3.forward);
        float distance = Mathf.Clamp(__instance.m_distance, __instance.m_minDistance, __instance.m_maxDistance);

        Vector3 targetPos = targetCenter + dir * distance;

        // Obstacle avoidance raycast against world geometry
        if (Physics.SphereCast(targetCenter, 0.25f, dir, out RaycastHit hit, distance, __instance.m_blockCameraMask))
        {
            targetPos = targetCenter + dir * Mathf.Max(0.5f, hit.distance - 0.15f);
        }

        __instance.transform.position = targetPos;
        __instance.transform.LookAt(targetCenter);
    }
}
