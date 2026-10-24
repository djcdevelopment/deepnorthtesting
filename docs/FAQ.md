# Deep North Testing :: Technical FAQ & Knowledgebase
> **Authoritative Engineering Answers, IL Decompilation Breakdowns, & Operational Runbooks for Valheim 1.0 Modding.**

[![Valheim 1.0](https://img.shields.io/badge/Valheim-1.0%20(Deep%20North)-blue.svg)](#)
[![BepInEx 5](https://img.shields.io/badge/BepInEx-5.4.2202-green.svg)](#)
[![Documentation Standard](https://img.shields.io/badge/Doc%20Standard-Engineering%20Grade-success.svg)](#)

> 📍 **Navigation**: [← Back to Root README](../README.md#technical-faq--knowledgebase) | [Operational Testing Workbook](./WORKBOOK.md)

---

## 📖 About This Knowledgebase

This knowledgebase serves as the sovereign engineering reference for the **Deep North Testing** project. Unlike informal forum threads or Discord chatter, every answer in this repository is backed by:
1. **Decompiled C# Source Analysis** of the target mod and base game assemblies (ssembly_valheim.dll).
2. **Intermediate Language (IL) & Harmony Transpiler Audits**.
3. **Reproducible Workable Solutions** (both zero-code data configurations and C# code patches).
4. **Execution Runbooks & Observability Commands** tested on sovereign local hardware.

All technical articles have been decomposed into modular, standalone deep-dive documents below.

---

## 🗂️ Master FAQ Directory

| Article | Category | Complexity | Summary |
| :--- | :--- | :---: | :--- |
| 🎒 [**FAQ-001: Can You Stack Multiple Status Effects on Backpacks in Smoothbrain's Mod?**](./faq/FAQ-001-backpack-status-effects.md) | Mod Configuration & Item Mechanics | Medium | Explains why comma-separated status effects fail in Backpacks.yml due to 32-bit FNV-1a hash lookup and single-slot SEMan limits; provides composite SE_Stats config and Harmony multi-token companion patch. |
| 🪦 [**FAQ-002: How to Implement a Tombstone Despawn Countdown Timer Without RPC Spam (EWP vs. timeOfDeath)**](./faq/FAQ-002-tombstone-despawn-timer.md) | Prefab Lifecycle & Server Networking | Medium | Compares Expand World Prefabs (EWP) recursive self-poking against native ZDO 	imeOfDeath & m_worldText; eliminates 30x RPC_DamageText network broadcast storms. |
| ⬛ [**FAQ-003: Why Does a Connecting Player Get Stuck on a Black Screen After Entering Password in Client-Hosted Multiplayer?**](./faq/FAQ-003-multiplayer-black-screen.md) | Prefab Lifecycle & Server Networking | Advanced | Diagnoses why peer authentication succeeds but client remains on a black screen (HudBlackScreen); breaks down PlayFab relay NAT throttling vs Steam P2P sockets and corrupted character saves. |
| 🧲 [**FAQ-004: Why Does Auto-Pickup Throw MissingMethodException (Character.Message) and Suppress Notifications in Valheim 1.0?**](./faq/FAQ-004-auto-pickup-missingmethodexception.md) | Runtime Exceptions & Binary Compatibility | Deep IL | Details the Valheim 1.0 Character.Message 5th boolean parameter signature shift; provides HookGen cache purge runbook and Mono.Cecil migration script. |
| 🏃 [**FAQ-005: Why Does the Valheim 1.0 Run Animation Cause Rigid Bobbing and Vertigo in Burial Crypts, and Can It Be Reverted?**](./faq/FAQ-005-crypt-headache-camera-sway.md) | Camera Ergonomics & Animation Mechanics | Medium | Analyzes 1.0 head bone translation bounce in tight crypts; presents the Unswayed camera anchor decoupling architecture and live in-game tuning. |
| 🐧 [**FAQ-006: Why Do Valheim Mods and BepInEx Fail to Load on Linux When Launching Gale Profiles via Steam?**](./faq/FAQ-006-linux-gale-bepinex-loading.md) | Linux Runtime & Mod Manager Architecture | Advanced | Diagnoses Gale's symlink and Linux native launch wrapper architecture; provides zero-copy profile linking and Steam Proton vs Native launch arguments. |
| 📦 [**FAQ-007: How to Resolve Valheim 1.0 Pickup Failure and EpicLoot Artisan Table Incompatibilities in InfinityInventory (And How to Safely Security-Vet Unofficial Patches)**](./faq/FAQ-007-infinity-inventory-pickup-epicloot.md) | Binary Compatibility & Mod Security | Deep IL | Resolves all 4 breaking Valheim 1.0 API changes in InfinityInventory; explains EpicLoot Artisan Table container bypass; provides a 25-line Cecil self-patcher script, upstream diff, and 3-step security vetting runbook. |
| 🛒 [**FAQ-008: What Is a Working Valheim 1.0.20 Alternative to Wylker/CartFlipper That Supports Both Vanilla and Modded Carts?**](./faq/FAQ-008-cartflipper-1-0-modded-carts-vagon.md) | Vehicle Physics & Multiplayer Ownership | Medium | Explains why prefab-name cart flippers miss modded carts and desync in 1.0.20; provides the [`CartRighter`](../plugins/CartRighter) Harmony plugin hooking `Vagon.Update` with `ZNetView.ClaimOwnership()` and auto-upright + hotkey recovery. |
| 🖼️ [**FAQ-009: Why Do Inventory Tooltips Get Sliced Off Along the Equipment Panel Border (PathOfValheim / BTRValheim / Norsgods)?**](./faq/FAQ-009-tooltip-cutout-overflow-uitooltip-fix.md) | UI Layout & Mask Clipping (`assembly_guiutils.dll`) | Deep IL | Traces `UITooltip.AnchorTooltip()` parenting `m_tooltip` inside masked custom `EQUIPMENT` slot anchors; provides the [`TooltipUnclipped`](../plugins/TooltipUnclipped) Harmony Postfix that re-parents tooltips to `rootCanvas`, disables `MaskableGraphic.maskable`, and runs `Utils.ClampUIToScreen`. |
| ⛵ [**FAQ-010: Why Does ValheimRAFT Cause Severe FPS Drops When Boarding a Boat in Valheim 1.0.20?**](./faq/FAQ-010-valheimraft-boat-fps-lag-wearntear-support.md) | Ship Physics & Structural Support Caching | Advanced | Diagnoses `WearNTear.UpdateSupport()` world-space `m_supportPositions` cache invalidation storms and point-light shadow recalculations when `Ship.GetLocalShip() != null`; provides the [`RaftCalm`](../plugins/RaftCalm) Harmony Prefix and step-by-step FPS recovery checklist. |

---

## 📐 FAQ Contribution Standard & Entry Template

All future FAQ entries added to this repository must follow this standardized schema to maintain editorial rigor.

<details>
<summary>📋 <b>Click to expand the FAQ Contribution Standard & Markdown Template Schema</b></summary>

`markdown
# FAQ-XXX: [Descriptive Technical Question]

> 📍 **Navigation**: [← Back to README Table of Contents](../../README.md#technical-faq--knowledgebase) | [Knowledgebase Hub](../FAQ.md)

---

| Metadata | Specification |
| :--- | :--- |
| **Category** | [e.g., Configuration / Item Mechanics / Networking / Shaders] |
| **Target Mods** | [Mod Name & Version, Author] |
| **Engine / Game** | Valheim 1.0 (Deep North) + BepInEx 5.4.2202 |
| **Complexity** | [Low / Medium / Advanced / Deep IL] |
| **Status** | Verified Clean |

#### Context
[Screenshot, Discord inquiry, or GitHub issue quotation]

#### TL;DR Verdict
[Clear, unambiguous one-paragraph answer: Yes/No, why, and what to do]

#### Root Cause Engineering Analysis
[Decompiled source analysis, IL disassembly, and architectural breakdown]

#### Architectural Data Flow
[Archify visual map or sequence diagram illustrating the data/logic flow]

#### Workable Solutions
- **Solution A (Zero-Code / Data-Driven)**: [Easiest workable configuration]
- **Solution B (C# Harmony Extension)**: [Production-ready code solution if config is impossible]

#### Under the Hood: Engine Lifecycle
[Step-by-step trace through Unity/Valheim lifecycle methods]

#### Observability & Diagnostics
[How to inspect live in-game or in log files, what tools to use]

#### Verification Runbook
[Exact copy-paste terminal commands to test and observe]

---

*End of FAQ-XXX. For contributions and additions, see the [FAQ Contribution Standard](../FAQ.md#faq-contribution-standard--entry-template).*

> 📍 **Navigation**: [← Back to README Table of Contents](../../README.md#technical-faq--knowledgebase) | [Browse All FAQs in Hub](../FAQ.md)
`

</details>

---

> 📍 **Navigation**: [← Back to Deep North Testing README](../README.md#technical-faq--knowledgebase)
