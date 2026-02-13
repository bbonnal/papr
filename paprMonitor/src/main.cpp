#include <Arduino.h>
#include <M5Unified.h>
#include <ArduinoJson.h>
#include <math.h>

M5Canvas canvas(&M5.Display);
String inputLine;

struct Vec2 {
  double x;
  double y;
};

static Vec2 normalize(Vec2 v) {
  const double m = sqrt((v.x * v.x) + (v.y * v.y));
  if (m <= 0.000001) {
    return {1.0, 0.0};
  }

  return {v.x / m, v.y / m};
}

static Vec2 perp(Vec2 v) {
  return {-v.y, v.x};
}

static int iround(double v) {
  return (int)lround(v);
}

static void drawLineV(Vec2 a, Vec2 b) {
  canvas.drawLine(iround(a.x), iround(a.y), iround(b.x), iround(b.y), TFT_BLACK);
}

static void drawArrowHead(Vec2 tip, Vec2 from, double size) {
  Vec2 dir = normalize({tip.x - from.x, tip.y - from.y});
  Vec2 n = perp(dir);

  Vec2 p1 = {tip.x - (dir.x * size) + (n.x * size * 0.5), tip.y - (dir.y * size) + (n.y * size * 0.5)};
  Vec2 p2 = {tip.x - (dir.x * size) - (n.x * size * 0.5), tip.y - (dir.y * size) - (n.y * size * 0.5)};

  drawLineV(tip, p1);
  drawLineV(tip, p2);
}

static void drawArcBySegments(Vec2 center, double radius, double startRad, double sweepRad, int steps = 48) {
  if (radius <= 0.01) {
    return;
  }

  if (fabs(sweepRad) < 0.001) {
    sweepRad = 0.001;
  }

  const int segments = max(8, (int)(fabs(sweepRad) / (2 * M_PI) * steps));

  Vec2 prev = {center.x + (cos(startRad) * radius), center.y + (sin(startRad) * radius)};
  for (int i = 1; i <= segments; ++i) {
    const double t = (double)i / (double)segments;
    const double a = startRad + (sweepRad * t);
    Vec2 current = {center.x + (cos(a) * radius), center.y + (sin(a) * radius)};
    drawLineV(prev, current);
    prev = current;
  }
}

static void setApproxFont(double fontSize) {
  // Approximate Avalonia/rUI size with available M5Paper font stack.
  canvas.setFont(&fonts::FreeSans12pt7b);
  const double scale = fontSize <= 0 ? 1.0 : (fontSize / 16.0);
  int textScale = (int)lround(scale);
  textScale = constrain(textScale, 1, 4);
  canvas.setTextSize(textScale);
  canvas.setTextColor(TFT_BLACK, TFT_WHITE);
  canvas.setTextDatum(TL_DATUM);
}

static void deepCleanDisplay() {
  // Deep clean to reduce ghosting before a full canvas update.
  M5.Display.setEpdMode(epd_quality);

  canvas.fillSprite(TFT_BLACK);
  canvas.pushSprite(0, 0);
  delay(180);

  canvas.fillSprite(TFT_WHITE);
  canvas.pushSprite(0, 0);
  delay(180);

  canvas.fillSprite(TFT_BLACK);
  canvas.pushSprite(0, 0);
  delay(180);

  canvas.fillSprite(TFT_WHITE);
  canvas.pushSprite(0, 0);

  M5.Display.setEpdMode(epd_fast);
}

static double getNumber(JsonObjectConst obj, const char* key, double fallback = 0.0) {
  if (!obj[key].isNull()) {
    return obj[key].as<double>();
  }

  return fallback;
}

static String getText(JsonObjectConst obj, const char* key, const char* fallback = "") {
  if (!obj[key].isNull()) {
    const char* value = obj[key].as<const char*>();
    return value == nullptr ? String(fallback) : String(value);
  }

  return String(fallback);
}

static void drawSceneShape(JsonObjectConst shape) {
  const String kind = getText(shape, "Kind");

  const Vec2 pos = {
    getNumber(shape, "PositionX", 0),
    getNumber(shape, "PositionY", 0)
  };

  const Vec2 orientation = normalize({
    getNumber(shape, "OrientationX", 1),
    getNumber(shape, "OrientationY", 0)
  });

  const Vec2 normal = perp(orientation);

  if (kind == "Point") {
    canvas.fillCircle(iround(pos.x), iround(pos.y), 3, TFT_BLACK);
    return;
  }

  if (kind == "Line") {
    const double length = getNumber(shape, "Length", 0);
    Vec2 end = {pos.x + (orientation.x * length), pos.y + (orientation.y * length)};
    drawLineV(pos, end);
    return;
  }

  if (kind == "Rectangle") {
    const double w = getNumber(shape, "Width", 0);
    const double h = getNumber(shape, "Height", 0);
    const double hw = w * 0.5;
    const double hh = h * 0.5;

    Vec2 tl = {pos.x - (orientation.x * hw) - (normal.x * hh), pos.y - (orientation.y * hw) - (normal.y * hh)};
    Vec2 tr = {pos.x + (orientation.x * hw) - (normal.x * hh), pos.y + (orientation.y * hw) - (normal.y * hh)};
    Vec2 br = {pos.x + (orientation.x * hw) + (normal.x * hh), pos.y + (orientation.y * hw) + (normal.y * hh)};
    Vec2 bl = {pos.x - (orientation.x * hw) + (normal.x * hh), pos.y - (orientation.y * hw) + (normal.y * hh)};

    drawLineV(tl, tr);
    drawLineV(tr, br);
    drawLineV(br, bl);
    drawLineV(bl, tl);
    return;
  }

  if (kind == "Circle") {
    const double r = getNumber(shape, "Radius", 0);
    canvas.drawCircle(iround(pos.x), iround(pos.y), max(1, iround(r)), TFT_BLACK);
    return;
  }

  if (kind == "Text") {
    const String text = getText(shape, "Text", "Text");
    const double fontSize = getNumber(shape, "FontSize", 16);
    setApproxFont(fontSize);
    canvas.drawString(text, iround(pos.x), iround(pos.y));
    return;
  }

  if (kind == "MultilineText") {
    String text = getText(shape, "Text", "Line 1\nLine 2");
    const double fontSize = getNumber(shape, "FontSize", 16);
    setApproxFont(fontSize);

    int y = iround(pos.y);
    while (true) {
      int sep = text.indexOf('\n');
      if (sep < 0) {
        canvas.drawString(text, iround(pos.x), y);
        break;
      }

      String line = text.substring(0, sep);
      canvas.drawString(line, iround(pos.x), y);
      text = text.substring(sep + 1);
      y += (int)(fontSize * 1.35);
    }

    return;
  }

  if (kind == "Icon") {
    const String icon = getText(shape, "IconKey", "*");
    const double size = getNumber(shape, "Size", 24);
    setApproxFont(size);
    canvas.drawString(icon, iround(pos.x), iround(pos.y));
    return;
  }

  if (kind == "Image") {
    // Placeholder rendering: frame + cross.
    const double w = getNumber(shape, "Width", 0);
    const double h = getNumber(shape, "Height", 0);
    const int x = iround(pos.x - (w * 0.5));
    const int y = iround(pos.y - (h * 0.5));
    const int wi = max(1, iround(w));
    const int hi = max(1, iround(h));

    canvas.drawRect(x, y, wi, hi, TFT_BLACK);
    canvas.drawLine(x, y, x + wi, y + hi, TFT_BLACK);
    canvas.drawLine(x + wi, y, x, y + hi, TFT_BLACK);
    return;
  }

  if (kind == "TextBox") {
    const double w = getNumber(shape, "Width", 0);
    const double h = getNumber(shape, "Height", 0);
    const String text = getText(shape, "Text", "Text");
    const double fontSize = getNumber(shape, "FontSize", 14);

    const int x = iround(pos.x - (w * 0.5));
    const int y = iround(pos.y - (h * 0.5));
    const int wi = max(1, iround(w));
    const int hi = max(1, iround(h));

    canvas.drawRect(x, y, wi, hi, TFT_BLACK);
    setApproxFont(fontSize);
    canvas.drawString(text, x + 6, y + 6);
    return;
  }

  if (kind == "Arrow") {
    const double length = getNumber(shape, "Length", 0);
    const double headLength = getNumber(shape, "HeadLength", 18);
    Vec2 end = {pos.x + (orientation.x * length), pos.y + (orientation.y * length)};

    drawLineV(pos, end);
    drawArrowHead(end, pos, headLength);
    return;
  }

  if (kind == "CenterlineRectangle") {
    const double length = getNumber(shape, "Length", 0);
    const double width = getNumber(shape, "Width", 0);
    const Vec2 start = pos;
    const Vec2 end = {pos.x + (orientation.x * length), pos.y + (orientation.y * length)};
    const Vec2 halfW = {normal.x * (width * 0.5), normal.y * (width * 0.5)};

    Vec2 tl = {start.x + halfW.x, start.y + halfW.y};
    Vec2 tr = {end.x + halfW.x, end.y + halfW.y};
    Vec2 bl = {start.x - halfW.x, start.y - halfW.y};
    Vec2 br = {end.x - halfW.x, end.y - halfW.y};

    drawLineV(tl, tr);
    drawLineV(tr, br);
    drawLineV(br, bl);
    drawLineV(bl, tl);
    drawLineV(start, end);
    return;
  }

  if (kind == "Referential") {
    const double xLen = getNumber(shape, "XAxisLength", 80);
    const double yLen = getNumber(shape, "YAxisLength", 80);

    Vec2 xEnd = {pos.x + (orientation.x * xLen), pos.y + (orientation.y * xLen)};
    Vec2 yEnd = {pos.x + (normal.x * yLen), pos.y + (normal.y * yLen)};

    drawLineV(pos, xEnd);
    drawLineV(pos, yEnd);
    drawArrowHead(xEnd, pos, 10);
    drawArrowHead(yEnd, pos, 10);
    return;
  }

  if (kind == "Dimension") {
    const double length = getNumber(shape, "Length", 0);
    const double offset = getNumber(shape, "Offset", 24);
    String label = getText(shape, "Text", "");

    Vec2 end = {pos.x + (orientation.x * length), pos.y + (orientation.y * length)};
    Vec2 offsetV = {normal.x * offset, normal.y * offset};

    Vec2 os = {pos.x + offsetV.x, pos.y + offsetV.y};
    Vec2 oe = {end.x + offsetV.x, end.y + offsetV.y};

    drawLineV(pos, os);
    drawLineV(end, oe);
    drawLineV(os, oe);
    drawArrowHead(os, oe, 9);
    drawArrowHead(oe, os, 9);

    if (label.length() == 0) {
      label = String(length, 1);
    }

    setApproxFont(12);
    canvas.drawString(label, iround((os.x + oe.x) * 0.5) + 4, iround((os.y + oe.y) * 0.5) - 14);
    return;
  }

  if (kind == "AngleDimension") {
    const double radius = getNumber(shape, "Radius", 40);
    const double start = getNumber(shape, "StartAngleRad", 0);
    const double sweep = getNumber(shape, "SweepAngleRad", M_PI / 2.0);
    String label = getText(shape, "Text", "");

    Vec2 startP = {pos.x + (cos(start) * radius), pos.y + (sin(start) * radius)};
    Vec2 endP = {pos.x + (cos(start + sweep) * radius), pos.y + (sin(start + sweep) * radius)};

    drawLineV(pos, startP);
    drawLineV(pos, endP);
    drawArcBySegments(pos, radius, start, sweep);

    if (label.length() == 0) {
      label = String(fabs(sweep * 180.0 / M_PI), 1) + String("deg");
    }

    setApproxFont(12);
    Vec2 mid = {pos.x + (cos(start + (sweep * 0.5)) * (radius + 10)), pos.y + (sin(start + (sweep * 0.5)) * (radius + 10))};
    canvas.drawString(label, iround(mid.x), iround(mid.y));
    return;
  }

  if (kind == "Arc") {
    const double radius = getNumber(shape, "Radius", 40);
    const double start = getNumber(shape, "StartAngleRad", 0);
    const double sweep = getNumber(shape, "SweepAngleRad", M_PI / 2.0);
    drawArcBySegments(pos, radius, start, sweep);
    return;
  }
}

static void renderScene(JsonObjectConst root) {
  canvas.fillSprite(TFT_WHITE);

  JsonArrayConst shapes = root["Shapes"].as<JsonArrayConst>();
  for (JsonObjectConst shape : shapes) {
    drawSceneShape(shape);
  }

  canvas.pushSprite(0, 0);
  Serial.println("Scene rendered");
}

static void renderLegacyPayload(JsonObjectConst root) {
  if (root["clear"].as<bool>()) {
    canvas.fillSprite(TFT_WHITE);
  }

  JsonArrayConst commands = root["commands"].as<JsonArrayConst>();
  for (JsonObjectConst cmd : commands) {
    const char* type = cmd["type"];
    if (type == nullptr) {
      continue;
    }

    if (strcmp(type, "text") == 0) {
      int x = cmd["x"] | cmd["X"] | 0;
      int y = cmd["y"] | cmd["Y"] | 0;
      int size = cmd["size"] | cmd["Size"] | 1;
      const char* content = cmd["content"] | cmd["Content"] | "";
      setApproxFont(size * 16);
      canvas.drawString(content, x, y);
    } else if (strcmp(type, "rect") == 0) {
      int x = cmd["x"] | cmd["X"] | 0;
      int y = cmd["y"] | cmd["Y"] | 0;
      int w = cmd["w"] | cmd["W"] | 0;
      int h = cmd["h"] | cmd["H"] | 0;
      bool fill = cmd["fill"] | cmd["Fill"] | true;
      if (fill) {
        canvas.fillRect(x, y, w, h, TFT_BLACK);
      } else {
        canvas.drawRect(x, y, w, h, TFT_BLACK);
      }
    } else if (strcmp(type, "line") == 0) {
      int x1 = cmd["x1"] | cmd["X1"] | 0;
      int y1 = cmd["y1"] | cmd["Y1"] | 0;
      int x2 = cmd["x2"] | cmd["X2"] | 0;
      int y2 = cmd["y2"] | cmd["Y2"] | 0;
      canvas.drawLine(x1, y1, x2, y2, TFT_BLACK);
    } else if (strcmp(type, "circle") == 0) {
      int x = cmd["x"] | cmd["X"] | 0;
      int y = cmd["y"] | cmd["Y"] | 0;
      int r = cmd["r"] | cmd["R"] | 0;
      bool fill = cmd["fill"] | cmd["Fill"] | true;
      if (fill) {
        canvas.fillCircle(x, y, r, TFT_BLACK);
      } else {
        canvas.drawCircle(x, y, r, TFT_BLACK);
      }
    }
  }

  canvas.pushSprite(0, 0);
  Serial.println("Legacy payload rendered");
}

static void handleJson(const String &json) {
  JsonDocument doc;
  DeserializationError error = deserializeJson(doc, json);

  if (error) {
    Serial.print("JSON Parse failed: ");
    Serial.println(error.c_str());
    return;
  }

  JsonObjectConst root = doc.as<JsonObjectConst>();

  // Deep clean before applying each full canvas transmission.
  deepCleanDisplay();

  if (!root["Shapes"].isNull()) {
    renderScene(root);
    return;
  }

  renderLegacyPayload(root);
}

static void handleCommand(const String &cmd) {
  if (cmd.startsWith("{") || cmd.startsWith("[")) {
    handleJson(cmd);
    return;
  }

  if (cmd == "clear") {
    deepCleanDisplay();
    canvas.fillSprite(TFT_WHITE);
    canvas.pushSprite(0, 0);
    Serial.println("Screen cleared");
    return;
  }

  Serial.println("Unknown command");
}

void setup() {
  auto cfg = M5.config();
  M5.begin(cfg);

  M5.Display.setRotation(1);
  M5.Display.setEpdMode(epd_fast);
  M5.Display.setFont(&fonts::FreeSans12pt7b);
  M5.Display.setTextColor(TFT_BLACK, TFT_WHITE);

  M5.Display.clear(TFT_WHITE);

  canvas.setColorDepth(8);
  canvas.createSprite(M5.Display.width(), M5.Display.height());
  canvas.setFont(&fonts::FreeSans12pt7b);
  canvas.setTextSize(1);
  canvas.setTextColor(TFT_BLACK, TFT_WHITE);
  canvas.setTextDatum(TL_DATUM);

  canvas.fillSprite(TFT_WHITE);
  canvas.drawString("READY", 50, 50);
  canvas.pushSprite(0, 0);

  Serial.begin(115200);
  delay(200);
  Serial.println("\\n\\nM5Paper renderer ready");
  Serial.println("Accepts Scene JSON (rUI format) and legacy payload JSON");
}

void loop() {
  M5.update();

  while (Serial.available()) {
    char c = (char)Serial.read();
    if (c == '\n' || c == '\r') {
      inputLine.trim();
      if (inputLine.length() > 0) {
        handleCommand(inputLine);
        inputLine = "";
      }
    } else if ((uint8_t)c >= 32) {
      inputLine += c;
    }
  }
}
