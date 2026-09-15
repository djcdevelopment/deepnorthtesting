# Unfaded :: 35-Second In-Game Showcase Video Storyboard & Production Guide
> **High-Conversion Video Blueprint for Reddit (r/valheim), YouTube Shorts, and Thunderstore.**

---

## 🎯 Video Thesis & The Psychological Hook

- **The Problem**: In vanilla Valheim, dying forces a **9.5-second pitch-black screen** (`m_loadingScreen.alpha`). You can't see the physics, you can't see who killed you, and you can't plan your corpse run.
- **The Solution**: **Unfaded** deletes the blackout entirely, adds cinematic bullet-time ragdoll slow-mo, a killer cam (`[K]`), free-fly drone scouting (`[F]`), and manual spacebar respawn (`[Space]`).
- **Target Duration**: **32–38 Seconds** (optimal completion rate on Reddit / Shorts / TikTok / Thunderstore preview).

---

## 🎬 Shot-by-Shot Storyboard

| Timestamp | Visual Action | On-Screen Text Overlay | In-Game Sound & Audio |
| :--- | :--- | :--- | :--- |
| **0:00 - 0:06**<br/>*(The Hook)* | **Vanilla Death**: Viking chopping a tree in Meadows. A falling log crushes the player.<br/>**Screen instantly fades to pitch black.** Cursor sits on black screen for 5 seconds. | `VANILLA VALHEIM:`<br/>`9.5 seconds of forced black screen void ⬛` | Heavy tree thud $\rightarrow$ Death groan $\rightarrow$ **Muffled silence / flatline buzzer**. |
| **0:06 - 0:13**<br/>*(The Contrast)* | **Unfaded Death**: Same tree crushes Viking.<br/>**Time slows to 0.35x bullet-time.** Viking ragdoll cartwheels hilariously over a fence in full color.<br/>Screen stays 100% crystal clear. | `WITH UNFADED:`<br/>`Zero blackout. Cinematic bullet-time slow-mo ⚡` | Dramatic slow-mo whoosh $\rightarrow$ Crisp physics crunch $\rightarrow$ Energetic upbeat Viking track begins. |
| **0:13 - 0:19**<br/>*(Feature 1)* | **Corpse Orbit & Death Banner**: Mouse smoothly rotates 360° around the cartwheeled ragdoll in the grass.<br/>Recap banner appears at top of HUD: `💀 Slain by: Gravity / Fall Damage [-120]`. | `360° Corpse Orbit & Combat Recap Banner` | Soft wind ambiance + banner chime. |
| **0:19 - 0:25**<br/>*(Feature 2)* | **Killer Cam (`[K]`)**: Cut to Plains. Player gets sniped from behind by a 2★ Fuling Berserker.<br/>Player taps **`[K]`**: Camera swings smoothly and snaps onto the massive brute swinging his club and walking away. | `Tap [K]: Killer Cam`<br/>`See exactly who got you 👀` | Punchy camera snap sound $\rightarrow$ Fuling cackle. |
| **0:25 - 0:31**<br/>*(Feature 3)* | **Free-Fly Drone (`[F]`)**: Cut to dark Swamp sunken ruins.<br/>Player dies near a Draugr spawner. Tombstone drops.<br/>Player taps **`[F]`**: Camera untethers! Fly in 60m radius with WASD over walls and water, spotting 3 Draugr archers guarding the grave. | `Tap [F]: Free-Fly Drone Recon`<br/>`Scout your grave BEFORE the naked run 🪓` | Drone rotor / wind lift sound. |
| **0:31 - 0:36**<br/>*(The Payoff)* | **Instant Respawn & CTA**: Player taps **`[Space]`** while flying.<br/>Instantly wakes up in bed ready to roll.<br/>Clean title card appears with Thunderstore badge and author. | `Tap [Space] to Instant Respawn`<br/>**UNFADED**<br/>`Free on Thunderstore • Mod by djcdevelopment` | Viking horn fanfare $\rightarrow$ Fade out. |

---

## 🎮 10-Minute In-Game Recording Setup Runbook

You can stage and record all 4 scenes locally in single-player without risking survival characters:

### 1. Enable Cheats & Clean HUD
Press `F5` in-game and run:
```text
devcommands
god
```

### 2. Scene 1 & 2: The Tree Physics Kill
1. Walk into Meadows near a Birch or Beech tree.
2. Type `god` to toggle invulnerability OFF.
3. Chop the tree base until it falls toward you, or spawn a falling log:
   ```text
   spawn Beech_log 1
   ```
4. Record the death with `DisableDeathFade = false` (or standard game) for the 5-second black screen clip.
5. Turn `Unfaded` ON. Let the tree hit you again to capture the 0.35x bullet-time ragdoll flight and 360° mouse orbit!

### 3. Scene 3: The Killer Cam (`[K]`)
1. Teleport to the Plains:
   ```text
   goto 1500 50 1500
   ```
2. Spawn an aggressive enemy right next to you:
   ```text
   spawn GoblinBrute 1 2
   ```
   *(Spawns a 2-Star Fuling Berserker)*
3. Let him deliver the killing blow.
4. Immediately press **`K`** on your keyboard: the camera locks onto the Berserker.

### 4. Scene 4: The Swamp Drone Recon (`[F]`)
1. Teleport to Swamp:
   ```text
   goto -500 30 1200
   ```
2. Type in console to trigger spectator mode without dying:
   ```text
   unfaded test
   ```
   *(Or let a Draugr kill you)*
3. Immediately press **`F`**.
4. Fly forward with `W`, ascend with `E`, descend with `Q`, and pan across the trees and crypt entrance.
5. Press **`Space`** to end the test.

---

## 📢 Ready-to-Post Copy for Community Launch

### Reddit Post (r/valheim)
**Title**:  
*I got sick of staring at a 10-second black screen every time a tree crushed me, so I made Unfaded: zero blackout, bullet-time slow-mo, and a free-fly drone to scout your grave before your naked corpse run.*

**Body**:  
> Every time you die in vanilla Valheim, the game slaps a 9.5-second pitch-black canvas over your camera while your ragdoll is doing hilarious physics in the background.
>
> We built **Unfaded** to fix that:
> - **Zero Blackout**: 100% viewport clarity on death.
> - **Bullet-Time Slow-Mo**: 0.35x cinematic slow motion on fatal hits.
> - **Corpse Orbit**: Full 360° mouse look and zoom around your ragdoll.
> - **Killer Cam (`[K]`)**: Locks camera onto the enemy that killed you.
> - **Free-Fly Drone (`[F]`)**: Fly up to 60m away to scout your tombstone and enemy patrols before running back naked.
> - **Instant Respawn (`[Space]`)**: Done watching? Hit spacebar to wake up in bed immediately.
>
> Download free on Thunderstore: https://thunderstore.io/c/valheim/p/djcdevelopment/Unfaded/
