namespace EarnYourKeep;

using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public sealed class EarnYourKeep : BaseUnityPlugin
{
    public const string PluginGUID = "djc.valheim.earnyourkeep";
    public const string PluginName = "EarnYourKeep";
    public const string PluginVersion = "1.0.0";

    public static ConfigEntry<bool> AllowWhileModded = null!;
    public static ConfigEntry<bool> AllowWithDevcommands = null!;
    public static ConfigEntry<bool> HideModdedWatermark = null!;

    private void Awake()
    {
        AllowWhileModded = Config.Bind(
            "General",
            "AllowWhileModded",
            true,
            "Enable earning achievements while playing with BepInEx / mods loaded."
        );

        AllowWithDevcommands = Config.Bind(
            "General",
            "AllowWithDevcommands",
            false,
            "Enable earning achievements even if devcommands / cheats were used on this character or world."
        );

        HideModdedWatermark = Config.Bind(
            "Visual",
            "HideModdedWatermark",
            false,
            "Hide the 'Modded' watermark on the main menu."
        );

        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGUID);

        Logger.LogInfo($"{PluginName} v{PluginVersion} initialized. Modded achievements allowed: {AllowWhileModded.Value}. Devcommand bypass: {AllowWithDevcommands.Value}.");
    }
}
