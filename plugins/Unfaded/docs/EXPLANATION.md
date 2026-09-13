# Technical Explanation: Valheim Death Pipeline & Unfaded Architecture

This document details the underlying decompiled mechanics of Valheim's death pipeline, the root causes of the notorious 9.5-second blackout, and how **Unfaded** surgically hooks the game engine to provide a smooth, tactical spectator experience.

---

## 1. The Vanilla Death Sequence

When a player reaches 0 HP in Valheim, the execution path flows through `Player.OnDeath()`:

```csharp
// Source: assembly_valheim.dll -> Player.OnDeath()
public override void OnDeath()
{
    if (!m_nview.IsOwner()) return;

    bool flag = HardDeath();
    m_nview.GetZDO().Set(ZDOVars.s_dead, value: true);
    m_nview.InvokeRPC(ZNetView.Everybody, "OnDeath");
    ...
    CreateDeathEffects(); // Spawns ragdoll prefab and sets m_ragdoll
    CreateTombStone();    // Spawns tombstone with player inventory
    m_foods.Clear();
    m_seman.RemoveAllStatusEffects();

    // ⚠️ THE BOTTLENECK:
    Game.instance.RequestRespawn(10f, afterDeath: true);

    m_timeSinceDeath = 0f;
    Message(MessageHud.MessageType.Center, "$msg_youdied");
    ...
}
```

### The Irony of Ragdoll Tracking
Inside `GameCamera.UpdateCamera(float dt)`:

```csharp
// Source: assembly_valheim.dll -> GameCamera.UpdateCamera()
if (localPlayer.IsDead() && (bool)localPlayer.GetRagdoll())
{
    Vector3 averageBodyPosition = localPlayer.GetRagdoll().GetAverageBodyPosition();
    base.transform.LookAt(averageBodyPosition);
}
```

Iron Gate intentionally programmed the `GameCamera` to calculate the center of mass of the player's ragdoll (`averageBodyPosition`) and continuously orient the camera toward it. 

However, players almost never saw this in action because of `Hud.UpdateBlackScreen()`.

---

## 2. The 9.5-Second Blackout Mechanism

Every frame in `Hud.LateUpdate()`, the game updates the black loading screen:

```csharp
// Source: assembly_valheim.dll -> Hud.UpdateBlackScreen()
private float GetFadeDuration(Player player)
{
    if (player != null)
    {
        if (player.IsDead()) return Game.instance.m_fadeTimeDeath; // 9.5f
        if (player.IsSleeping()) return Game.instance.m_fadeTimeSleep;
    }
    return 1f;
}

private void UpdateBlackScreen(Player player, float dt)
{
    if (player == null || player.IsDead() || player.IsTeleporting() || Game.instance.IsShuttingDown() || player.IsSleeping())
    {
        m_loadingScreen.gameObject.SetActive(true);
        float alpha = m_loadingScreen.alpha;
        float fadeDuration = GetFadeDuration(player); // 9.5 seconds!
        alpha = Mathf.MoveTowards(alpha, 1f, dt / fadeDuration);
        m_loadingScreen.alpha = alpha;
        ...
```

Because `m_fadeTimeDeath` is 9.5 seconds and `RequestRespawn` delays for 10.0 seconds:
1. Seconds 0–2: Viewport rapidly dims.
2. Seconds 3–9.5: Screen is pitch black (`alpha = 1.0f`).
3. Second 10.0: `Game._RequestRespawn()` destroys the player object and teleports to bed.

Players were forced to spend 95% of their death timer staring at an empty black canvas.

---

## 3. How Unfaded Resolves It

### A. Surgical Blackout Suppression
In [`HudBlackScreenPatch.cs`](../Patches/HudBlackScreenPatch.cs), Unfaded injects a Harmony prefix before `Hud.UpdateBlackScreen`:

```csharp
if (player != null && player.IsDead() && (Game.instance == null || !Game.instance.WaitingForRespawn()))
{
    if (__instance.m_loadingScreen != null)
    {
        __instance.m_loadingScreen.alpha = 0f;
        __instance.m_loadingScreen.gameObject.SetActive(false);
    }
    return false; // Skip vanilla blackout completely!
}
```

- While the player is dead and spectating, `alpha` is forced to `0f`.
- Once `Game.instance.WaitingForRespawn()` becomes `true` (when the bed reload actually begins), the prefix returns `true`, allowing the normal loading screen to cover the 0.5s character instantiation at the bed.

### B. Orbital Camera & Terrain Raycasting
In [`GameCameraDeathPatch.cs`](../Patches/GameCameraDeathPatch.cs), Unfaded calculates an orbital transform around the ragdoll's average body position:

```csharp
Vector3 targetCenter = ragdoll.GetAverageBodyPosition();
Quaternion rot = Quaternion.Euler(s_orbitPitch, s_orbitYaw, 0f);
Vector3 dir = -(rot * Vector3.forward);
Vector3 targetPos = targetCenter + dir * distance;

// SphereCast prevents clipping inside dungeon walls or boulders
if (Physics.SphereCast(targetCenter, 0.25f, dir, out RaycastHit hit, distance, __instance.m_blockCameraMask))
{
    targetPos = targetCenter + dir * Mathf.Max(0.5f, hit.distance - 0.15f);
}

__instance.transform.position = targetPos;
__instance.transform.LookAt(targetCenter);
```

### C. Killer Attribution Extraction
In [`DeathStateManager.cs`](../Core/DeathStateManager.cs), Unfaded extracts the attacker from `Player.m_lastHit`:

```csharp
Character attacker = hit.GetAttacker();
if (attacker != null)
{
    LastKiller = attacker;
    LastKillerName = attacker.GetHoverName();
    LastKillerStars = attacker.GetLevel() - 1;
}
```

This feeds both the **Death Cause Banner** and the **Killer Focus Cam (`[K]`)**, which snaps the camera to `LastKiller.GetCenterPoint()`.

### D. Bullet-Time TimeScale Synchronization
To create the cinematic slow-mo on death without breaking multiplayer state:
- In single-player / local host: `Time.timeScale` drops to `0.35f` and `Time.fixedDeltaTime = 0.02f * 0.35f` for physics smoothing.
- In dedicated multiplayer client sessions: Slow-mo is automatically bypassed if other players are present on the server to maintain network synchronization.
