using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public static class GameDataAccess
{
    public static bool TryReadResourceText(string resourcePath, out string text)
    {
        text = string.Empty;
        var normalized = NormalizeResourcePath(resourcePath);
        if (!Godot.FileAccess.FileExists(normalized))
        {
            return false;
        }

        using var file = Godot.FileAccess.Open(normalized, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            return false;
        }

        text = file.GetAsText();
        return true;
    }

    public static string ReadResourceText(string resourcePath)
    {
        if (TryReadResourceText(resourcePath, out var text))
        {
            return text;
        }

        throw new FileNotFoundException($"Cannot locate resource text file: {NormalizeResourcePath(resourcePath)}");
    }

    public static bool TryReadText(IEnumerable<string> filesystemCandidates, IEnumerable<string>? resourceCandidates, out string text, out string source)
    {
        foreach (var resourcePath in resourceCandidates ?? Enumerable.Empty<string>())
        {
            if (TryReadResourceText(resourcePath, out text))
            {
                source = NormalizeResourcePath(resourcePath);
                return true;
            }
        }

        foreach (var path in filesystemCandidates.Distinct())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            text = File.ReadAllText(path, Encoding.UTF8);
            source = path;
            return true;
        }

        text = string.Empty;
        source = string.Empty;
        return false;
    }

    public static string NormalizeResourcePath(string resourcePath)
    {
        if (resourcePath.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
            || resourcePath.StartsWith("user://", StringComparison.OrdinalIgnoreCase))
        {
            return resourcePath.Replace('\\', '/');
        }

        return $"res://{resourcePath.TrimStart('/', '\\').Replace('\\', '/')}";
    }
}
