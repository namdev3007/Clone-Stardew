"""Paint every detected prop magenta so anything left over is a missed prop."""
import json
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from analyze_v4 import ASSETS, REFERENCE, rescale, slices  # noqa: E402

sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__))

records = json.load(open(os.path.join(HERE, "manifest_cells.json"), encoding="utf-8"))["records"]
ref = np.array(Image.open(REFERENCE).convert("RGB"), dtype=np.uint8)

cache = {}
for record in records:
    key = (record["texture"], record["sprite"], record.get("scale", 1.0))
    if key not in cache:
        path = os.path.join(ASSETS, record["texture"].replace("/", os.sep))
        for name, template in slices(path):
            if name == record["sprite"]:
                cache[key] = rescale(template, record.get("scale", 1.0))
                break
    template = cache.get(key)
    if template is None:
        continue
    ys, xs = np.nonzero(template[:, :, 3] >= 128)
    y = ys + record["y0"]
    x = xs + record["x0"]
    keep = (y >= 0) & (y < ref.shape[0]) & (x >= 0) & (x < ref.shape[1])
    ref[y[keep], x[keep]] = (255, 0, 255)

image = Image.fromarray(ref)
image.save(os.path.join(HERE, "residual_full.png"))
image.resize((image.width // 2, image.height // 2), Image.NEAREST).save(
    os.path.join(HERE, "residual_half.png"))
print("wrote residual images")
