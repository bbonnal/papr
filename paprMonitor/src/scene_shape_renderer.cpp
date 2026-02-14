#include "scene_shape_renderer.h"

#include "image_matrix_renderer.h"
#include "scene_geometry.h"

#include <math.h>

namespace papr {

namespace {

double GetNumber(JsonObjectConst obj, const char* key, double fallback = 0.0)
{
  if (!obj[key].isNull()) {
    return obj[key].as<double>();
  }

  return fallback;
}

String GetText(JsonObjectConst obj, const char* key, const char* fallback = "")
{
  if (!obj[key].isNull()) {
    const char* value = obj[key].as<const char*>();
    return value == nullptr ? String(fallback) : String(value);
  }

  return String(fallback);
}

void SetApproxFont(M5Canvas& canvas, double fontSize)
{
  canvas.setFont(&fonts::FreeSans12pt7b);
  const double scale = fontSize <= 0 ? 1.0 : (fontSize / 16.0);
  int textScale = static_cast<int>(lround(scale));
  textScale = constrain(textScale, 1, 4);
  canvas.setTextSize(textScale);
  canvas.setTextColor(TFT_BLACK, TFT_WHITE);
  canvas.setTextDatum(TL_DATUM);
}

void DrawSceneShape(M5Canvas& canvas, JsonObjectConst shape)
{
  const String kind = GetText(shape, "Kind");
  const Vec2 pos = {GetNumber(shape, "PositionX", 0), GetNumber(shape, "PositionY", 0)};
  const Vec2 orientation = Normalize({GetNumber(shape, "OrientationX", 1), GetNumber(shape, "OrientationY", 0)});
  const Vec2 normal = Perp(orientation);

  if (kind == "Point") {
    canvas.fillCircle(IRound(pos.x), IRound(pos.y), 3, TFT_BLACK);
    return;
  }

  if (kind == "Line") {
    const double length = GetNumber(shape, "Length", 0);
    const Vec2 end = {pos.x + (orientation.x * length), pos.y + (orientation.y * length)};
    DrawLine(canvas, pos, end);
    return;
  }

  if (kind == "Rectangle") {
    const double w = GetNumber(shape, "Width", 0);
    const double h = GetNumber(shape, "Height", 0);
    const double hw = w * 0.5;
    const double hh = h * 0.5;

    const Vec2 tl = {pos.x - (orientation.x * hw) - (normal.x * hh), pos.y - (orientation.y * hw) - (normal.y * hh)};
    const Vec2 tr = {pos.x + (orientation.x * hw) - (normal.x * hh), pos.y + (orientation.y * hw) - (normal.y * hh)};
    const Vec2 br = {pos.x + (orientation.x * hw) + (normal.x * hh), pos.y + (orientation.y * hw) + (normal.y * hh)};
    const Vec2 bl = {pos.x - (orientation.x * hw) + (normal.x * hh), pos.y - (orientation.y * hw) + (normal.y * hh)};

    DrawLine(canvas, tl, tr);
    DrawLine(canvas, tr, br);
    DrawLine(canvas, br, bl);
    DrawLine(canvas, bl, tl);
    return;
  }

  if (kind == "Circle") {
    const double r = GetNumber(shape, "Radius", 0);
    canvas.drawCircle(IRound(pos.x), IRound(pos.y), max(1, IRound(r)), TFT_BLACK);
    return;
  }

  if (kind == "Text") {
    const String text = GetText(shape, "Text", "Text");
    const double fontSize = GetNumber(shape, "FontSize", 16);
    SetApproxFont(canvas, fontSize);
    canvas.drawString(text, IRound(pos.x), IRound(pos.y));
    return;
  }

  if (kind == "MultilineText") {
    String text = GetText(shape, "Text", "Line 1\nLine 2");
    const double fontSize = GetNumber(shape, "FontSize", 16);
    SetApproxFont(canvas, fontSize);

    int y = IRound(pos.y);
    while (true) {
      const int sep = text.indexOf('\n');
      if (sep < 0) {
        canvas.drawString(text, IRound(pos.x), y);
        break;
      }

      const String line = text.substring(0, sep);
      canvas.drawString(line, IRound(pos.x), y);
      text = text.substring(sep + 1);
      y += static_cast<int>(fontSize * 1.35);
    }

    return;
  }

  if (kind == "Icon") {
    const String icon = GetText(shape, "IconKey", "*");
    const double size = GetNumber(shape, "Size", 24);
    SetApproxFont(canvas, size);
    canvas.drawString(icon, IRound(pos.x), IRound(pos.y));
    return;
  }

  if (kind == "Image") {
    const double w = GetNumber(shape, "Width", 0);
    const double h = GetNumber(shape, "Height", 0);
    const int x = IRound(pos.x - (w * 0.5));
    const int y = IRound(pos.y - (h * 0.5));
    const int wi = max(1, IRound(w));
    const int hi = max(1, IRound(h));

    if (RenderImageMatrix(canvas, shape, x, y, wi, hi)) {
      return;
    }

    canvas.drawRect(x, y, wi, hi, TFT_BLACK);
    canvas.drawLine(x, y, x + wi, y + hi, TFT_BLACK);
    canvas.drawLine(x + wi, y, x, y + hi, TFT_BLACK);
    return;
  }

  if (kind == "TextBox") {
    const double w = GetNumber(shape, "Width", 0);
    const double h = GetNumber(shape, "Height", 0);
    const String text = GetText(shape, "Text", "Text");
    const double fontSize = GetNumber(shape, "FontSize", 14);

    const int x = IRound(pos.x - (w * 0.5));
    const int y = IRound(pos.y - (h * 0.5));
    const int wi = max(1, IRound(w));
    const int hi = max(1, IRound(h));

    canvas.drawRect(x, y, wi, hi, TFT_BLACK);
    SetApproxFont(canvas, fontSize);
    canvas.drawString(text, x + 6, y + 6);
    return;
  }

  if (kind == "Arrow") {
    const double length = GetNumber(shape, "Length", 0);
    const double headLength = GetNumber(shape, "HeadLength", 18);
    const Vec2 end = {pos.x + (orientation.x * length), pos.y + (orientation.y * length)};

    DrawLine(canvas, pos, end);
    DrawArrowHead(canvas, end, pos, headLength);
    return;
  }

  if (kind == "CenterlineRectangle") {
    const double length = GetNumber(shape, "Length", 0);
    const double width = GetNumber(shape, "Width", 0);
    const Vec2 start = pos;
    const Vec2 end = {pos.x + (orientation.x * length), pos.y + (orientation.y * length)};
    const Vec2 halfW = {normal.x * (width * 0.5), normal.y * (width * 0.5)};

    const Vec2 tl = {start.x + halfW.x, start.y + halfW.y};
    const Vec2 tr = {end.x + halfW.x, end.y + halfW.y};
    const Vec2 bl = {start.x - halfW.x, start.y - halfW.y};
    const Vec2 br = {end.x - halfW.x, end.y - halfW.y};

    DrawLine(canvas, tl, tr);
    DrawLine(canvas, tr, br);
    DrawLine(canvas, br, bl);
    DrawLine(canvas, bl, tl);
    DrawLine(canvas, start, end);
    return;
  }

  if (kind == "Referential") {
    const double xLen = GetNumber(shape, "XAxisLength", 80);
    const double yLen = GetNumber(shape, "YAxisLength", 80);

    const Vec2 xEnd = {pos.x + (orientation.x * xLen), pos.y + (orientation.y * xLen)};
    const Vec2 yEnd = {pos.x + (normal.x * yLen), pos.y + (normal.y * yLen)};

    DrawLine(canvas, pos, xEnd);
    DrawLine(canvas, pos, yEnd);
    DrawArrowHead(canvas, xEnd, pos, 10);
    DrawArrowHead(canvas, yEnd, pos, 10);
    return;
  }

  if (kind == "Dimension") {
    const double length = GetNumber(shape, "Length", 0);
    const double offset = GetNumber(shape, "Offset", 24);
    String label = GetText(shape, "Text", "");

    const Vec2 end = {pos.x + (orientation.x * length), pos.y + (orientation.y * length)};
    const Vec2 offsetV = {normal.x * offset, normal.y * offset};

    const Vec2 os = {pos.x + offsetV.x, pos.y + offsetV.y};
    const Vec2 oe = {end.x + offsetV.x, end.y + offsetV.y};

    DrawLine(canvas, pos, os);
    DrawLine(canvas, end, oe);
    DrawLine(canvas, os, oe);
    DrawArrowHead(canvas, os, oe, 9);
    DrawArrowHead(canvas, oe, os, 9);

    if (label.length() == 0) {
      label = String(length, 1);
    }

    SetApproxFont(canvas, 12);
    canvas.drawString(label, IRound((os.x + oe.x) * 0.5) + 4, IRound((os.y + oe.y) * 0.5) - 14);
    return;
  }

  if (kind == "AngleDimension") {
    const double radius = GetNumber(shape, "Radius", 40);
    const double start = GetNumber(shape, "StartAngleRad", 0);
    const double sweep = GetNumber(shape, "SweepAngleRad", M_PI / 2.0);
    String label = GetText(shape, "Text", "");

    const Vec2 startP = {pos.x + (cos(start) * radius), pos.y + (sin(start) * radius)};
    const Vec2 endP = {pos.x + (cos(start + sweep) * radius), pos.y + (sin(start + sweep) * radius)};

    DrawLine(canvas, pos, startP);
    DrawLine(canvas, pos, endP);
    DrawArcBySegments(canvas, pos, radius, start, sweep);

    if (label.length() == 0) {
      label = String(fabs(sweep * 180.0 / M_PI), 1) + String("deg");
    }

    SetApproxFont(canvas, 12);
    const Vec2 mid = {
      pos.x + (cos(start + (sweep * 0.5)) * (radius + 10)),
      pos.y + (sin(start + (sweep * 0.5)) * (radius + 10))
    };
    canvas.drawString(label, IRound(mid.x), IRound(mid.y));
    return;
  }

  if (kind == "Arc") {
    const double radius = GetNumber(shape, "Radius", 40);
    const double start = GetNumber(shape, "StartAngleRad", 0);
    const double sweep = GetNumber(shape, "SweepAngleRad", M_PI / 2.0);
    DrawArcBySegments(canvas, pos, radius, start, sweep);
    return;
  }

  Serial.printf("Scene: unsupported shape kind '%s'\n", kind.c_str());
}

} // namespace

bool RenderSceneFromRoot(M5Canvas& canvas, JsonObjectConst root)
{
  const JsonArrayConst shapes = root["Shapes"].as<JsonArrayConst>();
  if (shapes.isNull()) {
    Serial.println("Scene JSON invalid: missing Shapes array");
    return false;
  }

  canvas.fillSprite(TFT_WHITE);

  for (JsonObjectConst shape : shapes) {
    DrawSceneShape(canvas, shape);
  }

  canvas.pushSprite(0, 0);
  Serial.println("Scene rendered");
  return true;
}

} // namespace papr
