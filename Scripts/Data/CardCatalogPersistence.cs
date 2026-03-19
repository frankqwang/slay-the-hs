using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public sealed class CardCatalogData
{
    public List<CardEntryData> Cards { get; set; } = new();
    public List<string> StarterDeck { get; set; } = new();
    public List<string> RewardPool { get; set; } = new();
}

public sealed class CardEntryData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameKey { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? DescriptionZh { get; set; }
    public string? DescriptionKey { get; set; }
    public string? ArtPath { get; set; }
    public string Kind { get; set; } = string.Empty;
    public int Cost { get; set; }
    public List<CardEffectEntryData> Effects { get; set; } = new();
}

public sealed class CardEffectEntryData
{
    public string Type { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public int Amount { get; set; }
    public int Repeat { get; set; } = 1;
    public bool UseAttackerStrength { get; set; } = true;
    public bool UseTargetVulnerable { get; set; } = true;
    public int FlatBonus { get; set; }
}

public static class CardCatalogPersistence
{
    private const string CardsResourcePath = "res://Data/cards.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string ResolveCardsJsonPath()
    {
        if (GameDataAccess.TryReadResourceText(CardsResourcePath, out _))
        {
            return CardsResourcePath;
        }

        var candidates = new List<string>();

        var envPath = Environment.GetEnvironmentVariable("SLAY_THE_HS_CARDS_JSON");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            candidates.Add(envPath);
        }

        candidates.AddRange(EnumerateCardsJsonCandidates(AppContext.BaseDirectory));
        candidates.AddRange(EnumerateCardsJsonCandidates(Directory.GetCurrentDirectory()));

        foreach (var path in candidates.Distinct())
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException("Cannot locate Data/cards.json for card catalog loading.");
    }

    public static CardCatalogData LoadFromFile(string path)
    {
        var json = path.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
            ? GameDataAccess.ReadResourceText(path)
            : File.ReadAllText(path);
        return LoadFromJson(json, path);
    }

    public static CardCatalogData LoadFromJson(string json, string sourceLabel = "cards.json")
    {
        var dto = JsonSerializer.Deserialize<CardCatalogData>(json, SerializerOptions);
        if (dto == null)
        {
            throw new InvalidOperationException($"Invalid card catalog JSON: {sourceLabel}");
        }

        dto.Cards ??= new List<CardEntryData>();
        dto.StarterDeck ??= new List<string>();
        dto.RewardPool ??= new List<string>();

        ValidateOrThrow(dto, sourceLabel);
        return dto;
    }

    public static void SaveToFile(string path, CardCatalogData catalog)
    {
        ValidateOrThrow(catalog, path);
        var json = JsonSerializer.Serialize(catalog, SerializerOptions);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{path}.tmp";
        File.WriteAllText(tempPath, json + Environment.NewLine);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(tempPath, path);
    }

    public static void ValidateOrThrow(CardCatalogData catalog, string sourceLabel)
    {
        var errors = Validate(catalog);
        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException($"Card catalog validation failed ({sourceLabel}): {string.Join(" | ", errors)}");
    }

    public static List<string> Validate(CardCatalogData catalog)
    {
        var errors = new List<string>();
        if (catalog.Cards.Count == 0)
        {
            errors.Add("cards cannot be empty");
            return errors;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var card in catalog.Cards)
        {
            if (string.IsNullOrWhiteSpace(card.Id))
            {
                errors.Add("card id is required");
                continue;
            }

            if (!ids.Add(card.Id))
            {
                errors.Add($"duplicate card id: {card.Id}");
            }

            if (!Enum.TryParse<CardKind>(card.Kind, ignoreCase: true, out _))
            {
                errors.Add($"invalid card kind for {card.Id}: {card.Kind}");
            }

            foreach (var effect in card.Effects ?? new List<CardEffectEntryData>())
            {
                if (!Enum.TryParse<CardEffectType>(effect.Type, ignoreCase: true, out _))
                {
                    errors.Add($"invalid effect type for {card.Id}: {effect.Type}");
                }

                if (!Enum.TryParse<CardEffectTarget>(effect.Target, ignoreCase: true, out _))
                {
                    errors.Add($"invalid effect target for {card.Id}: {effect.Target}");
                }

                if (effect.Repeat < 1)
                {
                    errors.Add($"effect repeat must be >= 1 for {card.Id}");
                }
            }
        }

        if (!ids.Contains("strike"))
        {
            errors.Add("required fallback card id missing: strike");
        }

        foreach (var starter in catalog.StarterDeck)
        {
            if (!ids.Contains(starter))
            {
                errors.Add($"starterDeck references unknown id: {starter}");
            }
        }

        foreach (var reward in catalog.RewardPool)
        {
            if (!ids.Contains(reward))
            {
                errors.Add($"rewardPool references unknown id: {reward}");
            }
        }

        return errors;
    }

    private static IEnumerable<string> EnumerateCardsJsonCandidates(string startDir)
    {
        var current = new DirectoryInfo(startDir);
        for (var i = 0; i < 8 && current != null; i++)
        {
            yield return Path.Combine(current.FullName, "Data", "cards.json");
            current = current.Parent;
        }
    }
}
