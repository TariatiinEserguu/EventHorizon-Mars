#!/usr/bin/env python3
"""Download the third-party assets listed in tools/asset_manifest.json.

    python3 tools/fetch_assets.py              # fetch everything not yet fetched
    python3 tools/fetch_assets.py --only nasa-perseverance,kenney-space-kit
    python3 tools/fetch_assets.py --force      # re-download
    python3 tools/fetch_assets.py --credits    # only regenerate ASSET_CREDITS.md + in-game credits
    python3 tools/fetch_assets.py --list

Raw downloads go to .asset_cache/ (git-ignored). Only whitelisted file types are
copied into Assets/ThirdParty/<dest>/. Archives are never executed, and .blend files
are skipped because Unity would try to launch Blender to import them. Each
destination gets a SOURCE.json holding the license and a SHA-256 for every file.

Google Drive folders (Quaternius) need the optional `gdown` tool
(pip install gdown). Drive throttles popular files, so if it fails, download the
folder in a browser and unzip it into the destination printed below.

Standard library only.
"""
from __future__ import annotations

import argparse
import fnmatch
import hashlib
import json
import os
import shutil
import subprocess
import sys
import urllib.parse
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MANIFEST = ROOT / "tools" / "asset_manifest.json"
CACHE = ROOT / ".asset_cache"
CREDITS_MD = ROOT / "ASSET_CREDITS.md"
CREDITS_TXT = ROOT / "Assets" / "EventHorizon" / "Resources" / "ThirdPartyCredits.txt"

ALLOWED_EXT = {
    ".fbx", ".obj", ".mtl", ".glb", ".gltf", ".bin",
    ".png", ".jpg", ".jpeg", ".tga", ".exr", ".hdr",
    ".ogg", ".wav", ".mp3",
    ".txt", ".md",
}


def load_manifest() -> dict:
    with MANIFEST.open(encoding="utf-8") as f:
        return json.load(f)


def http_get(url: str, ua: str, dest: Path | None = None, referer: str | None = None) -> bytes | None:
    headers = {"User-Agent": ua}
    if referer:
        headers["Referer"] = referer
    req = urllib.request.Request(url, headers=headers)
    with urllib.request.urlopen(req, timeout=120) as resp:
        if dest is None:
            return resp.read()
        dest.parent.mkdir(parents=True, exist_ok=True)
        tmp = dest.with_suffix(dest.suffix + ".part")
        with tmp.open("wb") as out:
            shutil.copyfileobj(resp, out, length=1 << 20)
        tmp.replace(dest)
    return None


def safe_extract(zip_path: Path, out_dir: Path) -> None:
    """Extract without zip-slip, symlinks or absolute paths."""
    out_dir = out_dir.resolve()
    with zipfile.ZipFile(zip_path) as zf:
        for info in zf.infolist():
            name = info.filename
            if name.endswith("/"):
                continue
            target = (out_dir / name).resolve()
            if not str(target).startswith(str(out_dir) + os.sep):
                print(f"    ! skipping unsafe path in archive: {name}")
                continue
            # High 16 bits of external_attr hold the Unix mode; 0o120000 marks a symlink.
            if (info.external_attr >> 16) & 0o170000 == 0o120000:
                print(f"    ! skipping symlink in archive: {name}")
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            with zf.open(info) as src, target.open("wb") as dst:
                shutil.copyfileobj(src, dst)


def matches(rel: str, patterns: list[str] | None) -> bool:
    if not patterns:
        return True
    return any(fnmatch.fnmatch(rel, p) for p in patterns)


def copy_filtered(src_root: Path, dest: Path, include: list[str] | None) -> list[Path]:
    copied = []
    for path in sorted(src_root.rglob("*")):
        if not path.is_file() or path.is_symlink():
            continue
        rel = path.relative_to(src_root).as_posix()
        if path.suffix.lower() not in ALLOWED_EXT:
            continue
        if not matches(rel, include):
            continue
        # Unity cannot have two assets differing only by case on case-insensitive filesystems;
        # keep the original layout, it is already unique inside each pack.
        out = dest / rel
        out.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, out)
        copied.append(out)
    return copied


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


# --------------------------------------------------------------------------- fetchers

def fetch_kenney(asset: dict, work: Path, ua: str) -> Path:
    slug = asset["fetch"]["slug"]
    page = f"https://kenney.nl/assets/{slug}"
    # Same request the page's "Continue without donating" link makes: it returns a short-lived signed URL.
    download_id = f"assets/{slug}".encode().hex()
    data = json.loads(http_get(f"https://kenney.nl/download-link/{download_id}", ua, referer=page))
    if not data.get("url"):
        raise RuntimeError(f"Kenney returned no download URL for {slug}")
    zip_path = work / f"{slug}.zip"
    http_get(data["url"], ua, zip_path, referer=page)
    extracted = work / "extracted"
    safe_extract(zip_path, extracted)
    return extracted


def fetch_polyhaven(asset: dict, work: Path, ua: str) -> Path:
    cfg = asset["fetch"]
    out = work / "files"
    for slug in cfg["slugs"]:
        files = json.loads(http_get(f"https://api.polyhaven.com/files/{slug}", ua))
        for map_name in cfg["maps"]:
            entry = files[map_name][cfg["resolution"]][cfg["format"]]
            fname = Path(urllib.parse.urlparse(entry["url"]).path).name
            dest = out / slug / fname
            http_get(entry["url"], ua, dest)
            if "md5" in entry and hashlib.md5(dest.read_bytes()).hexdigest() != entry["md5"]:
                raise RuntimeError(f"MD5 mismatch for {fname}")
    return out


def fetch_direct(asset: dict, work: Path, ua: str) -> Path:
    out = work / "files"
    for fname, url in asset["fetch"]["files"].items():
        http_get(url, ua, out / fname)
    return out


def fetch_gdrive_folder(asset: dict, work: Path, ua: str) -> Path:
    gdown = os.environ.get("GDOWN") or shutil.which("gdown")
    if not gdown:
        raise ManualStep("gdown is not installed (pip install gdown)")
    out = work / "drive"
    out.mkdir(parents=True, exist_ok=True)
    proc = subprocess.run(
        [gdown, "--folder", asset["fetch"]["folder_url"], "-O", str(out)],
        capture_output=True, text=True,
    )
    if proc.returncode != 0 and "usage:" in proc.stderr:
        raise RuntimeError(f"gdown rejected its arguments: {proc.stderr.strip().splitlines()[-1]}")
    # gdown leaves files out when Drive throttles them. Keep what arrived, report the rest.
    failed = proc.stderr.count("Failed to download")
    for z in list(out.rglob("*.zip")):
        safe_extract(z, z.parent / z.stem)
    if not any(p.is_file() for p in out.rglob("*")):
        raise ManualStep("Google Drive returned no files (throttled or offline)")
    if failed:
        print(f"    ! {failed} file(s) were throttled by Google Drive; re-run later or download manually")
    return out


class ManualStep(Exception):
    pass


FETCHERS = {
    "kenney": fetch_kenney,
    "polyhaven": fetch_polyhaven,
    "direct": fetch_direct,
    "gdrive_folder": fetch_gdrive_folder,
}


def fetch_asset(asset: dict, manifest: dict, force: bool) -> str:
    dest = ROOT / manifest["thirdparty_root"] / asset["dest"]
    marker = dest / "SOURCE.json"
    if not force and is_complete(marker):
        return "already fetched (checksums match)"
    method = asset["fetch"]["method"]
    work = CACHE / asset["id"]
    if work.exists():
        shutil.rmtree(work)
    work.mkdir(parents=True)
    try:
        src_root = FETCHERS[method](asset, work, manifest["user_agent"])
    except ManualStep as e:
        return (f"MANUAL: {e}. Download {asset['fetch'].get('folder_url', asset['source'])} "
                f"in a browser and unzip it into {dest.relative_to(ROOT)}/, then run with --only {asset['id']}")
    if dest.exists():
        shutil.rmtree(dest)
    copied = copy_filtered(src_root, dest, asset.get("include"))
    if not copied:
        return "ERROR: archive had no files matching the include filter"
    record = {k: asset[k] for k in ("id", "name", "creator", "source", "license", "license_url",
                                     "attribution_required", "used_for") if k in asset}
    record["files"] = {p.relative_to(dest).as_posix(): sha256(p) for p in copied}
    marker.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    shutil.rmtree(work, ignore_errors=True)  # raw archive no longer needed
    size = sum(p.stat().st_size for p in copied) / 1e6
    return f"ok: {len(copied)} files, {size:.1f} MB"


def is_complete(marker: Path) -> bool:
    """SOURCE.json is committed to git, so on a fresh clone it exists without the files: check the files too."""
    if not marker.exists():
        return False
    try:
        files = json.loads(marker.read_text(encoding="utf-8")).get("files", {})
    except json.JSONDecodeError:
        return False
    return bool(files) and all(
        (marker.parent / rel).is_file() and sha256(marker.parent / rel) == digest for rel, digest in files.items())


def register_manual_folder(asset: dict, manifest: dict) -> str | None:
    """Writes SOURCE.json for a folder the user filled by hand."""
    dest = ROOT / manifest["thirdparty_root"] / asset["dest"]
    if (dest / "SOURCE.json").exists() or not dest.exists():
        return None
    files = [p for p in dest.rglob("*") if p.is_file() and p.suffix.lower() in ALLOWED_EXT and p.name != "README.md"]
    if not files:
        return None
    for p in dest.rglob("*.blend*"):
        p.unlink()
    record = {k: asset[k] for k in ("id", "name", "creator", "source", "license") if k in asset}
    record["files"] = {p.relative_to(dest).as_posix(): sha256(p) for p in files}
    (dest / "SOURCE.json").write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    return f"registered {len(files)} manually downloaded files"


# --------------------------------------------------------------------------- credits

def md_escape(s: str) -> str:
    return s.replace("|", "\\|")


def write_credits(manifest: dict) -> None:
    assets = manifest["assets"]
    lines = [
        "# Asset Credits",
        "",
        "<!-- Generated by tools/fetch_assets.py from tools/asset_manifest.json. Edit the manifest, not this file. -->",
        "",
        "Every third-party file in `Assets/ThirdParty/` comes from one of the sources below. Each license was "
        "read on the source page on the date in the *Verified* column. A free download does not mean free "
        "for commercial use: add an asset to `tools/asset_manifest.json` only after checking its own license.",
        "",
        "| Asset | Creator | Source | License | Attribution Required | Used for |",
        "|---|---|---|---|---|---|",
    ]
    for a in assets:
        src = f"[{urllib.parse.urlparse(a['source']).netloc}]({a['source']})"
        for extra in a.get("extra_sources", []):
            src += f", [more]({extra})"
        lic = f"[{a['license']}]({a['license_url']})"
        attr = "Yes" if a["attribution_required"] else "No"
        lines.append(f"| {md_escape(a['name'])} | {md_escape(a['creator'])} | {src} | {lic} | {attr} | "
                     f"{md_escape(a['used_for'])} |")

    lines += ["", "## Verification log", "", "| Asset | Commercial use | Modification | Verified |", "|---|---|---|---|"]
    for a in assets:
        lines.append(f"| {md_escape(a['name'])} | {'Yes' if a['commercial_use'] else 'No'} | "
                     f"{'Yes' if a['modification'] else 'No'} | {md_escape(a['verified'])} |")

    lines += ["", "## Required attribution (shown in the in-game credits)", ""]
    for a in assets:
        if a["attribution_required"]:
            lines.append(f"- {a['attribution_text']}")
    lines += [
        "",
        "In-game text: `Assets/EventHorizon/Resources/ThirdPartyCredits.txt`, read at runtime by `GameCredits.Load()`.",
        "",
        "## Made for this project (no third-party license)",
        "",
        "| Asset | How it is made |",
        "|---|---|",
    ]
    for g in manifest.get("generated", []):
        lines.append(f"| {md_escape(g['name'])} | {md_escape(g['how'])} |")
    lines += ["", "## Not scripted (add by hand, then record here)", ""]
    for m in manifest.get("manual_only", []):
        lines.append(f"- **{m['name']}**: {m['why']}")
    lines += [
        "",
        "## Rules we follow",
        "",
        "- No paid, pirated or license-unclear assets. Unknown license means we don't use it.",
        "- NASA material: credit NASA. Never use the NASA insignia (\"meatball\"), logotype or \"worm\", "
        "and never imply that NASA endorses the game.",
        "- CC BY assets: the attribution text above must stay in the shipped credits.",
        "- Raw third-party files are git-ignored and re-fetched with `python3 tools/fetch_assets.py`, so the public "
        "repository never redistributes an asset pack on its own. Their `.meta` files are committed, which "
        "keeps prefab references stable.",
        "",
    ]
    CREDITS_MD.write_text("\n".join(lines), encoding="utf-8")

    txt = ["THIRD-PARTY ASSETS", ""]
    for a in assets:
        txt.append(f"{a['name']}: {a['creator']} ({a['license']})")
        if a["attribution_required"]:
            txt.append(f"  {a['attribution_text']}")
    txt += ["", "NASA material is used under NASA's media guidelines. Its use does not imply NASA endorsement.", ""]
    CREDITS_TXT.parent.mkdir(parents=True, exist_ok=True)
    CREDITS_TXT.write_text("\n".join(txt), encoding="utf-8")


# --------------------------------------------------------------------------- main

def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--only", help="comma-separated asset ids")
    ap.add_argument("--force", action="store_true")
    ap.add_argument("--credits", action="store_true", help="only regenerate the credits files")
    ap.add_argument("--list", action="store_true")
    args = ap.parse_args()

    manifest = load_manifest()
    assets = manifest["assets"]
    if args.only:
        wanted = set(args.only.split(","))
        unknown = wanted - {a["id"] for a in assets}
        if unknown:
            print(f"unknown asset ids: {', '.join(sorted(unknown))}", file=sys.stderr)
            return 2
        assets = [a for a in assets if a["id"] in wanted]

    if args.list:
        for a in assets:
            fetched = is_complete(ROOT / manifest["thirdparty_root"] / a["dest"] / "SOURCE.json")
            print(f"{'[x]' if fetched else '[ ]'} {a['id']:32} {a['license']:28} {a['source']}")
        return 0

    write_credits(manifest)
    print(f"wrote {CREDITS_MD.relative_to(ROOT)} and {CREDITS_TXT.relative_to(ROOT)}")
    if args.credits:
        return 0

    problems = 0
    for a in assets:
        print(f"- {a['id']} ({a['license']})")
        try:
            note = register_manual_folder(a, manifest) or fetch_asset(a, manifest, args.force)
        except Exception as e:  # network errors, bad archives
            note = f"ERROR: {e}"
        if note.startswith(("ERROR", "MANUAL")):
            problems += 1
        print(f"    {note}")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
