namespace Unfaded.Patches;

using HarmonyLib;
using Unfaded.Configuration;
using Unfaded.Core;
using UnityEngine;

public static class GameRespawnPatch
{
    [HarmonyPatch(typeof(Game), nameof(Game.RequestRespawn))]
    public static class RequestRespawnPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ref float delay, bool afterDeath)
        {
            if (!PluginConfig.IsModEnabled.Value || !afterDeath)
            {
                return;
            }

            // Only override if delay > 0f (i.e. not an explicit immediate respawn request)
            if (delay > 0f)
            {
                delay = Mathf.Max(0f, PluginConfig.RespawnDelay.Value);
            }
        }
    }

    [HarmonyPatch(typeof(Hud), nameof(Hud.Update))]
    public static class HudUpdateInputPatch
    {
        private static float s_lastPromptTime = 0f;

        [HarmonyPostfix]
        public static void Postfix(Hud __instance)
        {
            if (!PluginConfig.IsModEnabled.Value)
            {
                return;
            }

            Player localPlayer = Player.m_localPlayer;
            if (localPlayer == null || !localPlayer.IsDead())
            {
                return;
            }

            if (Game.instance == null || Game.instance.WaitingForRespawn() || Game.instance.IsShuttingDown())
            {
                return;
            }

            // Periodic status and control reminder
            if (PluginConfig.ShowRespawnPrompt.Value)
            {
                if (Time.time - s_lastPromptTime > 4.5f)
                {
                    s_lastPromptTime = Time.time;
                    string keyName = PluginConfig.ManualRespawnKey.Value.ToString();
                    string prompt = string.Format(PluginConfig.RespawnPromptText.Value, keyName);
                    localPlayer.Message(MessageHud.MessageType.TopLeft, prompt);
                }
            }

            // Manual respawn trigger
            if (PluginConfig.EnableManualRespawn.Value)
            {
                if (Input.GetKeyDown(PluginConfig.ManualRespawnKey.Value))
                {
                    // Clean up spectator state and immediately request respawn
                    DeathStateManager.ResetOnRespawn();
                    Game.instance.RequestRespawn(0f, afterDeath: true);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
    public static class PlayerOnDeathPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance)
        {
            if (!PluginConfig.IsModEnabled.Value || __instance != Player.m_localPlayer)
            {
                return;
            }

            // 1. Record lethal hit details for Killer Cam and Death Recap
            DeathStateManager.RecordLethalHit(__instance, __instance.m_lastHit);

            // 2. Trigger cinematic slow-motion bullet-time if enabled
            if (PluginConfig.EnableSlowMotion.Value && UnfadedPlugin.Instance != null)
            {
                UnfadedPlugin.Instance.StartCoroutine(DeathStateManager.SlowMotionRoutine(UnfadedPlugin.Instance));
            }

            // 3. Display Death Cause Recap
            if (PluginConfig.EnableDeathRecap.Value)
            {
                string recap = DeathStateManager.FormatDeathRecap();
                __instance.Message(MessageHud.MessageType.Center, recap);
            }

            // 4. Display spectator controls
            if (PluginConfig.ShowRespawnPrompt.Value)
            {
                string keyName = PluginConfig.ManualRespawnKey.Value.ToString();
                string prompt = string.Format(PluginConfig.RespawnPromptText.Value, keyName);
                __instance.Message(MessageHud.MessageType.TopLeft, prompt);
            }
        }
    }
}
