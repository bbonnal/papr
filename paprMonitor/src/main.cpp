#include <Arduino.h>
#include <M5Unified.h>

#include "scene_renderer.h"

M5Canvas canvas(&M5.Display);
String inputLine;

void setup()
{
  auto cfg = M5.config();
  M5.begin(cfg);

  M5.Display.setRotation(1);
  M5.Display.setEpdMode(epd_fast);
  M5.Display.setFont(&fonts::FreeSans12pt7b);
  M5.Display.setTextColor(TFT_BLACK, TFT_WHITE);
  M5.Display.clear(TFT_WHITE);

  papr::InitializeCanvas(canvas, M5.Display.width(), M5.Display.height());

  Serial.begin(115200);
  delay(100);
  Serial.println("Papr monitor ready");
}

void loop()
{
  M5.update();

  while (Serial.available()) {
    const char c = static_cast<char>(Serial.read());
    if (c == '\n' || c == '\r') {
      inputLine.trim();
      if (inputLine.length() > 0) {
        papr::HandleCommand(canvas, inputLine);
        inputLine = "";
      }
    } else if (static_cast<uint8_t>(c) >= 32) {
      inputLine += c;
    }
  }
}
