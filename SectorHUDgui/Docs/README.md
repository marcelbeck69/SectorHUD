# SectorHUD for ETS2 / ATS
*Know exactly which map mod you're on*

If you play Euro Truck Simulator 2 or American Truck Simulator and run a heavily modded map setup (e.g. ProMods, Rusmap, Roextended, various regional additions), you've probably wondered at some point: which mod is actually active right here?
SectorHUD answers that question in real time, directly on your screen.

![in-game overlay](https://ets2.marsthechemist.ch/sectorhud/overlay.png)

## What is does

SectorHUD is a small Windows overlay tool for ETS2 and ATS. While you drive, it displays a compact, always-visible HUD that shows:

- **The active map mod** at your current position – identified by sector, ranked by load priority, so you always see the top mod first
- **Job info** – origin, destination, and – most usefully – your estimated real-world arrival time, so you know whether you'll make it before dinner
- **A real-time clock**, so you don't have to alt-tab just to check the time

Behind the scenes, SectorHUD maintains a SQLite database of all your active mods and the map sectors they cover. It reads your mod list directly from `game.log.txt` and scans each .scs/.zip for its sector footprint. The overlay updates automatically as you drive across mod boundaries.

## Setup is straightforward

Install, point it at your game folders (usually auto-detected), run a one-time database scan, start the monitor. The overlay sits in a corner of your screen – position, font, size, and transparency are all adjustable. Multi-monitor setups are supported.

## Requirements

- Windows 10/11 (64-bit) Operating system
- .NET 6 or later Application runtime environment
- Euro Truck Simulator 2 or American Truck Simulator from SCS Software (Steam or standalone)
- FunBits Telementry Web Server from https://github.com/funbit/ets2-telemetry-server
- SK-ZK Extractor from https://github.com/sk-zk/Extractor

## Download & Links

- Download: https://ets2.marsthechemist.ch/sectorhud/SectorHUD-Setup.zip
- Documentation: https://ets2.marsthechemist.ch/sectorhud/SectorHUD%20Manual.pdf
- Website: https://ets2.marsthechemist.ch/sectorhud.php
- SCS Forum: https://forum.scssoft.com/viewtopic.php?t=351723

## License

This software is released to the public under the GNU GPL-2.0 license.  
SectorHUD is an unofficial community project and is not affiliated with SCS Software.

## Screenshots

![Main Window](https://ets2.marsthechemist.ch/sectorhud/active_windows_e.png)
![Settings](https://ets2.marsthechemist.ch/sectorhud/settings_e.png)
![In game](https://ets2.marsthechemist.ch/sectorhud/screenshot.png)
