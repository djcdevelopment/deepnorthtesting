namespace SealCompanion.Patches;

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SealCompanion.Components;
using SealCompanion.Configuration;
using UnityEngine;

[HarmonyPatch]
public static class SealPrefabPatch
{
    private static bool s_configured = false;
    private static GameObject? s_sealBitePrefab = null;

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    [HarmonyPostfix]
    public static void ZNetScene_Awake_Postfix(ZNetScene __instance)
    {
        if (s_configured || !PluginConfig.ModEnabled.Value) return;

        ConfigureSealPrefabs(__instance);
        s_configured = true;
    }

    public static void ConfigureSealPrefabs(ZNetScene netScene)
    {
        GameObject sealPrefab = netScene.GetPrefab("Seal");
        if (sealPrefab == null)
        {
            ZLog.LogWarning("[SealCompanion] 'Seal' prefab not found in ZNetScene.");
            return;
        }

        GameObject pupPrefab = netScene.GetPrefab("Seal_Pup");
        Tameable? referenceTameable = netScene.GetPrefab("Boar")?.GetComponent<Tameable>() 
                                   ?? netScene.GetPrefab("Wolf")?.GetComponent<Tameable>();

        // 1. Configure Adult Seal (Humanoid, Defense, Combat Attack, Diet, Taming)
        ConfigureSingleSealPrefab(sealPrefab, netScene, referenceTameable, isPup: false);

        // 2. Configure Seal Pup if available
        if (pupPrefab != null)
        {
            ConfigureSingleSealPrefab(pupPrefab, netScene, referenceTameable, isPup: true);
        }

        ZLog.Log("[SealCompanion] Successfully upgraded Seal prefabs to Humanoid with taming, master defense, bite attack, and behavior controllers.");
    }

    private static void ConfigureSingleSealPrefab(GameObject prefab, ZNetScene netScene, Tameable? refTameable, bool isPup)
    {
        // 1. Upgrade Character to Humanoid so MonsterAI, combat attacks, and item consumption work natively
        Humanoid humanoid = EnsureHumanoid(prefab);

        // 2. Convert AnimalAI to MonsterAI if necessary to support feeding, taming & defense
        var animalAI = prefab.GetComponent<AnimalAI>();
        var monsterAI = prefab.GetComponent<MonsterAI>();

        if (monsterAI == null && animalAI != null)
        {
            monsterAI = prefab.AddComponent<MonsterAI>();
            monsterAI.m_viewRange = animalAI.m_viewRange;
            monsterAI.m_hearRange = animalAI.m_hearRange;
            monsterAI.m_afraidOfFire = animalAI.m_afraidOfFire;
            monsterAI.m_avoidFire = true;
            UnityEngine.Object.DestroyImmediate(animalAI, true);
        }
        else if (monsterAI == null)
        {
            monsterAI = prefab.AddComponent<MonsterAI>();
        }

        monsterAI.m_character = humanoid;
        monsterAI.m_animator = prefab.GetComponent<ZSyncAnimation>();
        monsterAI.m_body = prefab.GetComponent<Rigidbody>();
        monsterAI.m_nview = prefab.GetComponent<ZNetView>();

        // Combat AI parameters
        monsterAI.m_alertRange = 20f;
        monsterAI.m_viewRange = 30f;
        monsterAI.m_hearRange = 20f;
        monsterAI.m_fleeIfNotAlerted = false;
        monsterAI.m_fleeInterval = 0f;
        monsterAI.m_circleTargetInterval = 3f;
        monsterAI.m_circleTargetDuration = 2f;
        monsterAI.m_circleTargetDistance = 2.5f;

        // Build fish diet list
        List<ItemDrop> diet = BuildFishDiet();
        monsterAI.m_consumeItems = diet;
        monsterAI.m_consumeSearchRange = 15f;
        monsterAI.m_consumeRange = 2f;
        monsterAI.m_consumeSearchInterval = 10f;

        // Health, Armor, and Damage Resistances (Blubber Defense)
        humanoid.m_health = isPup ? (PluginConfig.SealHealth.Value * 0.4f) : PluginConfig.SealHealth.Value;
        humanoid.m_damageModifiers.m_blunt = HitData.DamageModifier.Resistant;
        humanoid.m_damageModifiers.m_frost = HitData.DamageModifier.Resistant;
        humanoid.m_damageModifiers.m_pierce = HitData.DamageModifier.Resistant;

        // Equip melee bite attack weapon for defense
        if (!isPup)
        {
            AttachSealBiteAttack(humanoid, netScene);
        }

        // Ensure Tameable component
        var tameable = prefab.GetComponent<Tameable>();
        if (tameable == null)
        {
            tameable = prefab.AddComponent<Tameable>();
        }

        tameable.m_character = humanoid;
        tameable.m_monsterAI = monsterAI;
        tameable.m_nview = prefab.GetComponent<ZNetView>();
        tameable.m_commandable = !isPup; // Only adults are commandable to follow/stay
        tameable.m_fedDuration = PluginConfig.FedDuration.Value;
        tameable.m_tamingTime = PluginConfig.TamingTime.Value;
        tameable.m_startsTamed = false;
        tameable.m_tameText = "$hud_tame";

        if (refTameable != null)
        {
            tameable.m_sootheEffect = refTameable.m_sootheEffect;
            tameable.m_petEffect = refTameable.m_petEffect;
            tameable.m_tamedEffect = refTameable.m_tamedEffect;
        }

        // Add custom behavior controller (water, hydration, hot tub, boat disembark, master defense)
        if (prefab.GetComponent<SealBehaviorController>() == null)
        {
            prefab.AddComponent<SealBehaviorController>();
        }

        // Keep seals strictly unbreedable: remove any Procreation or Growup components
        var procreation = prefab.GetComponent<Procreation>();
        if (procreation != null)
        {
            UnityEngine.Object.DestroyImmediate(procreation, true);
        }

        var growup = prefab.GetComponent<Growup>();
        if (growup != null)
        {
            UnityEngine.Object.DestroyImmediate(growup, true);
        }
    }

    private static Humanoid EnsureHumanoid(GameObject prefab)
    {
        var humanoid = prefab.GetComponent<Humanoid>();
        if (humanoid != null) return humanoid;

        var oldChar = prefab.GetComponent<Character>();
        if (oldChar == null)
        {
            return prefab.AddComponent<Humanoid>();
        }

        humanoid = prefab.AddComponent<Humanoid>();

        // Copy all non-static fields from Character base class to Humanoid
        Type? currentType = typeof(Character);
        while (currentType != null && currentType != typeof(MonoBehaviour) && currentType != typeof(Behaviour) && currentType != typeof(Component) && currentType != typeof(UnityEngine.Object))
        {
            FieldInfo[] fields = currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (FieldInfo field in fields)
            {
                try
                {
                    field.SetValue(humanoid, field.GetValue(oldChar));
                }
                catch (Exception ex)
                {
                    ZLog.LogWarning($"[SealCompanion] Failed to copy field {field.Name}: {ex.Message}");
                }
            }
            currentType = currentType.BaseType;
        }

        // Re-target any other components referencing oldChar to the new Humanoid
        foreach (var comp in prefab.GetComponentsInChildren<Component>(true))
        {
            if (comp == null || comp == oldChar || comp == humanoid) continue;

            Type compType = comp.GetType();
            while (compType != null && compType != typeof(MonoBehaviour) && compType != typeof(Component) && compType != typeof(UnityEngine.Object))
            {
                FieldInfo[] fields = compType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (FieldInfo f in fields)
                {
                    if (typeof(Character).IsAssignableFrom(f.FieldType))
                    {
                        try
                        {
                            var val = f.GetValue(comp);
                            if (ReferenceEquals(val, oldChar))
                            {
                                f.SetValue(comp, humanoid);
                            }
                        }
                        catch { }
                    }
                }
                compType = compType.BaseType;
            }
        }

        // Remove old Character component so only Humanoid is present
        UnityEngine.Object.DestroyImmediate(oldChar, true);

        return humanoid;
    }

    private static void AttachSealBiteAttack(Humanoid humanoid, ZNetScene netScene)
    {
        if (s_sealBitePrefab == null)
        {
            GameObject baseAttack = netScene.GetPrefab("boar_base_attack");
            if (baseAttack != null)
            {
                s_sealBitePrefab = UnityEngine.Object.Instantiate(baseAttack);
                s_sealBitePrefab.name = "seal_bite_attack";
                if (s_sealBitePrefab.TryGetComponent<ItemDrop>(out var itemDrop))
                {
                    itemDrop.m_itemData.m_shared.m_name = "Seal Bite";
                    float totalDmg = PluginConfig.SealAttackDamage.Value;
                    itemDrop.m_itemData.m_shared.m_damages.m_slash = totalDmg * 0.6f;
                    itemDrop.m_itemData.m_shared.m_damages.m_blunt = totalDmg * 0.4f;
                    itemDrop.m_itemData.m_shared.m_damages.m_pierce = 0f;
                }
                netScene.m_prefabs.Add(s_sealBitePrefab);
                netScene.m_namedPrefabs[s_sealBitePrefab.name.GetStableHashCode()] = s_sealBitePrefab;
            }
        }

        if (s_sealBitePrefab != null)
        {
            humanoid.m_defaultItems = new GameObject[] { s_sealBitePrefab };
        }
    }

    private static List<ItemDrop> BuildFishDiet()
    {
        List<ItemDrop> diet = new();
        string[] fishNames = new[]
        {
            "Fish1", "Fish2", "Fish3", "Fish4_cave", "Fish5",
            "Fish6", "Fish7", "Fish8", "Fish10", "Fish11", "Fish12",
            "FishRaw", "FishCooked"
        };

        foreach (string name in fishNames)
        {
            GameObject itemObj = ZNetScene.instance.GetPrefab(name);
            if (itemObj != null && itemObj.TryGetComponent<ItemDrop>(out var itemDrop))
            {
                diet.Add(itemDrop);
            }
        }

        if (PluginConfig.IncludePufferfish.Value)
        {
            GameObject puffer = ZNetScene.instance.GetPrefab("Fish9");
            if (puffer != null && puffer.TryGetComponent<ItemDrop>(out var pufferDrop))
            {
                diet.Add(pufferDrop);
            }
        }

        return diet;
    }

    #region Defensive MonsterAI Safety Patches

    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateTarget))]
    [HarmonyPrefix]
    public static bool MonsterAI_UpdateTarget_Prefix(MonsterAI __instance, ref Humanoid humanoid, float dt, ref bool canHearTarget, ref bool canSeeTarget)
    {
        if (humanoid == null)
        {
            if (__instance.m_character is Humanoid hum)
            {
                humanoid = hum;
            }
            else
            {
                // Non-humanoid character: bypass UpdateTarget safely to prevent crashing MonoUpdaters.FixedUpdate()
                canHearTarget = false;
                canSeeTarget = false;
                return false;
            }
        }
        return true;
    }

    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateConsumeItem))]
    [HarmonyPrefix]
    public static bool MonsterAI_UpdateConsumeItem_Prefix(MonsterAI __instance, ref Humanoid humanoid, float dt)
    {
        if (humanoid == null)
        {
            if (__instance.m_character is Humanoid hum)
            {
                humanoid = hum;
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.SelectBestAttack))]
    [HarmonyPrefix]
    public static bool MonsterAI_SelectBestAttack_Prefix(MonsterAI __instance, ref Humanoid humanoid, float dt, ref ItemDrop.ItemData __result)
    {
        if (humanoid == null)
        {
            if (__instance.m_character is Humanoid hum)
            {
                humanoid = hum;
            }
            else
            {
                __result = null!;
                return false;
            }
        }

        // If seal is currently curious and in training, suppress attacks against players
        if (__instance.TryGetComponent<SealBehaviorController>(out var seal) && seal.IsCurious)
        {
            Character target = __instance.GetTargetCreature();
            if (target != null && target.IsPlayer())
            {
                __result = null!;
                return false;
            }
        }

        return true;
    }

    [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.IsEnemy), new Type[] { typeof(Character), typeof(Character) })]
    [HarmonyPrefix]
    public static bool BaseAI_IsEnemy_Prefix(Character a, Character b, ref bool __result)
    {
        if (a == null || b == null) return true;

        if (a.IsPlayer() && b.TryGetComponent<SealBehaviorController>(out var sealB) && sealB.IsCurious)
        {
            __result = false;
            return false;
        }

        if (b.IsPlayer() && a.TryGetComponent<SealBehaviorController>(out var sealA) && sealA.IsCurious)
        {
            __result = false;
            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(Tameable), nameof(Tameable.OnConsumedItem))]
    [HarmonyPostfix]
    public static void Tameable_OnConsumedItem_Postfix(Tameable __instance, ItemDrop item)
    {
        if (__instance.TryGetComponent<SealBehaviorController>(out var sealController))
        {
            Vector3 pos = item != null ? item.transform.position : __instance.transform.position;
            sealController.OnFishConsumed(pos, Player.GetClosestPlayer(pos, 25f));
        }
    }

    #endregion
}
