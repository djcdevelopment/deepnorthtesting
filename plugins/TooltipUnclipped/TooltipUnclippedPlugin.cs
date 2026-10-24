using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace TooltipUnclipped
{
    [BepInPlugin("com.deepnorthtesting.tooltipunclipped", "TooltipUnclipped", "1.0.0")]
    public class TooltipUnclippedPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.deepnorthtesting.tooltipunclipped";
        public const string PluginName = "TooltipUnclipped";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log { get; private set; } = null!;

        public static ConfigEntry<bool> EnableMod { get; private set; } = null!;
        public static ConfigEntry<bool> ClampToScreen { get; private set; } = null!;

        private FileSystemWatcher? _configWatcher;
        private DateTime _lastConfigReloadUtc = DateTime.MinValue;

        private void Awake()
        {
            Log = Logger;

            EnableMod = Config.Bind(
                section: "1. General",
                key: "EnableMod",
                defaultValue: true,
                description: "Master switch. Set to false to disable all TooltipUnclipped gameplay hooks immediately."
            );

            ClampToScreen = Config.Bind(
                section: "2. Gameplay",
                key: "ClampToScreen",
                defaultValue: true,
                description: "Clamp tooltip to screen bounds after reparenting."
            );

            SetupConfigWatcher();

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGuid);

            Log.LogInfo("==================================================================");
            Log.LogInfo($" {PluginName} v{PluginVersion} loaded! Tooltip clipping fix active.");
            Log.LogInfo("==================================================================");
        }

        private void OnDestroy()
        {
            _configWatcher?.Dispose();
        }

        private void SetupConfigWatcher()
        {
            string configFileName = $"{PluginGuid}.cfg";
            if (string.IsNullOrEmpty(Paths.ConfigPath) || !Directory.Exists(Paths.ConfigPath))
            {
                return;
            }

            _configWatcher = new FileSystemWatcher(Paths.ConfigPath, configFileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                SynchronizingObject = ThreadingHelper.SynchronizingObject,
                EnableRaisingEvents = true
            };

            _configWatcher.Changed += OnConfigFileChanged;
            _configWatcher.Created += OnConfigFileChanged;
            _configWatcher.Renamed += OnConfigFileChanged;
        }

        private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (DateTime.UtcNow - _lastConfigReloadUtc < TimeSpan.FromMilliseconds(250))
                {
                    return;
                }
                _lastConfigReloadUtc = DateTime.UtcNow;
                Config.Reload();
                Log.LogInfo($"[TooltipUnclipped] Config reloaded live from {e.FullPath}");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[TooltipUnclipped] Config reload failed: {ex.Message}");
            }
        }

        public static bool IsDedicatedServerHeadless()
        {
            return ZNet.instance != null && ZNet.instance.IsDedicated();
        }

        [HarmonyPatch(typeof(UITooltip), "LateUpdate")]
        [HarmonyPostfix]
        private static void LateUpdate_Postfix(GameObject ___m_tooltip)
        {
            try
            {
                if (!EnableMod.Value || ___m_tooltip == null || !___m_tooltip.activeInHierarchy)
                {
                    return;
                }

                Canvas canvas = ___m_tooltip.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.rootCanvas != null && ___m_tooltip.transform.parent != canvas.rootCanvas.transform)
                {
                    ___m_tooltip.transform.SetParent(canvas.rootCanvas.transform, true);
                }

                MaskableGraphic[] graphics = ___m_tooltip.GetComponentsInChildren<MaskableGraphic>(true);
                for (int i = 0; i < graphics.Length; i++)
                {
                    graphics[i].maskable = false;
                }

                if (ClampToScreen.Value && ___m_tooltip.transform.childCount > 0 && ___m_tooltip.transform.GetChild(0) is RectTransform rt)
                {
                    Utils.ClampUIToScreen(rt);
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[TooltipUnclipped] LateUpdate_Postfix exception: {ex.Message}");
            }
        }

        [HarmonyFinalizer]
        private static Exception? Finalizer(Exception? __exception)
        {
            if (__exception != null)
            {
                Log.LogWarning($"[TooltipUnclipped] Finalizer caught: {__exception.Message}");
            }
            return null;
        }
    }
}
