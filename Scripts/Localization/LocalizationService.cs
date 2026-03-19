using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public enum GameLanguage
{
    En,
    ZhHans
}

public static class LocalizationService
{
    private static readonly Dictionary<string, string> _en = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> _zhHans = new(StringComparer.OrdinalIgnoreCase);
    private static bool _initialized;

    public static IReadOnlyDictionary<string, string> DebugEnglish => _en;
    public static IReadOnlyDictionary<string, string> DebugChinese => _zhHans;

    public static string Get(string key, string fallback = "")
    {
        if (!_initialized)
        {
            Load();
        }

        var lang = LocalizationSettings.CurrentLanguage;
        var table = lang == GameLanguage.ZhHans ? _zhHans : _en;
        if (table.TryGetValue(key, out var value))
        {
            return value;
        }

        if (lang == GameLanguage.ZhHans && _en.TryGetValue(key, out var enFallback))
        {
            return enFallback;
        }

        return fallback;
    }

    public static string Format(string key, string fallback, params object[] args)
    {
        return string.Format(Get(key, fallback), args);
    }

    public static void Load()
    {
        if (_initialized)
        {
            return;
        }

        LoadLanguage(GameLanguage.En, "en.json", _en);
        LoadLanguage(GameLanguage.ZhHans, "zh_hans.json", _zhHans);
        _initialized = true;
    }

    public static void Reload()
    {
        _en.Clear();
        _zhHans.Clear();
        _initialized = false;
        Load();
    }

    private static void LoadLanguage(GameLanguage language, string fileName, Dictionary<string, string> target)
    {
        var resourcePath = $"res://Data/Localization/{fileName}";
        if (!GameDataAccess.TryReadResourceText(resourcePath, out var json))
        {
            return;
        }

        Dictionary<string, string>? dict;
        try
        {
            dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch
        {
            return;
        }
        if (dict == null)
        {
            return;
        }

        foreach (var kv in dict)
        {
            target[kv.Key] = kv.Value;
        }

        GD.Print($"Loaded {language}: {dict.Count} entries from {resourcePath}");
    }

    public static void EnsureLoaded()
    {
        if (!_initialized)
        {
            Load();
        }
    }
}
