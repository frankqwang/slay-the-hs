using System;
using System.Collections.Generic;

public static class EnemyEncounterBuilder
{
    private static readonly EnemyEncounterCatalog Catalog = EnemyEncounterCatalog.Load();

    public static List<EnemyUnit> BuildEncounter(MapNodeType encounterType, int floor)
    {
        if (!Catalog.EncounterMembersByType.TryGetValue(encounterType, out var members))
        {
            throw new InvalidOperationException($"No encounter rule configured for encounterType={encounterType}");
        }

        var enemies = new List<EnemyUnit>();
        foreach (var member in members)
        {
            if (floor < member.MinFloor)
            {
                continue;
            }

            enemies.Add(Create(member, floor));
        }

        return enemies;
    }

    private static EnemyUnit Create(EnemyEncounterCatalog.EncounterMember member, int floor)
    {
        if (!Catalog.ArchetypesById.TryGetValue(member.ArchetypeId, out var archetype))
        {
            throw new InvalidOperationException($"Unknown enemy archetype: {member.ArchetypeId}");
        }

        var hp = archetype.BaseHp + floor * archetype.HpPerFloor;
        return new EnemyUnit
        {
            ArchetypeId = archetype.Id,
            Name = Catalog.BuildName(archetype, member.Suffix),
            VisualId = archetype.VisualId,
            Hp = hp,
            MaxHp = hp,
            Strength = Catalog.ResolveStrength(member.Strength, floor)
        };
    }
}
