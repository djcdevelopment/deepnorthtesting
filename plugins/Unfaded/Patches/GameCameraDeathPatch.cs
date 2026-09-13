namespace Unfaded.Patches;

using HarmonyLib;
using Unfaded.Configuration;
using Unfaded.Core;
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
        bool isDead = localPlayer != null && localPlayer.IsDead();
        bool isTest = DeathStateManager.IsTestSimActive && Time.time < DeathStateManager.TestSimEndTime;

        if (!isDead && !isTest)
        {
            if (s_wasDead)
            {
                DeathStateManager.ResetOnRespawn();
                s_wasDead = false;
            }
            return;
        }

        if (!PluginConfig.IsModEnabled.Value)
        {
            return;
        }

        // Handle camera mode switching hotkeys
        HandleModeSwitchKeys();

        // 1. FREE-FLY DRONE SPECTATOR MODE
        if (DeathStateManager.CurrentMode == DeathCameraMode.FreeFly && PluginConfig.EnableFreeFly.Value)
        {
            UpdateFreeFlyMode(__instance, dt);
            return;
        }

        // Determine center target to track
        Vector3 targetCenter = Vector3.zero;

        if (DeathStateManager.CurrentMode == DeathCameraMode.KillerFocus && PluginConfig.EnableKillerCam.Value)
        {
            if (DeathStateManager.LastKiller != null && DeathStateManager.LastKiller.gameObject != null)
            {
                targetCenter = DeathStateManager.LastKiller.GetCenterPoint();
            }
            else
            {
                // Fallback to ragdoll if killer despawned
                DeathStateManager.CurrentMode = DeathCameraMode.RagdollOrbit;
            }
        }

        if (DeathStateManager.CurrentMode == DeathCameraMode.RagdollOrbit)
        {
            if (isDead)
            {
                Ragdoll ragdoll = localPlayer!.GetRagdoll();
                if (ragdoll != null)
                {
                    targetCenter = ragdoll.GetAverageBodyPosition();
                }
                else
                {
                    targetCenter = localPlayer.transform.position + Vector3.up * 1.5f;
                }
            }
            else if (isTest)
            {
                targetCenter = localPlayer!.transform.position + Vector3.up * 1.5f;
            }
        }

        // 2. ORBITAL SPECTATOR MODE
        if (PluginConfig.EnableCameraOrbit.Value)
        {
            if (!s_wasDead)
            {
                s_wasDead = true;
                s_orbitYaw = __instance.transform.eulerAngles.y;
                s_orbitPitch = Mathf.Clamp(__instance.transform.eulerAngles.x, 5f, 75f);
            }

            Vector2 mouseDelta = ZInput.GetMouseDelta();
            s_orbitYaw += mouseDelta.x * PluginConfig.CameraOrbitSensitivity.Value;
            s_orbitPitch -= mouseDelta.y * PluginConfig.CameraOrbitSensitivity.Value;
            s_orbitPitch = Mathf.Clamp(s_orbitPitch, -10f, 85f);

            Quaternion rot = Quaternion.Euler(s_orbitPitch, s_orbitYaw, 0f);
            Vector3 dir = -(rot * Vector3.forward);
            float distance = Mathf.Clamp(__instance.m_distance, __instance.m_minDistance, __instance.m_maxDistance);

            Vector3 targetPos = targetCenter + dir * distance;

            // Geometry collision check
            if (Physics.SphereCast(targetCenter, 0.25f, dir, out RaycastHit hit, distance, __instance.m_blockCameraMask))
            {
                targetPos = targetCenter + dir * Mathf.Max(0.5f, hit.distance - 0.15f);
            }

            __instance.transform.position = targetPos;
            __instance.transform.LookAt(targetCenter);
        }
        else
        {
            __instance.transform.LookAt(targetCenter);
        }
    }

    private static void HandleModeSwitchKeys()
    {
        // Toggle Free-Fly Mode
        if (PluginConfig.EnableFreeFly.Value && Input.GetKeyDown(PluginConfig.FreeFlyKey.Value))
        {
            if (DeathStateManager.CurrentMode == DeathCameraMode.FreeFly)
            {
                DeathStateManager.CurrentMode = DeathCameraMode.RagdollOrbit;
                Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "Spectator: Ragdoll Orbit");
            }
            else
            {
                DeathStateManager.CurrentMode = DeathCameraMode.FreeFly;
                Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "Spectator: Free-Fly Drone (WASD to Move)");
            }
        }

        // Toggle Killer Cam
        if (PluginConfig.EnableKillerCam.Value && Input.GetKeyDown(PluginConfig.KillerCamKey.Value))
        {
            if (DeathStateManager.CurrentMode == DeathCameraMode.KillerFocus)
            {
                DeathStateManager.CurrentMode = DeathCameraMode.RagdollOrbit;
                Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "Spectator: Ragdoll Orbit");
            }
            else if (DeathStateManager.LastKiller != null)
            {
                DeathStateManager.CurrentMode = DeathCameraMode.KillerFocus;
                Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, $"Spectator: Killer Focus ({DeathStateManager.LastKillerName})");
            }
            else
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "No enemy killer to spectate (environmental death)");
            }
        }
    }

    private static void UpdateFreeFlyMode(GameCamera cam, float dt)
    {
        Vector2 mouseDelta = ZInput.GetMouseDelta();
        DeathStateManager.FreeFlyYaw += mouseDelta.x * PluginConfig.CameraOrbitSensitivity.Value;
        DeathStateManager.FreeFlyPitch -= mouseDelta.y * PluginConfig.CameraOrbitSensitivity.Value;
        DeathStateManager.FreeFlyPitch = Mathf.Clamp(DeathStateManager.FreeFlyPitch, -85f, 85f);

        Quaternion rot = Quaternion.Euler(DeathStateManager.FreeFlyPitch, DeathStateManager.FreeFlyYaw, 0f);

        // Movement with WASD + Space/Ctrl
        Vector3 moveDir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) moveDir += rot * Vector3.forward;
        if (Input.GetKey(KeyCode.S)) moveDir -= rot * Vector3.forward;
        if (Input.GetKey(KeyCode.D)) moveDir += rot * Vector3.right;
        if (Input.GetKey(KeyCode.A)) moveDir -= rot * Vector3.right;
        if (Input.GetKey(KeyCode.Space)) moveDir += Vector3.up;
        if (Input.GetKey(KeyCode.LeftControl)) moveDir -= Vector3.up;

        float speed = PluginConfig.FreeFlySpeed.Value;
        if (Input.GetKey(KeyCode.LeftShift)) speed *= 2f;

        Vector3 newPos = DeathStateManager.FreeFlyPosition + moveDir * speed * dt;

        // Clamp to allowed radius from death position
        Vector3 anchor = DeathStateManager.DeathPosition;
        if (anchor != Vector3.zero)
        {
            float maxDist = PluginConfig.FreeFlyRadius.Value;
            Vector3 offset = newPos - anchor;
            if (offset.magnitude > maxDist)
            {
                newPos = anchor + offset.normalized * maxDist;
            }
        }

        DeathStateManager.FreeFlyPosition = newPos;
        cam.transform.position = newPos;
        cam.transform.rotation = rot;
    }
}
