# Agent Entry Contract — Deep North Testing (`deepnorthtesting`)

Welcome, Agent! This repository hosts the autonomous Valheim `1.0.21` (*Deep North*) modding harness, zero-copy profile switcher, `Mono.Cecil` bytecode auditor (`tools/Inspector`), and the `./vh` CLI toolkit used to build, audit, boot-verify, and publish the [`djcdevelopment` Thunderstore fleet](https://thunderstore.io/c/valheim/p/djcdevelopment/).

---

## 1. Quick CLI & Harness Primitives (`./vh` & `harness/`)

Before writing or modifying any Valheim C# plugin, run the deterministic harness tools:

| Goal | Command (Linux / WSL2 Bash) | Command (Windows PowerShell) | Latency |
| :--- | :--- | :--- | :---: |
| **1. Environment & 1.0.21 Hash Check** | `./vh doctor` | `python tools\vh.py doctor` | `~110 ms` |
| **2. Search 1.0.21 Game Signatures (`Mono.Cecil` / `ilspycmd`)** | `./vh api-search "SEMan.AddStatusEffect"` | `python tools\vh.py api-search "SEMan.AddStatusEffect"` | `< 1.0 s` |
| **3. Compile + Zero-Leak Gate + `Mono.Cecil` Bytecode Audit** | `./vh audit plugins/Unswayed/Unswayed.csproj` *(or `bash harness/fast-verify-mod.sh <Mod.csproj>`)* | `python tools\vh.py audit plugins\Unswayed\Unswayed.csproj` | `~600 ms` (`~55 ms` Cecil) |
| **4. Zero-Copy Profile Swap (`0 Bytes` Copied)** | `bash tools/switch-profile.sh --switch isolated-unfaded` | `powershell -ExecutionPolicy Bypass -File tools\switch-profile.ps1 -Profile isolated-unfaded` | `< 1 ms` (Linux) / `~42 ms` (Win) |
| **5. Live `.cfg` / `.dll` Hot-Swap** | `bash tools/switch-profile.sh --hotswap <path.cfg>` | `python tools\vh.py hot-swap <path>` | `< 1 ms` |
| **6. Autonomous Steam Fast-Boot Verification (`~12.05s`)** | See [docs/compatibility/valheim-1.0.21.md](docs/compatibility/valheim-1.0.21.md) | `.\harness\Start-ValheimHarness.ps1` + `.\harness\Assert-CleanBoot.ps1` | `~12.05 s` to `Amb_MainMenu` |

---

## 2. Non-Negotiable Valheim `1.0.21` Engineering Rules

1. **Always Query `assembly_valheim.dll` Before Guessing Signatures:**
   - Never rely on 2021–2023 (`0.217`–`0.218`) training weights. Run `./vh api-search "<Type.Member>"` (or `dotnet tools/Inspector/bin/Release/net8.0/Inspector.dll --query-symbol "<Type.Member>"`) to inspect the exact `1.0.21` signature from `/dev/shm/valheim-index`.
2. **Target `net48` with `<Private>false</Private>` on All Game/BepInEx References:**
   - Never copy `UnityEngine*.dll`, `0Harmony.dll`, `BepInEx.dll`, or `assembly_*.dll` into `BepInEx/plugins/`.
3. **Key Valheim `1.0` Breaking Shifts Enforced by `tools/Inspector`:**
   - `ZDOMan.FindSectorObjects(Vector2s sector, ...)` uses `Vector2s` (`assembly_utils.dll`), not `Vector2i`.
   - `SEMan.AddStatusEffect(int nameHash, bool resetTime, int itemLevel, float skillLevel, short variant)` requires the 5th `short variant` parameter.
   - `Humanoid.Pickup(GameObject go, bool autoequip, bool autoPickupDelay)` takes 3 parameters.
   - `Character.Message(MessageHud.MessageType type, string msg, int amount, Sprite icon, bool large)` takes 5 parameters.
   - `PieceTable.SetCategory` is overloaded; always disambiguate parameter types in `[HarmonyPatch]`.
4. **Ship an `F5` Simulator Command (`<modname> test`):**
   - Modeled on `unfaded test` (`Unfaded`) and `totem 1` (`TotemSentinel`), every gameplay mod should include a terminal command in `Terminal.InitTerminal` so mechanics can be verified in-game immediately without grinding or sailing to specific biomes.
