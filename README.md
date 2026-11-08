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

The repository root is a Unity project (Unity 6.3 LTS, URP).

1. Fetch the free third-party assets (Python 3, standard library only):
   ```bash
   python3 tools/fetch_assets.py          # Kenney, Poly Haven, Solar System Scope, NASA
   pip install gdown                      # optional: lets the script try the Quaternius Google Drive packs
   ```
   Do this **before** opening Unity, so the committed `.meta` files find their assets.
2. Open the folder with Unity Hub (*Add > Add project from disk*) using a 6000.3 editor. On first load
   `ProjectBootstrap` switches to linear colour and creates and assigns a URP pipeline asset.
3. In Unity, run in order:
   - **Event Horizon > Assets > 1. Build Integrated Prefabs**
   - **Event Horizon > Assets > 2. Build Mars Colony Scene**, then press Play (WASD, mouse, Shift, Space, **E** interact, **V** first/third person)
   - **Event Horizon > Assets > 3. Build Space Lab Scene** (black hole, planets)
   - **Event Horizon > Assets > Validate Third-Party Assets**: writes `Logs/AssetValidation.txt`

Licenses and attribution for every asset are in [`ASSET_CREDITS.md`](ASSET_CREDITS.md), generated from
[`tools/asset_manifest.json`](tools/asset_manifest.json).

## Contributing

Contributions are welcome. Open an issue or submit a pull request with improvements.

## Dih
BIGGG DIIHHH
