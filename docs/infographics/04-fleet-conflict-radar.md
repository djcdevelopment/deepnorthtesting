# Fleet Radar: Eliminating Keybind and Patch Collisions Before Launch

**Testing multiplayer mods across multiple machines usually ends in mystery crashes and desyncs. Fleet Radar audits every node before the server boots.**

---

## The Fleet Testing Reality

A real multiplayer mod test involves multiple distinct roles and machines:

```
                      +-----------------------------+
                      |      ISOLATE MCP GATEWAY    |
                      |    Central Fleet Auditor    |
                      +--------------+--------------+
                                     |
         +---------------------------+---------------------------+
         |                           |                           |
         v                           v                           v
+------------------+        +------------------+        +------------------+
|   OMEN [Node 1]  |        |   AM4 [Node 2]   |        |  FX99 / i5 [N3]  |
| Primary Client   | <====> | Dedicated Server | <====> | Secondary Client |
| Profile: gaming  |        | Profile: server  |        | Profile: test    |
| (65 Mods Active) |        | (Headless Sync)  |        | (Clean Baseline) |
+------------------+        +------------------+        +------------------+
```

---

## The Three Disasters Caught by the Radar

### 1. The Keybind Collision
- **The Risk**: Mod A binds `F7` for camera controls; Mod B binds `F7` for inventory sorting. In the middle of a raid, pressing `F7` triggers both, locking the player's UI.
- **The Radar Fix**: Pre-flight scan inspects `BepInEx/config/` across all profiles and warns before launch:
  `[WARN] Keybind F7 contention detected between ModA and ModB.`

### 2. The Headless Server Poison
- **The Risk**: A client-side rendering mod (like camera rigs or GUI enhancements) accidentally loads on a headless dedicated server, throwing a fatal `NullReferenceException` in Unity headless mode.
- **The Radar Fix**: Server profiles validate against a strict `ClientOnly = false` filter, preventing UI mods from poisoning headless servers.

### 3. Version & RPC Mismatch
- **The Risk**: Client 1 runs v1.2.0 with a new RPC payload, while the server or Client 2 runs v1.1.0. Players connect, but interactions silently fail.
- **The Radar Fix**: Real-time hash and semantic version checks verify all nodes match before test execution.

---

## Pre-Flight Audit Checklist

| Check Category | Verification Action | Status |
|---|---|---|
| **Keybind Clashes** | Cross-indexes all active mod bindings (`F1`–`F12`, alpha keys) | Verified Clean |
| **RPC Handlers** | Checks for unique custom network RPC method registration | Verified Isolated |
| **Headless Safety** | Confirms server profile contains 0 client-only UI hooks | 100% Pass |
| **Profile Integrity** | Confirms NTFS reparse point targets valid directory | Atomic Pass |

---

## The Takeaway

> **Catch mod collisions on the radar before they crash your server.**
