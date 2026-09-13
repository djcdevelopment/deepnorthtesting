namespace Unfaded;

using System.Reflection;
using BepInEx;
using HarmonyLib;
using Unfaded.Configuration;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public sealed class UnfadedPlugin : BaseUnityPlugin
{
    public const string PluginGUID = "djc.valheim.unfaded";
    public const string PluginName = "Unfaded";
    public const string PluginVersion = "1.0.0";

    private Harmony? _harmony;

    private void Awake()
    {
        PluginConfig.BindConfig(Config);

        _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGUID);

        Logger.LogInfo($"{PluginName} v{PluginVersion} loaded successfully. Death blackout: {(PluginConfig.DisableDeathFade.Value ? "Disabled" : "Enabled")}. Respawn delay: {PluginConfig.RespawnDelay.Value}s. Manual respawn: {PluginConfig.EnableManualRespawn.Value} [{PluginConfig.ManualRespawnKey.Value}].");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }
}
