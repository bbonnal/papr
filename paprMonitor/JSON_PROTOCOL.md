# M5Paper JSON Protocol

The device now accepts the same scene JSON format used by the rUI canvas serializer.
It also still accepts the legacy `clear + commands[]` payload for backward compatibility.

## Transport
- Baud rate: `115200`
- One JSON document per line (newline-terminated)

## Preferred Format: Scene JSON

### Root fields
- `Version` (int)
- `CanvasBackgroundColor` (string, `#AARRGGBB`)
- `ShowCanvasBoundary` (bool)
- `CanvasBoundaryWidth` (number)
- `CanvasBoundaryHeight` (number)
- `Shapes` (array of shape records)

### Shape record fields
- `Kind` (string)
- `Id` (string)
- `IsComputed` (bool)
- `PositionX`, `PositionY` (number)
- `OrientationX`, `OrientationY` (number)
- Optional numeric fields by kind: `Length`, `Width`, `Height`, `Radius`, `Offset`, `FontSize`, `XAxisLength`, `YAxisLength`, `HeadLength`, `HeadAngleRad`, `StartAngleRad`, `SweepAngleRad`, `Size`
- Optional text fields by kind: `Text`, `SourcePath`, `IconKey`

Supported shape kinds on ESP32 renderer:
- `Point`, `Line`, `Rectangle`, `Circle`
- `Text`, `MultilineText`, `Icon`
- `Image`, `TextBox`, `Arrow`
- `CenterlineRectangle`, `Referential`, `Dimension`
- `AngleDimension`, `Arc`

Notes:
- Some advanced kinds are approximated to available e-paper primitives.
- `Image` supports matrix payload rendering via optional `ImageMatrix`:
  - `Width` (int)
  - `Height` (int)
  - `Bpp` (int, currently `1` supported)
  - `BlackIsOne` (bool)
  - `Data` (base64, row-major packed bits)
- If `ImageMatrix` is missing/invalid, `Image` falls back to placeholder frame/cross.
- Device performs a deep-clean cycle before each JSON canvas transmission.

## Legacy Format (still accepted)
```json
{
  "clear": true,
  "commands": [
    { "type": "text", "x": 100, "y": 60, "size": 2, "content": "Hello" },
    { "type": "line", "x1": 20, "y1": 20, "x2": 200, "y2": 200 }
  ]
}
```
