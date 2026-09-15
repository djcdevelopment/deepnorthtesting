namespace Unswayed;

using System.Reflection;
using BepInEx;
using HarmonyLib;
using Unswayed.Configuration;
using Unswayed.Core;
using UnityEngine;

/// <summary>
/// Unswayed: Ergonomic Camera & Locomotion Bobbing Stabilizer for Valheim 1.0 (Deep North).
/// Conceived and engineered in direct response to community member Crusnik's inquiry
/// regarding motion sickness and camera vertigo in Burial Crypts.
/// </summary>
[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public sealed class UnswayedPlugin : BaseUnityPlugin
{
    public const string PluginGUID = "djc.valheim.unswayed";
    public const string PluginName = "Unswayed";
    public const string PluginVersion = "1.0.0";

    public static UnswayedPlugin? Instance { get; private set; }

    private Harmony? _harmony;

    private void Awake()
    {
        Instance = this;
        PluginConfig.BindConfig(Config);

        _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGUID);

        Logger.LogInfo($"{PluginName} v{PluginVersion} initialized. Bobbing Suppression: {PluginConfig.EnableBobSuppression.Value} (Damping: {PluginConfig.VerticalDamping.Value:F2}), Dungeon Ergonomics: {PluginConfig.EnableDungeonErgonomics.Value}, Shake Scale: {PluginConfig.CameraShakeScale.Value:F2}. Hotkey: [{PluginConfig.ToggleHotkey.Value}]");
    }

    private void Update()
    {
        if (PluginConfig.ToggleHotkey != null && Input.GetKeyDown(PluginConfig.ToggleHotkey.Value))
        {
            StabilizerManager.ToggleStabilization(out string message);
            Logger.LogInfo(message);
            if (Console.instance != null)
            {
                Console.instance.AddString(message);
            }
        }
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        Instance = null;
    }
}
