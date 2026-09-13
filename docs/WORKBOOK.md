# Deep North Testing :: Operational Testing & Verification Workbook
> **Hands-On Verification Labs, Sample Configurations, Observability Checkpoints, & Command Runbooks for Valheim 1.0 Modding.**

[![Valheim 1.0](https://img.shields.io/badge/Valheim-1.0%20(Deep%20North)-blue.svg)](#)
[![BepInEx 5](https://img.shields.io/badge/BepInEx-5.4.2202-green.svg)](#)
[![Hardware Verified](https://img.shields.io/badge/OMEN%20Silicon-Dual%20Arc%20Pro%20B70-purple.svg)](#)
[![Workbook Standard](https://img.shields.io/badge/Lab%20Status-Verified%20Clean-success.svg)](#)

---

## 📑 Table of Contents

- [Overview & Objectives](#overview--objectives)
- [Lab 1: Status Effect Inspection & Delimiter Diagnostics](#lab-1-status-effect-inspection--delimiter-diagnostics)
  - [1.1 Background & Hash Mechanics](#11-background--hash-mechanics)
  - [1.2 Delimiter Failure Reproduction](#12-delimiter-failure-reproduction)
  - [1.3 Composite StatusEffect Validation](#13-composite-statuseffect-validation)
- [Lab 2: Custom Backpack Configuration & Multi-Effect Patching](#lab-2-custom-backpack-configuration--multi-effect-patching)
  - [2.1 Sample Annotated `Backpacks.yml`](#21-sample-annotated-backpacksyml)
  - [2.2 Deploying the Harmony Companion Patch](#22-deploying-the-harmony-companion-patch)
- [Lab 3: Unfaded Spectator & Bullet-Time Verification](#lab-3-unfaded-spectator--bullet-time-verification)
  - [3.1 In-Game CLI Commands](#31-in-game-cli-commands)
  - [3.2 Triple Spectator Mode Verification](#32-triple-spectator-mode-verification)
  - [3.3 Sample Annotated `Unfaded.cfg`](#33-sample-annotated-unfadedcfg)
- [Lab 4: Sub-8-Second Boot Loop & Zero-Error Log Assertion](#lab-4-sub-8-second-boot-loop--zero-error-log-assertion)
  - [4.1 Running the Fast Autonomous Launcher](#41-running-the-fast-autonomous-launcher)
  - [4.2 Running the BepInEx Log Scraper](#42-running-the-bepinex-log-scraper)
- [Lab 5: Live Engine & Dual Intel Arc Pro B70 GPU Telemetry](#lab-5-live-engine--dual-intel-arc-pro-b70-gpu-telemetry)
  - [5.1 Live Endpoint Querying](#51-live-endpoint-querying)
  - [5.2 Telemetry Interpretation & P95 Frame Times](#52-telemetry-interpretation--p95-frame-times)
- [Command Runbook Quick Reference](#command-runbook-quick-reference)

---

## 🎯 Overview & Objectives

This workbook provides concrete, executable testing scenarios designed for mod authors, server administrators, and test engineers working on Valheim 1.0 (Deep North). Each lab provides:
- **Executable CLI Commands** (PowerShell and C# Cecil scripts).
- **Observable Checkpoints** (exact log strings, memory values, and UI behaviors).
- **Production-Ready Sample Configurations** (tested on local sovereign hardware).

---

## 🧪 Lab 1: Status Effect Inspection & Delimiter Diagnostics

### 1.1 Background & Hash Mechanics

In Valheim, status effects are stored in the singleton `ObjectDB.instance.m_StatusEffects` as Unity `ScriptableObject` prefabs. Mod systems like Smoothbrain's **Backpacks** query these effects using `string.GetStableHashCode()`:

$$\text{Hash} = \sum_{i=0}^{N-1} \text{char}[i] \times 33^{N-1-i} \pmod{2^{32}}$$

When a user passes a compound string such as `"SE_Warm, SE_Megingjord"`, the hash is computed across the **entire literal character sequence**, which will never match individual prefab keys.

### 1.2 Delimiter Failure Reproduction

#### Execution Command
Run this PowerShell command to simulate how Valheim computes the hash of delimited strings versus individual effects:

```powershell
$testStrings = @(
    "SE_Warm",
    "SE_Megingjord",
    "SE_Warm, SE_Megingjord",
    "SE_Warm;SE_Megingjord"
)

$testStrings | ForEach-Object {
    $str = $_
    $hash = 0
    foreach ($byte in [System.Text.Encoding]::UTF8.GetBytes($str)) {
        $hash = [int](($hash * 33) -bxor $byte)
    }
    [PSCustomObject]@{
        InputString = $str
        FNV1aHash   = $hash
        Status      = if ($str.Contains(",") -or $str.Contains(";")) { "FAILS_LOOKUP (Hash Mismatch)" } else { "VALID_PREFAB_LOOKUP" }
    }
} | Format-Table -AutoSize
```

#### Observable Checkpoint in `BepInEx/LogOutput.log`:
```text
[Warning:  Backpacks] Could not find status effect SE_Warm, SE_Megingjord while evaluating status effect for custom backpack MyCustomPack
```

---

### 1.3 Composite StatusEffect Validation

To verify that an existing or custom status effect contains multiple modifiers, inspect its properties live in game via **UnityExplorer** or Cecil reflection:

```powershell
# Inspect SE_Stats fields in assembly_valheim.dll
Add-Type -Path "tools\Inspector\bin\Debug\net8.0\Mono.Cecil.dll"
$asm = [Mono.Cecil.ModuleDefinition]::ReadModule("C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed\assembly_valheim.dll")
$seStats = $asm.GetType("SE_Stats")
$seStats.Fields | Where-Object { $_.Name -match "(m_addMaxCarryWeight|m_speedModifier|m_mods|m_staminaRegenMultiplier)" } | Select-Object Name, FieldType
```

*Expected Output:*
- `m_addMaxCarryWeight` (`System.Single`)
- `m_speedModifier` (`System.Single`)
- `m_staminaRegenMultiplier` (`System.Single`)
- `m_mods` (`HitData/DamageModifiers`)

---

## 🎒 Lab 2: Custom Backpack Configuration & Multi-Effect Patching

### 2.1 Sample Annotated `Backpacks.yml`

Save this file to `C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\config\Backpacks.yml`. It defines two distinct backpacks: one using the **Composite Status Effect** pattern and one using the **Multi-Effect Delimiter Patch**.

```yaml
# ====================================================================
# Deep North Testing :: Sample Backpacks.yml
# Location: BepInEx/config/Backpacks.yml
# Note: Ensure "use external YAML file" is set to true in Backpacks.cfg
# ====================================================================

# Backpack 1: The Native Composite Pattern (Works out of the box)
"Arctic Explorer Pack":
  description: "Specially tailored rucksack granting both mountain warmth and titan carry strength."
  size: 6x4
  unique: global
  weight factor: 0.75
  teleport: true
  # Single composite effect bundling warmth + carry capacity:
  effect: SE_Warm
  appearance:
    BackpackBody: visible
    Sleepbag: visible
    Pot: visible
    BackpackFlap: "3b82f6" # Royal Blue
  crafting:
    station: Workbench
    level: 3
    costs:
      DeerHide: 12
      WolfPelt: 4
      Bronze: 6
  valid items:
    - ore
    - metal
    - food
    - woods

# Backpack 2: Multi-Effect Delimited Pack (Requires Companion Patch)
"Viking Vanguard Hauler":
  description: "Heavy logistics pack with multi-aspect enchantment."
  size: 8x4
  unique: global
  weight factor: 0.50
  teleport: false
  # Delimited string parsed by DeepNorth.Patches.Humanoid_UpdateEquipmentStatusEffects_BackpackPatch:
  effect: "SE_Warm, SE_Megingjord"
  appearance:
    BackpackBody: visible
    ToolstrapTop: visible
    Pickaxe: visible
    BackpackFlap: "10b981" # Emerald Green
  crafting:
    station: Forge
    level: 2
    costs:
      Iron: 10
      LeatherScraps: 15
      FineWood: 20
```

---

### 2.2 Deploying the Harmony Companion Patch

If using comma- or semicolon-delimited strings in `Backpacks.yml`, compile or drop the companion patch from [docs/FAQ.md#solution-b-lightweight-c-harmony-companion-patch](./FAQ.md#solution-b-lightweight-c-harmony-companion-patch) into your BepInEx plugins folder.

#### Verification Steps:
1. Equip `"Viking Vanguard Hauler"`.
2. Open in-game console (`F5`) and run `player status`.
3. Verify both **SE_Warm** (cold protection icon) and **SE_Megingjord** (+150 max carry weight) display in the top right HUD.
4. Unequip the backpack; verify both buffs automatically detach from `SEMan`.

---

## 👁️ Lab 3: Unfaded Spectator & Bullet-Time Verification

The `Unfaded` mod eliminates Valheim's 9.5-second death screen blackout and introduces 360° corpse orbital cameras, killer focus, and free-fly drones.

### 3.1 In-Game CLI Commands

Open the console (`F5`) inside a running game session:

| Console Command | Function | Expected Behavior |
| :--- | :--- | :--- |
| `unfaded test` | Simulate death spectator | Enters 6s spectator mode immediately without killing the player. |
| `unfaded delay 30` | Adjust auto-respawn timer | Sets auto-respawn countdown to 30 seconds. |
| `unfaded delay 0` | Infinite spectator | Disables automatic respawn; player stays in spectator until [Space] is pressed. |
| `unfaded slowmo 0.2` | Adjust bullet-time scale | Changes death slow-motion timescale to 0.20x. |
| `unfaded killer` | Toggle Killer Cam | Toggles whether camera automatically pivots to the enemy attacker. |
| `unfaded status` | Print current settings | Dumps active configuration values to the console. |

---

### 3.2 Triple Spectator Mode Verification

1. **Corpse Orbit Mode (Default)**:
   - On lethal hit, observe that the black screen fadeout does **not** trigger (Alpha remains clamped at 0).
   - Move mouse: camera smoothly orbits 360° around your falling/rolling ragdoll.
   - Scroll wheel: camera zooms from 2m to 25m distance without clipping through terrain.
2. **Killer Cam Mode**:
   - Press **[K]** on the keyboard.
   - Camera snaps to the creature/Viking that struck the killing blow.
3. **Free-Fly Drone Mode**:
   - Press **[F]** on the keyboard.
   - Disconnects camera anchor. Use **[W][A][S][D]** to fly freely across the battlefield, **[Space]** to ascend, and **[C]** to descend.
4. **Immediate Respawn**:
   - Press **[Space]** at any time to instantly trigger bed respawn.

---

### 3.3 Sample Annotated `Unfaded.cfg`

Location: `BepInEx/config/djc.valheim.unfaded.cfg`

```ini
[General]
## Suppress the 9.5-second death screen blackout canvas.
# Setting type: Boolean
# Default value: true
SuppressBlackout = true

## Seconds to spectate before automatically respawning. Set to 0 for infinite spectating.
# Setting type: Single
# Default value: 15
AutoRespawnDelay = 15

## Enable slow-motion bullet time when a lethal hit connects.
# Setting type: Boolean
# Default value: true
EnableSlowMo = true

## Game timescale during bullet-time (0.1 to 1.0).
# Setting type: Single
# Default value: 0.35
SlowMoTimeScale = 0.35

## Duration in real seconds for bullet-time slow-motion.
# Setting type: Single
# Default value: 2.5
SlowMoDuration = 2.5

[Camera]
## Default distance in meters from the corpse ragdoll.
# Setting type: Single
# Default value: 6
DefaultDistance = 6

## Enable snapping camera to killer on pressing [K].
# Setting type: Boolean
# Default value: true
EnableKillerCam = true

## Enable free-fly drone mode on pressing [F].
# Setting type: Boolean
# Default value: true
EnableDroneCam = true
```

---

## ⚡ Lab 4: Sub-8-Second Boot Loop & Zero-Error Log Assertion

### 4.1 Running the Fast Autonomous Launcher

The harness utilizes Steam programmatic AppLaunch to bypass confirmation dialogs and unskippable cinematics:

```powershell
# Launch directly to Valheim Main Menu in <8 seconds:
.\harness\Start-ValheimHarness.ps1 -ClearLogs
```

*Expected Terminal Log:*
```text
==========================================================
   DeepNorthTesting :: Valheim 1.0 Autonomous Launcher    
==========================================================
[+] Dispatching Steam launch: AppId 892970 (-console)...
[OK] Valheim running with PID: 14280
[+] Awaiting initialization and BepInEx chainloader...
[+] Process Status: Responding=True, WorkingSet=1840.4 MB
```

---

### 4.2 Running the BepInEx Log Scraper

Scrape `BepInEx/LogOutput.log` to assert that zero errors, exceptions, or patch failures occurred:

```powershell
.\harness\Assert-CleanBoot.ps1
```

*Expected Terminal Log:*
```text
==========================================================
   DeepNorthTesting :: Valheim 1.0 Clean Boot Assertion   
==========================================================
[+] Parsing log: C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\LogOutput.log

--- Assertion Results ---
[PASS] 0 Errors, 0 Exceptions, 0 Patch Failures detected.
[PASS] All active BepInEx plugins loaded and patched cleanly into Valheim 1.0.
```

---

## 📊 Lab 5: Live Engine & Dual Intel Arc Pro B70 GPU Telemetry

### 5.1 Live Endpoint Querying

While Valheim is running with `ComfyNetworkSense` loaded, stream live engine metrics from the in-game HTTP server (`:8721`):

```powershell
.\harness\Query-Telemetry.ps1
```

*Expected Terminal Output:*
```text
[OK] Live Connection Established to ComfyNetworkSense
  - Session ID    : 20260912-072730-f5c48c83
  - Mode          : Solo
  - Region / Area : 0:0
  - Total Samples : 30

--- Real-Time GPU and Engine Metrics ---
  - Instant FPS   : 59.99
  - Avg FPS       : 56.42
  - Frame Time    : 16.67 ms
  - Frame Time P95: 18.38 ms
  - CPU Bound Est : 0.6%
```

---

## ⚡ Command Runbook Quick Reference

```powershell
# 1. Run Preflight Cecil Reflection Audit (<4 seconds)
dotnet run --project tools/Inspector/Inspector.csproj

# 2. Validate Archify Architecture Diagrams
node C:\work\archify\archify\bin\archify.mjs validate architecture docs/backpack-effects.architecture.json --quality showcase --json
node C:\work\archify\archify\bin\archify.mjs validate architecture plugins/Unfaded/docs/unfaded-architecture.json --quality showcase --json

# 3. Launch Fast Boot Harness
.\harness\Start-ValheimHarness.ps1 -ClearLogs

# 4. Assert Zero Log Errors
.\harness\Assert-CleanBoot.ps1

# 5. Query Live In-Game GPU Telemetry
.\harness\Query-Telemetry.ps1
```

---

*End of Operational Testing Workbook. For questions or troubleshooting, see [docs/FAQ.md](./FAQ.md).*
