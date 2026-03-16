using System;
using System.Collections.Generic;

public static class EnemyCatalog
{
    public sealed record EnemyArchetype(
        string Id,
        string DisplayName,
        string VisualId,
        int BaseHp,
        int HpPerFloor,
        Func<int, int> StrengthByFloor);

    public sealed record EnemySpawnRule(string ArchetypeId, string Suffix, Func<int, bool> Condition);

    private static readonly Dictionary<string, EnemyArchetype> Archetypes = new()
    {
        ["cultist"] = new EnemyArchetype(
            "cultist",
            "Cultist",
            "cultist",
            36,
            7,
            floor => Math.Max(floor - 1, 0)),
        ["cultist_scout"] = new EnemyArchetype(
            "cultist_scout",
            "Cultist",
            "cultist",
            32,
            6,
            floor => Math.Max(floor - 1, 0)),
        ["elite_sentinel"] = new EnemyArchetype(
            "elite_sentinel",
            "Elite Sentinel",
            "elite_sentinel",
            78,
            10,
            floor => Math.Max(floor / 2, 1))
    };

    private static readonly Dictionary<MapNodeType, List<EnemySpawnRule>> EncounterRules = new()
    {
        [MapNodeType.NormalBattle] = new List<EnemySpawnRule>
        {
            new("cultist", "A", _ => true),
            new("cultist", "B", _ => true),
            new("cultist_scout", "C", floor => floor >= 3)
        },
        [MapNodeType.EliteBattle] = new List<EnemySpawnRule>
        {
            new("elite_sentinel", "A", _ => true),
            new("elite_sentinel", "B", _ => true)
        }
    };

    public static EnemyArchetype GetArchetype(string id)
    {
        if (!Archetypes.TryGetValue(id, out var archetype))
        {
            throw new InvalidOperationException($"Unknown enemy archetype: {id}");
        }

        return archetype;
    }

    public static List<EnemySpawnRule> GetEncounterRules(MapNodeType encounterType)
    {
        if (EncounterRules.TryGetValue(encounterType, out var rules))
        {
            return rules;
        }

        return EncounterRules[MapNodeType.NormalBattle];
    }
}
