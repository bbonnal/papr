#include "scene_geometry.h"

#include <math.h>

namespace papr {

Vec2 Normalize(Vec2 v)
{
  const double m = sqrt((v.x * v.x) + (v.y * v.y));
  if (m <= 0.000001) {
    return {1.0, 0.0};
  }

  return {v.x / m, v.y / m};
}

Vec2 Perp(Vec2 v)
{
  return {-v.y, v.x};
}

int IRound(double v)
{
  return static_cast<int>(lround(v));
}

void DrawLine(M5Canvas& canvas, Vec2 a, Vec2 b, int thickness)
{
  const int stroke = max(1, thickness);
  if (stroke == 1) {
    canvas.drawLine(IRound(a.x), IRound(a.y), IRound(b.x), IRound(b.y), TFT_BLACK);
    return;
  }

  const Vec2 dir = Normalize({b.x - a.x, b.y - a.y});
  const Vec2 normal = Perp(dir);
  const bool even = (stroke % 2) == 0;
  const double centerOffset = even ? 0.5 : 0.0;
  const int half = stroke / 2;

  for (int i = -half; i <= half; ++i) {
    if (even && i == half) {
      continue;
    }

    const double offset = static_cast<double>(i) + centerOffset;
    const Vec2 da = {a.x + (normal.x * offset), a.y + (normal.y * offset)};
    const Vec2 db = {b.x + (normal.x * offset), b.y + (normal.y * offset)};
    canvas.drawLine(IRound(da.x), IRound(da.y), IRound(db.x), IRound(db.y), TFT_BLACK);
  }
}

void DrawArrowHead(M5Canvas& canvas, Vec2 tip, Vec2 from, double size, int thickness)
{
  const Vec2 dir = Normalize({tip.x - from.x, tip.y - from.y});
  const Vec2 n = Perp(dir);

  const Vec2 p1 = {tip.x - (dir.x * size) + (n.x * size * 0.5), tip.y - (dir.y * size) + (n.y * size * 0.5)};
  const Vec2 p2 = {tip.x - (dir.x * size) - (n.x * size * 0.5), tip.y - (dir.y * size) - (n.y * size * 0.5)};

  DrawLine(canvas, tip, p1, thickness);
  DrawLine(canvas, tip, p2, thickness);
}

void DrawArcBySegments(M5Canvas& canvas, Vec2 center, double radius, double startRad, double sweepRad, int steps, int thickness)
{
  if (radius <= 0.01) {
    return;
  }

  if (fabs(sweepRad) < 0.001) {
    sweepRad = 0.001;
  }

  const int segments = max(8, static_cast<int>(fabs(sweepRad) / (2 * M_PI) * steps));
  Vec2 prev = {center.x + (cos(startRad) * radius), center.y + (sin(startRad) * radius)};

  for (int i = 1; i <= segments; ++i) {
    const double t = static_cast<double>(i) / static_cast<double>(segments);
    const double a = startRad + (sweepRad * t);
    const Vec2 current = {center.x + (cos(a) * radius), center.y + (sin(a) * radius)};
    DrawLine(canvas, prev, current, thickness);
    prev = current;
  }
}

} // namespace papr
