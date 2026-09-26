# Valheim 1.0.16 fleet compatibility evidence

- Status: **PASS**
- Run: `20260926T110355Z`
- Host: `OMEN`
- Completed: 2026-09-26 11:05:11 UTC

## Game baseline

- Valheim version detected from `Version..cctor`: `1.0.16`
- Steam build: `25527674`
- `assembly_valheim.dll` SHA-256: `96CFC004F7F4A6F30D070BEF39EAFD79C466A137121C4665A2F19FB9C15C6127`
- BepInEx package dependency: `denikson-BepInExPack_Valheim-5.4.2202`
- Test method: release build, Cecil hook/reflection audit, one isolated main-menu boot per mod, BepInEx error scan, `tcli` package validation
- Profile restoration: `BepInEx/plugins` returned to `BepInEx/profiles/sovereign-suite`

## Results

| Mod | Candidate | Declared support | Build | Hook/reflection audit | Isolated boot | DLL SHA-256 | Package SHA-256 |
|---|---:|---|---|---|---|---|---|
| IsModded | 1.0.5 | 1.0.0-1.0.16 | PASS | PASS | PASS, 11.54s | `FB4F6DFC6CBC01EA9C52B8FBD1392F9A8DEB6EB359EE09EE8B8AE51083F7692B` | `DCC8CC9B7D0C10438C0163D22FC57177C1073901CF018D788AD190B926BD0509` |
| SelfieStick | 0.3.3 | 1.0.0-1.0.16 | PASS | PASS | PASS, 12.54s | `2DBF5E3B274345FB5B0BB8F7BC9FC9589F72C3FD4EE85EE64BB136887B558A3A` | `688EB342EC898D9E945495504DE3A60076CD1AD29876F30B2E21647787932AEE` |
| Unfaded | 1.0.9 | 1.0.0-1.0.16 | PASS | PASS | PASS, 12.56s | `E9A7E8560A966DA6111C37520078B74C04D8D0B4C7B9C71AA0DF9D473C47E77B` | `67A044501AEBBE76CAFD8D7E30B7BE1A6085416AFE98B6BDC210E08DB8AEC45C` |
| TotemSentinel | 1.5.3 | 1.0.0-1.0.16 | PASS | PASS | PASS, 13.04s | `F7B418669BC5922548F9E6CD8F6A095E0DFC2320704E563664ABDE1E137F5A6F` | `FC23053D3331E7957C9FA07AE8C945B33EDFC26A50DD2FBE1F6AD7DC097DB67C` |
| Unswayed | 1.0.3 | 1.0.0-1.0.16 | PASS | PASS | PASS, 12.52s | `78B17D8206968732B0E13F1EBB1296CE4E49385266295DF2F4B1D25AD2F2115B` | `7A2B05E98A9A22C674D54D32F12A0B5FA1A24FE723FD91487AFF1E8487CDA712` |

Every boot contained the exact `Loading [<plugin> <candidate>]` BepInEx record and completed with zero matched errors, exceptions, or Harmony failures.

The exact five ZIPs above were also checked against Thunderstore's live API. Each candidate is newer than the currently published package: IsModded 1.0.4, SelfieStick 0.3.2, TotemSentinel 1.5.2, Unfaded 1.0.8, and Unswayed 1.0.2.

## Patch-specific findings

All declared Harmony hooks, IL references, and reflection surfaces across the fleet remain 100% binary-compatible with Valheim 1.0.16:
- `Achievements.IsCheatedAtAll()` continues to read `Game.isModded` in 1.0.16. IsModded 1.0.5 successfully isolates this check without altering Iron Gate's cheat/bypass logic.
- TotemSentinel 1.5.3 resolves camp radar sectors via `m_width` and `ZoneSystem.SectorToIndex` without regression.
- SelfieStick 0.3.3, Unfaded 1.0.9, and Unswayed 1.0.3 camera, HUD, death, and locomotion hooks all bound and loaded cleanly.

## Scope

This evidence supports package compatibility and clean initialization on 1.0.16. It does not claim a full gameplay regression pass. Achievement unlock, death/respawn, live sonar, crypt camera, and photographic capture scenarios should be repeated when their feature code changes.

The raw run output is generated locally under `artifacts/compatibility/valheim-1.0.16/20260926T110355Z/`; CI uploads the equivalent directory as a 30-day GitHub Actions artifact.
