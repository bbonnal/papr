#pragma once

#include <M5Unified.h>

namespace papr {

struct Vec2 {
  double x;
  double y;
};

Vec2 Normalize(Vec2 v);
Vec2 Perp(Vec2 v);
int IRound(double v);
void DrawLine(M5Canvas& canvas, Vec2 a, Vec2 b);
void DrawArrowHead(M5Canvas& canvas, Vec2 tip, Vec2 from, double size);
void DrawArcBySegments(M5Canvas& canvas, Vec2 center, double radius, double startRad, double sweepRad, int steps = 48);

} // namespace papr
