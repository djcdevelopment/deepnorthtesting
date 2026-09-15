namespace Unfaded;

using System.Reflection;
using BepInEx;
using HarmonyLib;
using Unfaded.Configuration;
using Unfaded.Core;
using UnityEngine;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public sealed class UnfadedPlugin : BaseUnityPlugin
{
    public const string PluginGUID = "djc.valheim.unfaded";
    public const string PluginName = "Unfaded";
    public const string PluginVersion = "1.0.2";

    public static UnfadedPlugin? Instance { get; private set; }

    private Harmony? _harmony;

    private void Awake()
    {
        Instance = this;
        PluginConfig.BindConfig(Config);

        _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGUID);

        Logger.LogInfo($"{PluginName} v{PluginVersion} loaded successfully. Blackout: {(PluginConfig.DisableDeathFade.Value ? "Disabled" : "Enabled")}, Respawn: {PluginConfig.RespawnDelay.Value}s ([{PluginConfig.ManualRespawnKey.Value}]), KillerCam: {PluginConfig.EnableKillerCam.Value}, FreeFly: {PluginConfig.EnableFreeFly.Value}, SlowMo: {PluginConfig.EnableSlowMotion.Value}, RecordHotkey: {PluginConfig.RecordHotkey?.Value}.");
    }

    private void Update()
    {
        if (PluginConfig.EnableRecordingHotkey != null && PluginConfig.EnableRecordingHotkey.Value &&
            PluginConfig.RecordHotkey != null && Input.GetKeyDown(PluginConfig.RecordHotkey.Value))
        {
            if (RecordingManager.IsRecording)
            {
                RecordingManager.StopRecording(out string stopMsg);
                Logger.LogInfo(stopMsg);
                if (Console.instance != null)
                {
                    Console.instance.AddString(stopMsg);
                }
            }
            else
            {
                RecordingManager.StartRecording(out string startMsg);
                Logger.LogInfo(startMsg);
                if (Console.instance != null)
                {
                    Console.instance.AddString(startMsg);
                }
            }
        }
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        Instance = null;
    }
}
