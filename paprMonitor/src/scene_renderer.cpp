#include "scene_renderer.h"

#include "scene_json_protocol.h"
#include "scene_shape_renderer.h"

namespace papr {

namespace {

void DeepCleanDisplay(M5Canvas& canvas)
{
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

void HandleSceneJsonCommand(M5Canvas& canvas, const String& json)
{
  JsonDocument doc;
  JsonObjectConst root;

  if (!TryParseSceneJson(json, doc, root)) {
    return;
  }

  DeepCleanDisplay(canvas);
  RenderSceneFromRoot(canvas, root);
}

} // namespace

void InitializeCanvas(M5Canvas& canvas, int width, int height)
{
  canvas.setColorDepth(8);
  canvas.createSprite(width, height);
  canvas.setFont(&fonts::FreeSans12pt7b);
  canvas.setTextSize(1);
  canvas.setTextColor(TFT_BLACK, TFT_WHITE);
  canvas.setTextDatum(TL_DATUM);

  canvas.fillSprite(TFT_WHITE);
  canvas.drawString("READY", 50, 50);
  canvas.pushSprite(0, 0);
}

void HandleCommand(M5Canvas& canvas, const String& cmd)
{
  if (cmd.startsWith("{")) {
    HandleSceneJsonCommand(canvas, cmd);
    return;
  }

  if (cmd == "clear") {
    DeepCleanDisplay(canvas);
    canvas.fillSprite(TFT_WHITE);
    canvas.pushSprite(0, 0);
    Serial.println("Screen cleared");
    return;
  }

  Serial.println("Unknown command");
}

} // namespace papr
