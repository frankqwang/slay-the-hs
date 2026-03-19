using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public sealed class RelicData
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string Rarity { get; }
    public string Archetype { get; }

    public RelicData(string id, string name, string description, string rarity = "Common", string archetype = "General")
    {
        Id = id;
        Name = name;
        Description = description;
        Rarity = rarity;
        Archetype = archetype;
    }

    public string ToRelicText()
    {
        return $"{LocalizedName}\n{LocalizedDescription}";
    }

    public string LocalizedName => LocalizationService.Get($"relic.{Id}.name", Name);

    public string LocalizedDescription => LocalizationService.Get($"relic.{Id}.description", Description);

    public string LocalizedArchetype => LocalizationService.Get(
        $"relic.archetype.{Archetype.ToLowerInvariant().Replace(' ', '_').Replace('-', '_')}",
        Archetype);

    public static IReadOnlyList<string> AllRelicIds()
    {
        return Catalog.AllIds;
    }

    public static RelicData CreateById(string id)
    {
        if (Catalog.ById.TryGetValue(id, out var relic))
        {
            return relic;
        }

        return Catalog.Fallback;
    }

    public static IReadOnlyDictionary<string, List<RelicData>> GroupByRarity()
    {
        var grouped = new Dictionary<string, List<RelicData>>(StringComparer.OrdinalIgnoreCase);
        foreach (var relicId in Catalog.AllIds)
        {
            var relic = Catalog.ById[relicId];
            if (!grouped.TryGetValue(relic.Rarity, out var bucket))
            {
                bucket = new List<RelicData>();
                grouped[relic.Rarity] = bucket;
            }

            bucket.Add(relic);
        }

        return grouped;
    }

    private static class Catalog
    {
        public static readonly RelicData Fallback = new("lantern", "Lantern", "Gain +1 Energy on turn 1.", "Starter", "Tempo");
        public static readonly Dictionary<string, RelicData> ById = LoadRelics();
        public static readonly IReadOnlyList<string> AllIds = ById.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList();

        private static Dictionary<string, RelicData> LoadRelics()
        {
            var parsed = TryLoadConfiguredRelics();
            if (parsed.Count > 0)
            {
                return parsed;
            }

            return BuildFallbackRelics();
        }

        private static Dictionary<string, RelicData> TryLoadConfiguredRelics()
        {
            try
            {
                var (json, _) = ReadRelicsJson();
                var dto = JsonSerializer.Deserialize<RelicCatalogDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var result = new Dictionary<string, RelicData>();
                foreach (var entry in dto?.Relics ?? new List<RelicEntryDto>())
                {
                    if (string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Name))
                    {
                        continue;
                    }

                    result[entry.Id] = new RelicData(
                        entry.Id,
                        entry.Name,
                        string.IsNullOrWhiteSpace(entry.Description) ? "No description." : entry.Description,
                        string.IsNullOrWhiteSpace(entry.Rarity) ? "Common" : entry.Rarity,
                        string.IsNullOrWhiteSpace(entry.Archetype) ? "General" : entry.Archetype);
                }

                return result;
            }
            catch
            {
                return new Dictionary<string, RelicData>();
            }
        }

        private static Dictionary<string, RelicData> BuildFallbackRelics()
        {
            var relics = new[]
            {
                new RelicData("lantern", "Lantern", "Gain +1 Energy on turn 1.", "Starter", "Tempo"),
                new RelicData("anchor", "Anchor", "Gain 8 Block on turn 1.", "Starter", "Block"),
                new RelicData("whetstone", "Whetstone", "Your attacks deal +1 damage.", "Common", "Strike"),
                new RelicData("charm", "Lucky Charm", "Heal 5 HP after each battle.", "Common", "Sustain"),
                new RelicData("ember_ring", "Ember Ring", "Gain +1 Energy at the start of every turn.", "Rare", "Combo"),
                new RelicData("iron_shell", "Iron Shell", "Gain 3 Block at the start of every turn.", "Rare", "Block"),
                new RelicData("blood_vial", "Blood Vial", "Heal 2 HP after each battle.", "Uncommon", "Sustain")
            };

            return relics.ToDictionary(r => r.Id, r => r, StringComparer.Ordinal);
        }

        private static (string Json, string Source) ReadRelicsJson()
        {
            var candidates = new List<string>();
            var envPath = Environment.GetEnvironmentVariable("SLAY_THE_HS_RELICS_JSON");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                candidates.Add(envPath);
            }

            candidates.AddRange(EnumerateCandidates(AppContext.BaseDirectory));
            candidates.AddRange(EnumerateCandidates(Directory.GetCurrentDirectory()));
            candidates.AddRange(EnumerateTestProjectCandidates(AppContext.BaseDirectory));
            candidates.AddRange(EnumerateTestProjectCandidates(Directory.GetCurrentDirectory()));

            if (GameDataAccess.TryReadText(candidates, new[] { "res://Data/relics.json" }, out var json, out var source))
            {
                return (json, source);
            }

            throw new FileNotFoundException("Cannot locate Data/relics.json for relic catalog loading.");
        }

        private static IEnumerable<string> EnumerateCandidates(string startDir)
        {
            var current = new DirectoryInfo(startDir);
            for (var i = 0; i < 8 && current != null; i++)
            {
                yield return Path.Combine(current.FullName, "Data", "relics.json");
                current = current.Parent;
            }
        }

        private static IEnumerable<string> EnumerateTestProjectCandidates(string startDir)
        {
            var current = new DirectoryInfo(startDir);
            for (var i = 0; i < 8 && current != null; i++)
            {
                yield return Path.Combine(current.FullName, "Tests", "CombatLogicTests", "Data", "relics.json");
                current = current.Parent;
            }
        }
    }

    private sealed class RelicCatalogDto
    {
        public List<RelicEntryDto> Relics { get; set; } = new();
    }

    private sealed class RelicEntryDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Rarity { get; set; } = string.Empty;
        public string Archetype { get; set; } = string.Empty;
    }
}
