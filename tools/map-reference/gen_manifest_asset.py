"""Write Map Prop Placement Manifest.asset from the independent 16x16 analysis.

Keeps the schema of MapPropPlacementManifest so the existing importer window can
preview, validate and apply the entries unchanged.
"""
import codecs
import json
import os
import re
import sys

import numpy as np
from PIL import Image

sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = r"d:\Unity\Meadom"
ASSETS = os.path.join(ROOT, "Assets")
BASE = os.path.join(ASSETS, "Sprites", "b\u1ed1 c\u1ee5c map.png")
REGIONS = os.path.join(ASSETS, "Settings", "Map Regions", "Map Region Collection.asset")
MANIFEST = os.path.join(ASSETS, "Settings", "Map Decorations", "Map Prop Placement Manifest.asset")
MINE = os.path.join(HERE, "manifest_cells.json")

TOP_CROP = 96
CELL = 0.16
GRASS_ORDER = -32768

VALID, ON_PATH, IN_WATER, IN_CROP, AT_NPC, OUTSIDE = 0, 1, 2, 3, 4, 5


def is_path(colour):
    r, g, b = (int(v) for v in colour[:3])
    return 175 <= r <= 205 and 155 <= g <= 190 and 115 <= b <= 160


def is_water(colour):
    r, g, b = (int(v) for v in colour[:3])
    return b > 140 and b > r + 40 and b > g + 20


def sprite_reference(texture_rel):
    """(guid, fileID) for the sprite Unity exposes for this texture."""
    path = os.path.join(ASSETS, texture_rel.replace("/", os.sep))
    meta = open(path + ".meta", encoding="utf-8", errors="ignore").read()
    guid = re.search(r"^guid: (\w+)", meta, re.M).group(1)
    mode = int(re.search(r"^\s*spriteMode: (\d+)", meta, re.M).group(1))
    return guid, mode, meta


def slice_file_id(meta, slice_name):
    for block in re.finditer(r"      name: (.*?)\n(?:.*\n)*?      internalID: (-?\d+)", meta):
        name = block.group(1).strip().strip('"')
        decoded = codecs.decode(name.encode("latin-1", "backslashreplace"), "unicode_escape")
        if name == slice_name or decoded == slice_name:
            return int(block.group(2))
    return 21300000


def load_regions():
    text = open(REGIONS, encoding="utf-8").read()
    lines = text.split("\n")
    out = []
    i = 0
    while i < len(lines):
        if lines[i].startswith("  - regionName:"):
            value = lines[i].split("regionName:", 1)[1].strip()
            while value.startswith('"') and not value.endswith('"'):
                i += 1
                value += " " + lines[i].strip()
            if value.startswith('"'):
                value = codecs.decode(value.strip('"').encode("latin-1", "backslashreplace"), "unicode_escape")
            mn = re.search(r"x: (-?\d+), y: (-?\d+)", lines[i + 2])
            mx = re.search(r"x: (-?\d+), y: (-?\d+)", lines[i + 3])
            out.append((value.lower(), tuple(map(int, mn.groups())), tuple(map(int, mx.groups()))))
        i += 1
    return out


# The home orchard is a planting area, but its grass cover is part of the art.
ORCHARD_TOKEN = "sau khi m\u1edf ru\u1ed9ng"


def region_status(name):
    if ORCHARD_TOKEN in name:
        return IN_CROP, name
    if "d\u01b0a chu\u1ed9t" in name and "v\u00f9ng tr\u1ed3ng" in name:
        return IN_CROP, name
    if "thanh long" in name and ("\u0111\u1ea5t tr\u1ed3ng" in name or "v\u00f9ng tr\u1ed3ng" in name):
        return IN_CROP, name
    if "v\u00f9ng tr\u1ed3ng c\u00e2y b\u00ecnh th\u01b0\u1eddng" in name:
        return IN_CROP, name
    if "\u00f4ng n\u1ed9i" in name or "b\u00e1n h\u1ea1t gi\u1ed1ng" in name or "c\u1eafm bi\u1ec3n" in name:
        return AT_NPC, name
    if "h\u00e0ng r\u00e0o" in name or "r\u00e0o ch\u1eafn" in name:
        return AT_NPC, name
    return VALID, name


AUTHORED = ("hoa g", "lu n", "u-lua", "ch\u1ebft")
GRASS = ("co-cao", "co-nho-1", "co-nho-2")


def main():
    base = np.array(Image.open(BASE).convert("RGB"), dtype=np.int16)
    regions = load_regions()
    records = json.load(open(MINE, encoding="utf-8"))["records"]
    records.sort(key=lambda r: (r["anchorPixelY"], r["anchorPixelX"]))

    entries = []
    counts = {}
    for index, record in enumerate(records, start=1):
        guid, mode, meta = sprite_reference(record["texture"])
        sprite_name = (os.path.splitext(os.path.basename(record["texture"]))[0]
                       if mode == 1 else record["sprite"])
        file_id = 21300000 if mode == 1 else slice_file_id(meta, record["sprite"])

        ax, ay = record["anchorPixelX"], record["anchorPixelY"]
        cell_x, cell_y = record["targetCellX"], record["targetCellY"]
        world = (cell_x * CELL + CELL / 2, cell_y * CELL + CELL / 2)
        by = ay - TOP_CROP

        status, region_name = VALID, ""
        authored = any(token in record["sprite"] for token in AUTHORED)
        if by < 0 or by >= base.shape[0]:
            status = OUTSIDE
        elif is_water(base[by, min(ax, base.shape[1] - 1)]):
            status = IN_WATER
        elif not authored and is_path(base[by, min(ax, base.shape[1] - 1)]):
            status = ON_PATH
        else:
            grass_here = any(record["sprite"].startswith(g) for g in GRASS)
            for name, (rx0, ry0), (rx1, ry1) in regions:
                if rx0 <= cell_x <= rx1 and ry0 <= cell_y <= ry1:
                    status, region_name = region_status(name)
                    # Grass carries no collider and never blocks planting, so the
                    # orchard keeps the ground cover drawn in the reference.
                    if status == IN_CROP and grass_here and ORCHARD_TOKEN in name:
                        status, region_name = VALID, ""
                    if status != VALID:
                        break

        is_grass = any(sprite_name.startswith(g) for g in GRASS)
        sorting = GRASS_ORDER if is_grass else int(round(-world[1] * 100))
        add_collider = 0 if is_grass else 1
        counts[status] = counts.get(status, 0) + 1

        safe_region = ""
        if region_name:
            escaped = region_name.encode("ascii", "backslashreplace").decode("ascii").replace('"', "'")
            safe_region = f'"{escaped}"'

        entries.append(f"""  - id: prop_{index:04d}_{sprite_name}
    sourcePixel: {{x: {ax}, y: {ay}}}
    sourceCell: {{x: {record['sourceCellX']}, y: {record['sourceCellY']}}}
    targetCell: {{x: {cell_x}, y: {cell_y}, z: 0}}
    worldAnchor: {{x: {world[0]:.4f}, y: {world[1]:.4f}, z: 0}}
    sprite: {{fileID: {file_id}, guid: {guid}, type: 3}}
    scale: {record.get('scale', 1.0):g}
    flipX: 0
    addCollider: {add_collider}
    regionName: {safe_region}
    sortingOrder: {sorting}
    status: {status}""")

    valid = counts.get(VALID, 0)
    header = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 531a7ac6c3a247e783e06ecf55653825, type: 3}
  m_Name: Map Prop Placement Manifest
  m_EditorClassIdentifier:
  entries:
"""
    footer = (f"  lastAnalysisTimestamp: 2026-09-18 16x16 python analysis\n"
              f"  validCount: {valid}\n  skippedCount: {len(entries) - valid}\n")
    open(MANIFEST, "w", encoding="utf-8", newline="\n").write(header + "\n".join(entries) + "\n" + footer)

    labels = {VALID: "valid", ON_PATH: "on path", IN_WATER: "in water",
              IN_CROP: "in crop area", AT_NPC: "npc/sign/fence", OUTSIDE: "outside map"}
    for key in sorted(counts):
        print(f"  {labels[key]:15} {counts[key]}")
    print("total entries:", len(entries))
    print("->", MANIFEST)


if __name__ == "__main__":
    main()
