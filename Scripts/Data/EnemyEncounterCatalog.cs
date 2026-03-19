using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

public sealed class EnemyEncounterCatalog
{
    public sealed record EnemyArchetype(string Id, string DisplayName, string VisualId, string NameTemplate, int BaseHp, int HpPerFloor);
    public sealed record EncounterMember(string ArchetypeId, string Suffix, int MinFloor, StrengthFormula Strength);
    public sealed record StrengthFormula(int BaseValue, int FloorMultiplier, int FloorDivisor, int FloorOffset, int MinValue);

    public IReadOnlyDictionary<string, EnemyArchetype> ArchetypesById { get; }
    public IReadOnlyDictionary<MapNodeType, List<EncounterMember>> EncounterMembersByType { get; }

    private EnemyEncounterCatalog(
        Dictionary<string, EnemyArchetype> archetypesById,
        Dictionary<MapNodeType, List<EncounterMember>> encounterMembersByType)
    {
        ArchetypesById = archetypesById;
        EncounterMembersByType = encounterMembersByType;
    }

    public static EnemyEncounterCatalog Load()
    {
        var (json, source) = ReadEnemiesJson();
        var dto = JsonSerializer.Deserialize<EnemyCatalogDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (dto == null || dto.Archetypes == null || dto.Archetypes.Count == 0)
        {
            throw new InvalidOperationException($"Invalid enemy catalog JSON: {source}");
        }

        var archetypes = new Dictionary<string, EnemyArchetype>();
        foreach (var archetypeDto in dto.Archetypes)
        {
            if (string.IsNullOrWhiteSpace(archetypeDto.Id))
            {
                throw new InvalidOperationException("Enemy archetype id is required in enemies.json.");
            }

            var displayName = string.IsNullOrWhiteSpace(archetypeDto.DisplayName) ? archetypeDto.Id : archetypeDto.DisplayName;
            var visualId = string.IsNullOrWhiteSpace(archetypeDto.VisualId) ? archetypeDto.Id : archetypeDto.VisualId;
            var nameTemplate = string.IsNullOrWhiteSpace(archetypeDto.NameTemplate) ? "{displayName} {suffix}" : archetypeDto.NameTemplate;

            archetypes[archetypeDto.Id] = new EnemyArchetype(
                archetypeDto.Id,
                displayName,
                visualId,
                nameTemplate,
                archetypeDto.BaseHp,
                archetypeDto.HpPerFloor);
        }

        var membersByType = new Dictionary<MapNodeType, List<EncounterMember>>();
        foreach (var kv in dto.Encounters ?? new Dictionary<string, List<EncounterMemberDto>>())
        {
            var type = ParseEnum<MapNodeType>(kv.Key, "encounter type");
            var members = new List<EncounterMember>();
            foreach (var memberDto in kv.Value ?? new List<EncounterMemberDto>())
            {
                var formulaDto = memberDto.Strength ?? new StrengthFormulaDto();
                members.Add(new EncounterMember(
                    memberDto.ArchetypeId ?? string.Empty,
                    memberDto.Suffix ?? string.Empty,
                    memberDto.MinFloor,
                    new StrengthFormula(
                        formulaDto.BaseValue,
                        formulaDto.FloorMultiplier == 0 ? 1 : formulaDto.FloorMultiplier,
                        formulaDto.FloorDivisor <= 0 ? 1 : formulaDto.FloorDivisor,
                        formulaDto.FloorOffset,
                        formulaDto.MinValue)));
            }

            membersByType[type] = members;
        }

        return new EnemyEncounterCatalog(archetypes, membersByType);
    }

    public int ResolveStrength(StrengthFormula formula, int floor)
    {
        var scaledFloor = floor + formula.FloorOffset;
        var scaled = (scaledFloor * formula.FloorMultiplier) / formula.FloorDivisor;
        var raw = formula.BaseValue + scaled;
        return Math.Max(raw, formula.MinValue);
    }

    public string BuildName(EnemyArchetype archetype, string suffix)
    {
        return archetype.NameTemplate
            .Replace("{displayName}", archetype.DisplayName)
            .Replace("{suffix}", suffix);
    }

    private static (string Json, string Source) ReadEnemiesJson()
    {
        var candidates = new List<string>();
        var envPath = Environment.GetEnvironmentVariable("SLAY_THE_HS_ENEMIES_JSON");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            candidates.Add(envPath);
        }

        candidates.AddRange(EnumerateCandidates(AppContext.BaseDirectory));
        candidates.AddRange(EnumerateCandidates(Directory.GetCurrentDirectory()));
        candidates.AddRange(EnumerateTestProjectCandidates(AppContext.BaseDirectory));
        candidates.AddRange(EnumerateTestProjectCandidates(Directory.GetCurrentDirectory()));
        candidates.AddRange(EnumerateExportDataCandidates(AppContext.BaseDirectory));
        candidates.AddRange(EnumerateExportDataCandidates(Directory.GetCurrentDirectory()));

        if (GameDataAccess.TryReadText(candidates, new[] { "res://Data/enemies.json" }, out var json, out var source))
        {
            return (json, source);
        }

        throw new FileNotFoundException("Cannot locate Data/enemies.json for enemy encounter loading.");
    }

    private static IEnumerable<string> EnumerateCandidates(string startDir)
    {
        var current = new DirectoryInfo(startDir);
        for (var i = 0; i < 8 && current != null; i++)
        {
            yield return Path.Combine(current.FullName, "Data", "enemies.json");
            current = current.Parent;
        }
    }



    private static IEnumerable<string> EnumerateTestProjectCandidates(string startDir)
    {
        var current = new DirectoryInfo(startDir);
        for (var i = 0; i < 8 && current != null; i++)
        {
            yield return Path.Combine(current.FullName, "Tests", "CombatLogicTests", "Data", "enemies.json");
            current = current.Parent;
        }
    }

    private static IEnumerable<string> EnumerateExportDataCandidates(string startDir)
    {
        if (string.IsNullOrWhiteSpace(startDir) || !Directory.Exists(startDir))
        {
            yield break;
        }

        var baseDir = new DirectoryInfo(startDir);
        var roots = new List<DirectoryInfo> { baseDir };
        if (baseDir.Parent != null)
        {
            roots.Add(baseDir.Parent);
        }

        foreach (var root in roots)
        {
            foreach (var dir in root.EnumerateDirectories())
            {
                if (!dir.Name.StartsWith("data_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return Path.Combine(dir.FullName, "Data", "enemies.json");
            }
        }
    }
    private static T ParseEnum<T>(string? raw, string label) where T : struct
    {
        if (!string.IsNullOrWhiteSpace(raw) && Enum.TryParse<T>(raw, true, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Invalid {label}: '{raw}'.");
    }

    private sealed class EnemyCatalogDto
    {
        public List<EnemyArchetypeDto>? Archetypes { get; set; }
        public Dictionary<string, List<EncounterMemberDto>>? Encounters { get; set; }
    }

    private sealed class EnemyArchetypeDto
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string VisualId { get; set; } = string.Empty;
        public string NameTemplate { get; set; } = string.Empty;
        public int BaseHp { get; set; }
        public int HpPerFloor { get; set; }
    }

    private sealed class EncounterMemberDto
    {
        public string ArchetypeId { get; set; } = string.Empty;
        public string Suffix { get; set; } = string.Empty;
        public int MinFloor { get; set; }
        public StrengthFormulaDto? Strength { get; set; }
    }

    private sealed class StrengthFormulaDto
    {
        public int BaseValue { get; set; }
        public int FloorMultiplier { get; set; } = 1;
        public int FloorDivisor { get; set; } = 1;
        public int FloorOffset { get; set; }
        public int MinValue { get; set; }
    }
}
