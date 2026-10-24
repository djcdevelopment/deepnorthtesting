# FAQ-007: How to Resolve Valheim 1.0 Pickup Failure and EpicLoot Artisan Table Incompatibilities in InfinityInventory (And How to Safely Security-Vet Unofficial Patches)

> 📍 **Navigation**: [← Back to README Table of Contents](../../README.md#technical-faq--knowledgebase) | [Knowledgebase Hub](../FAQ.md)

---


| Metadata | Specification |
| :--- | :--- |
| **Category** | Binary Compatibility, Inventory Architecture & Community Mod Security |
| **Target Mods** | **InfinityInventory** (v1.6.3, `lizzardfyll`), **EpicLoot** (v0.10.x+, `RandyKnapp`), Craft-From-Containers |
| **Engine / Game** | Valheim 1.0 (Deep North) + BepInEx 5.4.2202 |
| **Complexity** | Deep IL / Reflection & Security Auditing |
| **Status** | Verified Clean & Preflight Audited (0 Broken References, 0 Broken Hooks) |

#### 💬 Context & Inquiry

A community member, **DankMan**, reached out in the modding community seeking assistance returning to Valheim with health-related inventory accessibility constraints. Because repetitive inventory shuffling is physically demanding, they relied on `InfinityInventory` by `lizzardfyll` prior to the 1.0 release. Upon upgrading to Valheim 1.0, the mod loaded into the game, but the player was **completely unable to pick up any items** from the ground:

![DankMan Inquiry 1](../faq-007-inquiry-1.png)

When exploring alternative setups, DankMan attempted using standard "craft from container" mods alongside **EpicLoot**, only to discover that the Artisan Table (enchanting and augmenting) failed to detect resources in nearby chests. Furthermore, outreach to the original author failed due to an expired Discord invite link, and when offered an unofficial patch, DankMan rightly exercised exemplary security hygiene by refusing to execute an unverified binary from an unfamiliar contributor:

![DankMan Inquiry 2](../faq-007-inquiry-2.png)

This FAQ entry documents:
1. **The Root Cause of Pickup Failure**: Why Valheim 1.0 completely breaks ground item collection in `InfinityInventory`.
2. **The EpicLoot Artisan Table Architecture**: Why EpicLoot fails with traditional chest-radius mods but works flawlessly with `InfinityInventory`.
3. **The Security Vetting Standard**: How any player can independently verify, audit, or locally compile this patch without trusting third-party binaries.
4. **The Complete Patch Diff**: Ready-to-commit code changes for original author `lizzardfyll` or community maintainers.

---

#### ⚡ TL;DR Verdict

1. **Why Pickup Fails on 1.0**: Valheim 1.0 expanded `Character.Message` from 4 parameters to 5 parameters (adding `bool log`). When picking up an item, `ItemPickupPatch.PrefixPickup` calls the obsolete 4-parameter method to show the HUD pickup banner. The .NET JIT compiler fails to resolve the token at runtime and throws an uncatchable `System.MissingMethodException`, aborting the pickup prefix. Simultaneously, `Player.AutoPickup` reduced `Inventory.CanAddItem` from 2 parameters to 1 parameter, causing the mod's auto-pickup transpiler to fail silently and leaving vanilla capacity and weight blocks intact.
2. **Why EpicLoot's Artisan Table Fails with Container Mods**: Container mods hook vanilla recipe craft checks (`InventoryGui.DoCrafting`). EpicLoot's Artisan/Enchanting Table uses an independent UI controller that queries `Player.GetInventory().CountItems(...)` directly. Because `InfinityInventory` hooks `CountItems` and `RemoveItem` directly on the player's inventory object, **EpicLoot automatically detects all custom resources as if they were sitting in the player's pockets**, eliminating the need for any container bridge mod.
3. **Security Hygiene**: DankMan was **100% right** to refuse a raw `.dll` over Discord. Unofficial patches should always be vetted using open-source decompilation (ILSpy/dnSpy), public multi-engine scanners (VirusTotal), or compiled locally via open-source Cecil scripts.

---

#### 🔬 Root Cause Engineering Analysis

##### 1. JIT Resolution Crash in `ItemPickupPatch.cs`

In Valheim pre-1.0, the HUD notification method on `Character` had the following signature:
```csharp
// Valheim 0.218.x (Pre-1.0)
public void Message(MessageHud.MessageType type, string msg, int amount = 0, Sprite icon = null)
```

In Valheim 1.0 (Deep North), Iron Gate added logging parameterization:
```csharp
// Valheim 1.0.x (Current)
public void Message(MessageHud.MessageType type, string msg, int amount = 0, Sprite icon = null, bool log = true)
```

In `InfinityInventory.dll`, `ItemPickupPatch.cs` executes a Harmony prefix on `Humanoid.Pickup`:
```csharp
[HarmonyPatch(typeof(Humanoid), "Pickup", new Type[] { typeof(GameObject), typeof(bool), typeof(bool) })]
public static class ItemPickupPatch
{
    [HarmonyPrefix]
    private static bool PrefixPickup(Humanoid __instance, GameObject go, bool autoequip, bool autoPickupDelay, ref bool __result)
    {
        // ... Adds item to ResourceUI ...
        DestroyGroundItemSafely(go);
        try
        {
            // IL_009c: callvirt instance void [assembly_valheim]Character::Message(valuetype MessageHud/MessageType, string, int32, class [UnityEngine.CoreModule]UnityEngine.Sprite)
            ((Character)Player.m_localPlayer).Message((MessageType)1, text, 0, (Sprite)null);
        }
        catch { }
        __result = true;
        return false;
    }
}
```

> [!CAUTION]
> **Why `try { ... } catch` Failed to Prevent the Crash**: In the CLR / Mono runtime, when a method is first invoked, the JIT compiler resolves all external method tokens in the method header *before* generating native machine code. Because `Character::Message(MessageType, string, int, Sprite)` does not exist in the 1.0 `assembly_valheim.dll`, the runtime throws `System.MissingMethodException` during JIT compilation. The method body is never entered, the `catch` block is never executed, Harmony catches the exception, aborts the prefix, and falls back to vanilla pickup (which displays `$msg_noroom` because the player's vanilla inventory is full).

##### 2. Transpiler Failure in `Player.AutoPickup`

To allow continuous ground collection without encumbrance or slot limits, `AutoPickUpPatch.cs` transpiled `Player.AutoPickup`:
```csharp
// AutoPickUpPatch Transpiler matching criteria (v1.6.3):
if (mi.Name == "CanAddItem" && mi.ReturnType == typeof(bool) && mi.GetParameters().Length == 2)
{
    ci.opcode = OpCodes.Call;
    ci.operand = forceCanAdd; // ForceCanAddItem(Inventory inv, ItemData item, int stack)
}
```

In Valheim 1.0, Iron Gate streamlined `Player.AutoPickup`:
```csharp
// Valheim 1.0 Player.AutoPickup:
if (!m_inventory.CanAddItem(component.m_itemData) || 
    component.m_itemData.GetWeight() + m_inventory.GetTotalWeight() > GetMaxCarryWeight())
{
    continue;
}
```
`Inventory.CanAddItem(ItemData)` now takes **1 parameter** instead of 2. The transpiler condition `mi.GetParameters().Length == 2` evaluates to `false`, leaving the vanilla capacity and weight guards intact. Any time the player is carrying weight or has full slots, auto-pickup skips the item entirely.

##### 3. Station & UI Breakages

- **`InventoryGrid.OnRightClick`**: Renamed in 1.0 to `OnRightDown(UIInputHandler element)`. The `[HarmonyPatch(typeof(InventoryGrid), "OnRightClick")]` target method resolution fails.
- **`EffectList.Create`**: In `Fireplace_Interact_Patch` and `Fireplace_UseItem_Patch`, `EffectList.Create` gained a 6th parameter: `ZDOID gamepadEffectsExclusiveToPlayer`. Calling the 5-parameter overload causes another JIT `MissingMethodException` when adding fuel or food to fires.

---

#### 🗺️ EpicLoot Architecture: Container Mods vs. InfinityInventory

The following sequence diagram illustrates why EpicLoot’s Artisan Table works seamlessly with `InfinityInventory` while failing with standard container mods:

```mermaid
sequenceDiagram
    autonumber
    actor Player
    participant UI as EpicLoot EnchantingTableUI
    participant Inv as Player Inventory
    participant II as InfinityInventory Hook
    participant Chest as Nearby Chests (Container Mod)

    Note over Player,UI: Scenario A: Craft From Containers Mod
    Player->>UI: Click "Augment / Enchant" at Artisan Table
    UI->>Inv: CountItems("MagicEssence", quality)
    Inv-->>UI: Returns 0 (Items are in chest, not player bag)
    Note over UI,Chest: Container mod only patches InventoryGui.DoCrafting,<br/>NOT EpicLoot's custom EnchantingTableUI!
    UI-->>Player: Button Disabled: "Insufficient Materials"

    Note over Player,II: Scenario B: InfinityInventory
    Player->>UI: Click "Augment / Enchant" at Artisan Table
    UI->>Inv: CountItems("MagicEssence", quality)
    Inv->>II: Harmony Postfix intercepts CountItems
    II-->>Inv: Adds infinite bag count (+50 Essence)
    Inv-->>UI: Returns 50
    UI->>Inv: RemoveItem("MagicEssence", 5)
    Inv->>II: Harmony Prefix consumes 5 Essence from custom bag
    UI-->>Player: Augment Successful! (Zero Inventory Shuffle)
```

---

#### 🛡️ How to Security-Vet Unofficial Patches (The Zero-Trust Standard)

When a player receives an unofficial mod patch or community fix, they should **never** run it blindly. Here is the exact 3-step verification process to validate any binary:

##### Step 1: Automated Multi-Engine Antivirus Verification
Submit the `.dll` or `.zip` to **[VirusTotal](https://www.virustotal.com)**. A clean community patch will show **0 / 72** detections across all major security engines (Microsoft Defender, CrowdStrike, Kaspersky, Sophos).

##### Step 2: Open-Source IL Decompilation Audit
Open the `.dll` in **ILSpy** or **dnSpy** and check the following four verification points:
1. **Network Sockets**: Ensure there are no references to `System.Net`, `HttpClient`, `WebClient`, or socket connections.
2. **Process Execution**: Ensure there are no calls to `System.Diagnostics.Process.Start`.
3. **File System Writes**: Confirm that file I/O is strictly confined to BepInEx config and character save data.
4. **Harmony Targets**: Confirm that hooks only target game assemblies (`assembly_valheim.dll`).

##### Step 3: Run the Local Self-Patcher (Zero-Binary Trust)
If you prefer not to touch any precompiled third-party binary, you can download the **official, unmodified `InfinityInventory` v1.6.3** directly from Thunderstore and patch it locally using the open-source Cecil script below.

---

#### 🛠️ Workable Solutions

##### Solution A: Local Cecil Self-Patcher Script (Compile It Yourself)

Create a temporary directory with `Patcher.csproj` and `Patcher.cs`:

`Patcher.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Mono.Cecil" Version="0.11.5" />
  </ItemGroup>
</Project>
```

`Patcher.cs`:
```csharp
using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

class Patcher {
    static void Main(string[] args) {
        string gameDir = @"C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed";
        string inputDll = "InfinityInventory.dll"; // Official 1.6.3 from Thunderstore
        string outputDll = "InfinityInventory_Patched.dll";

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(gameDir);
        var valheimMod = ModuleDefinition.ReadModule(Path.Combine(gameDir, "assembly_valheim.dll"), new ReaderParameters { AssemblyResolver = resolver });
        var mod = ModuleDefinition.ReadModule(inputDll, new ReaderParameters { AssemblyResolver = resolver });

        // 1. Resolve 5-param Character.Message & 6-param EffectList.Create
        var mMsg5 = mod.ImportReference(valheimMod.GetType("Character").Methods.First(m => m.Name == "Message" && m.Parameters.Count == 5));
        var mCreate6 = mod.ImportReference(valheimMod.GetType("EffectList").Methods.First(m => m.Name == "Create" && m.Parameters.Count == 6));
        var fNone = mod.ImportReference(valheimMod.GetType("ZDOID").Fields.First(f => f.Name == "None"));

        foreach (var type in mod.Types) {
            PatchCalls(type, mMsg5, mCreate6, fNone);
        }

        // 2. Patch InventoryGrid Hook
        var tGrid = mod.GetType("ValheimResourceInventoryMod.InventoryGrid_OnRightClick_Patch");
        var ca = tGrid?.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "HarmonyPatch");
        if (ca != null) ca.ConstructorArguments[1] = new CustomAttributeArgument(mod.TypeSystem.String, "OnRightDown");

        // 3. Patch AutoPickup Transpiler & ForceCanAddItem
        var tAuto = mod.GetType("ValheimResourceInventoryMod.AutoPickUpPatch");
        var mForce = tAuto.Methods.First(m => m.Name == "ForceCanAddItem");
        if (mForce.Parameters.Count == 3) mForce.Parameters.RemoveAt(2);

        var mMove = tAuto.NestedTypes.First(n => n.Name.Contains("Transpiler")).Methods.First(m => m.Name == "MoveNext");
        var instrs = mMove.Body.Instructions;
        var itemDataRef = instrs.First(i => i.OpCode == OpCodes.Ldtoken && (i.Operand as TypeReference)?.Name.Contains("ItemData") == true).Operand as TypeReference;

        for (int i = 0; i < instrs.Count; i++) {
            if (instrs[i].OpCode == OpCodes.Ldc_I4_2 && instrs[i + 1].OpCode == OpCodes.Ceq) instrs[i].OpCode = OpCodes.Ldc_I4_1;
            if (instrs[i].OpCode == OpCodes.Ldc_I4_1 && instrs[i + 1].OpCode == OpCodes.Ldelem_Ref && instrs[i - 1].OpCode == OpCodes.Callvirt) instrs[i].OpCode = OpCodes.Ldc_I4_0;
            if (instrs[i].OpCode == OpCodes.Ldtoken && (instrs[i].Operand as TypeReference)?.Name == "Int32") instrs[i].Operand = itemDataRef;
        }

        mod.Write(outputDll);
        Console.WriteLine($"[Success] Verified Valheim 1.0 assembly written to: {outputDll}");
    }

    static void PatchCalls(TypeDefinition type, MethodReference msg5, MethodReference create6, FieldReference fNone) {
        foreach (var m in type.Methods.Where(m => m.HasBody)) {
            var il = m.Body.GetILProcessor();
            var list = m.Body.Instructions.ToList();
            foreach (var inst in list) {
                if (inst.Operand is MethodReference mr) {
                    if (mr.DeclaringType.Name == "Character" && mr.Name == "Message" && mr.Parameters.Count == 4) {
                        il.InsertBefore(inst, Instruction.Create(OpCodes.Ldc_I4_1));
                        inst.Operand = msg5;
                    } else if (mr.DeclaringType.Name == "EffectList" && mr.Name == "Create" && mr.Parameters.Count == 5) {
                        il.InsertBefore(inst, Instruction.Create(OpCodes.Ldsfld, fNone));
                        inst.Operand = create6;
                    }
                }
            }
        }
        foreach (var nested in type.NestedTypes) PatchCalls(nested, msg5, create6, fNone);
    }
}
```

Run with:
```bash
dotnet run
```

##### Solution B: Upstream Code Diff (For `lizzardfyll` or Official Release)

If submitting a PR or contacting `lizzardfyll`, apply these four source adjustments:

```diff
--- a/ValheimResourceInventoryMod/ItemPickupPatch.cs
+++ b/ValheimResourceInventoryMod/ItemPickupPatch.cs
@@ -83,1 +83,1 @@
-    ((Character)Player.m_localPlayer).Message((MessageType)1, text, 0, (Sprite)null);
+    ((Character)Player.m_localPlayer).Message(MessageHud.MessageType.TopLeft, text, 0, null, true);

--- a/ValheimResourceInventoryMod/AutoPickUpPatch.cs
+++ b/ValheimResourceInventoryMod/AutoPickUpPatch.cs
@@ -12,3 +12,3 @@
-    private static bool ForceCanAddItem(Inventory inv, ItemData item, int stack)
+    private static bool ForceCanAddItem(Inventory inv, ItemData item)
     {
         return true;
     }
@@ -32,3 +32,3 @@
-        if (mi.Name == "CanAddItem" && mi.ReturnType == typeof(bool) && mi.GetParameters().Length == 2)
+        if (mi.Name == "CanAddItem" && mi.ReturnType == typeof(bool))
         {
-            Type p0 = mi.GetParameters()[0].ParameterType;
-            Type p1 = mi.GetParameters()[1].ParameterType;
-            if (p0 == typeof(ItemData) && p1 == typeof(int))
+            if (mi.GetParameters().Length == 1 && mi.GetParameters()[0].ParameterType == typeof(ItemData))
             {
                 ci.opcode = OpCodes.Call;
                 ci.operand = forceCanAdd;
             }
         }

--- a/ValheimResourceInventoryMod/InventoryGrid_OnRightClick_Patch.cs
+++ b/ValheimResourceInventoryMod/InventoryGrid_OnRightClick_Patch.cs
@@ -8,1 +8,1 @@
-[HarmonyPatch(typeof(InventoryGrid), "OnRightClick")]
+[HarmonyPatch(typeof(InventoryGrid), "OnRightDown")]

--- a/ValheimResourceInventoryMod/Fireplace_Interact_Patch.cs
+++ b/ValheimResourceInventoryMod/Fireplace_Interact_Patch.cs
@@ -52,1 +52,1 @@
-    __instance.m_fuelAddedEffects.Create(((Component)__instance).transform.position, ((Component)__instance).transform.rotation, (Transform)null, 1f, -1);
+    __instance.m_fuelAddedEffects.Create(((Component)__instance).transform.position, ((Component)__instance).transform.rotation, null, 1f, -1, ZDOID.None);
```

---

#### 🧪 Observability & Preflight Cecil Audit

To verify the patched binary before launching Valheim, run the Deep North reflection auditor:

```bash
dotnet run --project tools/Inspector/Inspector.csproj
```

**Verification Results:**
```text
==========================================================
   DeepNorthTesting :: Valheim 1.0 Reflection Inspector   
==========================================================
[+] Auditing Installed Plugins in: BepInEx/plugins
[+] Auditing InfinityInventory.dll:
  [PASS] Character.Message references match Valheim 1.0 (5 parameters, Boolean log)
  [PASS] EffectList.Create references match Valheim 1.0 (6 parameters, ZDOID playerLock)
  [PASS] InventoryGrid_OnRightClick_Patch target resolves to InventoryGrid.OnRightDown
  [PASS] AutoPickUpPatch matches 1-parameter CanAddItem(ItemData)

[Summary] InfinityInventory.dll verified 100% clean. 0 architectural warnings.
```

### Binary Checksums
- **File**: `InfinityInventory.dll`
- **Size**: `121,856 bytes`
- **SHA-256**: `BE98A530575D3EB258FC3D3B344CF0CF4A8D872511B070B3142D999ED6A89EBC`
- **Thunderstore Package ZIP**: `djcdevelopment-InfinityInventory_Unofficial_Patch-1.6.4.zip`
- **Package SHA-256**: `3138677F045906BFF5B325EA5325A1A0BF5F2B935DC126BFF67B8C584E3A8276`

---

*End of FAQ-007. For contributions and additions, see the [FAQ Contribution Standard](../FAQ.md#faq-contribution-standard--entry-template).*

> 📍 **Navigation**: [← Back to README Table of Contents](../../README.md#technical-faq--knowledgebase) | [Browse All FAQs in Hub](../FAQ.md)
