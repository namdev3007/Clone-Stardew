"""Write the final placement manifest into the Unity project."""
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
OUT_DIR = os.path.join(ASSETS, "Settings", "Map Decorations")
OUT = os.path.join(OUT_DIR, "Map Prop Placements.json")
TOP_CROP = 96

# Vegetation never stands on a path tile (same colour test as SetupMapProps).
def is_path(colour):
    r, g, b = int(colour[0]), int(colour[1]), int(colour[2])
    return 175 <= r <= 205 and 155 <= g <= 190 and 115 <= b <= 160


def unity_sprite_name(texture_rel, slice_name):
    """Single-mode textures expose one sprite named after the file, not the slice."""
    path = os.path.join(ASSETS, texture_rel.replace("/", os.sep))
    meta = path + ".meta"
    mode = 2
    if os.path.exists(meta):
        found = re.search(r"^\s*spriteMode: (\d+)", open(meta, encoding="utf-8", errors="ignore").read(), re.M)
        if found:
            mode = int(found.group(1))
    if mode == 1:
        return os.path.splitext(os.path.basename(path))[0]
    return slice_name


base = np.array(Image.open(BASE).convert("RGB"), dtype=np.int16)
records = json.load(open(os.path.join(HERE, "manifest_cells.json"), encoding="utf-8"))["records"]

GRASS = {"co-cao", "co-nho-1", "co-nho-2"}
out = []
skipped_path = 0
skipped_outside = 0
for index, record in enumerate(sorted(records, key=lambda r: (r["anchorPixelY"], r["anchorPixelX"]))):
    bx = record["anchorPixelX"]
    by = record["anchorPixelY"] - TOP_CROP
    # The reference keeps 96 px of art above the playable terrain; drop those.
    if by < 0 or by >= base.shape[0]:
        skipped_outside += 1
        continue
    # Only wild vegetation is banned from paths. Authored objects (flower tree,
    # water jars, rice pile, dead tree) are drawn next to the house on purpose.
    authored = any(token in record["sprite"] for token in ("hoa g", "lu n", "u-lua", "chết"))
    if (not authored and 0 <= by < base.shape[0] and 0 <= bx < base.shape[1]
            and is_path(base[by, bx])):
        skipped_path += 1
        continue

    sprite = unity_sprite_name(record["texture"], record["sprite"])
    out.append({
        "id": f"{sprite}_{record['sourceCellX']}_{record['sourceCellY']}",
        "texture": "Assets/" + record["texture"],
        "sprite": sprite,
        "kind": record["kind"],
        "scale": record.get("scale", 1.0),
        "sourceCellX": record["sourceCellX"],
        "sourceCellY": record["sourceCellY"],
        "cellX": record["targetCellX"],
        "cellY": record["targetCellY"],
        "anchorPixelX": record["anchorPixelX"],
        "anchorPixelY": record["anchorPixelY"],
        "addCollider": sprite not in GRASS,
    })

os.makedirs(OUT_DIR, exist_ok=True)
json.dump({"version": 1,
           "reference": "Assets/Sprites/tileset/full trang tri-1.png",
           "cellSize": 16,
           "placements": out},
          open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)

counts = {}
for record in out:
    counts[record["sprite"]] = counts.get(record["sprite"], 0) + 1
for name in sorted(counts):
    print(f"  {name:28} {counts[name]}")
print(f"placements: {len(out)} (skipped on paths: {skipped_path}, outside terrain: {skipped_outside})")
print("->", OUT)
