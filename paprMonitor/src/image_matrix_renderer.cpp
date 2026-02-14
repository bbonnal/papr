#include "image_matrix_renderer.h"

#include <mbedtls/base64.h>
#include <vector>

namespace papr {

namespace {

bool DecodeBase64(const String& encoded, std::vector<uint8_t>& out)
{
  const unsigned char* input = reinterpret_cast<const unsigned char*>(encoded.c_str());
  const size_t inputLen = encoded.length();
  size_t outputLen = 0;
  const size_t maxOutput = ((inputLen + 3) / 4) * 3;

  out.assign(maxOutput, 0);
  const int result = mbedtls_base64_decode(out.data(), out.size(), &outputLen, input, inputLen);
  if (result != 0) {
    out.clear();
    return false;
  }

  out.resize(outputLen);
  return true;
}

bool ReadPackedBit(const std::vector<uint8_t>& data, size_t bitIndex)
{
  const size_t byteIndex = bitIndex / 8;
  if (byteIndex >= data.size()) {
    return false;
  }

  const uint8_t bitMask = static_cast<uint8_t>(1u << (7u - (bitIndex % 8u)));
  return (data[byteIndex] & bitMask) != 0;
}

} // namespace

bool RenderImageMatrix(M5Canvas& canvas, JsonObjectConst shape, int dstX, int dstY, int dstW, int dstH)
{
  const JsonObjectConst matrix = shape["ImageMatrix"].as<JsonObjectConst>();
  if (matrix.isNull()) {
    Serial.println("ImageMatrix: missing matrix object");
    return false;
  }

  const int srcW = matrix["Width"] | 0;
  const int srcH = matrix["Height"] | 0;
  const int bpp = matrix["Bpp"] | 1;
  const bool blackIsOne = matrix["BlackIsOne"] | true;

  const JsonVariantConst dataVariant = matrix["Data"];
  const bool hasDataKey = !dataVariant.isNull();
  String dataBase64 = hasDataKey ? dataVariant.as<String>() : String("");

  const bool hasLowerDataKey = !matrix["data"].isNull();
  if (dataBase64.length() == 0 && hasLowerDataKey) {
    dataBase64 = matrix["data"].as<String>();
  }
  const int dataLen = dataBase64.length();

  if (srcW <= 0 || srcH <= 0 || bpp != 1 || dataLen == 0) {
    Serial.printf("ImageMatrix: invalid metadata W=%d H=%d Bpp=%d HasData=%d HasDataLower=%d DataLen=%d\n",
                  srcW, srcH, bpp, hasDataKey ? 1 : 0, hasLowerDataKey ? 1 : 0, dataLen);
    return false;
  }

  Serial.printf("ImageMatrix: metadata W=%d H=%d Bpp=%d BlackIsOne=%d DataLen=%d\n",
                srcW, srcH, bpp, blackIsOne ? 1 : 0, dataLen);

  std::vector<uint8_t> packed;
  if (!DecodeBase64(dataBase64, packed)) {
    Serial.printf("ImageMatrix: base64 decode failed DataLen=%d Prefix='%.24s'\n", dataLen, dataBase64.c_str());
    return false;
  }

  const size_t expectedBits = static_cast<size_t>(srcW) * static_cast<size_t>(srcH);
  const size_t expectedBytes = (expectedBits + 7) / 8;
  if (packed.size() < expectedBytes) {
    Serial.printf("ImageMatrix: decoded bytes too small (%u < %u)\n",
                  static_cast<unsigned>(packed.size()),
                  static_cast<unsigned>(expectedBytes));
    return false;
  }

  for (int y = 0; y < dstH; ++y) {
    const int srcY = static_cast<int>((static_cast<long long>(y) * srcH) / dstH);
    const int py = dstY + y;
    if (py < 0 || py >= canvas.height()) {
      continue;
    }

    for (int x = 0; x < dstW; ++x) {
      const int srcX = static_cast<int>((static_cast<long long>(x) * srcW) / dstW);
      const int px = dstX + x;
      if (px < 0 || px >= canvas.width()) {
        continue;
      }

      const size_t bitIndex = (static_cast<size_t>(srcY) * static_cast<size_t>(srcW)) + static_cast<size_t>(srcX);
      const bool bit = ReadPackedBit(packed, bitIndex);
      const bool black = blackIsOne ? bit : !bit;
      canvas.drawPixel(px, py, black ? TFT_BLACK : TFT_WHITE);
    }
  }

  return true;
}

} // namespace papr
