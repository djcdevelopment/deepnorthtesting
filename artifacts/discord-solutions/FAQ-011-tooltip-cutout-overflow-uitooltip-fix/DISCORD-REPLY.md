# FAQ-011 Discord Solution Package — `TooltipUnclipped` (For `@Drio!`)

- **Discord Thread:** `@Drio!` (Modding Valheim Discord · 2026-10-24 12:31 PM)
- **Inquiry:** *"Hi there, lurker here. does anybody know what my tooltip gets cut out like this? (im using norsgods + pathofvalheiman + btrvalheim)"*
- **Companion Plugin:** [`plugins/TooltipUnclipped/TooltipUnclippedPlugin.cs`](file:///home/derek/work/deepnorthtesting/plugins/TooltipUnclipped/TooltipUnclippedPlugin.cs)
- **FAQ Deep-Dive:** [`docs/faq/FAQ-011-tooltip-cutout-overflow-uitooltip-fix.md`](file:///home/derek/work/deepnorthtesting/docs/faq/FAQ-011-tooltip-cutout-overflow-uitooltip-fix.md)
- **Compiled & Bytecode-Audited DLL:** `/home/derek/work/deepnorthtesting/plugins/TooltipUnclipped/bin/Release/TooltipUnclipped.dll` (`10,240 B`, `592 ms` `.NET 4.8` build + `51 ms` `Mono.Cecil` `1.0.20` audit `PASS`)

---

## 1. Ready-to-Post Discord Reply (Copy-Paste)

> **@Drio!** Welcome! Look closely at *where* your tooltip is getting sliced off in your screenshot: the cut runs in a razor-sharp straight line along the **exact bottom edge and right gold border of the `EQUIPMENT` box**.
>
> **Why it happens (Bytecode Root Cause):**
> In Valheim `1.0.20` (`assembly_guiutils.dll`), `UITooltip.AnchorTooltip()` executes `m_tooltip.transform.SetParent(m_anchor)`. When **Path of Valheim** (`pathofvalheiman`) / **BTRValheim** sets up that custom `EQUIPMENT` panel, it uses a Unity `RectMask2D` / `Mask` on the panel container and sets the slot's `UITooltip.m_anchor` *inside* that masked container. As soon as `AnchorTooltip()` parents `m_tooltip` to `m_anchor`, the `EQUIPMENT` panel's mask slices off any part of the tooltip that extends past the gold border!
>
> **How to fix it:**
> 1. **Quick Mod isolation check:** As `@OrianaVenture` noted, disabling `PathOfValheim`'s custom equipment UI panel (or adjusting its tooltip anchor config in `BepInEx/config/`) stops it from parenting tooltips inside the masked panel.
> 2. **Universal Client Fix (`TooltipUnclipped`):** We built a tiny `10 KB` client-side Harmony plugin (`TooltipUnclipped` in [`deepnorthtesting`](https://github.com/djcdevelopment/deepnorthtesting)) that hooks `UITooltip.LateUpdate`, re-parents active tooltips to the root `Canvas` (`canvas.rootCanvas.transform`) so no sub-panel mask can clip them, sets `MaskableGraphic.maskable = false`, and re-runs `Utils.ClampUIToScreen(rt)`:
> ```csharp
> [HarmonyPatch(typeof(UITooltip), "LateUpdate")]
> public static class UITooltip_Unclip_Patch
> {
>     [HarmonyPostfix]
>     private static void Postfix(GameObject ___m_tooltip)
>     {
>         if (___m_tooltip == null || !___m_tooltip.activeInHierarchy) return;
>
>         Canvas canvas = ___m_tooltip.GetComponentInParent<Canvas>();
>         if (canvas != null && canvas.rootCanvas != null && ___m_tooltip.transform.parent != canvas.rootCanvas.transform)
>             ___m_tooltip.transform.SetParent(canvas.rootCanvas.transform, true);
>
>         foreach (MaskableGraphic g in ___m_tooltip.GetComponentsInChildren<MaskableGraphic>(true))
>             g.maskable = false;
>
>         if (___m_tooltip.transform.childCount > 0 && ___m_tooltip.transform.GetChild(0) is RectTransform rt)
>             Utils.ClampUIToScreen(rt);
>     }
> }
> ```
> If you want to build or customize the fix yourself, check out **Chamber 4 & Chamber 5** of our open-source **Valheim 1.0 Modding 101 Interactive Workbench** (covers `assembly_guiutils.dll` & `assembly_utils.dll` hooks):
> - **Web Workbench:** https://delightful-sand-0451e171e.1.azurestaticapps.net/
> - **GitHub Starter Kit:** https://github.com/djcdevelopment/valheim-modding-101

---

## 2. Ground-Truth `1.0.20` Bytecode Verification (`Mono.Cecil` + `omen-nemotron-74b` × `am4-vllm`)

- **Target Type:** `UITooltip` (`assembly_guiutils.dll`, base `MonoBehaviour`)
- **IL Sequence in `UITooltip.AnchorTooltip()`:**
  `UITooltip.m_current -> UITooltip.m_tooltip -> UITooltip.m_anchor -> Transform.SetParent(Transform) -> UITooltip.m_fixedPosition -> RectTransform.GetWorldCorners(Vector3[]) -> Transform.GetChild(int) -> Utils.ClampUIToScreen(RectTransform)`
- **Injected Private Static Field:** `private static GameObject m_tooltip;` (`___m_tooltip`)
- **Verified Cross-Assembly Call:** `Utils.ClampUIToScreen(RectTransform)` (`assembly_utils.dll`)
