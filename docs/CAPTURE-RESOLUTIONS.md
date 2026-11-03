# Capture region placement

[Русский](CAPTURE-RESOLUTIONS.ru.md) · **English**

Both layouts use formulas relative to the game field center, without per-key
coordinate arrays or separate aspect-ratio profiles.
Implementation: `MyriadMelodies/CaptureLayout.cs`.

## Measurement basis

The model was checked against both layouts at 3440×1440, 2560×1080,
1920×1200, 1280×960 and 1280×800. Measurements locate the bright circular
centers and exclude key labels. Coordinates for reproducible checks are in
`tests/CaptureLayout.Checks/observations.json`.

## Shared values

`(L,T,W,H)` are the game field bounds in physical pixels.
`i=0..5` identifies the regions, with default keys A/S/D/J/K/L.

```text
u = min(W / 1920, H / 1080)
s = max(1, round(33 * u))
x[i] = round(cx[i] - s/2)
y[i] = round(cy[i] - s/2)
```

`cx/cy` are centers; `x/y` are the top-left corners of square capture regions.
UI scale and lane geometry are calculated separately.

## Vertical layout

```text
step = 0.3 * H - 107.6 * u
cx[i] = L + W/2 + (i - 2.5) * step
cy[i] = T + 0.8975 * H - 48.3 * u - 0.5
```

The rule was fitted across all five resolutions. Maximum center error after
rounding the regions is 0.877 px on these measurements.

## Spread layout

```text
z = clamp((H/u - 1080) / 360, 0, 1)
j = abs(i - 2.5) - 0.5

d = u * (173.04 + 223.53*j - 8.96*j*j
         + z*(57.36 + 66.795*j + 8.667*j*j)
         + 8.955*z*z*j*(2-j))

cx[i] = L + W/2 + sign(i - 2.5) * d
cy[i] = T + 0.836686*H - u*(33.621 + 31.05*j + 19.567*j*j)
```

`j=0` is the inner D/J pair, `j=1` is S/K, and `j=2` is A/L.
Distance `d` is mirrored around the center. A quadratic curve describes the
height. Shape parameter `z` varies continuously: 0 for 16:9 and wider,
1/3 for 16:10, and 1 for 4:3.

This is an empirical parametric curve, not the game's source algorithm.
Constants describe the shared shape rather than individual key coordinates.
The final X correction only affects the middle pair and accounts for the
observed nonlinear change across aspect ratios. Omitting it and refitting
the linear model gives about 1.8 px maximum error after rounding. The full
formula gives 0.926 px maximum error on the measured frames.

Least-squares fitting uses 3440×1440, 1920×1200 and 1280×960 measurements.
The 2560×1080 and 1280×800 measurements are held out to check transfer to
another size at the same aspect ratio. Reproduce the fit with
`tools/fit_fan_layout.py` (requires NumPy).

A uniformly spaced circle does not match: angular spacing varies and radial
error reaches approximately 1 px before rounding. The formula retains
accuracy without storing coordinate tables.

## Validation

```text
dotnet run --project tests/CaptureLayout.Checks/CaptureLayout.Checks.csproj -- artifacts/capture-layout/validation.json
python tools/measure_capture_layout.py "<source screenshots directory>"
python tools/plot_capture_layout.py "<source screenshots directory>"
```

The C# checks compare the implementation against all 60 measured points.
They also cover negative monitor X, positive Y offsets, region bounds,
intermediate aspect ratios and screen heights up to 8640 px. The Python
tools optionally remeasure and plot the original screenshots, which are
not bundled with the source.

Generated outputs: `artifacts/capture-layout/comparison.png` and
`artifacts/capture-layout/validation.json`.

## Limits

Intermediate aspect ratios have geometry checks but have not been verified
against game frames. These checks do not measure recognition or input
accuracy. Aspect ratios narrower than 4:3 are rejected. Screens wider than
3440×1440 use the same central pattern and need additional game validation.

Display capture uses monitor bounds; window capture uses the client area
without the title bar or borders. Coordinates must match the game image in
physical pixels. Letterboxing inside the image is not detected automatically.
The application uses PerMonitorV2 and accounts for monitor offsets.
