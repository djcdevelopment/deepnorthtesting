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
    public const string PluginVersion = "1.0.1";

    public static UnfadedPlugin? Instance { get; private set; }

    private Harmony? _harmony;

    private void Awake()
    {
        Instance = this;
        PluginConfig.BindConfig(Config);

        _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGUID);

        Logger.LogInfo($"{PluginName} v{PluginVersion} loaded successfully. Blackout: {(PluginConfig.DisableDeathFade.Value ? "Disabled" : "Enabled")}, Respawn: {PluginConfig.RespawnDelay.Value}s ([{PluginConfig.ManualRespawnKey.Value}]), KillerCam: {PluginConfig.EnableKillerCam.Value}, FreeFly: {PluginConfig.EnableFreeFly.Value}, SlowMo: {PluginConfig.EnableSlowMotion.Value}.");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        Instance = null;
    }
}
