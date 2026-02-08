#include <Arduino.h>
#include <M5Unified.h>
#include <ArduinoJson.h>

// M5Paper canvas
M5Canvas canvas(&M5.Display);

String inputLine;
int cursorY = 50;

static void clearScreen() {
  canvas.fillSprite(TFT_WHITE);
  cursorY = 50;
  canvas.pushSprite(0, 0);
  Serial.println("Screen cleared");
}

static void drawText(const String &text) {
  canvas.setTextColor(TFT_BLACK, TFT_WHITE);
  canvas.setTextDatum(TL_DATUM); // Top-left alignment
  canvas.drawString(text, 50, cursorY);

  // Push to display
  canvas.pushSprite(0, 0);

  cursorY += 60;
  if (cursorY > M5.Display.height() - 60) cursorY = 50; // Wrap around

  Serial.print("Drew text: ");
  Serial.println(text);
}

static void drawRect() {
  canvas.fillRect(100, 100, 200, 150, TFT_BLACK); 
  canvas.pushSprite(0, 0);
  Serial.println("Drew rectangle");
}

static void fillWhite() {
  canvas.fillSprite(TFT_WHITE); 
  canvas.pushSprite(0, 0);
  Serial.println("Filled white");
}

static void testPattern() {
  canvas.setTextColor(TFT_BLACK, TFT_WHITE);
  canvas.setTextDatum(TL_DATUM);
  canvas.drawString("TEST", 100, 50);

  canvas.fillRect(100, 150, 300, 10, TFT_BLACK);  // Line
  canvas.fillRect(100, 200, 10, 100, TFT_BLACK);  // Vertical line
  canvas.fillCircle(500, 250, 50, TFT_BLACK);     // Circle

  canvas.pushSprite(0, 0);
  Serial.println("Drew test pattern");
}

static void handleJson(const String &json) {
  JsonDocument doc;
  DeserializationError error = deserializeJson(doc, json);

  if (error) {
    Serial.print("JSON Parse failed: ");
    Serial.println(error.c_str());
    return;
  }

  if (doc.containsKey("clear") && doc["clear"].as<bool>()) {
    canvas.fillSprite(TFT_WHITE);
  }

  JsonArray commands = doc["commands"].as<JsonArray>();
  for (JsonObject cmd : commands) {
    const char* type = cmd["type"];
    
    if (strcmp(type, "text") == 0) {
      int x = cmd["X"] | 0;
      int y = cmd["Y"] | 0;
      int size = cmd["Size"] | 1;
      const char* content = cmd["Content"] | "";
      canvas.setTextSize((float)size/2.0);
      canvas.drawString(content, x, y);
    } 
    else if (strcmp(type, "rect") == 0) {
      int x = cmd["X"] | 0;
      int y = cmd["Y"] | 0;
      int w = cmd["W"] | 0;
      int h = cmd["H"] | 0;
      bool fill = cmd["Fill"] | true;
      if (fill) {
        canvas.fillRect(x, y, w, h, TFT_BLACK);
      } else {
        canvas.drawRect(x, y, w, h, TFT_BLACK);
      }
    } 
    else if (strcmp(type, "line") == 0) {
      int x1 = cmd["X1"] | 0;
      int y1 = cmd["Y1"] | 0;
      int x2 = cmd["X2"] | 0;
      int y2 = cmd["Y2"] | 0;
      canvas.drawLine(x1, y1, x2, y2, TFT_BLACK);
    } 
    else if (strcmp(type, "circle") == 0) {
      int x = cmd["X"] | 0;
      int y = cmd["Y"] | 0;
      int r = cmd["R"] | 0;
      bool fill = cmd["Fill"] | true;
      if (fill) {
        canvas.fillCircle(x, y, r, TFT_BLACK);
      } else {
        canvas.drawCircle(x, y, r, TFT_BLACK);
      }
    }
  }

  canvas.pushSprite(0, 0);
  Serial.println("JSON commands executed");
}

static void handleCommand(const String &cmd) {
  Serial.print("Handling command: [");
  Serial.print(cmd);
  Serial.println("]");

  if (cmd.startsWith("{") || cmd.startsWith("[")) {
    handleJson(cmd);
    return;
  }

  if (cmd == "clear") {
    clearScreen();
    return;
  }

  if (cmd.startsWith("text ")) {
    drawText(cmd.substring(5));
    return;
  }

  if (cmd == "rect") {
    drawRect();
    return;
  }

  if (cmd == "fill") {
    fillWhite();
    return;
  }

  if (cmd == "test") {
    testPattern();
    return;
  }

  if (cmd == "info") {
    Serial.print("Canvas size: ");
    Serial.print(canvas.width());
    Serial.print(" x ");
    Serial.println(canvas.height());
    Serial.print("Cursor Y: ");
    Serial.println(cursorY);
    return;
  }

  Serial.println("Unknown command - try: clear, text <msg>, rect, fill, test, info");
}

void setup() {
  auto cfg = M5.config();
  M5.begin(cfg);

  M5.Display.setRotation(1);
  M5.Display.setEpdMode(epd_fast);
  M5.Display.setFont(&fonts::FreeMonoBold24pt7b);
  M5.Display.setTextColor(TFT_BLACK, TFT_WHITE);
  
  M5.Display.clear(TFT_WHITE);
  
  // Initialize canvas for M5Paper
  canvas.setColorDepth(8); // 8-bit grayscale for EPD
  canvas.createSprite(M5.Display.width(), M5.Display.height());
  canvas.setTextSize(1); // Use font size directly from setFont
  canvas.setFont(&fonts::FreeMonoBold24pt7b);
  canvas.setTextColor(TFT_BLACK, TFT_WHITE);

  // Startup message
  canvas.fillSprite(TFT_WHITE);
  canvas.drawString("READY", 50, 50);
  canvas.pushSprite(0, 0);

  Serial.begin(115200);
  delay(200);
  Serial.println("\n\nM5Paper CLI ready");
  Serial.println("Commands:");
  Serial.println("  clear");
  Serial.println("  text <message>");
  Serial.println("  rect");
  Serial.println("  fill");
  Serial.println("  test");
  Serial.println("  info");
}

void loop() {
  M5.update();
  
  if (M5.BtnA.wasPressed()) {
    Serial.println("BtnA was pressed");
  }

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