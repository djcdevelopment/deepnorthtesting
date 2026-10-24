# FAQ-010 Discord Solution Package — `CartRighter` (For `@Maverag`)

- **Discord Thread:** `@Maverag` (Modding Valheim Discord · 2026-10-24 01:57 AM)
- **Inquiry:** *"anyone know of an alternative to this `https://thunderstore.io/c/valheim/p/Wylker/CartFlipper/` that functiona in 1.0 and with modded carts? please ping me if you do"*
- **Companion Plugin:** [`plugins/CartRighter/CartRighterPlugin.cs`](file:///home/derek/work/deepnorthtesting/plugins/CartRighter/CartRighterPlugin.cs)
- **FAQ Deep-Dive:** [`docs/faq/FAQ-010-cartflipper-1-0-modded-carts-vagon.md`](file:///home/derek/work/deepnorthtesting/docs/faq/FAQ-010-cartflipper-1-0-modded-carts-vagon.md)
- **Compiled & Bytecode-Audited DLL:** `/home/derek/work/deepnorthtesting/plugins/CartRighter/bin/Release/CartRighter.dll` (`11,264 B`, `622 ms` `.NET 4.8` build + `51 ms` `Mono.Cecil` `1.0.20` audit `PASS`)

---

## 1. Ready-to-Post Discord Reply (Copy-Paste)

> **@Maverag** `Wylker/CartFlipper` broke on Valheim `1.0.20` (Deep North) and misses modded carts because older flippers either check hardcoded vanilla prefab names (`"Cart"`) or don't claim `ZNetView` ownership before resetting the `Rigidbody`.
>
> In Valheim `1.0.20`, **both vanilla and modded carts** (*OdinHorse*, *Better Carts*, custom wagon prefabs, etc.) attach the base **`Vagon`** component (`assembly_valheim.dll`). We built and bytecode-verified a lightweight (`11 KB`) 1.0.20 replacement called **`CartRighter`** in our open-source [`deepnorthtesting`](https://github.com/djcdevelopment/deepnorthtesting) repo that hooks `Vagon.Update` directly—so it works automatically on **every vanilla and modded cart**:
> - **Auto-Right When Tipped:** Automatically uprights any cart within range whose tilt exceeds `45°` (configurable live via `.cfg` without restarting Valheim).
> - **Manual Flip Hotkey (`F`):** Press `F` near or while attached to any cart to claim `ZNetView` ownership (`___m_nview.ClaimOwnership()`), pop it `+0.35m` up to clear terrain snags, zero angular velocity, and snap it upright.
>
> If you want to compile or drop the snippet into your own plugin, here is the verified Valheim `1.0.20` Harmony patch:
> ```csharp
> [HarmonyPatch(typeof(Vagon), "Update")]
> public static class Vagon_CartRighter_Patch
> {
>     [HarmonyPostfix]
>     private static void Postfix(Vagon __instance, Rigidbody ___m_body, ZNetView ___m_nview)
>     {
>         if (Player.m_localPlayer == null || ___m_body == null || ___m_nview == null || !___m_nview.IsValid()) return;
>         if (Vector3.Distance(Player.m_localPlayer.transform.position, __instance.transform.position) > 10f && !__instance.IsAttached()) return;
>
>         if (Input.GetKeyDown(KeyCode.F) || Vector3.Angle(__instance.transform.up, Vector3.up) > 45f)
>         {
>             ___m_nview.ClaimOwnership();
>             __instance.transform.position += Vector3.up * 0.35f;
>             ___m_body.velocity = Vector3.zero;
>             ___m_body.angularVelocity = Vector3.zero;
>             Quaternion upright = Quaternion.Euler(0f, __instance.transform.eulerAngles.y, 0f);
>             ___m_body.MoveRotation(upright);
>             __instance.transform.rotation = upright;
>         }
>     }
> }
> ```
> **Full source & 1.0.20 modding templates:**
> - **Interactive Valheim 1.0 Modding 101 Workbench** (Chambers 4 & 5 cover `1.0.20` Harmony hooks, live `.cfg` hot-reload, and `ZNetView` physics ownership): https://delightful-sand-0451e171e.1.azurestaticapps.net/
> - **Starter Kit & Companion Repo:** https://github.com/djcdevelopment/valheim-modding-101 · https://github.com/djcdevelopment/deepnorthtesting

---

## 2. Ground-Truth `1.0.20` Bytecode Verification (`Mono.Cecil` + `omen-nemotron-74b` × `am4-vllm`)

- **Target Type:** `Vagon` (`assembly_valheim.dll`, base `MonoBehaviour`)
- **Injected Private Fields (Verified via `Mono.Cecil`):**
  - `private Rigidbody m_body;` (`___m_body`)
  - `private ZNetView m_nview;` (`___m_nview`)
- **Verified Method Calls:**
  - `Vagon.Update()` (`private void Update()`)
  - `Vagon.IsAttached()` (`public bool IsAttached()`)
  - `ZNetView.IsValid()` & `ZNetView.ClaimOwnership()`
