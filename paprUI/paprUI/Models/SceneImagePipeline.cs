using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using rUI.Drawing.Core.Scene;

namespace paprUI.Models;

/// <summary>
/// Converts scene image paths to embedded data URIs for persistence/transport,
/// and materializes embedded data URIs back to local files for canvas rendering.
/// </summary>
public sealed class SceneImagePipeline
{
    private const string DataUriPrefix = "data:";
    private readonly string _materializedImageDirectory;
    private readonly ILogger<SceneImagePipeline> _logger;

    public SceneImagePipeline(ILogger<SceneImagePipeline> logger)
    {
        _logger = logger;
        _materializedImageDirectory = Path.Combine(Path.GetTempPath(), "paprUI", "embedded-images");
        Directory.CreateDirectory(_materializedImageDirectory);
    }

    public SceneDocument EmbedImages(SceneDocument scene)
    {
        var mappedShapes = new List<SceneShapeDto>(scene.Shapes.Count);
        foreach (var shape in scene.Shapes)
        {
            if (!string.Equals(shape.Kind, "Image", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(shape.SourcePath))
            {
                mappedShapes.Add(shape);
                continue;
            }

            var sourcePath = shape.SourcePath!;
            if (IsDataUri(sourcePath))
            {
                mappedShapes.Add(shape);
                continue;
            }

            if (!File.Exists(sourcePath))
            {
                mappedShapes.Add(shape);
                continue;
            }

            try
            {
                var bytes = File.ReadAllBytes(sourcePath);
                var mimeType = GuessMimeTypeFromPath(sourcePath);
                var dataUri = $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
                mappedShapes.Add(shape with { SourcePath = dataUri });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to embed image from path {Path}", sourcePath);
                mappedShapes.Add(shape);
            }
        }

        return CloneSceneWithShapes(scene, mappedShapes);
    }

    public SceneDocument MaterializeEmbeddedImages(SceneDocument scene)
    {
        var mappedShapes = new List<SceneShapeDto>(scene.Shapes.Count);
        foreach (var shape in scene.Shapes)
        {
            if (!string.Equals(shape.Kind, "Image", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(shape.SourcePath))
            {
                mappedShapes.Add(shape);
                continue;
            }

            var source = shape.SourcePath!;
            if (!TryParseDataUri(source, out var mimeType, out var bytes))
            {
                mappedShapes.Add(shape);
                continue;
            }

            try
            {
                var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                var extension = ExtensionFromMimeType(mimeType);
                var filePath = Path.Combine(_materializedImageDirectory, $"{hash}{extension}");

                if (!File.Exists(filePath))
                    File.WriteAllBytes(filePath, bytes);

                mappedShapes.Add(shape with { SourcePath = filePath });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to materialize embedded image.");
                mappedShapes.Add(shape);
            }
        }

        return CloneSceneWithShapes(scene, mappedShapes);
    }

    private static SceneDocument CloneSceneWithShapes(SceneDocument source, List<SceneShapeDto> shapes)
    {
        return new SceneDocument
        {
            Version = source.Version,
            CanvasBackgroundColor = source.CanvasBackgroundColor,
            ShowCanvasBoundary = source.ShowCanvasBoundary,
            CanvasBoundaryWidth = source.CanvasBoundaryWidth,
            CanvasBoundaryHeight = source.CanvasBoundaryHeight,
            Shapes = shapes
        };
    }

    private static bool IsDataUri(string value)
        => value.StartsWith(DataUriPrefix, StringComparison.OrdinalIgnoreCase);

    private static string GuessMimeTypeFromPath(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    private static string ExtensionFromMimeType(string? mimeType)
    {
        return mimeType?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/bmp" => ".bmp",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            _ => ".bin"
        };
    }

    private static bool TryParseDataUri(string value, out string? mimeType, out byte[] bytes)
    {
        mimeType = null;
        bytes = [];

        if (!IsDataUri(value))
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
