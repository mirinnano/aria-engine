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

## Runtime Scenarios

| Scenario | Required Checks |
| --- | --- |
| Startup | config load, init load, main script load |
| Save | manual save, load, corrupt save behavior |
| Persistence | read keys, flags, counters, skip unread |
| UI | title, chapter select, ADV, NVL, menus |
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
