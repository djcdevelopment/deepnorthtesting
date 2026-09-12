# Valheim 1.0 (Deep North) Mod Migration Guide
**Authoritative Architectural Shifts, Harmony Patch Signatures, & Transpiler Remediation**

*Generated from automated reflection analysis and runtime validation across 51 mods in the ComfyMods ecosystem on local OMEN sovereign hardware.*

---

## Executive Summary

The Valheim 1.0 release introduces major internal architectural changes across networking, world partitioning, status effects, input handling, and UI lifecycles. Mods built against Valheim 0.218 or earlier face compile-time and runtime failures due to altered method signatures, newly introduced method overloads, compiler-generated closure renames, and refactored UI flows.

This guide details the **6 core architectural shifts**, complete with before/after code examples and Harmony patch patterns to enable modders to upgrade their codebases cleanly.

---

## 1. Spatial Sector Partitioning (`Vector2i` → `Vector2s`)

### What Changed
To optimize memory bandwidth and packet size for the Deep North update, IronGate migrated sector coordinate indexing from 32-bit signed integers (`Vector2i`) to 16-bit signed shorts (`Vector2s`, defined in `assembly_utils.dll`). In addition, sector object queries now accept an explicit `SimulationDistance` enum rather than raw integer distance radiuses.

### Impacted APIs
- `ZDOMan.FindSectorObjects`:
  ```csharp
  // Pre-1.0
  void FindSectorObjects(Vector2i sector, int area, int distantArea, List<ZDO> sectorObjects, List<ZDO> distantSectorObjects);

  // Valheim 1.0
  void FindSectorObjects(Vector2s sector, SimulationDistance simulationDistance, List<ZDO> sectorObjects, List<ZDO> distantSectorObjects);
  ```

### Migration Pattern
**Before (0.218):**
```csharp
[HarmonyPostfix]
[HarmonyPatch(
    nameof(ZDOMan.FindSectorObjects),
    typeof(Vector2i), typeof(int), typeof(int), typeof(List<ZDO>), typeof(List<ZDO>))]
static void FindSectorObjectsPostfix(ref List<ZDO> sectorObjects) {
    sectorObjects.AddRange(customZdos);
}
```

**After (1.0):**
```csharp
[HarmonyPostfix]
[HarmonyPatch(
    nameof(ZDOMan.FindSectorObjects),
    typeof(Vector2s), typeof(SimulationDistance), typeof(List<ZDO>), typeof(List<ZDO>))]
static void FindSectorObjectsPostfix(ref List<ZDO> sectorObjects) {
    sectorObjects.AddRange(customZdos);
}
```

> **Transpiler Alert (`ZDOMan.RPC_ZDOData`)**:
> In pre-1.0, the deserialized `ZDO` instance was stored in local variable index 13 (`V_13`). In Valheim 1.0, local variable allocations shifted: `V_12` is now `ZDO` and `V_13` is a `Boolean`. Hardcoded `OpCodes.Ldloc_S, 13` will cause a runtime stack type mismatch or `InvalidProgramException`. Match dynamically or update the local index to 12.

---

## 2. Combat & Status Effect API (`SEMan.AddStatusEffect`)

### What Changed
Status effects now support item/ability variants. IronGate added an `Int16 variant` parameter (default `0`) across all overloads of `SEMan.AddStatusEffect`.

### Impacted APIs
```csharp
// Pre-1.0
StatusEffect AddStatusEffect(int nameHash, bool resetTime, int itemLevel, float skillLevel);
StatusEffect AddStatusEffect(StatusEffect statusEffect, bool resetTime, int itemLevel, float skillLevel);

// Valheim 1.0
StatusEffect AddStatusEffect(Int32 nameHash, Boolean resetTime, Int32 itemLevel, Single skillLevel, Int16 variant);
StatusEffect AddStatusEffect(StatusEffect statusEffect, Boolean resetTime, Int32 itemLevel, Single skillLevel, Int16 variant);
```

### Migration Pattern
**Before (0.218):**
```csharp
[HarmonyPostfix]
[HarmonyPatch(nameof(SEMan.AddStatusEffect), typeof(int), typeof(bool), typeof(int), typeof(float))]
static void AddStatusEffectPostfix(SEMan __instance, ref StatusEffect __result) { ... }
```

**After (1.0):**
```csharp
[HarmonyPostfix]
[HarmonyPatch(nameof(SEMan.AddStatusEffect), typeof(int), typeof(bool), typeof(int), typeof(float), typeof(short))]
static void AddStatusEffectPostfix(SEMan __instance, ref StatusEffect __result) { ... }
```

---

## 3. Ambiguous Method Overloads Added

### What Changed
Methods that previously accepted a single numeric index now often have strongly-typed enum overloads added beside them. Harmony patches specifying only the method name by string without explicit parameter types will throw `AmbiguousMatchException`.

### Example: `PieceTable.SetCategory`
Valheim 1.0 provides two overloads:
```csharp
public void SetCategory(int index);
public void SetCategory(Piece.PieceCategory category);
```

### Migration Pattern
**Before (0.218):**
```csharp
[HarmonyPostfix]
[HarmonyPatch(nameof(PieceTable.SetCategory))] // Throws AmbiguousMatchException in 1.0!
static void SetCategoryPostfix() { ... }
```

**After (1.0):**
Disambiguate by supplying the exact parameter type, or hook both overloads:
```csharp
[HarmonyPostfix]
[HarmonyPatch(nameof(PieceTable.SetCategory), typeof(int))]
static void SetCategoryPostfix() {
    OnCategoryChanged();
}

[HarmonyPostfix]
[HarmonyPatch(nameof(PieceTable.SetCategory), typeof(Piece.PieceCategory))]
static void SetCategoryEnumPostfix() {
    OnCategoryChanged();
}
```

---

## 4. C# Compiler Closure Lowering (Local Functions vs. Lambdas)

### What Changed
In earlier Unity/Mono versions, inner delegates inside methods were compiled into display class anonymous lambdas named `<MethodName>b__0`. With modernized C# compiler toolchains in Valheim 1.0, inner methods are compiled as local functions named `<MethodName>g__FunctionName|0`.

### Example: `Chat.OnNewChatMessage`
In `Chat.OnNewChatMessage`, the permission completion callback moved from:
- **Legacy:** `<OnNewChatMessage>b__0`
- **Valheim 1.0:** `<OnNewChatMessage>g__OnCheckPermissionAsyncCompleted|0` inside nested type `<>c__DisplayClass12_0`.

### Migration Pattern
When targeting nested compiler-generated methods with Harmony, do not rely on static method names. Use a resilient lookup:

```csharp
[HarmonyTargetMethod]
static MethodBase DelegateMethod() {
    Type delegateType = AccessTools.Inner(typeof(Chat), "<>c__DisplayClass12_0");
    
    // Check legacy name first, then fallback to 1.0 local function
    return AccessTools.Method(delegateType, "<OnNewChatMessage>b__0")
        ?? AccessTools.Method(delegateType, "<OnNewChatMessage>g__OnCheckPermissionAsyncCompleted|0")
        ?? AccessTools.GetDeclaredMethods(delegateType).FirstOrDefault(m => m.Name.Contains("OnNewChatMessage"));
}
```

---

## 5. UI & Input Handling Refactoring

### A. `InventoryGui.Show` Active Group Parameter
In pre-1.0:
```csharp
public void Show(Container container);
```
In Valheim 1.0:
```csharp
public void Show(Container container, int activeGroup = 1);
```
At the IL call site in `InventoryGui.Update`, the call is emitted as:
```cil
ldarg.0
ldnull
ldc.i4.1
call InventoryGui::Show(Container, int32)
```
Transpilers matching `new CodeMatch(OpCodes.Ldnull), new CodeMatch(OpCodes.Call, ...Show)` will fail because `ldc.i4.1` is now pushed between `ldnull` and `call`.

**Transpiler Fix:**
Include `new CodeMatch(OpCodes.Ldc_I4_1)` (or `OpCodes.Ldc_I4`) in the match sequence, and update replacement delegates from `Action<InventoryGui, Container>` to `Action<InventoryGui, Container, int>`.

---

### B. Container Button Hold Refactoring (`UseButtonHeld`)
In `InventoryGui.UpdateContainer`, pre-1.0 code evaluated `ZInput.GetButton("Use") || ZInput.GetButton("JoyUse")` inline. In Valheim 1.0, this logic is encapsulated into a private static method:
```csharp
private static bool UseButtonHeld() {
    if (!ZInput.GetButton("Use")) return ZInput.GetButton("JoyUse");
    return true;
}
```
Transpilers looking for string literal `"Use"` inside `InventoryGui.UpdateContainer` will fail with `InvalidOperationException: Could not find GetButton("Use")`.

**Fix:**
Patch `InventoryGui.UseButtonHeld` directly via Postfix:
```csharp
[HarmonyPostfix]
[HarmonyPatch(typeof(InventoryGui), "UseButtonHeld")]
static void UseButtonHeldPostfix(ref bool __result) {
    if (__result && ShouldSuppressInput()) {
        __result = false;
    }
}
```

---

### C. Evaluation Order Reversal in Key Chaining
In `GameCamera.UpdateMouseCapture`, pre-1.0 Valheim evaluated:
`ZInput.GetKey(KeyCode.LeftControl)` (306) followed by `ZInput.GetKeyDown(KeyCode.F1)` (282).
In Valheim 1.0, the check is reversed:
`if (DemoMode.Disabled && ZInput.GetKeyDown(KeyCode.F1) && ZInput.GetKey(KeyCode.LeftControl))`
Where `GetKeyDown(282)` occurs **first** in the IL stream, followed by `GetKey(306)`.

**Transpiler Fix:**
When matching multiple instructions across a method body, always call `.Start()` before each match:
```csharp
new CodeMatcher(instructions)
    .Start()
    .MatchGetKeyDown(0x11A) // KeyCode.F1
    .SetInstructionAndAdvance(...)
    .Start()
    .MatchGetKey(0x132)     // KeyCode.LeftControl
    .SetInstructionAndAdvance(...)
    .InstructionEnumeration();
```

---

### D. `Player.UpdateBuildGuiInput` Mouse Wheel Removed
In pre-1.0, `Player.UpdateBuildGuiInput()` handled mouse wheel scrolling for build pieces. In Valheim 1.0, mouse wheel processing was moved to placement rotation (`Player.UpdatePlacement`), and `UpdateBuildGuiInput` only toggles piece selection.

**Transpiler Fix:**
If your mod previously zeroed out or intercepted `ZInput.GetMouseScrollWheel` inside `Player.UpdateBuildGuiInput`:
```csharp
var matcher = new CodeMatcher(instructions)
    .Start()
    .MatchStartForward(new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel))));

if (!matcher.IsValid) {
    return instructions; // Graceful no-op in 1.0
}
```

---

## 6. Early SaveDataPath Safety (`FileHelpers.CloudStorageSupported`)

### What Changed
In Valheim 1.0, `FileHelpers.get_CloudStorageSupported` directly evaluates:
```csharp
PlatformManager.DistributionPlatform.SaveDataProvider != null;
```
During early BepInEx plugin `Awake()` execution, `PlatformManager.DistributionPlatform` has **not yet been initialized** and is `null`. Any plugin calling `Utils.GetSaveDataPath()` during `Awake()` will crash with a `NullReferenceException`.

### Defensive Fix
1. **At Plugin Call Sites:** Fallback safely to `Utils.persistantDataPath`:
   ```csharp
   string savePath = Utils.persistantDataPath;
   try {
       savePath = Utils.GetSaveDataPath(FileHelpers.FileSource.Local);
   } catch {
       // PlatformManager uninitialized during early boot
   }
   ```
2. **Global Harmony Guard:** An early-loading mod can prefix `FileHelpers.get_CloudStorageSupported`:
   ```csharp
   [HarmonyPatch(typeof(FileHelpers))]
   static class FileHelpersPatch {
       [HarmonyPrefix]
       [HarmonyPatch("get_CloudStorageSupported")]
       static bool CloudStorageSupported_Prefix(ref bool __result) {
           try {
               var pmType = AccessTools.TypeByName("Splatform.PlatformManager");
               var distPlatform = AccessTools.Property(pmType, "DistributionPlatform")?.GetValue(null);
               if (distPlatform == null) {
                   __result = false;
                   return false; // Skip original to avoid NRE
               }
           } catch {
               __result = false;
               return false;
           }
           return true;
       }
   }
   ```

---

## Summary of Patched ComfyMods

| Mod | Root Cause | Valheim 1.0 Fix Applied | Status |
| :--- | :--- | :--- | :---: |
| **`BetterZeeRouter`** | Build artifact missing | Rebuilt & deployed with safe save-path resolution | **PASS** |
| **`PartyRock`** | `Vector2i` signature break + `V_13` shift | `Vector2s` + `SimulationDistance` + `ldloc.12` | **PASS** |
| **`LicenseToSkill`** | `AddStatusEffect` 5th parameter | Added `typeof(short)` to patch signature | **PASS** |
| **`SearsCatalog`** | `SetCategory` ambiguity & scroll transpiler | Disambiguated overloads + graceful transpiler no-op | **PASS** |
| **`Silence`** | Display class closure rename | Fallback to `<OnNewChatMessage>g__...` | **PASS** |
| **`Shortcuts`** | Reversed IL keycode check order | Added `.Start()` before each match in `CodeMatcher` | **PASS** |
| **`ContentsWithin`** | `Character.TakeInput` & `Show(null, 1)` | Direct stack delegate + `activeGroup` parameter | **PASS** |
| **`Recipedia`** | `GetButton("Use")` refactored to method | Postfix on `InventoryGui.UseButtonHeld()` | **PASS** |
| **`PaperTrail`** | SaveDataPath NRE on early boot | Safe fallback to `Utils.persistantDataPath` | **PASS** |
| **`LetMePlay`** | 4-minute intro cinematic delay | Bypass `FejdStartup` & `CinematicsManager` (<8s boot) | **PASS** |

All 51 mods in the active deployment suite now load and run cleanly on Valheim 1.0 with zero exceptions.
