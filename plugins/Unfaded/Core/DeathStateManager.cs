namespace Unfaded.Core;

using System.Collections;
using Unfaded.Configuration;
using UnityEngine;

public enum DeathCameraMode
{
    RagdollOrbit,
    KillerFocus,
    FreeFly
}

public static class DeathStateManager
{
    public static DeathCameraMode CurrentMode { get; set; } = DeathCameraMode.RagdollOrbit;
    public static Character? LastKiller { get; set; }
    public static string LastKillerName { get; set; } = "";
    public static int LastKillerStars { get; set; } = 0;
    public static float LastLethalDamage { get; set; } = 0f;
    public static string LastDamageType { get; set; } = "";
    public static Vector3 DeathPosition { get; set; } = Vector3.zero;

    // Free Fly state
    public static Vector3 FreeFlyPosition { get; set; } = Vector3.zero;
    public static float FreeFlyYaw { get; set; } = 0f;
    public static float FreeFlyPitch { get; set; } = 20f;

    // Test simulation state
    public static bool IsTestSimActive { get; set; } = false;
    public static float TestSimEndTime { get; set; } = 0f;

    public static void RecordLethalHit(Player player, HitData hit)
    {
        CurrentMode = DeathCameraMode.RagdollOrbit;
        DeathPosition = player.transform.position;
        FreeFlyPosition = player.transform.position + Vector3.up * 3f - player.transform.forward * 4f;
        FreeFlyYaw = player.transform.eulerAngles.y;
        FreeFlyPitch = 25f;

        LastLethalDamage = hit.GetTotalDamage();

        Character attacker = hit.GetAttacker();
        if (attacker != null)
        {
            LastKiller = attacker;
            LastKillerName = attacker.GetHoverName();
            if (string.IsNullOrEmpty(LastKillerName))
            {
                LastKillerName = attacker.m_name;
            }
            LastKillerStars = attacker.GetLevel() - 1; // 1 = 0 stars, 2 = 1 star, 3 = 2 stars
        }
        else
        {
            LastKiller = null;
            LastKillerStars = 0;
            switch (hit.m_hitType)
            {
                case HitData.HitType.Water:
                    LastKillerName = "Drowning / Water";
                    break;
                case HitData.HitType.Fall:
                    LastKillerName = "Gravity / Fall Damage";
                    break;
                case HitData.HitType.Tree:
                    LastKillerName = "Falling Tree / Timber";
                    break;
                case HitData.HitType.Smoke:
                    LastKillerName = "Smoke Inhalation";
                    break;
                default:
                    LastKillerName = "The Harsh Environment";
                    break;
            }
        }

        // Determine primary damage type
        if (hit.m_damage.m_fire > 0f) LastDamageType = "Fire";
        else if (hit.m_damage.m_frost > 0f) LastDamageType = "Frost";
        else if (hit.m_damage.m_lightning > 0f) LastDamageType = "Lightning";
        else if (hit.m_damage.m_poison > 0f) LastDamageType = "Poison";
        else if (hit.m_damage.m_spirit > 0f) LastDamageType = "Spirit";
        else if (hit.m_damage.m_slash > 0f) LastDamageType = "Slash";
        else if (hit.m_damage.m_pierce > 0f) LastDamageType = "Pierce";
        else if (hit.m_damage.m_blunt > 0f) LastDamageType = "Blunt";
        else LastDamageType = "Physical";
    }

    public static string FormatDeathRecap()
    {
        string stars = LastKillerStars > 0 ? $" ({LastKillerStars}★)" : "";
        string dmg = LastLethalDamage > 0f ? $" [{LastLethalDamage:F0} {LastDamageType}]" : "";
        return $"💀 Slain by: <color=#FF6B6B><b>{LastKillerName}{stars}</b></color>{dmg}";
    }

    public static IEnumerator SlowMotionRoutine(MonoBehaviour runner)
    {
        // Only run slow motion in singleplayer / local host to avoid desyncing network peers
        if (ZNet.instance != null && !ZNet.instance.IsServer() && ZNet.instance.GetNrOfPlayers() > 1)
        {
            yield break;
        }

        float targetScale = Mathf.Clamp(PluginConfig.SlowMotionScale.Value, 0.05f, 1.0f);
        float duration = Mathf.Max(0.1f, PluginConfig.SlowMotionDuration.Value);

        Time.timeScale = targetScale;
        Time.fixedDeltaTime = 0.02f * targetScale;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Smoothly restore
        float restoreDuration = 0.5f;
        float restoreElapsed = 0f;
        while (restoreElapsed < restoreDuration)
        {
            restoreElapsed += Time.unscaledDeltaTime;
            float t = restoreElapsed / restoreDuration;
            Time.timeScale = Mathf.Lerp(targetScale, 1.0f, t);
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            yield return null;
        }

        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
    }

    public static void ResetOnRespawn()
    {
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
        CurrentMode = DeathCameraMode.RagdollOrbit;
        LastKiller = null;
        IsTestSimActive = false;
    }
}
