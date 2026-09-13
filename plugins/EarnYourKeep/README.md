# EarnYourKeep :: Valheim 1.0 Modded Achievement Enabler

> **Lightweight BepInEx Plugin (8.7 KB)** that restores achievement progression while playing Valheim 1.0 with mods.

---

## 🧐 Why Valheim 1.0 Disables Achievements When Modded

In Valheim 1.0 (Ashlands & Deep North), Iron Gate added Steam and platform achievements, accompanied by a check to disable them if the session is detected as "cheated" or "modded":

```csharp
// Valheim 1.0 internal logic inside Achievements.IsCheatedAtAll():
if (profileCheated || worldCheated || itemCheated) {
    Achievements.m_cheatCheckCache = true;
} else {
    Achievements.m_cheatCheckCache = Game.isModded; // <--- The culprit!
}
```

### The Chain of Events:
1. When BepInEx initializes, `BepInEx.Bootstrap.Chainloader.SetIsModdedTrue()` reflects into `Game.isModded` and politely sets it to `true` (honoring an old request from Iron Gate's code comments to indicate modded clients for player bug reports).
2. Valheim 1.0 checks `Achievements.IsCheatedAtAll()` whenever any player stat increments (killing enemies, crafting items, harvesting crops, building structures).
3. Because `Game.isModded == true`, `IsCheatedAtAll()` returns `true`.
4. `Achievements.CanGetAchievements()` returns `false`.
5. All stat incrementers (`PlayerProfile.IncrementStat*`) abort immediately before registering progress or triggering Steamworks `Unlock()`.

---

## ⚡ How `EarnYourKeep` Solves This

`EarnYourKeep` is a surgically lightweight Harmony patch that decouples `Game.isModded` from the achievement system:

1. **Decoupled Mod Check**: It intercepts `Achievements.IsCheatedAtAll()` and evaluates only genuine cheats (`PlayerProfile.m_usedCheats`, `Achievements.IsWorldCheated()`, `Inventory.AnyCheatedItem()`), while ignoring `Game.isModded`.
2. **Legitimate Achievements**: If you play legitimately with quality-of-life mods (like `ComfyMods`, custom UI, crafting helpers, camera controls), achievements trigger and unlock on Steam exactly like vanilla.
3. **Optional Devcommand Bypass**: A config option (`AllowWithDevcommands = true`) lets you earn achievements even if devcommands were used on your character or world.
4. **Visual Watermark Control**: A config option (`HideModdedWatermark = true`) lets you hide the "Modded" text on the main menu if desired.

---

## ⚙️ Configuration (`BepInEx/config/djc.valheim.earnyourkeep.cfg`)

```ini
[General]
## Enable earning achievements while playing with BepInEx / mods loaded.
# Setting type: Boolean
# Default value: true
AllowWhileModded = true

## Enable earning achievements even if devcommands / cheats were used on this character or world.
# Setting type: Boolean
# Default value: false
AllowWithDevcommands = false

[Visual]
## Hide the 'Modded' watermark on the main menu.
# Setting type: Boolean
# Default value: false
HideModdedWatermark = false
```

---

## 📦 Installation
Drop `EarnYourKeep.dll` into your `Valheim/BepInEx/plugins/` directory.
