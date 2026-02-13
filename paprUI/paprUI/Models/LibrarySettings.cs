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
}
