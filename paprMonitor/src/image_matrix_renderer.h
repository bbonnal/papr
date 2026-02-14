#pragma once

#include <ArduinoJson.h>
#include <M5Unified.h>

namespace papr {

bool RenderImageMatrix(M5Canvas& canvas, JsonObjectConst shape, int dstX, int dstY, int dstW, int dstH);

} // namespace papr
