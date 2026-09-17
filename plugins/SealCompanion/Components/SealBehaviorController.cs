namespace SealCompanion.Components;

using System;
using System.Collections.Generic;
using SealCompanion.Configuration;
using SealCompanion.HUD;
using UnityEngine;

public class SealBehaviorController : MonoBehaviour
{
    private Character _character = null!;
    private MonsterAI _monsterAI = null!;
    private Tameable _tameable = null!;
    private ZNetView _nview = null!;

    private float _lastHydratedTime;
    private float _periodicTimer;
    private float _hotTubCheckTimer;
    private bool _inHotTub;
    private float _sootheEmoteTimer;
    private float _fishSearchTimer;
    private float _lastConsumedTime;

    private static int s_itemMask = 0;
    private static readonly List<SealBehaviorController> s_instances = new();

    public static IReadOnlyList<SealBehaviorController> Instances => s_instances;

    // Coastline Kiting & Curiosity Training Properties
    public bool IsCurious { get; internal set; }
    public float TameSessionTotalTime { get; internal set; }
    public float TameSessionTimeRemaining { get; internal set; }
    public float CuriosityDecayDuration { get; internal set; } = 35f;
    public float CuriosityTimeRemaining { get; internal set; }
    public float CuriosityPercent => CuriosityDecayDuration > 0f ? Mathf.Clamp01(CuriosityTimeRemaining / CuriosityDecayDuration) : 0f;
    public Vector3 LastEatenFishPosition { get; internal set; } = Vector3.zero;
    public int FishLuredCount { get; internal set; }
    public bool LastGatePassed { get; internal set; } = true;
    public string StatusMessage { get; internal set; } = string.Empty;
    public Player? TrainingPlayer { get; internal set; }
    public int SealStarLevel => _character != null ? Mathf.Max(0, _character.GetLevel() - 1) : 0;

    public bool IsHappyAndTamed => _tameable != null && _tameable.IsTamed() && !_tameable.IsHungry() && !_monsterAI.IsAlerted();

    private void Awake()
    {
        EnsureComponents();
        _lastHydratedTime = Time.time;
        s_instances.Add(this);
    }

    private void EnsureComponents()
    {
        if (_character == null) _character = GetComponent<Character>();
        if (_monsterAI == null) _monsterAI = GetComponent<MonsterAI>();
        if (_tameable == null) _tameable = GetComponent<Tameable>();
        if (_nview == null) _nview = GetComponent<ZNetView>();
    }

    private void OnDestroy()
    {
        s_instances.Remove(this);
    }

    private void Update()
    {
        EnsureComponents();
        if (_nview == null || !_nview.IsValid()) return;
        if (_character == null || _monsterAI == null) return;

        float dt = Time.deltaTime;

        // Coastline Kiting & Training loop runs every frame when untamed
        if (_tameable != null && !_tameable.IsTamed())
        {
            UpdateKitingAndCuriosity(dt);
        }

        // Periodic update (1-second tick) for secondary behaviors
        _periodicTimer += dt;
        if (_periodicTimer >= 1f)
        {
            _periodicTimer = 0f;
            UpdateHydration(1f);
            UpdateBoatDisembark();
            UpdateMasterDefense();
            UpdateHotTubBehavior();
        }
    }

    #region Coastline Kiting & Curiosity Training

    private void UpdateKitingAndCuriosity(float dt)
    {
        if (IsCurious)
        {
            // 1. Decrement kiting session countdown & curiosity gauge
            TameSessionTimeRemaining -= dt;
            CuriosityTimeRemaining -= dt;

            // Check if taming finished!
            if (TameSessionTimeRemaining <= 0f)
            {
                CompleteKitingTame();
                return;
            }

            // Check if curiosity expired!
            if (CuriosityTimeRemaining <= 0f)
            {
                LoseCuriosity();
                return;
            }

            // Keep training player reference valid
            if (TrainingPlayer == null || TrainingPlayer.IsDead())
            {
                TrainingPlayer = Player.GetClosestPlayer(transform.position, 35f);
                if (TrainingPlayer == null)
                {
                    LoseCuriosity();
                    return;
                }
            }

            // Keep vanilla MonsterAI target clear so it doesn't try to attack the trainer
            if (_monsterAI.GetTargetCreature() == TrainingPlayer)
            {
                _monsterAI.SetTarget(null);
            }

            // 2. Scan for fish checkpoint diversion
            ItemDrop? nearbyFish = FindNearbyFish(14f);
            if (nearbyFish != null)
            {
                // Divert to fish!
                Vector3 fishPos = nearbyFish.transform.position;
                float distToFish = Vector3.Distance(transform.position, fishPos);
                _monsterAI.LookAt(fishPos);

                if (distToFish <= 2.0f)
                {
                    ConsumeFish(nearbyFish, TrainingPlayer);
                }
                else
                {
                    _monsterAI.MoveTo(dt, fishPos, 1.2f, false);
                }
            }
            else
            {
                // 3. No fish immediately visible: kite / follow training player
                Vector3 playerPos = TrainingPlayer.transform.position;
                float distToPlayer = Vector3.Distance(transform.position, playerPos);

                if (distToPlayer > 3.0f)
                {
                    bool run = distToPlayer > 8.0f;
                    _monsterAI.MoveTo(dt, playerPos, 2.5f, run);
                }
                else
                {
                    _monsterAI.LookAt(playerPos);
                    _monsterAI.StopMoving();
                }
            }
        }
        else
        {
            // Untamed, not yet curious: scan periodically for first fish drop
            _fishSearchTimer += dt;
            if (_fishSearchTimer >= 0.5f)
            {
                _fishSearchTimer = 0f;
                ItemDrop? nearbyFish = FindNearbyFish(12f);
                if (nearbyFish != null)
                {
                    Vector3 fishPos = nearbyFish.transform.position;
                    float distToFish = Vector3.Distance(transform.position, fishPos);
                    _monsterAI.LookAt(fishPos);

                    if (distToFish <= 2.0f)
                    {
                        Player? closestPlayer = Player.GetClosestPlayer(transform.position, 25f);
                        ConsumeFish(nearbyFish, closestPlayer);
                    }
                    else
                    {
                        _monsterAI.MoveTo(dt, fishPos, 1.2f, false);
                    }
                }
            }
        }
    }

    public void OnFishConsumed(Vector3 fishPos, Player? player)
    {
        if (Time.time - _lastConsumedTime < 0.2f) return;
        _lastConsumedTime = Time.time;

        if (_tameable == null || _tameable.IsTamed()) return;

        // Reset vanilla feeding timer so vanilla Tameable stays calm
        _tameable.ResetFeedingTimer();

        if (!IsCurious)
        {
            // FIRST FISH EATEN: Awaken Curiosity!
            IsCurious = true;
            TrainingPlayer = player ?? Player.GetClosestPlayer(transform.position, 25f);

            int level = _character != null ? _character.GetLevel() : 1;
            float totalTime = level switch
            {
                2 => PluginConfig.KiteTameTimeOneStar.Value,
                >= 3 => PluginConfig.KiteTameTimeTwoStar.Value,
                _ => PluginConfig.KiteTameTimeNormal.Value
            };

            TameSessionTotalTime = totalTime;
            TameSessionTimeRemaining = totalTime;
            CuriosityDecayDuration = PluginConfig.CuriosityDecayDuration.Value;
            CuriosityTimeRemaining = CuriosityDecayDuration;
            LastEatenFishPosition = fishPos;
            FishLuredCount = 1;
            LastGatePassed = true;
            StatusMessage = "CURIOUS! Lead along coast";

            // Neutral training emote feedback
            _tameable.m_sootheEffect?.Create(transform.position + Vector3.up * 0.5f, Quaternion.identity);

            if (TrainingPlayer != null)
            {
                TrainingPlayer.Message(MessageHud.MessageType.Center, "<color=#54D7FF>★ Seal is curious!</color>\nLead it along the coast and drop spaced fish (≥8m).");
            }

            ZLog.Log($"[SealCompanion] {_character?.GetHoverName()} became curious! Rank: {level - 1}★, Time: {totalTime}s.");
        }
        else
        {
            // ALREADY CURIOUS: Check fish spacing gate
            float distFromLast = Vector3.Distance(fishPos, LastEatenFishPosition);
            float minSpacing = PluginConfig.MinFishSpacingDistance.Value;

            if (distFromLast >= minSpacing)
            {
                // Valid checkpoint gate!
                FishLuredCount++;
                LastEatenFishPosition = fishPos;
                LastGatePassed = true;

                float boost = CuriosityDecayDuration * (PluginConfig.CuriosityBoostPerFish.Value / 100f);
                CuriosityTimeRemaining = Mathf.Min(CuriosityDecayDuration, CuriosityTimeRemaining + boost);
                StatusMessage = $"+{PluginConfig.CuriosityBoostPerFish.Value:0}% BOOST! (Gate #{FishLuredCount})";

                // Neutral training emote feedback
                _tameable.m_sootheEffect?.Create(transform.position + Vector3.up * 0.5f, Quaternion.identity);

                if (TrainingPlayer != null)
                {
                    TrainingPlayer.Message(MessageHud.MessageType.TopLeft, $"<color=#54D7FF>🐟 Gate #{FishLuredCount} passed!</color> (+{PluginConfig.CuriosityBoostPerFish.Value:0}% Curiosity)");
                }

                ZLog.Log($"[SealCompanion] Gate #{FishLuredCount} passed! Spacing: {distFromLast:F1}m >= {minSpacing:F1}m. Remaining curiosity: {CuriosityTimeRemaining:F1}s.");
            }
            else
            {
                // Fish dropped too close to last eaten fish! Clumped drop!
                LastGatePassed = false;
                StatusMessage = $"TOO CLOSE! (< {minSpacing:0}m)";

                if (TrainingPlayer != null)
                {
                    TrainingPlayer.Message(MessageHud.MessageType.TopLeft, $"<color=#FF7043>⚠ Fish too close ({distFromLast:F1}m < {minSpacing:0}m)!</color> Drop fish further along coast.");
                }

                ZLog.Log($"[SealCompanion] Fish eaten too close ({distFromLast:F1}m < {minSpacing:F1}m). No curiosity boost.");
            }
        }

        SealCuriosityPanel.SetActiveSeal(this);
    }

    public void ConsumeFish(ItemDrop fish, Player? player)
    {
        if (fish == null || fish.gameObject == null) return;
        Vector3 pos = fish.transform.position;

        if (_monsterAI != null && _monsterAI.m_animator != null)
        {
            _monsterAI.m_animator.SetTrigger("consume");
        }

        fish.RemoveOne();
        OnFishConsumed(pos, player);
    }

    private void CompleteKitingTame()
    {
        IsCurious = false;
        StatusMessage = "TAMED!";

        if (_nview != null && !_nview.IsOwner())
        {
            _nview.ClaimOwnership();
        }

        if (_tameable != null)
        {
            _tameable.Tame();
        }
        else if (_monsterAI != null)
        {
            _monsterAI.MakeTame();
            _character?.SetTamed(true);
        }

        if (TrainingPlayer != null)
        {
            TrainingPlayer.Message(MessageHud.MessageType.Center, "<color=#69F0AE>❤ Seal companion successfully trained!</color>\nIt is now tamed and loyal to you.");
        }

        SealCuriosityPanel.OnSealTamed(this);
        ZLog.Log($"[SealCompanion] {_character?.GetHoverName()} successfully trained and tamed via coastline kiting!");
    }

    private void LoseCuriosity()
    {
        IsCurious = false;
        CuriosityTimeRemaining = 0f;
        StatusMessage = "CURIOSITY LOST";

        if (TrainingPlayer != null)
        {
            TrainingPlayer.Message(MessageHud.MessageType.Center, "<color=#FF7043>Seal lost curiosity and wandered off.</color>");
        }

        _monsterAI.SetTarget(null);
        _monsterAI.SetAlerted(false);

        SealCuriosityPanel.OnCuriosityLost(this);
        ZLog.Log($"[SealCompanion] {_character?.GetHoverName()} lost curiosity and abandoned training.");
    }

    private ItemDrop? FindNearbyFish(float searchRadius)
    {
        if (s_itemMask == 0)
        {
            s_itemMask = LayerMask.GetMask("item");
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, searchRadius, s_itemMask);
        ItemDrop? closestFish = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.attachedRigidbody == null) continue;
            var itemDrop = hit.attachedRigidbody.GetComponent<ItemDrop>();
            if (itemDrop == null || !itemDrop.GetComponent<ZNetView>().IsValid()) continue;
            if (!IsFishItem(itemDrop)) continue;

            float dist = Vector3.Distance(transform.position, itemDrop.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestFish = itemDrop;
            }
        }

        return closestFish;
    }

    private bool IsFishItem(ItemDrop item)
    {
        if (item == null || item.m_itemData == null || item.m_itemData.m_dropPrefab == null) return false;
        string name = item.m_itemData.m_dropPrefab.name;
        if (name.StartsWith("Fish", StringComparison.OrdinalIgnoreCase))
        {
            if (!PluginConfig.IncludePufferfish.Value && name.Equals("Fish9", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return true;
        }
        return false;
    }

    #endregion

    #region Master Defense & Companion Combat

    private void UpdateMasterDefense()
    {
        if (!PluginConfig.EnableMasterDefense.Value) return;
        if (_tameable == null || !_tameable.IsTamed()) return;

        Character currentTarget = _monsterAI.GetTargetCreature();
        if (currentTarget != null && !currentTarget.IsDead())
        {
            return;
        }

        GameObject followObj = _monsterAI.GetFollowTarget();
        Player? master = null;
        if (followObj != null)
        {
            master = followObj.GetComponent<Player>();
        }

        if (master == null)
        {
            master = Player.GetClosestPlayer(transform.position, PluginConfig.MasterDefenseRange.Value);
        }

        if (master == null) return;

        Character? threat = FindThreatToMaster(master, PluginConfig.MasterDefenseRange.Value);
        if (threat != null)
        {
            _monsterAI.SetTarget(threat);
            _monsterAI.SetAlerted(true);
            ZLog.Log($"[SealCompanion] {_tameable.GetHoverName()} engages {threat.GetHoverName()} to protect master {master.GetPlayerName()}!");
        }
    }

    private Character? FindThreatToMaster(Player master, float scanRange)
    {
        Vector3 masterPos = master.transform.position;
        Character? bestThreat = null;
        float bestDist = float.MaxValue;

        foreach (Character c in Character.GetAllCharacters())
        {
            if (c == null || c.IsDead() || c.IsPlayer() || c.IsTamed()) continue;

            if (BaseAI.IsEnemy(master, c))
            {
                float dist = Vector3.Distance(c.transform.position, masterPos);
                if (dist <= scanRange && dist < bestDist)
                {
                    bestDist = dist;
                    bestThreat = c;
                }
            }
        }

        return bestThreat;
    }

    #endregion

    #region Hydration, Boat & Hot Tub Behaviors

    private void UpdateHydration(float dt)
    {
        bool inWater = _character.InWater() || _character.IsSwimming();

        if (inWater || _inHotTub)
        {
            _lastHydratedTime = Time.time;

            if (_inHotTub && IsHappyAndTamed)
            {
                _sootheEmoteTimer += 1f;
                if (_sootheEmoteTimer >= 12f)
                {
                    _sootheEmoteTimer = 0f;
                    _tameable.m_sootheEffect?.Create(transform.position + Vector3.up * 0.5f, Quaternion.identity);
                }
            }
            return;
        }

        if (Time.time - _lastHydratedTime > PluginConfig.DehydrationGracePeriod.Value)
        {
            if (_nview.IsOwner() && _tameable != null && !_tameable.IsHungry())
            {
                float multiplier = Mathf.Max(1f, PluginConfig.DehydrationHungerMultiplier.Value);
                float penaltySeconds = (multiplier - 1f) * 1f;
                long penaltyTicks = TimeSpan.FromSeconds(penaltySeconds).Ticks;

                long lastFeeding = _nview.GetZDO().GetLong(ZDOVars.s_tameLastFeeding, 0L);
                if (lastFeeding > 0)
                {
                    _nview.GetZDO().Set(ZDOVars.s_tameLastFeeding, lastFeeding - penaltyTicks);
                }
            }
        }
    }

    private void UpdateBoatDisembark()
    {
        if (!PluginConfig.BoatDisembarkNearShore.Value) return;

        Ship standingShip = _character.GetStandingOnShip();
        if (standingShip == null) return;

        Vector3 shipPos = standingShip.transform.position;
        if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(shipPos, out float groundHeight))
        {
            float waterDepth = shipPos.y - groundHeight;
            if (waterDepth <= PluginConfig.DisembarkWaterDepthThreshold.Value)
            {
                Vector3 jumpDir = (transform.position - shipPos).normalized;
                jumpDir.y = 0.2f;
                _character.InNumShipVolumes = 0;
                if (TryGetComponent<Rigidbody>(out var body))
                {
                    body.AddForce(jumpDir * 4f, ForceMode.VelocityChange);
                }
            }
        }
    }

    private void UpdateHotTubBehavior()
    {
        if (!PluginConfig.EnableHotTubAttraction.Value) return;
        if (!IsHappyAndTamed) return;

        if (_monsterAI.GetFollowTarget() != null || _monsterAI.IsAlerted() || _monsterAI.GetTargetCreature() != null)
        {
            _inHotTub = false;
            return;
        }

        _hotTubCheckTimer += 1f;
        if (_hotTubCheckTimer < 5f) return;
        _hotTubCheckTimer = 0f;

        Collider[] hits = Physics.OverlapSphere(transform.position, 1.2f);
        _inHotTub = false;
        foreach (var col in hits)
        {
            if (col.name.IndexOf("bathtub", StringComparison.OrdinalIgnoreCase) >= 0 ||
                col.name.IndexOf("hottub", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _inHotTub = true;
                break;
            }
        }

        if (_inHotTub) return;

        float radius = PluginConfig.HotTubSearchRadius.Value;
        Collider[] nearby = Physics.OverlapSphere(transform.position, radius);
        GameObject? closestTub = null;
        float closestDist = float.MaxValue;

        foreach (var col in nearby)
        {
            if (col.name.IndexOf("bathtub", StringComparison.OrdinalIgnoreCase) >= 0 ||
                col.name.IndexOf("hottub", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                float dist = Vector3.Distance(transform.position, col.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestTub = col.gameObject;
                }
            }
        }

        if (closestTub != null && closestDist > 1.5f)
        {
            _monsterAI.MoveTo(1f, closestTub.transform.position, 0.8f, false);
        }
    }

    #endregion
}
