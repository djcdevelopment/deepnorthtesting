# Valheim 1.0.17 fleet compatibility evidence

- Status: **PASS**
- Run: `20261008T082620Z`
- Host: `OMEN`
- Completed: 2026-10-08 08:27:40 UTC

## Game baseline

- Valheim version detected from `Version..cctor`: `1.0.17`
- Steam build: `25730771`
- `assembly_valheim.dll` SHA-256: `25A0A107DCE4D834C44C2B72D0EAFD5CB7793933BDA81816ACCFA1EA9543DACE`
- BepInEx package dependency: `denikson-BepInExPack_Valheim-5.4.2202`
- Test method: release build, Cecil hook/reflection audit, one isolated main-menu boot per mod, BepInEx error scan, `tcli` package validation
- Profile restoration: `BepInEx/plugins` returned to `BepInEx/profiles/sovereign-suite`

## Results

| Mod | Candidate | Declared support | Build | Hook/reflection audit | Isolated boot | DLL SHA-256 | Package SHA-256 |
|---|---:|---|---|---|---|---|---|
| IsModded | 1.0.6 | 1.0.0-1.0.17 | PASS | PASS | PASS, 11.59s | `7C7868BF043BB0E1C8C976EF440CC9C149469FB3575A3ADB313803DE9753D5DC` | `DBB2F590592CBC73E5EC196B60BD30D18BD6473C92DADC1A74A5223736AB81CF` |
| SelfieStick | 0.3.4 | 1.0.0-1.0.17 | PASS | PASS | PASS, 13.04s | `2B1276822408B439884D2722175709E9F3A1152AB14BBE76ECBB9C4F11785DA4` | `7F95C54B7A440E2A887D63A1AF9AEA77FB675A327847FB9DD4BCC5F5C71438FF` |
| Unfaded | 1.0.10 | 1.0.0-1.0.17 | PASS | PASS | PASS, 12.56s | `9C82A5A1805A5C4E63D4CECD23B86017C197A5AB321C8C41C53BC96264DC1FB9` | `8EC09A968BE717950790A38ED83E2B88255D2183BEB868C468693394E89DB04F` |
| TotemSentinel | 1.5.4 | 1.0.0-1.0.17 | PASS | PASS | PASS, 13.07s | `5082311D53C7E28ECC94251667FB0256CDE0FA99EFF6FD8B69A501ED868FF408` | `2052AC7941D79072064DAE69C9E4CAEA504176B9400ED9987C40432268F0906A` |
| Unswayed | 1.0.4 | 1.0.0-1.0.17 | PASS | PASS | PASS, 12.54s | `641B411BC7AFECD80A5B52FD49D10E46A8925256E0064A373FF17C3255EA7CDB` | `8F33821B44B18427C4EE58ED84F23C9B7A76D54C9F6B6E54BFCA33C208CFEC18` |

Every boot contained the exact `Loading [<plugin> <candidate>]` BepInEx record and completed with zero matched errors, exceptions, or Harmony failures.

The exact five ZIPs above were also checked against Thunderstore's live API. Each candidate is newer than the currently published package: IsModded 1.0.5, SelfieStick 0.3.3, TotemSentinel 1.5.3, Unfaded 1.0.9, and Unswayed 1.0.3.

## Patch-specific findings

All declared Harmony hooks, IL references, and reflection surfaces across the fleet remain 100% binary-compatible with Valheim 1.0.17:
- `Achievements.IsCheatedAtAll()` continues to read `Game.isModded` in 1.0.17. IsModded 1.0.6 successfully isolates this check without altering Iron Gate's cheat/bypass logic.
- TotemSentinel 1.5.4 resolves camp radar sectors via `m_width` and `ZoneSystem.SectorToIndex` without regression.
- SelfieStick 0.3.4, Unfaded 1.0.10, and Unswayed 1.0.4 camera, HUD, death, and locomotion hooks all bound and loaded cleanly.

## Scope

This evidence supports package compatibility and clean initialization on 1.0.17. It does not claim a full gameplay regression pass. Achievement unlock, death/respawn, live sonar, crypt camera, and photographic capture scenarios should be repeated when their feature code changes.

The raw run output is generated locally under `artifacts/compatibility/valheim-1.0.17/20261008T082620Z/`; CI uploads the equivalent directory as a 30-day GitHub Actions artifact.
