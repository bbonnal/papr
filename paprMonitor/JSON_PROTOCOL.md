# M5Paper Scene JSON Protocol

ESP32 now accepts only the rUI scene JSON document.
Legacy `clear + commands[]` JSON payloads are no longer supported.

## Transport
- Baud rate: `115200`
- One JSON document per line (newline-terminated)
- Root must be a JSON object with a `Shapes` array

## Accepted Payload Shape
The payload matches `SceneDocument` emitted by rUI canvas, including:
- `Version`
- `CanvasBackgroundColor`
- `ShowCanvasBoundary`
- `CanvasBoundaryWidth`
- `CanvasBoundaryHeight`
- `Shapes` (array)

Supported `Kind` values on ESP32 renderer:
- `Point`, `Line`, `Rectangle`, `Circle`
- `Text`, `MultilineText`, `Icon`
- `Image`, `TextBox`, `Arrow`
- `CenterlineRectangle`, `Referential`, `Dimension`
- `AngleDimension`, `Arc`

## Image Transfer Contract
At save time, the base scene JSON keeps image source information (for example `SourcePath`, often data URI or file path).

At send time (`DeviceScenePayloadBuilder`), paprUI transforms each `Image` shape as follows:
1. Rasterize source image to target size (clamped by settings).
2. Convert to 1bpp monochrome with luminance threshold and optional invert.
3. Pack bits row-major, MSB-first in each byte.
4. Encode packed bytes to base64.
5. Add `ImageMatrix` object.
6. Remove `SourcePath` from transmitted payload.

`ImageMatrix` fields:
- `Width` (int)
- `Height` (int)
- `Bpp` (must be `1`)
- `BlackIsOne` (bool)
- `Data` (base64 packed bitmap)

If `ImageMatrix` is missing or invalid on device, the renderer draws the image placeholder (frame + cross).

## Base JSON vs Sent JSON (Image Shape)
Base scene JSON (stored):
```json
{
  "Kind": "Image",
  "PositionX": 318,
  "PositionY": 256.5,
  "Width": 172,
  "Height": 177,
  "SourcePath": "data:image/png;base64,..."
}
```

Sent JSON (to ESP32):
```json
{
  "Kind": "Image",
  "PositionX": 318,
  "PositionY": 256.5,
  "Width": 172,
  "Height": 177,
  "ImageMatrix": {
    "Width": 172,
    "Height": 177,
    "Bpp": 1,
    "BlackIsOne": true,
    "Data": "...base64 packed bits..."
  }
}
```

## Notes
- Device runs a deep clean cycle before each rendered scene JSON.
- Non-JSON commands still accepted:
  - `clear`: clears screen after deep clean.
