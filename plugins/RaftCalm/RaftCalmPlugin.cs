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

namespace RaftCalm
{
    [BepInPlugin("com.deepnorthtesting.raftcalm", "RaftCalm", "1.0.0")]
    public class RaftCalmPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.deepnorthtesting.raftcalm";
        public const string PluginName = "RaftCalm";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log { get; private set; } = null!;

        public static ConfigEntry<bool> EnableMod { get; private set; } = null!;
        public static ConfigEntry<float> ProtectRadius { get; private set; } = null!;

        private FileSystemWatcher? _configWatcher;
        private DateTime _lastConfigReloadUtc = DateTime.MinValue;

        private void Awake()
        {
            Log = Logger;

            EnableMod = Config.Bind(section: "General", key: "EnableMod", defaultValue: true, description: "Master switch for RaftCalm.");
            ProtectRadius = Config.Bind(section: "General", key: "ProtectRadius", defaultValue: 25f, configDescription: new ConfigDescription("Radius around player on ship to skip support wear updates.", new AcceptableValueRange<float>(5f, 100f)));

            SetupConfigWatcher();
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGuid);

            Log.LogInfo($"[RaftCalm] v{PluginVersion} loaded. ProtectRadius={ProtectRadius.Value:0}");
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
                TimeSpan diff = DateTime.UtcNow - _lastConfigReloadUtc;
                if (diff.TotalMilliseconds < 200)
                {
                    return;
                }
                _lastConfigReloadUtc = DateTime.UtcNow;
                Config.Reload();
                Log.LogInfo("[RaftCalm] Config reloaded live.");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[RaftCalm] Config reload failed: {ex.Message}");
            }
        }

        public static bool IsDedicatedServerHeadless()
        {
            return ZNet.instance != null && ZNet.instance.IsDedicated();
        }

        [HarmonyPatch(typeof(WearNTear), "UpdateSupport")]
        [HarmonyPrefix]
        private static bool UpdateSupport_Prefix(WearNTear __instance)
        {
            try
            {
                if (!EnableMod.Value)
                {
                    return true;
                }

                if (IsDedicatedServerHeadless())
                {
                    return true;
                }

                if (Player.m_localPlayer == null)
                {
                    return true;
                }

                Ship? localShip = Ship.GetLocalShip();
                if (localShip == null)
                {
                    return true;
                }

                if (Vector3.Distance(__instance.transform.position, Player.m_localPlayer.transform.position) <= ProtectRadius.Value)
                {
                    __instance.m_noSupportWear = true;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"[RaftCalm] UpdateSupport_Prefix exception: {ex.Message}");
                return true;
            }
        }

        [HarmonyFinalizer]
        private static Exception? Finalizer(Exception? __exception)
        {
            if (__exception != null)
            {
                Log.LogError($"[RaftCalm] Harmony exception: {__exception.Message}");
            }
            return null;
        }
    }
}
