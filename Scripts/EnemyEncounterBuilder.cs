using System;
using System.Collections.Generic;

public static class EnemyEncounterBuilder
{
    public static List<EnemyUnit> BuildEncounter(MapNodeType encounterType, int floor)
    {
        var enemies = new List<EnemyUnit>();
        var rules = EnemyCatalog.GetEncounterRules(encounterType);
        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (!rule.Condition(floor))
            {
                continue;
            }

            enemies.Add(Create(rule.ArchetypeId, floor, rule.Suffix));
        }

        if (enemies.Count == 0)
        {
            enemies.Add(Create("cultist", floor, "A"));
        }

        return enemies;
    }

    private static EnemyUnit Create(string archetypeId, int floor, string suffix)
    {
        var archetype = EnemyCatalog.GetArchetype(archetypeId);

        var hp = archetype.BaseHp + floor * archetype.HpPerFloor;
        return new EnemyUnit
        {
            Name = $"{archetype.DisplayName} {suffix}",
            VisualId = archetype.VisualId,
            Hp = hp,
            MaxHp = hp,
            Strength = archetype.StrengthByFloor(floor)
        };
    }
}
