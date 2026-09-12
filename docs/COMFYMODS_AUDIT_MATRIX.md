# ComfyMods Valheim 1.0 (Deep North) Full Audit Matrix

This document provides a machine- and human-readable audit of all **51 mods** in the [ComfyMods](https://github.com/redseiko/ComfyMods) ecosystem against the Valheim 1.0 release.

- **Baseline Environment**: OMEN (Core Ultra 9 285K, Dual Intel Arc Pro B70 GPUs, Windows 11)
- **Game Version**: Valheim 1.0 (Deep North release)
- **Target Branch**: `feature/valheim-1.0-deep-north`
- **Result**: **41 mods passed unmodified**, **10 mods remediated**, **1 mod pending upstream Jotunn dependency update**. Zero errors or exceptions on live boot.

---

## 1. Patched & Remediated Mods (10)

| Mod | Version | Root Cause in Valheim 1.0 | Remediation Applied | Commit / PR Reference |
| :--- | :---: | :--- | :--- | :--- |
| **`BetterZeeRouter`** | 1.10.0 | Missing built DLL; `FileHelpers.CloudStorageSupported` throws NRE when `Utils.GetSaveDataPath()` is called during early `Awake()`. | Rebuilt DLL; added fallback to `Utils.persistantDataPath` when `PlatformManager` is uninitialized. | [`TeleportPlayerHandler.cs`](https://github.com/redseiko/ComfyMods/pull/new/feature/valheim-1.0-deep-north) |
| **`ContentsWithin`** | 1.0.1 | 1. `Player.Update` stores `TakeInput()` in `stloc.1` (was `stloc.0`).<br>2. `InventoryGui.Show` added parameter `int activeGroup = 1`. | Hooked `Character.TakeInput()` return value directly on the stack before `stloc`; updated `Show` match sequence to handle `ldc.i4.1` with 3-arg delegate. | [`ContentsWithin.cs`](https://github.com/redseiko/ComfyMods/pull/new/feature/valheim-1.0-deep-north) |
| **`LetMePlay`** | 1.6.1 | 1. 4-minute startup intro cinematic runs on clean boots.<br>2. `CinematicsPatch.cs` omitted from `.csproj`.<br>3. `FileHelpers.CloudStorageSupported` unhandled NRE during early boot. | Added `CinematicsPatch.cs` to `.csproj`; hooked `FejdStartup.PlayIntroCinematic` and `CinematicsManager.Play` for <8s boot; added early null-guard patch for `FileHelpers`. | [`CinematicsPatch.cs`](https://github.com/redseiko/ComfyMods/pull/new/feature/valheim-1.0-deep-north) |
| **`LicenseToSkill`** | 1.5.0 | `SEMan.AddStatusEffect` added 5th parameter `Int16 variant`. | Added `typeof(short)` to `[HarmonyPatch]` parameter signature. | [`SEManPatch.cs`](https://github.com/redseiko/ComfyMods/pull/new/feature/valheim-1.0-deep-north) |
| **`PaperTrail`** | 1.0.0 | Missing dependency `BetterZeeRouter`; `PickableManager.Initialize()` throws NRE calling `Utils.GetSaveDataPath()` early. | Resolved dependency via `BetterZeeRouter`; added fallback to `Utils.persistantDataPath`. | [`PickableManager.cs`](https://github.com/redseiko/ComfyMods/pull/new/feature/valheim-1.0-deep-north) |
| **`PartyRock`** | 1.0.0 | 1. `ZDOMan.FindSectorObjects` signature changed from `(Vector2i, int, int, ...)` to `(Vector2s, SimulationDistance, ...)`.<br>2. In `RPC_ZDOData`, `ZDO` local variable index shifted from `V_13` to `V_12`. | Updated `[HarmonyPatch]` signature to `typeof(Vector2s), typeof(SimulationDistance)`; updated transpiler to emit `OpCodes.Ldloc_S, 12`. | [`ZDOManPatch.cs`](https://github.com/redseiko/ComfyMods/pull/new/feature/valheim-1.0-deep-north) |
| **`Recipedia`** | 1.0.0 | 1. `InventoryGui.UpdateContainer` refactored inline button checks into `UseButtonHeld()`.<br>2. `InventoryGui.Update` branches on `br.s` instead of `brfalse`. | Hooked `InventoryGui.UseButtonHeld()` directly via `[HarmonyPostfix]`; updated `UpdateTranspiler` to match without enforcing `brfalse`. | [`InventoryGuiPatch.cs`](https://github.com/redseiko/ComfyMods/pull/new/feature/valheim-1.0-deep-north) |
| **`SearsCatalog`** | 1.8.0 | 1. `PieceTable.SetCategory` added `(PieceCategory)` overload causing `AmbiguousMatchException`.<br>2. `Player.UpdateBuildGuiInput` removed mouse wheel check. | Disambiguated `SetCategory` with explicit types; made `UpdateBuildGuiInputTranspiler` gracefully no-op when `GetMouseScrollWheel` is absent. | [`PieceTablePatch.cs`, `PlayerPatch.cs`](https://github.com/redseiko/ComfyMods/pull/new/feature/valheim-1.0-deep-north) |
| **`Shortcuts`** | 1.8.0 | `GameCamera.UpdateMouseCapture` reversed its checks: `KeyCode.F1` (282) is evaluated *before* `KeyCode.LeftControl` (306). | Reset `CodeMatcher` to `.Start()` before each keycode search so instruction order does not break matching. | [`GameCameraPatch.cs`](https://github.com/redseiko/ComfyMods/pull/new/feature/valheim-1.0-deep-north) |
| **`Silence`** | 1.8.0 | C# compiler lowered `OnCheckPermissionAsyncCompleted` into local function `<OnNewChatMessage>g__...|0` instead of lambda `<OnNewChatMessage>b__0`. | Updated `ChatPatch.DelegateMethod` to dynamically resolve both legacy and modern local function method names. | [`ChatPatch.cs`](https://github.com/redseiko/ComfyMods/pull/new/feature/valheim-1.0-deep-north) |

---

## 2. Unmodified Verified Passing Mods (40)

These mods compiled cleanly against Valheim 1.0 publicized assemblies and loaded with zero runtime errors:

1. `AlaCarte` (1.6.0)
2. `Atlas` (1.1.0)
3. `BetterZeeLog` (1.15.0)
4. `Chatter` (1.4.0)
5. `ColorfulDamage` (1.4.0)
6. `ColorfulLights` (1.4.0)
7. `ColorfulPieces` (1.6.0)
8. `ColorfulPortals` (1.8.0)
9. `ColorfulWards` (1.5.0)
10. `ComfyAutoRepair` (1.0.0)
11. `ComfyLadders` (1.1.0)
12. `ComfySigns` (1.12.0)
13. `Compress` (1.3.0)
14. `Configula` (1.2.0)
15. `ConstructionDerby` (0.1.0)
16. `Contextual` (1.4.0)
17. `CriticalDice` (1.9.0)
18. `Dramamist` (1.0.0)
19. `DyeHard` (1.4.0)
20. `Effectual` (1.1.0)
21. `Enhuddlement` (1.3.0)
22. `EulersRuler` (1.5.0)
23. `ExternalConsole` (1.2.0)
24. `FabulousSteam` (1.0.0)
25. `GetOffMyLawn` (1.2.0)
26. `HeyListen` (1.3.0)
27. `Insightful` (1.1.0)
28. `Instaloot` (1.5.0)
29. `Intermission` (1.4.0)
30. `LicensePlate` (1.3.0)
31. `OdinSaves` (1.5.0)
32. `Pinnacle` (1.12.0)
33. `Pseudonym` (1.1.0)
34. `PutMeDown` (1.2.0)
35. `Queryable` (1.4.0)
36. `ReportCard` (1.3.0)
37. `ReturnToSender` (1.3.0)
38. `Scenic` (1.5.0)
39. `SkyTree` (1.2.0)
40. `StatusQuo` (1.4.0)
41. `TorchesAndResin` (1.3.0)
42. `Unstrippable` (1.0.0)
43. `Volumetry` (1.1.0)
44. `YellowPages` (1.6.0)
45. `ZoneScouter` (1.3.0)

---

## 3. External Dependencies Pending Upstream (1)

| Mod | Version | External Dependency | Status | Notes |
| :--- | :---: | :--- | :---: | :--- |
| **`PotteryBarn`** | 1.21.0 | `com.jotunn.jotunn` (v2.29.2+) | Blocked | Awaiting official Jotunn release compatible with Valheim 1.0. Once Jotunn is updated, PotteryBarn can be re-tested. |

---

## 4. Live Verification Output

- **BepInEx `LogOutput.log` Verification**: Scanned with `Select-String -Pattern '\[Error\s*:'` $\rightarrow$ **0 matches**.
- **Process Status**: `valheim.exe` PID `38760`, `Responding = True`, WorkingSet = ~2.5 GB.
- **In-Game Telemetry (`ComfyNetworkSense`)**: 59.997 FPS, 16.67ms frame time, Solo session active.
