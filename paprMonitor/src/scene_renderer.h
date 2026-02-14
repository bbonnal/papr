#pragma once

#include <M5Unified.h>

namespace papr {

void InitializeCanvas(M5Canvas& canvas, int width, int height);
void HandleCommand(M5Canvas& canvas, const String& cmd);

} // namespace papr
