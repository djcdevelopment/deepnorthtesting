namespace EarnYourKeep.Patches;

using HarmonyLib;
using UnityEngine;

[HarmonyPatch(typeof(Achievements), nameof(Achievements.IsCheatedAtAll))]
static class Achievements_IsCheatedAtAll_Patch
{
    [HarmonyPrefix]
    static bool Prefix(ref bool __result)
    {
        if (!EarnYourKeep.AllowWhileModded.Value)
        {
            return true; // Let vanilla run
        }

        if (Time.frameCount == Achievements.m_cheatCheckFrame)
        {
            __result = Achievements.m_cheatCheckCache;
            return false;
        }

        Achievements.m_cheatCheckFrame = Time.frameCount;

        if (EarnYourKeep.AllowWithDevcommands.Value)
        {
            Achievements.m_cheatCheckCache = false;
            __result = false;
            return false;
        }

        bool profileCheated = (Game.instance != null) && Game.instance.GetPlayerProfile().m_usedCheats;
        bool worldCheated = Achievements.IsWorldCheated();
        bool itemCheated = (Player.m_localPlayer != null) && Player.m_localPlayer.GetInventory().AnyCheatedItem();

        // Crucial fix: Decouple Game.isModded from cheat evaluation!
        Achievements.m_cheatCheckCache = profileCheated || worldCheated || itemCheated;
        __result = Achievements.m_cheatCheckCache;
        return false;
    }
}

[HarmonyPatch(typeof(Achievements), nameof(Achievements.CanGetAchievements))]
static class Achievements_CanGetAchievements_Patch
{
    [HarmonyPrefix]
    static bool Prefix(bool cheated, ref bool __result)
    {
        if (!EarnYourKeep.AllowWhileModded.Value)
        {
            return true; // Let vanilla run
        }

        if (EarnYourKeep.AllowWithDevcommands.Value)
        {
            __result = true;
            return false;
        }

        if (cheated)
        {
            __result = PlayerProfile.s_bypassCheatChecks;
            return false;
        }

        if (!Achievements.IsCheatedAtAll())
        {
            __result = true;
            return false;
        }

        __result = PlayerProfile.s_bypassCheatChecks;
        return false;
    }
}

[HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.SetupGui))]
static class FejdStartup_SetupGui_Patch
{
    [HarmonyPostfix]
    static void Postfix(FejdStartup __instance)
    {
        if (EarnYourKeep.HideModdedWatermark.Value && __instance.m_moddedText != null)
        {
            __instance.m_moddedText.SetActive(false);
        }
    }
}

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateAchievementsList))]
static class InventoryGui_UpdateAchievementsList_Patch
{
    [HarmonyPostfix]
    static void Postfix(InventoryGui __instance)
    {
        if (EarnYourKeep.AllowWithDevcommands.Value && __instance.m_achievementsCheatedText != null)
        {
            __instance.m_achievementsCheatedText.gameObject.SetActive(false);
        }
    }
}
