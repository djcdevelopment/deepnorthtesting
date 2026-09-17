namespace SealCompanion.Configuration;

using BepInEx.Configuration;

public static class PluginConfig
{
    public static ConfigEntry<bool> ModEnabled = null!;

    // Taming & Feeding
    public static ConfigEntry<float> TamingTime = null!;
    public static ConfigEntry<float> FedDuration = null!;
    public static ConfigEntry<bool> IncludePufferfish = null!;

    // Coastline Kiting & Curiosity Training
    public static ConfigEntry<float> KiteTameTimeNormal = null!;
    public static ConfigEntry<float> KiteTameTimeOneStar = null!;
    public static ConfigEntry<float> KiteTameTimeTwoStar = null!;
    public static ConfigEntry<float> CuriosityDecayDuration = null!;
    public static ConfigEntry<float> MinFishSpacingDistance = null!;
    public static ConfigEntry<float> CuriosityBoostPerFish = null!;
    public static ConfigEntry<bool> EnableCuriosityHUD = null!;

    // Combat & Master Defense
    public static ConfigEntry<float> SealHealth = null!;
    public static ConfigEntry<float> SealAttackDamage = null!;
    public static ConfigEntry<bool> EnableMasterDefense = null!;
    public static ConfigEntry<float> MasterDefenseRange = null!;

    // Fishing Assistant
    public static ConfigEntry<bool> EnableFishingAssistant = null!;
    public static ConfigEntry<float> FishingAssistChance = null!;
    public static ConfigEntry<float> FishingAssistMaxDistance = null!;

    // Water & Hot Tub Behaviors
    public static ConfigEntry<bool> EnableHotTubAttraction = null!;
    public static ConfigEntry<float> HotTubSearchRadius = null!;
    public static ConfigEntry<float> DehydrationGracePeriod = null!;
    public static ConfigEntry<float> DehydrationHungerMultiplier = null!;

    // Boat Behavior
    public static ConfigEntry<bool> BoatDisembarkNearShore = null!;
    public static ConfigEntry<float> DisembarkWaterDepthThreshold = null!;

    public static void BindConfig(ConfigFile config)
    {
        ModEnabled = config.Bind(
            "General",
            "Enabled",
            true,
            "Enable or disable the Seal Companion mod entirely."
        );

        TamingTime = config.Bind(
            "Taming",
            "TamingTime",
            1800f,
            "Seconds required to tame a seal while continuously fed and calm (unalerted)."
        );

        FedDuration = config.Bind(
            "Taming",
            "FedDuration",
            1800f,
            "Duration in seconds that a seal remains fed after eating a fish."
        );

        IncludePufferfish = config.Bind(
            "Taming",
            "IncludePufferfish",
            false,
            "Whether seals will eat toxic Pufferfish (Fish9)."
        );

        KiteTameTimeNormal = config.Bind(
            "CuriosityTraining",
            "KiteTameTimeNormal",
            120f,
            "Total kiting training time required in seconds to tame a normal (0-star) seal (default: 2 minutes)."
        );

        KiteTameTimeOneStar = config.Bind(
            "CuriosityTraining",
            "KiteTameTimeOneStar",
            180f,
            "Total kiting training time required in seconds to tame a 1-star seal (default: 3 minutes)."
        );

        KiteTameTimeTwoStar = config.Bind(
            "CuriosityTraining",
            "KiteTameTimeTwoStar",
            300f,
            "Total kiting training time required in seconds to tame a 2-star seal (default: 5 minutes)."
        );

        CuriosityDecayDuration = config.Bind(
            "CuriosityTraining",
            "CuriosityDecayDuration",
            35f,
            "Seconds that seal curiosity lasts without being lured to a new spaced fish before expiring."
        );

        MinFishSpacingDistance = config.Bind(
            "CuriosityTraining",
            "MinFishSpacingDistance",
            8.0f,
            "Minimum distance in meters between consecutive fish drops required to count as a valid training gate."
        );

        CuriosityBoostPerFish = config.Bind(
            "CuriosityTraining",
            "CuriosityBoostPerFish",
            35.0f,
            "Percentage (0 to 100) of curiosity gauge restored whenever the seal reaches a validly spaced fish."
        );

        EnableCuriosityHUD = config.Bind(
            "CuriosityTraining",
            "EnableCuriosityHUD",
            true,
            "Whether to render the TotemSentinel-style HUD card displaying taming countdown, curiosity gauge, and kiting status."
        );

        SealHealth = config.Bind(
            "CombatAndDefense",
            "SealHealth",
            160f,
            "Base maximum health for seals, giving them thick companion durability."
        );

        SealAttackDamage = config.Bind(
            "CombatAndDefense",
            "SealAttackDamage",
            35f,
            "Total physical bite damage dealt when a tamed seal attacks enemies."
        );

        EnableMasterDefense = config.Bind(
            "CombatAndDefense",
            "EnableMasterDefense",
            true,
            "Whether tamed seals actively engage hostile monsters attacking their master."
        );

        MasterDefenseRange = config.Bind(
            "CombatAndDefense",
            "MasterDefenseRange",
            25f,
            "Radius around the master to scan for hostile attackers to intercept."
        );

        EnableFishingAssistant = config.Bind(
            "FishingAssistant",
            "EnableFishingAssistant",
            true,
            "Whether happy, tamed seals assist the player with hooked fish."
        );

        FishingAssistChance = config.Bind(
            "FishingAssistant",
            "FishingAssistChance",
            35.0f,
            new ConfigDescription("Percentage chance (0 to 100) that a nearby happy tamed seal retrieves a hooked fish.", new AcceptableValueRange<float>(0f, 100f))
        );

        FishingAssistMaxDistance = config.Bind(
            "FishingAssistant",
            "FishingAssistMaxDistance",
            30.0f,
            "Maximum distance between the fishing float and the seal to trigger a retrieval assist."
        );

        EnableHotTubAttraction = config.Bind(
            "WaterAndHotTub",
            "EnableHotTubAttraction",
            true,
            "Whether idle tamed seals in bases are drawn to soak in hot tubs (piece_bathtub)."
        );

        HotTubSearchRadius = config.Bind(
            "WaterAndHotTub",
            "HotTubSearchRadius",
            25.0f,
            "Radius around an idle seal to scan for built hot tubs."
        );

        DehydrationGracePeriod = config.Bind(
            "WaterAndHotTub",
            "DehydrationGracePeriod",
            600f,
            "Time in seconds a seal can stay out of water/hot tub before dehydration accelerates hunger."
        );

        DehydrationHungerMultiplier = config.Bind(
            "WaterAndHotTub",
            "DehydrationHungerMultiplier",
            2.5f,
            "Multiplier to hunger consumption rate when the seal is dehydrated (requires extra feeding to stay happy)."
        );

        BoatDisembarkNearShore = config.Bind(
            "BoatBehavior",
            "BoatDisembarkNearShore",
            true,
            "Whether seals riding boats/rafts jump into the water when approaching the shoreline."
        );

        DisembarkWaterDepthThreshold = config.Bind(
            "BoatBehavior",
            "DisembarkWaterDepthThreshold",
            2.8f,
            "Water depth in meters below which the boat is considered close to shore, prompting the seal to jump off."
        );
    }
}
