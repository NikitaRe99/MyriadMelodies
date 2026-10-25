"""Fit shared fan equations to three reference frames; requires NumPy.

Frames 111 (2560x1080) and 118 (1280x800) are excluded from fitting
and remain size-transfer checks in CaptureLayout.Checks.
"""
import json
from pathlib import Path
import numpy as np

rows = json.loads(Path('tests/CaptureLayout.Checks/observations.json').read_text(encoding='utf-8'))
x_basis, y_basis, distances, heights = [], [], [], []
for row in rows:
    if row['number'] not in (110, 114, 115):
        continue
    h, w = row['height'], row['width']
    scale = min(w / 1920, h / 1080)
    z = (h / scale - 1080) / 360
    for j in range(3):
        left, right = row['points'][2-j], row['points'][3+j]
        distances.append((right['x'] - left['x']) / (2 * scale))
        heights.append((right['y'] + left['y']) / (2 * scale))
        x_basis.append([1, j, j*j, z, z*j, z*j*j, z*z*j*(2-j)])
        y_basis.append([h / scale, 1, j, j*j])

for name, basis, values in [('distance / scale', x_basis, distances), ('height / scale', y_basis, heights)]:
    coefficients = np.linalg.lstsq(basis, values, rcond=None)[0]
    print(name, coefficients.tolist())
