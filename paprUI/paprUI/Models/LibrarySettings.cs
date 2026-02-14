using System;
using System.IO;

namespace paprUI.Models;

public class LibrarySettings
{
    public LibrarySettings()
    {
        var envPath = Environment.GetEnvironmentVariable("PAPR_LIBRARY_PATH");
        LibraryPath = string.IsNullOrWhiteSpace(envPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "papr", "library")
            : envPath;

        EnsureLibraryDirectory();
    }

    public string LibraryPath { get; private set; }
    public int ImageMatrixMaxWidth { get; private set; } = 200;
    public int ImageMatrixMaxHeight { get; private set; } = 200;
    public int ImageMatrixThreshold { get; private set; } = 160;
    public bool ImageMatrixInvert { get; private set; }

    public void SetLibraryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        LibraryPath = path.Trim();
        EnsureLibraryDirectory();
    }

    public void EnsureLibraryDirectory()
    {
        Directory.CreateDirectory(LibraryPath);
    }

    public void SetImageMatrixOptions(int maxWidth, int maxHeight, int threshold, bool invert)
    {
        ImageMatrixMaxWidth = Math.Clamp(maxWidth, 8, 960);
        ImageMatrixMaxHeight = Math.Clamp(maxHeight, 8, 540);
        ImageMatrixThreshold = Math.Clamp(threshold, 0, 255);
        ImageMatrixInvert = invert;
    }
}
