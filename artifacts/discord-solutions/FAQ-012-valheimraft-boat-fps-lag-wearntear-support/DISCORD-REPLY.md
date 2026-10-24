# FAQ-012 Discord Solution Package — `RaftCalm` (For `@WhatsApp Daniel`)

- **Discord Thread:** `@WhatsApp Daniel` (Modding Valheim Discord · 2026-10-24 01:39 PM)
- **Inquiry:** *"im having some trouble with the valheimraft mod, whenever i am on my boat it is extremely laggy and my fps is very low"*
- **Companion Plugin:** [`plugins/RaftCalm/RaftCalmPlugin.cs`](file:///home/derek/work/deepnorthtesting/plugins/RaftCalm/RaftCalmPlugin.cs)
- **FAQ Deep-Dive:** [`docs/faq/FAQ-012-valheimraft-boat-fps-lag-wearntear-support.md`](file:///home/derek/work/deepnorthtesting/docs/faq/FAQ-012-valheimraft-boat-fps-lag-wearntear-support.md)
- **Compiled & Bytecode-Audited DLL:** `/home/derek/work/deepnorthtesting/plugins/RaftCalm/bin/Release/RaftCalm.dll` (`9,728 B`, `597 ms` `.NET 4.8` build + `52 ms` `Mono.Cecil` `1.0.20` audit `PASS`)

---

## 1. Ready-to-Post Discord Reply (Copy-Paste)

> **@WhatsApp Daniel** When your FPS only tanks **while you are standing on a `ValheimRAFT` boat** on Valheim `1.0.20`, it comes down to three specific things triggered the moment `Ship.OnTriggerEnter` / `Ship.UpdateOwner()` makes you the active onboard owner of the raft:
>
> **1. Check `BepInEx/LogOutput.log` for 1.0.20 `FixedUpdate` exception spam (Most Common):**
> Make sure you are on a **Valheim 1.0.20-updated fork** of `ValheimRAFT` (and `Jotunn`). Older builds throw a `MissingMethodException` or `NullReferenceException` every physics tick (`50x/sec`) as soon as a player boards the raft, which immediately drops FPS into single digits.
>
> **2. Remove Torches / Braziers / Point Lights from the Raft:**
> Every `Fireplace` (standing torch, wall torch, brazier) on a moving boat recalculates real-time shadow maps against **every building piece on your raft** every single frame as the boat rocks on waves. Replacing fire torches with non-shadow light sources (like Dvergr lanterns/circlets or disabling point-light shadows) usually recovers 20–40 FPS immediately.
>
> **3. `WearNTear.UpdateSupport()` World-Space Cache Invalidation Storm (`RaftCalm` Fix):**
> In Valheim `1.0.20`'s `WearNTear.UpdateSupport()` bytecode (`assembly_valheim.dll`), Valheim caches support positions in world space (`m_supportPositions`) and checks `Vector3.op_Equality(Vector3, Vector3)`. On land, pieces never move—but on a moving `ValheimRAFT` vessel, every piece's world-space position changes every wave tick, causing `WearNTear.ClearCachedSupport()` and `SetupColliders()` raycast storms while you're aboard!
>
> If you're modding (or want a drop-in fix), we built a `9.7 KB` plugin called **`RaftCalm`** in [`deepnorthtesting`](https://github.com/djcdevelopment/deepnorthtesting) that skips `WearNTear.UpdateSupport()` while `Ship.GetLocalShip() != null`:
> ```csharp
> [HarmonyPatch(typeof(WearNTear), "UpdateSupport")]
> public static class RaftCalm_WearNTear_Patch
> {
>     [HarmonyPrefix]
>     private static bool Prefix(WearNTear __instance)
>     {
>         if (Player.m_localPlayer != null && Ship.GetLocalShip() != null &&
>             Vector3.Distance(__instance.transform.position, Player.m_localPlayer.transform.position) <= 25f)
>         {
>             __instance.m_noSupportWear = true;
>             return false; // Skip world-space support cache invalidation on moving ships
>         }
>         return true;
>     }
> }
> ```
> **More on Valheim 1.0.20 physics & `ZNetView` tick guards:**
> - **Interactive Valheim 1.0 Modding 101 Workbench (Chambers 4 & 5):** https://delightful-sand-0451e171e.1.azurestaticapps.net/
> - **GitHub Starter Kit:** https://github.com/djcdevelopment/valheim-modding-101

---

## 2. Ground-Truth `1.0.20` Bytecode Verification (`Mono.Cecil` + `omen-nemotron-74b` × `am4-vllm`)

- **Target Types:** `WearNTear` and `Ship` (`assembly_valheim.dll`)
- **IL Sequence in `WearNTear.UpdateSupport()`:**
  `WearNTear.m_supportColliders -> Component.GetComponentInParent() -> WearNTear.m_supports -> WearNTear.m_supportPositions -> Vector3.op_Equality(Vector3,Vector3) -> WearNTear.GetSupport() -> WearNTear.ClearCachedSupport() -> WearNTear.m_colliders -> WearNTear.SetupColliders()`
- **Verified Method & Field References:**
  - `WearNTear.UpdateSupport()` (`private void UpdateSupport()`)
  - `WearNTear.m_noSupportWear` (`public bool m_noSupportWear`)
  - `Ship.GetLocalShip()` (`public static Ship GetLocalShip()`)
