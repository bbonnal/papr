# M5Paper JSON Protocol Documentation

This document describes the JSON format used to send drawing commands to the M5Paper device via Serial.

## Communication
- **Baud Rate:** 115200
- **Format:** Single line JSON (end with `\n` or `\r`)

## Root Object
The root of the message must be a JSON object.

| Field | Type | Description |
| :--- | :--- | :--- |
| `clear` | boolean | (Optional) If `true`, clears the screen with white before drawing. |
| `commands` | array | An array of drawing command objects. |

## Drawing Commands

### Text
Draws a string at a specific position.
```json
{
  "type": "text",
  "x": 100,
  "y": 50,
  "size": 1,
  "content": "Hello World"
}
```
- `x`, `y`: Integer coordinates.
- `size`: Integer font size multiplier (default 1).
- `content`: String to display.

### Rectangle
Draws or fills a rectangle.
```json
{
  "type": "rect",
  "x": 100,
  "y": 100,
  "w": 200,
  "h": 150,
  "fill": true
}
```
- `x`, `y`: Top-left corner coordinates.
- `w`, `h`: Width and height.
- `fill`: Boolean (default `true`). If `false`, draws an outline.

### Line
Draws a line between two points.
```json
{
  "type": "line",
  "x1": 0,
  "y1": 0,
  "x2": 100,
  "y2": 100
}
```
- `x1`, `y1`: Start point.
- `x2`, `y2`: End point.

### Circle
Draws or fills a circle.
```json
{
  "type": "circle",
  "x": 300,
  "y": 300,
  "r": 50,
  "fill": true
}
```
- `x`, `y`: Center coordinates.
- `r`: Radius.
- `fill`: Boolean (default `true`). If `false`, draws an outline.

## C# Implementation Notes
When building the C# UI:
1. Use `System.Text.Json` or `Newtonsoft.Json` to serialize your drawing objects.
2. Ensure the resulting JSON is sent as a single line over the Serial port.
3. The M5Paper screen resolution is 960x540.
