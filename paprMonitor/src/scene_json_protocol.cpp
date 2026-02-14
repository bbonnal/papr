#include "scene_json_protocol.h"

namespace papr {

bool TryParseSceneJson(const String& json, JsonDocument& doc, JsonObjectConst& root)
{
  const DeserializationError error = deserializeJson(doc, json);
  if (error) {
    Serial.print("JSON Parse failed: ");
    Serial.println(error.c_str());
    return false;
  }

  if (!doc.is<JsonObject>()) {
    Serial.println("Scene JSON invalid: root must be an object");
    return false;
  }

  root = doc.as<JsonObjectConst>();

  if (root["Shapes"].isNull() || !root["Shapes"].is<JsonArrayConst>()) {
    Serial.println("Scene JSON invalid: missing Shapes array");
    return false;
  }

  return true;
}

} // namespace papr
