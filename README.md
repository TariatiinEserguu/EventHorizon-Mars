# Space Design Game

A basic space-themed design/build game project created for the **2026 NASA Hackathon**.

## Overview

In this game, players design and build their own space setup by combining different components and making strategic choices.

## Goals

- Build and customize a space design
- Make efficient design decisions
- Create a stable and high-performing setup

## Project Status

This project is currently in early development.

## Getting Started

Clone the repository:

```bash
git clone https://github.com/TariatiinEserguu/Space-design-game.git
cd Space-design-game
```

## Unity project setup (EVENT HORIZON: MARS)

The repository root is a Unity project (Unity 6, URP; tested with 6000.6.8f1).

1. Fetch the free third-party assets (Python 3, standard library only):
   ```bash
   python3 tools/fetch_assets.py          # Kenney, Poly Haven, Solar System Scope, NASA
   pip install gdown                      # optional: lets the script try the Quaternius Google Drive packs
   ```
   Do this **before** opening Unity, so the committed `.meta` files find their assets.
2. Open the folder with Unity Hub (*Add > Add project from disk*) using a Unity 6 editor (tested: 6000.6.8f1). On first load
   `ProjectBootstrap` switches to linear colour and creates and assigns a URP pipeline asset.
3. Build everything (prefabs, the MainMenu / MarsColony / SpaceLab scenes, asset validation and a Linux player)
   from the command line:
   ```bash
   Unity -batchmode -nographics -projectPath . -executeMethod EventHorizon.EditorTools.BuildScript.SetupAndBuildLinux -quit
   ./Builds/Linux/EventHorizonMars.x86_64
   ```
   Or in the editor use the **Event Horizon > Assets** menu items 0 to 4, then open `Scenes/MainMenu` and press Play.

Controls: WASD move, mouse look, Shift run, Ctrl crouch, Space jump, **E** interact, **V** first/third person,
Tab/I inventory, J missions, M map, **Esc** pause. All keys can be rebound in Settings > Controls.

Visual test: `./Builds/Linux/EventHorizonMars.x86_64 -eh-screenshots <folder>` tours the menus, the colony
(day, sunset, night, dust storm, terminal, pause menu) and the black-hole lab. It saves a screenshot of each view
plus `performance.txt` (FPS and memory), then quits.

Licenses and attribution for every asset are in [`ASSET_CREDITS.md`](ASSET_CREDITS.md), generated from
[`tools/asset_manifest.json`](tools/asset_manifest.json).

## Contributing

Contributions are welcome. Open an issue or submit a pull request with improvements.
