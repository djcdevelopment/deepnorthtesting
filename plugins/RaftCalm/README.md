# RaftCalm (Valheim `1.0.20`)

> **Eliminates `WearNTear.UpdateSupport()` World-Space Cache Thrashing & Frame Spikes on Moving `ValheimRAFT` Ships.**

[![Valheim 1.0.20](https://img.shields.io/badge/Valheim-1.0.20%20Verified-blue.svg)](../../docs/faq/FAQ-010-valheimraft-boat-fps-lag-wearntear-support.md)
[![BepInEx 5.4.2202](https://img.shields.io/badge/BepInEx-5.4.2202-green.svg)](#)
[![Mono.Cecil Audit](https://img.shields.io/badge/Mono.Cecil-PASS%20(52ms)-success.svg)](#)

## ⚡ Overview

In Valheim `1.0.20`, `WearNTear` caches structural support vectors in world space (`private List<Vector3> m_supportPositions`) and computes piece Center of Mass via `GetCOM()` (`transform.position + transform.rotation * m_comOffset`). On a moving `ValheimRAFT` vessel, every ocean wave pitches and translates `100–600+` parented building pieces simultaneously, invalidating their world-space support cache and triggering expensive `Physics.OverlapSphere` / `FindSupportPoint` cascades while the player is aboard (`Ship.GetLocalShip() != null`).

`RaftCalm` patches `[HarmonyPatch(typeof(WearNTear), "UpdateSupport")]` with a fast Prefix that skips structural support recalculations (`m_noSupportWear = true; return false;`) for pieces within `ProtectRadius` (`25m` default, configurable up to `100m`) whenever `Ship.GetLocalShip() != null`.

- 📖 **Full Bytecode Deep Dive:** [FAQ-010: Why Does ValheimRAFT Cause Severe FPS Drops When Boarding a Boat?](../../docs/faq/FAQ-010-valheimraft-boat-fps-lag-wearntear-support.md)
- 🌐 **Interactive Archify System Map:** [`raftcalm-support.architecture.html`](../../docs/raftcalm-support.architecture.html)
- 📥 **Pre-Compiled Binary:** [`bin/Release/RaftCalm.dll`](./bin/Release/RaftCalm.dll)
- 🎓 **Learn How It Was Built:** [Valheim 1.0 Modding 101 Workbench](https://delightful-sand-0451e171e.1.azurestaticapps.net/)

[![RaftCalm Architecture](../../docs/raftcalm-support.architecture.visual-check.1440x900.dark.png)](../../docs/raftcalm-support.architecture.html)

## 🛠️ Features & Configuration (`com.deepnorthtesting.raftcalm.cfg`)

- **`EnableMod`** (`true`): Master switch with live `.cfg` hot-reload.
- **`ProtectRadius`** (`25.0`, range `5.0–100.0`): Radius in meters around `Player.m_localPlayer` while aboard a `Ship` where `WearNTear.UpdateSupport()` is bypassed.
