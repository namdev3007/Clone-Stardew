"""Full template pass: separated props + sheet frames, at scale 1.0 and 0.5."""
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
REFERENCE = os.path.join(ASSETS, "Sprites", "tileset", "full trang tri-1.png")
OUT = os.path.join(HERE, "manifest_raw.json")

TOLERANCE = 8
PROBE_COUNT = 36
SETTINGS = {
    "tree": {"band": 16, "ratio": 0.80, "probe_ratio": 0.60},
    "bush": {"band": 0, "ratio": 0.85, "probe_ratio": 0.65},
    "grass": {"band": 0, "ratio": 0.90, "probe_ratio": 0.70},
    "prop": {"band": 0, "ratio": 0.85, "probe_ratio": 0.65},
    "banana": {"band": 20, "ratio": 0.80, "probe_ratio": 0.60},
}

SEP = os.path.join(ASSETS, "Sprites", "props-items", "trang tr\u00ed map-t\u00e1ch ri\u00eang")
RICE = os.path.join(ASSETS, "Sprites", "props-items", "\u1ee5 l\u00faa-t\u00e1ch ri\u00eang")
PROPS = os.path.join(ASSETS, "Sprites", "props-items")
PERENNIAL = os.path.join(ASSETS, "Sprites", "c\u00e2y tr\u1ed3ng", "c\u00e2y l\u00e2u n\u0103m")

# path, kind, scales to try
SOURCES = [
    (os.path.join(SEP, "cay-lon-1.png"), "tree", [1.0]),
    (os.path.join(SEP, "cay-lon-2.png"), "tree", [1.0]),
    (os.path.join(SEP, "cay-vua-1.png"), "tree", [1.0]),
    (os.path.join(SEP, "cay-nho-1.png"), "tree", [1.0]),
    (os.path.join(SEP, "bui-cay.png"), "bush", [1.0]),
    (os.path.join(SEP, "co-cao.png"), "grass", [1.0]),
    (os.path.join(SEP, "co-nho-1.png"), "grass", [1.0]),
    (os.path.join(SEP, "co-nho-2.png"), "grass", [1.0]),
    (os.path.join(RICE, "u-lua-vang.png"), "prop", [1.0]),
    (os.path.join(RICE, "u-lua-xanh.png"), "prop", [1.0]),
    (os.path.join(PROPS, "c\u00e2y-ch\u1ebft.png"), "tree", [1.0]),
    (os.path.join(PROPS, "lu n\u01b0\u1edbc.png"), "prop", [1.0, 0.5]),
    (os.path.join(PROPS, "cay hoa g\u1ea1o-Sheet.png"), "tree", [1.0]),
    (os.path.join(PERENNIAL, "banana", "bananatree_200.png"), "banana", [1.0, 0.5]),
    (os.path.join(PERENNIAL, "mango", "mangotree_200.png"), "tree", [1.0, 0.5]),
]


def load_rgba(path):
    return np.array(Image.open(path).convert("RGBA"), dtype=np.int16)


def slices(path):
    """All sprite slices in a texture, as (name, top-left cropped array)."""
    image = load_rgba(path)
    height = image.shape[0]
    meta = path + ".meta"
    result = []
    if os.path.exists(meta):
        text = open(meta, encoding="utf-8", errors="ignore").read()
        for block in re.finditer(r"- serializedVersion: \d+\s*\n\s*name: (.*?)\n(?:.*\n)*?"
                                 r"\s*rect:\s*\n\s*serializedVersion: \d+\s*\n\s*x: (\d+)\s*\n\s*y: (\d+)"
                                 r"\s*\n\s*width: (\d+)\s*\n\s*height: (\d+)", text):
            name = block.group(1).strip().strip('"')
            x, y, w, h = (int(v) for v in block.groups()[1:])
            top = height - (y + h)
            result.append((name, image[top:top + h, x:x + w]))
    if not result:
        result.append((os.path.splitext(os.path.basename(path))[0], image))
    return result


def rescale(template, scale):
    if scale == 1.0:
        return template
    step = int(round(1 / scale))
    return template[::step, ::step, :]


def find(ref, template, kind):
    h, w = template.shape[:2]
    cfg = SETTINGS[kind]
    band = cfg["band"] if cfg["band"] else h
    band = min(band, h)
    top = h - band
    used = template[top:, :, :]
    mask = used[:, :, 3] >= 200
    total = int(mask.sum())
    if total < 8:
        return []

    ref_h, ref_w = ref.shape[:2]
    if band > ref_h or w > ref_w:
        return []

    ys, xs = np.nonzero(mask)
    colours = used[ys, xs, :3]
    rows, cols = ref_h - band + 1, ref_w - w + 1
    idx = np.linspace(0, total - 1, min(PROBE_COUNT, total)).astype(int)
    votes = np.zeros((rows, cols), dtype=np.int16)
    for i in idx:
        py, px = int(ys[i]), int(xs[i])
        votes += (np.abs(ref[py:py + rows, px:px + cols, :3] - colours[i]) <= TOLERANCE).all(axis=2)

    needed = int(len(idx) * cfg["probe_ratio"])
    cy, cx = np.nonzero(votes >= needed)
    out = []
    for by, x0 in zip(cy.tolist(), cx.tolist()):
        sample = ref[by + ys, x0 + xs, :3]
        score = float((np.abs(sample - colours) <= TOLERANCE).all(axis=1).mean())
        if score >= cfg["ratio"]:
            out.append((x0, by - top, score))
    return out


def main():
    ref = load_rgba(REFERENCE)
    records = []
    for path, kind, scale_list in SOURCES:
        if not os.path.exists(path):
            print("  MISSING", path)
            continue
        for name, template in slices(path):
            for scale in scale_list:
                scaled = rescale(template, scale)
                th, tw = scaled.shape[:2]
                if th < 6 or tw < 6:
                    continue
                hits = find(ref, scaled, kind)
                if hits:
                    print(f"  {name:28} {tw}x{th} scale={scale} kind={kind:6} hits={len(hits)}")
                for (x0, y0, score) in hits:
                    records.append({"sprite": name, "kind": kind, "scale": scale,
                                    "texture": os.path.relpath(path, ASSETS).replace("\\", "/"),
                                    "x0": int(x0), "y0": int(y0), "w": int(tw), "h": int(th),
                                    "score": round(score, 4)})

    json.dump({"reference": [ref.shape[1], ref.shape[0]], "records": records},
              open(OUT, "w", encoding="utf-8"))
    print("raw records:", len(records))


if __name__ == "__main__":
    main()
