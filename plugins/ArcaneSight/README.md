# Arcane Sight

> **Ethereal 3D World Inspection & Rune Projection for Valheim 1.0**

[![Valheim 1.0](https://img.shields.io/badge/Valheim-1.0%20(Deep%20North)-blue.svg)](#)
[![BepInEx 5](https://img.shields.io/badge/BepInEx-5.4.2202-green.svg)](#)
[![Client Side](https://img.shields.io/badge/Architecture-100%25%20Client--Side-success.svg)](#)
[![AI Assisted](https://img.shields.io/badge/Development-AI--Assisted-blueviolet.svg)](#)

---

## ?? Overview

Tired of walking up to 30 chests in your storehouse just to find your iron? Wondering if a remote portal is linked before stepping through? 

**Arcane Sight** grants vikings an ethereal, in-world rune projection lens. At the press of a key (`F7`), nearby interactive world objects are illuminated with gentle rune-light and project floating, legible 3D HUD markers directly above them.

---

## ? Features

- ?? **Portal Inspection:** Instantly view portal destination tags (e.g. `"NORTH_EXPEDITION"`) and live connection status (`[Connected]` in cyan or `[Unconnected]` in red) across your base.
- ?? **Chest & Container Overview:** View slot occupancy (`8/18 slots`, `Full`), total item count, and a preview of the top items inside without opening the chest.
- ?? **Legible 3D Signs:** Floats sign text above wooden and stone signs in clean, readable rune banners.
- ?? **Processing Station Status:** Check beehives (`Honey: 4/4`), fermenters (`Mead: Fermenting (72%)`), and smelters (`Fuel: 10/10, Ore: 20/20`) from across your courtyard.
- ? **Creator OS / Quest Ready:** Natively recognizes and highlights Creator OS / Comfy Quest charm bindings and ritual shrines in mystic purple.
- ??? **Zero Multiplayer Desync:** 100% client-side rendering. Never mutates synchronized ZDOs or sends network RPCs.

---

## ?? Controls & Configuration

- **Toggle Key:** Press `F7` (configurable in `BepInEx/config/com.djcdevelopment.valheim.arcanesight.cfg`).
- **Scan Radius:** Default `28m` (smoothly culled by camera frustum and distance).
- **Visuals:** Toggle ethereal rune point lights, item preview detail, and structure stability in the config file.

---

## ?? AI Disclosure & Provenance

In accordance with open source transparency and community guidelines:
- **AI-Assisted Engineering:** Engineered through collaborative human-AI pair programming (Google DeepMind Antigravity / Gemini) under human architectural direction.
- **Verification:** 100% verified on sovereign local hardware (OMEN rig) with clean reflection audits and zero runtime errors.
- **Ecosystem:** Part of the sovereign **Creator OS / Comfy Quest** project line.

---

## ?? License

Distributed under the [MIT License](LICENSE). Copyright (c) 2026 djcdevelopment.
