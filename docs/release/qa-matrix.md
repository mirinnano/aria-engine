# QA Matrix

## Environments

| Area | Required Checks |
| --- | --- |
| OS | Windows 10, Windows 11 |
| Path | ASCII path, Japanese path, OneDrive path |
| Storage | normal folder, read-only folder behavior |
| Display | 100% DPI, high DPI, fullscreen/windowed behavior |
| Audio | normal audio device, no audio device |
| Input | mouse, keyboard, gamepad unavailable |
| Windows Native | `win-x64-fd-singlefile`, `win-x64-sc-singlefile`, trim/AOT experimental launch gate |
| Web/PWA | Chrome, Edge, Safari, mobile browser, offline static package launch |
| Language | ja-JP default, en-US fallback, zh-CN, zh-TW, language switch, missing key fallback, font glyph coverage |
| Runtime Profile | Debug, Demo, Release, `--profile`, production mode, dev hotkey policy, debug command policy |
| Steam | portable Depot layout, `steam_appid.txt` local run, Steam Cloud save path, overlay-safe launch |

## Runtime Scenarios

| Scenario | Required Checks |
| --- | --- |
| Startup | config load, init load, main script load |
| Save | manual save, load, corrupt save behavior |
| Persistence | read keys, flags, counters, skip unread |
| UI | title, chapter select, ADV, NVL, menus |
| Demo Flow | PROLOGUE through DAY 4, `scenario_05.aria` branch to `demo_end`, no normal DAY 5+ unlock |
| Promo Links | `browser_open` user-click only, Steam, X, official site, unsafe URL rejection, allowlist enforcement |
| Browser Parity | 16:9 scaling, font loading, text rasterization tolerance, UI hit testing, right-click menu |
| Web Storage | IndexedDB save/settings, OPFS large local assets when enabled, export/import backup |
| Story Locale | locale-specific scenario file first, Japanese fallback file second, stable `readid` continuity |
| Stress | rapid skip, save/load repeated, menu repeated |
| Long run | one hour idle, long backlog |

## Visual Regression Screens

- title screen
- chapter select
- ADV textbox
- NVL screen
- save menu
- load menu
- backlog menu
- right-click menu
- settings screen
- gallery screen
- demo_end share/store/SNS screen
