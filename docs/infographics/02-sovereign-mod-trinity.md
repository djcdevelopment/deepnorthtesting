# The Sovereign Mod Trinity: Zero Conflicts in the Deep North

**Most Valheim modpacks collapse under broken patches and conflicting keybinds. The Sovereign Mod Trinity proves three complex mods can run in total harmony.**

---

## The Three Pillars

```
+-------------------------------------------------------------------+
|                        THE SOVEREIGN TRIO                         |
+-------------------------------------------------------------------+
|  1. IsModded          |  2. Unfaded           |  3. TotemSentinel |
|  The Trust Anchor     |  The Visual Freedom   |  The Tactical Ward|
|  - Passive Hook       |  - Keybind: [F7]      |  - Keybind: [F9]  |
|  - Steam Achievements |  - Instant Blackout   |  - 64m Sonar Pulse|
|  - Server Handshake   |  - Selfie Drone Cam   |  - Greed's Gambit |
+-------------------------------------------------------------------+
```

---

## 1. IsModded — The Trust Anchor

- **What it does**: Preserves Steam achievements on modded servers and normalizes the client-to-server handshake so vanilla servers don't reject custom clients.
- **Harmony Patches**: `FejdStartup`, `SteamManager`.
- **Keybinds Used**: **None (0)**. Pure passive telemetry hook.
- **Footprint**: Zero in-game UI overhead; silent background protection.

---

## 2. Unfaded — Visual Freedom & Camera

- **What it does**: Eliminates Valheim's lingering screen fade delays, unlocks instant game world transitions, and enables high-mobility selfie drone / freecam for content creators.
- **Harmony Patches**: `Hud.FadeToBlack`, `GameCamera.UpdateCamera`.
- **Keybinds Used**: **[F7]** (Camera toggle & HUD suppress).
- **Footprint**: Zero game-loop physics impact; isolated to local rendering and camera rigs.

---

## 3. TotemSentinel — The Tactical Ward

- **What it does**: Re-engineers the Valheim Wardstone into an active defense sentinel: casts a 64m cyan sonar pulse to detect incoming raids and activates Greed's Gambit auto-loot suppression.
- **Harmony Patches**: `WardStone.Awake`, `ZNetScene.RPC_ClientUpdate`.
- **Keybinds Used**: **[F9]** (Ward pulse trigger & configuration menu).
- **Footprint**: Bounded network RPCs; runs strictly within local ward bounds.

---

## The Conflict Verification Matrix

| Verification Dimension | IsModded | Unfaded | TotemSentinel | Result |
|---|---|---|---|---|
| **Keybind Contention** | None | `F7` | `F9` | **ZERO COLLISION** |
| **Harmony Patch Targets** | Steamworks | UI/Camera | Wardstone | **ZERO OVERLAP** |
| **Network RPC Clashes** | None | None | `TotemRPC` | **ISOLATED** |
| **Savegame Pollution** | None | None | Vanilla Ward | **CLEAN EXIT** |

---

## The Takeaway

> **Three mods. Three distinct layers. Total harmony in the Deep North.**
