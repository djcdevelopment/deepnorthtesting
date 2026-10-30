# Valheim 1.0.21 compatibility evidence

- Run: `20261030T115435Z`
- Status: **pass**
- Steam build: `26249040`
- Game assembly SHA-256: `C39A1F03D7918B725366E711CDCB08F72F637FC675877856D4B48C0B5092D882`
- Valheim Profile Manager restored profile: `plugins_local`
- Valheim Profile Manager restored target: `/home/derek/.local/share/Steam/steamapps/common/Valheim/BepInEx/plugins_local`

| Mod | Candidate | Supported Valheim | Build | Hook audit | Isolated boot | DLL SHA-256 |
|---|---:|---|---|---|---|---|
| IsModded | 1.0.8 | 1.0.0-1.0.21 | pass | pass | pass, 12.060s | `BDC9526CF53498D06DCB3D57923C6961FFC17C6C3AFCB493087DF206F6314DC8` |
| SelfieStick | 0.3.6 | 1.0.0-1.0.21 | pass | pass | pass, 12.049s | `01DD4912192C98C371D1FACB2D449B10D61A6D4D23D79A5A4BD335C6008D42BF` |
| Unfaded | 1.0.12 | 1.0.0-1.0.21 | pass | pass | pass, 12.041s | `9A4BEA9B524203370F4B4618FCE81EB0231FB805FC7BE97A0CF94E56D1B5D12C` |
| TotemSentinel | 1.5.6 | 1.0.0-1.0.21 | pass | pass | pass, 12.060s | `BF355A26821D97B8CE34B38069C46D98E590BAB9BD98702154D4CD0C00CD4838` |
| Unswayed | 1.0.6 | 1.0.0-1.0.21 | pass | pass | pass, 12.042s | `C11411D1E8346C36B0DB99F091A7F94717754AE822B6E1588EED91B5C7A79750` |

Raw BepInEx and Unity logs and package ZIPs are stored beside this report. Publishing is a separate protected step and consumes these exact ZIPs.

Setup attempts are retained separately and receive no runtime credit. Final boots require exact plugin/version loading, chainloader completion, Steam initialization and the Amb_MainMenu scene marker.
