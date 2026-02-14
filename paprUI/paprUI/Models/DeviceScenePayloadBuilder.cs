using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using SkiaSharp;
using rUI.Drawing.Core.Scene;

namespace paprUI.Models;

/// <summary>
/// Builds ESP32-ready payloads by converting Image shapes into 1bpp packed matrices.
/// </summary>
public sealed class DeviceScenePayloadBuilder
{
    private const string DataUriPrefix = "data:";

    public string BuildPayload(SceneDocument scene, LibrarySettings settings)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(scene);
        var root = JsonNode.Parse(json)?.AsObject();
        if (root is null)
            return json;

        if (root["Shapes"] is not JsonArray shapes)
            return json;

        foreach (var node in shapes)
        {
            if (node is not JsonObject shape)
                continue;

            if (!string.Equals(shape["Kind"]?.GetValue<string>(), "Image", StringComparison.Ordinal))
                continue;

            var sourcePath = shape["SourcePath"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(sourcePath))
                continue;

            var targetWidth = Math.Max(1, (int)Math.Round(shape["Width"]?.GetValue<double>() ?? 0));
            var targetHeight = Math.Max(1, (int)Math.Round(shape["Height"]?.GetValue<double>() ?? 0));
            targetWidth = Math.Min(targetWidth, settings.ImageMatrixMaxWidth);
            targetHeight = Math.Min(targetHeight, settings.ImageMatrixMaxHeight);

            if (TryBuildImageMatrix(sourcePath!, targetWidth, targetHeight, settings.ImageMatrixThreshold, settings.ImageMatrixInvert, out var matrix))
            {
                shape["ImageMatrix"] = matrix;
                // Matrix payload is sufficient for device rendering; avoid huge source payloads.
                shape.Remove("SourcePath");
            }
        }

        return root.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    private static bool TryBuildImageMatrix(string sourcePath, int targetWidth, int targetHeight, int threshold, bool invert, out JsonObject matrix)
    {
        matrix = new JsonObject();

        try
        {
            using var bitmap = LoadBitmap(sourcePath);
            if (bitmap is null)
                return false;

            var info = new SKImageInfo(targetWidth, targetHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var resized = bitmap.Resize(info, SKFilterQuality.Medium);
            var src = resized ?? bitmap;

            var packed = PackMonochrome(src, threshold, invert);
            matrix["Width"] = src.Width;
            matrix["Height"] = src.Height;
            matrix["Bpp"] = 1;
            matrix["BlackIsOne"] = true;
            matrix["Data"] = Convert.ToBase64String(packed);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static SKBitmap? LoadBitmap(string sourcePath)
    {
        if (sourcePath.StartsWith(DataUriPrefix, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseDataUri(sourcePath, out _, out var bytes))
                return null;

            return SKBitmap.Decode(bytes);
        }

        if (!File.Exists(sourcePath))
            return null;

        return SKBitmap.Decode(sourcePath);
    }

    private static byte[] PackMonochrome(SKBitmap bitmap, int threshold, bool invert)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var bytes = new byte[(width * height + 7) / 8];
        var bitIndex = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var c = bitmap.GetPixel(x, y);
                var luminance = (0.2126 * c.Red) + (0.7152 * c.Green) + (0.0722 * c.Blue);
                var black = luminance < threshold;
                if (invert)
                    black = !black;

                if (black)
                    bytes[bitIndex / 8] |= (byte)(1 << (7 - (bitIndex % 8)));

                bitIndex++;
            }
        }

        return bytes;
    }

    private static bool TryParseDataUri(string value, out string? mimeType, out byte[] bytes)
    {
        mimeType = null;
        bytes = [];

        if (!value.StartsWith(DataUriPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var commaIndex = value.IndexOf(',');
        if (commaIndex <= 5 || commaIndex >= value.Length - 1)
            return false;

        var metadata = value[5..commaIndex];
        var payload = value[(commaIndex + 1)..];
        if (!metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase))
            return false;

        var semicolonIndex = metadata.IndexOf(';');
        mimeType = semicolonIndex > 0 ? metadata[..semicolonIndex] : "application/octet-stream";

        try
        {
            bytes = Convert.FromBase64String(payload);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
