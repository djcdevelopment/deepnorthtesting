namespace Unfaded.Patches;

using HarmonyLib;
using Unfaded.Configuration;
using UnityEngine;

[HarmonyPatch(typeof(Hud))]
public static class HudBlackScreenPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("UpdateBlackScreen")]
    public static bool UpdateBlackScreenPrefix(Hud __instance, Player player, float dt)
    {
        if (!PluginConfig.IsModEnabled.Value || !PluginConfig.DisableDeathFade.Value)
        {
            return true;
        }

        // When the player is dead and not yet executing the actual respawn scene reload:
        // Suppress the 9.5s blackout overlay completely.
        if (player != null && player.IsDead() && (Game.instance == null || !Game.instance.WaitingForRespawn()))
        {
            if (Game.instance != null && Game.instance.IsShuttingDown())
            {
                return true;
            }

            if (player.IsSleeping() || player.IsTeleporting())
            {
                return true;
            }

            // Zero out loading screen alpha and ensure blackout objects remain inactive
            if (__instance.m_loadingScreen != null)
            {
                __instance.m_loadingScreen.alpha = 0f;
                __instance.m_loadingScreen.gameObject.SetActive(false);
            }

            if (__instance.m_loadingProgress != null)
            {
                __instance.m_loadingProgress.SetActive(false);
            }

            if (__instance.m_sleepingProgress != null)
            {
                __instance.m_sleepingProgress.SetActive(false);
            }

            if (__instance.m_teleportingProgress != null)
            {
                __instance.m_teleportingProgress.SetActive(false);
            }

            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch("GetFadeDuration")]
    public static bool GetFadeDurationPrefix(Player player, ref float __result)
    {
        if (!PluginConfig.IsModEnabled.Value || player == null || !player.IsDead())
        {
            return true;
        }

        if (PluginConfig.DisableDeathFade.Value)
        {
            __result = 999999f;
            return false;
        }

        if (PluginConfig.CustomDeathFadeDuration.Value > 0f)
        {
            __result = Mathf.Max(0.01f, PluginConfig.CustomDeathFadeDuration.Value);
            return false;
        }

        return true;
    }
}
