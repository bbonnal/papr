#pragma once

#include <ArduinoJson.h>

namespace papr {

bool TryParseSceneJson(const String& json, JsonDocument& doc, JsonObjectConst& root);

} // namespace papr
