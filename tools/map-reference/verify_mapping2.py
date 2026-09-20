"""Landmark check: ground anchors of the old props vs the manifest cells."""
import json
import os
import re
import sys

sys.stdout.reconfigure(encoding='utf-8')
ROOT = r"d:\Unity\Meadom"
ASSETS = os.path.join(ROOT, "Assets")
SCENE = os.path.join(ASSETS, "Scenes", "Levels", "OutDoors", "Level_Farm.unity")
MANIFEST = os.path.join(ASSETS, "Settings", "Map Decorations", "Map Prop Placements.json")
SEP = os.path.join(ASSETS, "Sprites", "props-items", "trang tr\u00ed map-t\u00e1ch ri\u00eang")
CELL = 0.16
PPU = 100.0

# sprite name -> (width, height) in pixels; all use a centred pivot.
SIZES = {"cay-lon-1": (50, 80), "cay-lon-2": (50, 79), "cay-vua-1": (36, 59),
         "cay-nho-1": (35, 55), "bui-cay": (31, 25), "co-cao": (17, 32),
         "co-nho-1": (16, 17), "co-nho-2": (17, 18)}

text = open(SCENE, encoding="utf-8").read()
docs = {}
for doc in re.split(r"\n(?=--- !u!)", text):
    m = re.match(r"--- !u!(\d+) &(-?\d+)", doc)
    if m:
        docs[m.group(2)] = (m.group(1), doc)


def world_of(transform_id):
    x = y = 0.0
    while transform_id in docs:
        body = docs[transform_id][1]
        p = re.search(r"m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+)", body)
        if p:
            x += float(p.group(1))
            y += float(p.group(2))
        f = re.search(r"m_Father: \{fileID: (-?\d+)", body)
        transform_id = f.group(1) if f and f.group(1) != "0" else None
    return x, y


old = []
for key, (cls, doc) in docs.items():
    if cls != "1":
        continue
    name = re.search(r"m_Name: (.*)", doc)
    name = name.group(1).strip().strip("'") if name else ""
    if not name.startswith("Map Prop ") or " - " not in name:
        continue
    sprite = name.split(" - ", 1)[1]
    if sprite not in SIZES:
        continue
    transform = next((c for c in re.findall(r"component: \{fileID: (-?\d+)", doc)
                      if c in docs and docs[c][0] == "4"), None)
    if transform is None:
        continue
    x, y = world_of(transform)
    height = SIZES[sprite][1]
    # position = anchor - bounds.min.y  ->  anchor = position + bounds.min.y
    anchor_y = y - (height / 2.0) / PPU
    anchor_x = x
    old.append((sprite, anchor_x, anchor_y))

placements = json.load(open(MANIFEST, encoding="utf-8"))["placements"]
by_sprite = {}
for placement in placements:
    by_sprite.setdefault(placement["sprite"], []).append(placement)

exact = near = far = 0
samples = []
for sprite, ax, ay in old:
    cell_x = int((ax // CELL))
    cell_y = int((ay // CELL))
    options = by_sprite.get(sprite, [])
    if not options:
        continue
    best = min(options, key=lambda p: max(abs(p["cellX"] - cell_x), abs(p["cellY"] - cell_y)))
    delta = max(abs(best["cellX"] - cell_x), abs(best["cellY"] - cell_y))
    if delta == 0:
        exact += 1
    elif delta <= 1:
        near += 1
    else:
        far += 1
        if len(samples) < 8:
            samples.append((sprite, (cell_x, cell_y), (best["cellX"], best["cellY"])))

print(f"old props compared: {exact + near + far}")
print(f"  same cell           : {exact}")
print(f"  within 1 cell       : {near}")
print(f"  more than 1 cell off: {far}")
for sprite, old_cell, new_cell in samples:
    print(f"    {sprite:12} scene {old_cell} vs manifest {new_cell}")
