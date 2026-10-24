# CartRighter (Valheim `1.0.20`)

> **Universal `Vagon` Cart Recovery & Auto-Righting Plugin for Vanilla and Modded Carts.**

[![Valheim 1.0.20](https://img.shields.io/badge/Valheim-1.0.20%20Verified-blue.svg)](../../docs/faq/FAQ-008-cartflipper-1-0-modded-carts-vagon.md)
[![BepInEx 5.4.2202](https://img.shields.io/badge/BepInEx-5.4.2202-green.svg)](#)
[![Mono.Cecil Audit](https://img.shields.io/badge/Mono.Cecil-PASS%20(51ms)-success.svg)](#)

## ⚡ Overview

`CartRighter` is a modern Valheim `1.0.20` replacement for `Wylker/CartFlipper`. Instead of matching hardcoded vanilla prefab names (`"Cart"`), `CartRighter` patches `[HarmonyPatch(typeof(Vagon), "Update")]` directly—meaning it works out of the box with **100% of vanilla and modded carts** (*Better Carts*, *OdinHorse* wagons, custom bronze/iron/oak carts).

- 📖 **Full Bytecode Deep Dive:** [FAQ-008: CartFlipper 1.0.20 Alternative & Modded Cart `Vagon` Architecture](../../docs/faq/FAQ-008-cartflipper-1-0-modded-carts-vagon.md)
- 🌐 **Interactive Archify System Map:** [`cartrighter-vagon.architecture.html`](../../docs/cartrighter-vagon.architecture.html)
- 📥 **Pre-Compiled Binary:** [`bin/Release/CartRighter.dll`](./bin/Release/CartRighter.dll)
- 🎓 **Learn How It Was Built:** [Valheim 1.0 Modding 101 Workbench](https://delightful-sand-0451e171e.1.azurestaticapps.net/)

[![CartRighter Architecture](../../docs/cartrighter-vagon.architecture.visual-check.1440x900.dark.png)](../../docs/cartrighter-vagon.architecture.html)

## 🛠️ Features & Configuration (`com.deepnorthtesting.cartrighter.cfg`)

- **`EnableMod`** (`true`): Master toggle with live `FileSystemWatcher` hot-reload.
- **`AutoRightWhenTipped`** (`true`): Automatically rights any nearby cart when its tilt angle (`Vector3.Angle(transform.up, Vector3.up)`) exceeds `AutoFlipAngle` (`45°`).
- **`FlipKey`** (`70` = `KeyCode.F`): Press **`F`** while attached to or within `MaxDistance` (`10m`) of any cart to manually claim `ZNetView` ownership, lift `+0.35m`, zero linear/angular velocity, and restore upright yaw orientation.
