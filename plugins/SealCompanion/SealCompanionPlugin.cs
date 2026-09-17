namespace SealCompanion;

using System.Reflection;
using BepInEx;
using HarmonyLib;
using SealCompanion.Configuration;
using SealCompanion.HUD;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public sealed class SealCompanionPlugin : BaseUnityPlugin
{
    public const string PluginGUID = "djc.valheim.sealcompanion";
    public const string PluginName = "SealCompanion";
    public const string PluginVersion = "1.0.0";

    public static SealCompanionPlugin? Instance { get; private set; }

    private Harmony? _harmony;

    private void Awake()
    {
        Instance = this;
        PluginConfig.BindConfig(Config);

        if (!PluginConfig.ModEnabled.Value)
        {
            Logger.LogInfo($"{PluginName} is disabled via configuration.");
            return;
        }

        _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGUID);

        Logger.LogInfo($"{PluginName} v{PluginVersion} initialized successfully. TamingTime: {PluginConfig.TamingTime.Value}s, FishingAssist: {PluginConfig.FishingAssistChance.Value}%, HotTub: {PluginConfig.EnableHotTubAttraction.Value}.");
    }

    private void OnGUI()
    {
        if (!PluginConfig.ModEnabled.Value || !PluginConfig.EnableCuriosityHUD.Value) return;
        SealCuriosityPanel.Draw();
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        Instance = null;
    }
}
