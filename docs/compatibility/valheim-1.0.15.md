# Valheim 1.0.15 fleet compatibility evidence

- Status: **PASS**
- Run: `20260920T220355Z`
- Host: `OMEN`
- Completed: 2026-09-20 22:05:09 UTC

## Game baseline

- Valheim version detected from `Version..cctor`: `1.0.15`
- Steam build: `25390630`
- `assembly_valheim.dll` SHA-256: `59F53FB55D99D22A33E8ED094EEC8D21E9F133543BCE92BC3D80DCE44033ADB1`
- BepInEx package dependency: `denikson-BepInExPack_Valheim-5.4.2202`
- Test method: release build, Cecil hook/reflection audit, one isolated main-menu boot per mod, BepInEx error scan, `tcli` package validation
- Profile restoration: `BepInEx/plugins` returned to `BepInEx/profiles/sovereign-suite`

## Results

| Mod | Candidate | Declared support | Build | Hook/reflection audit | Isolated boot | DLL SHA-256 | Package SHA-256 |
|---|---:|---|---|---|---|---|---|
| IsModded | 1.0.4 | 1.0.0-1.0.15 | PASS | PASS | PASS, 11.60s | `0B07876C9F183BFE302B43D5603A111255ED922E3FF5C93D357127B09C30C016` | `569B7744F726208F1F60938F72387206255105C743039F7E6E3760C40017AC3A` |
| SelfieStick | 0.3.2 | 1.0.0-1.0.15 | PASS | PASS | PASS, 12.56s | `3BC19A407CE85C96A6344061DA066A878DDC1F59D01AD9642DC7D5FDAAE5086F` | `EED49252B081E6823E11DE1C2C167000A5461C2C98556CE924E514A52A22E9DD` |
| Unfaded | 1.0.8 | 1.0.0-1.0.15 | PASS | PASS | PASS, 12.92s | `2887A027D3CFCC6077BE5A1F9B74D6E62D1DB17A63A81939746F3CC993F171F8` | `2C4D1809EAA98EF62F2419C575B207B74E63124C15852D8C8B804D8D32E61681` |
| TotemSentinel | 1.5.2 | 1.0.0-1.0.15 | PASS | PASS | PASS, 12.58s | `A55C93BF38DE4F404BF81A2868AF4F2D9527529339753E26100EA20F36D223F2` | `00E0F112439102183D8825E087AC41C32C19FEF9B1F5B2E073878AD442E35D45` |
| Unswayed | 1.0.2 | 1.0.0-1.0.15 | PASS | PASS | PASS, 12.57s | `75F210AAA9FF8AC2B150D64F0759095D0963EDE96901264FA1AEFCA90AEFE8E2` | `AA2E2311DB0F2E2226E54540ED2DC344298061CE32ED7CC2885D75C8B8D56460` |

Every boot contained the exact `Loading [<plugin> <candidate>]` BepInEx record and completed with zero matched errors, exceptions, or Harmony failures.

The exact five ZIPs above were also checked against Thunderstore's live API. Each candidate is newer than the currently published package: IsModded 1.0.3, SelfieStick 0.3.1, TotemSentinel 1.5.1, Unfaded 1.0.7, and Unswayed 1.0.1.

## Patch-specific findings

`Achievements.IsCheatedAtAll()` still reads `Game.isModded` in 1.0.15. IsModded 1.0.4 now masks that flag only while the original method executes, then restores it in both postfix and finalizer paths. This retains Iron Gate's 1.0.15 item, world, devcommand, caching, and official achievement-bypass behavior.

The audit found that `ZDOMan.m_halfWidth`, previously reflected by TotemSentinel, is absent in 1.0.15. TotemSentinel 1.5.2 now bounds sectors from `m_width` and resolves indices through the public `ZoneSystem.SectorToIndex(int, int)` API. The isolated log confirms that sector access initialized and gameplay patches installed.

The other declared Harmony and reflection surfaces used by SelfieStick, Unfaded, and Unswayed remain present with their expected signatures.

## Scope

This evidence supports package compatibility and clean initialization on 1.0.15. It does not claim a full gameplay regression pass. Achievement unlock, death/respawn, live sonar, crypt camera, and photographic capture scenarios should be repeated when their feature code changes.

The raw run output is generated locally under `artifacts/compatibility/valheim-1.0.15/20260920T220355Z/`; CI uploads the equivalent directory as a 30-day GitHub Actions artifact.
