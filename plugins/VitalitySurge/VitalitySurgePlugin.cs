using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace VitalitySurge
{
    [BepInPlugin("com.deepnorthtesting.vitalitysurge", "VitalitySurge", "1.0.0")]
    public sealed class VitalitySurgePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.deepnorthtesting.vitalitysurge";
        public const string PluginName = "VitalitySurge";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log { get; private set; } = null!;

        public static ConfigEntry<bool> EnableMod { get; private set; } = null!;
        public static ConfigEntry<float> StaminaRegenMultiplier { get; private set; } = null!;
        public static ConfigEntry<float> CarryWeightBonus { get; private set; } = null!;

        private FileSystemWatcher? _configWatcher;
        private DateTime _lastConfigReloadUtc = DateTime.MinValue;

        private void Awake()
        {
            Log = Logger;

            EnableMod = Config.Bind(
                section: "1. General",
                key: "EnableMod",
                defaultValue: true,
                description: "Master switch for VitalitySurge."
            );

            StaminaRegenMultiplier = Config.Bind(
                section: "2. Gameplay",
                key: "StaminaRegenMultiplier",
                defaultValue: 1.2f,
                configDescription: new ConfigDescription(
                    "Multiplier applied to stamina regeneration when a VitalitySurge status effect is active.",
                    new AcceptableValueRange<float>(0.1f, 5.0f)
                )
            );

            CarryWeightBonus = Config.Bind(
                section: "2. Gameplay",
                key: "CarryWeightBonus",
                defaultValue: 0f,
                configDescription: new ConfigDescription(
                    "Flat carry weight bonus added when a VitalitySurge status effect is active.",
                    new AcceptableValueRange<float>(0f, 1000f)
                )
            );

            SetupConfigWatcher();

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGuid);

            Log.LogInfo("==================================================================");
            Log.LogInfo($" {PluginName} v{PluginVersion} loaded on Valheim 1.0.20 (Deep North)!");
            Log.LogInfo($" -> StaminaRegenMultiplier : {StaminaRegenMultiplier.Value:0.00}x");
            Log.LogInfo($" -> CarryWeightBonus       : +{CarryWeightBonus.Value:0}");
            Log.LogInfo(" -> In-Game Console        : Press F5 and type 'vitalitysurge'");
            Log.LogInfo("==================================================================");
        }

        private void SetupConfigWatcher()
        {
            try
            {
                string configPath = Path.Combine(BepInEx.Paths.ConfigPath, PluginGuid + ".cfg");
                if (!File.Exists(configPath))
                {
                    return;
                }

                _configWatcher = new FileSystemWatcher(Path.GetDirectoryName(configPath)!)
                {
                    Filter = Path.GetFileName(configPath),
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                _configWatcher.Changed += (sender, e) =>
                {
                    try
                    {
                        DateTime now = DateTime.UtcNow;
                        if ((now - _lastConfigReloadUtc).TotalMilliseconds < 250)
                        {
                            return;
                        }
                        _lastConfigReloadUtc = now;

                        Log.LogInfo("[VitalitySurge] Config file changed; reloading live...");
                        Config.Reload();
                        Log.LogInfo("[VitalitySurge] Config reloaded successfully.");
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning("[VitalitySurge] Config reload failed: " + ex);
                    }
                };
            }
            catch (Exception ex)
            {
                Log.LogWarning("[VitalitySurge] Could not start FileSystemWatcher: " + ex);
            }
        }

        [HarmonyPatch(typeof(Terminal), "InitTerminal")]
        internal static class VitalitySurgeTerminalPatch
        {
            [HarmonyPostfix]
            private static void InitTerminal_Postfix()
            {
                _ = new Terminal.ConsoleCommand(
                    "vitalitysurge",
                    "VitalitySurge status inspector",
                    (Terminal.ConsoleEventArgs args) =>
                    {
                        args.Context.AddString("----------------------------------------------------------");
                        args.Context.AddString("  VitalitySurge :: Live Runtime Inspector                ");
                        args.Context.AddString("----------------------------------------------------------");
                        args.Context.AddString("  Mod Enabled : " + EnableMod.Value);
                        args.Context.AddString("  Stamina Regen Multiplier : " + StaminaRegenMultiplier.Value.ToString("0.00") + "x");
                        args.Context.AddString("  Carry Weight Bonus       : +" + CarryWeightBonus.Value.ToString("0"));

                        Player? localPlayer = Player.m_localPlayer;
                        if (localPlayer == null)
                        {
                            args.Context.AddString("  [Player]  Player.m_localPlayer is NULL (Main Menu or scene transition)");
                        }
                        else
                        {
                            args.Context.AddString(
                                "  [Player]  HP: " + localPlayer.GetHealth().ToString("0") + "/" + localPlayer.GetMaxHealth().ToString("0") +
                                " | Stamina: " + localPlayer.GetStamina().ToString("0") +
                                " | MaxCarryWeight: " + localPlayer.GetMaxCarryWeight().ToString("0")
                            );
                        }

                        if (ZNet.instance != null)
                        {
                            args.Context.AddString("  [ZNet]    Dedicated: " + ZNet.instance.IsDedicated());
                        }
                        else
                        {
                            args.Context.AddString("  [ZNet]    ZNet.instance is NULL");
                        }

                        args.Context.AddString("----------------------------------------------------------");
                    }
                );
            }
        }

        [HarmonyPatch(typeof(SEMan), nameof(SEMan.AddStatusEffect), new Type[] { typeof(int), typeof(bool), typeof(int), typeof(float), typeof(short) })]
        internal static class VitalitySurgeStatusEffectPatch
        {
            private static bool _loggedErrorOnce;

            [HarmonyPostfix]
            private static void AddStatusEffect_Postfix(int nameHash, bool resetTime, int itemLevel, float skillLevel, short variant, StatusEffect __result)
            {
                if (!EnableMod.Value)
                {
                    return;
                }

                if (__result == null)
                {
                    return;
                }

                try
                {
                    if (Player.m_localPlayer == null)
                    {
                        return;
                    }

                    if (ZNet.instance != null && ZNet.instance.IsDedicated())
                    {
                        return;
                    }

                    string effectName = __result.m_name ?? string.Empty;
                    if (effectName.IndexOf("VitalitySurge", StringComparison.OrdinalIgnoreCase) < 0 &&
                        effectName.IndexOf("Vitality", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return;
                    }

                    ApplyVitalitySurgeBonuses(Player.m_localPlayer);
                }
                catch (Exception ex)
                {
                    if (!_loggedErrorOnce)
                    {
                        _loggedErrorOnce = true;
                        Log.LogError("[VitalitySurge] Exception in AddStatusEffect postfix: " + ex);
                    }
                }
            }

            private static void ApplyVitalitySurgeBonuses(Player player)
            {
                if (player == null)
                {
                    return;
                }

                float staminaRegen = player.m_staminaRegen;
                staminaRegen *= StaminaRegenMultiplier.Value;
                player.m_staminaRegen = staminaRegen;

                float carryWeight = player.GetMaxCarryWeight();
                carryWeight += CarryWeightBonus.Value;
                player.m_maxCarryWeight = carryWeight;
            }

            [HarmonyFinalizer]
            private static Exception? Finalizer(Exception? __exception)
            {
                if (__exception != null && !_loggedErrorOnce)
                {
                    _loggedErrorOnce = true;
                    Log.LogError("[VitalitySurge] Fault isolation caught exception in DMD<SEMan::AddStatusEffect>: " + __exception);
                }
                return null;
            }
        }
    }
}

