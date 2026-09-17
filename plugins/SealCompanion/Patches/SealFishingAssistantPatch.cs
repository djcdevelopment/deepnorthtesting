namespace SealCompanion.Patches;

using System;
using System.Collections.Generic;
using HarmonyLib;
using SealCompanion.Components;
using SealCompanion.Configuration;
using UnityEngine;

[HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.FixedUpdate))]
public static class SealFishingAssistantPatch
{
    private static readonly HashSet<int> s_assistedFloats = new();

    [HarmonyPostfix]
    public static void Postfix(FishingFloat __instance)
    {
        if (!PluginConfig.ModEnabled.Value || !PluginConfig.EnableFishingAssistant.Value) return;

        // Verify float has an active catch hooked
        Fish catchFish = __instance.GetCatch();
        if (catchFish == null) return;

        int floatId = __instance.GetInstanceID();
        if (s_assistedFloats.Contains(floatId)) return;

        Character owner = __instance.GetOwner();
        if (owner is not Player player) return;

        // Find a nearby happy, tamed seal
        SealBehaviorController? assistantSeal = FindEligibleAssistantSeal(player, __instance.transform.position);
        if (assistantSeal == null) return;

        // Mark as evaluated so we only roll once per hook
        s_assistedFloats.Add(floatId);

        // Roll probability
        float roll = UnityEngine.Random.Range(0f, 100f);
        if (roll > PluginConfig.FishingAssistChance.Value) return;

        // Perform retrieval!
        PerformSealRetrieve(assistantSeal, __instance, catchFish, player);
    }

    private static SealBehaviorController? FindEligibleAssistantSeal(Player player, Vector3 floatPos)
    {
        float maxDist = PluginConfig.FishingAssistMaxDistance.Value;
        SealBehaviorController? bestSeal = null;
        float bestDist = float.MaxValue;
        string playerName = player.GetPlayerName();

        foreach (var seal in SealBehaviorController.Instances)
        {
            if (seal == null || !seal.IsHappyAndTamed) continue;

            Tameable tameable = seal.GetComponent<Tameable>();
            ZNetView nview = seal.GetComponent<ZNetView>();
            if (tameable == null || nview == null || !nview.IsValid()) continue;

            // Must follow player or be in close proximity
            string follow = nview.GetZDO().GetString(ZDOVars.s_follow);
            bool isFollowingPlayer = !string.IsNullOrEmpty(follow) && follow == playerName;
            float distToPlayer = Vector3.Distance(seal.transform.position, player.transform.position);

            if (!isFollowingPlayer && distToPlayer > maxDist) continue;

            float distToFloat = Vector3.Distance(seal.transform.position, floatPos);
            if (distToFloat <= maxDist && distToFloat < bestDist)
            {
                bestDist = distToFloat;
                bestSeal = seal;
            }
        }

        return bestSeal;
    }

    private static void PerformSealRetrieve(SealBehaviorController seal, FishingFloat fishingFloat, Fish fish, Player player)
    {
        Tameable tameable = seal.GetComponent<Tameable>();
        string sealName = tameable != null ? tameable.GetHoverName() : "Seal";

        // Splash effect and swim towards fish
        Vector3 catchPos = fish.transform.position;
        seal.transform.position = Vector3.Lerp(seal.transform.position, catchPos, 0.75f);

        // Soothe / happy heart emote
        tameable?.m_sootheEffect?.Create(seal.transform.position + Vector3.up * 0.5f, Quaternion.identity);

        // Execute catch
        string catchMsg = FishingFloat.Catch(fish, player);
        player.Message(MessageHud.MessageType.Center, $"★ {sealName} caught the fish for you!\n{catchMsg}");

        // Clean up float
        fishingFloat.SetCatch(null);
        fish.OnHooked(null);
        if (fishingFloat.m_nview != null && fishingFloat.m_nview.IsValid())
        {
            fishingFloat.m_nview.Destroy();
        }

        ZLog.Log($"[SealCompanion] {sealName} assisted player {player.GetPlayerName()} with catching {fish.GetHoverName()}");
    }

    [HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.OnDestroy))]
    [HarmonyPrefix]
    public static void OnDestroy_Prefix(FishingFloat __instance)
    {
        s_assistedFloats.Remove(__instance.GetInstanceID());
    }
}
