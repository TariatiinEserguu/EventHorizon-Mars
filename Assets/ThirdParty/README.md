# Third-party assets

Raw files in this folder are **not committed**. Recreate them with:

```bash
python3 tools/fetch_assets.py
```

Each pack folder holds a `SOURCE.json` with the source, license and a SHA-256 for every file. Those
records and the Unity `.meta` files are committed; the files themselves are not.

- Licenses, attribution and what each asset is used for: [`ASSET_CREDITS.md`](../../ASSET_CREDITS.md)
- To add an asset, add it to [`tools/asset_manifest.json`](../../tools/asset_manifest.json) **after** reading
  its license, then run the script. Folders without a `SOURCE.json` fail
  *Event Horizon > Assets > Validate Third-Party Assets*.
- Packs hosted on Google Drive (Quaternius) are often throttled. If the script reports `MANUAL`, download the
  folder in a browser, unzip it into the destination it prints, and run
  `python3 tools/fetch_assets.py --only <id>` to register it. `.blend` files are deleted, because Unity
  would try to launch Blender to import them.
