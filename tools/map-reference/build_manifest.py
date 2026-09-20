"""Deduplicate raw matches, map them to Unity cells, render check + residual images."""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = r"d:\Unity\Meadom"
ASSETS = os.path.join(ROOT, "Assets")
REFERENCE = os.path.join(ASSETS, "Sprites", "tileset", "full trang tri-1.png")
RAW = os.path.join(HERE, "manifest_raw.json")
OUT = os.path.join(HERE, "manifest_cells.json")

CELL = 16
LEFT_CELL = -56       # MapLeftWorld (-8.96) / 0.16
TOP_OFFSET = 720      # MapTopWorld (6.24 -> 624px) + ReferenceTopCrop (96px)

PALETTE = [(255, 60, 60), (255, 140, 0), (255, 230, 0), (120, 255, 60), (0, 200, 255),
           (255, 0, 255), (255, 255, 255), (150, 80, 255), (0, 120, 255), (0, 255, 200),
           (180, 0, 255), (255, 170, 170), (170, 255, 170)]


def anchor_of(record):
    return record["x0"] + record["w"] / 2.0, record["y0"] + record["h"]


def deduplicate(records):
    # Highest pixel score wins; larger sprites break ties (a canopy beats a leaf).
    records = sorted(records, key=lambda r: (-r.get("score", 0), -(r["w"] * r["h"])))
    kept = []
    for record in records:
        ax, ay = anchor_of(record)
        clash = False
        for other in kept:
            bx, by = anchor_of(other)
            same = other["sprite"] == record["sprite"]
            limit = 6.0 if same else 4.0
            if abs(ax - bx) <= limit and abs(ay - by) <= limit:
                clash = True
                break
            # A smaller sprite fully inside a bigger one is part of that drawing.
            if not same and record["w"] * record["h"] < other["w"] * other["h"]:
                if (other["x0"] <= record["x0"] and other["y0"] <= record["y0"] and
                        other["x0"] + other["w"] >= record["x0"] + record["w"] and
                        other["y0"] + other["h"] >= record["y0"] + record["h"]):
                    clash = True
                    break
        if not clash:
            kept.append(record)
    return kept


def to_cells(record):
    ax, ay = anchor_of(record)
    out = dict(record)
    out.update({
        "sourceCellX": int(ax // CELL),
        "sourceCellY": int(ay // CELL),
        "targetCellX": int(ax // CELL) + LEFT_CELL,
        "targetCellY": int((TOP_OFFSET - ay) // CELL),
        "anchorPixelX": int(ax),
        "anchorPixelY": int(ay),
    })
    return out


def main():
    raw = json.load(open(RAW, encoding="utf-8"))
    cells = [to_cells(r) for r in deduplicate(raw["records"])]

    counts = {}
    for record in cells:
        key = f'{record["sprite"]}@{record.get("scale", 1.0)}'
        counts[key] = counts.get(key, 0) + 1
    for name in sorted(counts):
        print(f"  {name:34} {counts[name]}")
    print("total", len(cells))
    json.dump({"records": cells}, open(OUT, "w", encoding="utf-8"))

    names = sorted({r["sprite"] for r in cells})
    colour_of = {name: PALETTE[i % len(PALETTE)] for i, name in enumerate(names)}
    image = Image.open(REFERENCE).convert("RGB")
    draw = ImageDraw.Draw(image)
    for record in cells:
        x, y = record["anchorPixelX"], record["anchorPixelY"]
        colour = colour_of[record["sprite"]]
        draw.line([(x - 3, y), (x + 3, y)], fill=colour)
        draw.line([(x, y - 3), (x, y + 3)], fill=colour)
        draw.rectangle([record["x0"], record["y0"], record["x0"] + record["w"] - 1,
                        record["y0"] + record["h"] - 1], outline=colour)
    image.save(os.path.join(HERE, "check_full.png"))
    image.resize((image.width // 2, image.height // 2), Image.NEAREST).save(
        os.path.join(HERE, "check_half.png"))
    print("wrote check images")


if __name__ == "__main__":
    main()
