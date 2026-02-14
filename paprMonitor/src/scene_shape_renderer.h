#pragma once

#include <ArduinoJson.h>
#include <M5Unified.h>

namespace papr {

bool RenderSceneFromRoot(M5Canvas& canvas, JsonObjectConst root);

} // namespace papr
