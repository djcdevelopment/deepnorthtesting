using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace CartRighter
{
    [BepInPlugin("com.deepnorthtesting.cartrighter", "CartRighter", "1.0.0")]
    public class CartRighterPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.deepnorthtesting.cartrighter";
        public const string PluginName = "CartRighter";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log { get; private set; } = null!;

        public static ConfigEntry<bool> EnableMod { get; private set; } = null!;
        public static ConfigEntry<float> MaxDistance { get; private set; } = null!;
        public static ConfigEntry<int> FlipKey { get; private set; } = null!;
        public static ConfigEntry<bool> AutoRightWhenTipped { get; private set; } = null!;
        public static ConfigEntry<float> AutoFlipAngle { get; private set; } = null!;

        private FileSystemWatcher? _configWatcher;
        private DateTime _lastConfigReloadUtc = DateTime.MinValue;

        private void Awake()
        {
            Log = Logger;

            EnableMod = Config.Bind(section: "1. General", key: "EnableMod", defaultValue: true, description: "Master switch.");
            MaxDistance = Config.Bind(section: "2. Gameplay", key: "MaxDistance", defaultValue: 10f, configDescription: new ConfigDescription("Max distance to cart.", new AcceptableValueRange<float>(1f, 100f)));
            FlipKey = Config.Bind(section: "2. Gameplay", key: "FlipKey", defaultValue: 70, description: "Key code to manually flip cart (Unity KeyCode).");
            AutoRightWhenTipped = Config.Bind(section: "2. Gameplay", key: "AutoRightWhenTipped", defaultValue: true, description: "Auto-right cart when tipped over.");
            AutoFlipAngle = Config.Bind(section: "2. Gameplay", key: "AutoFlipAngle", defaultValue: 45f, configDescription: new ConfigDescription("Angle threshold for auto-right.", new AcceptableValueRange<float>(10f, 179f)));

            SetupConfigWatcher();
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGuid);

            Log.LogInfo($"[CartRighter] v{PluginVersion} loaded!");
        }

        private void OnDestroy()
        {
            _configWatcher?.Dispose();
        }

        private void SetupConfigWatcher()
        {
            string configFileName = $"{PluginGuid}.cfg";
            if (string.IsNullOrEmpty(Paths.ConfigPath) || !Directory.Exists(Paths.ConfigPath)) return;

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
                if (DateTime.UtcNow - _lastConfigReloadUtc < TimeSpan.FromMilliseconds(500)) return;
                _lastConfigReloadUtc = DateTime.UtcNow;
                Config.Reload();
                Log.LogInfo("[CartRighter] Config reloaded live!");
            }
            catch (Exception ex)
            {
                Log.LogError($"[CartRighter] Config reload failed: {ex.Message}");
            }
        }

        public static bool IsDedicatedServerHeadless()
        {
            return ZNet.instance != null && ZNet.instance.IsDedicated();
        }

        [HarmonyPatch(typeof(Vagon), "Update")]
        [HarmonyPostfix]
        private static void VagonUpdate_Postfix(Vagon __instance, Rigidbody ___m_body, ZNetView ___m_nview)
        {
            try
            {
                if (!EnableMod.Value || Player.m_localPlayer == null || ___m_body == null || ___m_nview == null || !___m_nview.IsValid()) return;

                float dist = Vector3.Distance(Player.m_localPlayer.transform.position, __instance.transform.position);
                if (dist > MaxDistance.Value && !__instance.IsAttached()) return;

                bool flipTriggered = Input.GetKeyDown((KeyCode)FlipKey.Value);
                bool autoFlipTriggered = AutoRightWhenTipped.Value && Vector3.Angle(__instance.transform.up, Vector3.up) > AutoFlipAngle.Value;

                if (!flipTriggered && !autoFlipTriggered) return;

                ___m_nview.ClaimOwnership();

                __instance.transform.position += Vector3.up * 0.35f;

                ___m_body.velocity = Vector3.zero;
                ___m_body.angularVelocity = Vector3.zero;

                Quaternion upright = Quaternion.Euler(0f, __instance.transform.eulerAngles.y, 0f);
                ___m_body.MoveRotation(upright);
                __instance.transform.rotation = upright;

                Log.LogInfo("[CartRighter] Cart righted!");
            }
            catch (Exception ex)
            {
                Log.LogError($"[CartRighter] Patch exception: {ex.Message}");
            }
        }

        [HarmonyFinalizer]
        private static Exception? Finalizer(Exception? __exception)
        {
            if (__exception != null)
            {
                Log.LogError($"[CartRighter] Finalizer caught: {__exception.Message}");
            }
            return null;
        }
    }
}
