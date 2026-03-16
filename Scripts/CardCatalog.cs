using System;
using System.Collections.Generic;
using System.Linq;

public static class CardCatalog
{
    public sealed record CardDefinition(
        string Id,
        string Name,
        string Description,
        CardKind Kind,
        int Cost,
        IReadOnlyList<CardEffectSpec> Effects);

    private static readonly Dictionary<string, CardDefinition> DefinitionsById = BuildDefinitionsById();

    private static readonly List<string> StarterDeck = new()
    {
        "strike", "defend",
        "strike", "defend",
        "strike", "defend",
        "strike", "defend",
        "strike", "defend",
        "bash"
    };

    private static readonly List<string> RewardPool = new()
    {
        "heavy_slash",
        "shrug",
        "quick_slash",
        "bash",
        "strike",
        "defend"
    };

    public static CardDefinition GetDefinition(string id)
    {
        if (DefinitionsById.TryGetValue(id, out var definition))
        {
            return definition;
        }

        return DefinitionsById["strike"];
    }

    public static List<string> StarterDeckIds() => new(StarterDeck);

    public static List<string> RewardPoolIds() => new(RewardPool);

    public static IReadOnlyCollection<CardDefinition> AllDefinitions => DefinitionsById.Values;

    private static Dictionary<string, CardDefinition> BuildDefinitionsById()
    {
        var definitions = new[]
        {
            new CardDefinition(
                "strike",
                "Strike",
                "Deal 6 damage.",
                CardKind.Attack,
                1,
                new[] { new CardEffectSpec(CardEffectType.DealDamage, 6) }),
            new CardDefinition(
                "defend",
                "Defend",
                "Gain 5 Block.",
                CardKind.Skill,
                1,
                new[] { new CardEffectSpec(CardEffectType.GainBlock, 5) }),
            new CardDefinition(
                "heavy_slash",
                "Heavy Slash",
                "Deal 12 damage.",
                CardKind.Attack,
                2,
                new[] { new CardEffectSpec(CardEffectType.DealDamage, 12) }),
            new CardDefinition(
                "bash",
                "Bash",
                "Deal 8 damage. Apply 2 Vulnerable.",
                CardKind.Attack,
                2,
                new[]
                {
                    new CardEffectSpec(CardEffectType.DealDamage, 8),
                    new CardEffectSpec(CardEffectType.ApplyVulnerable, 2)
                }),
            new CardDefinition(
                "shrug",
                "Shrug It Off",
                "Gain 8 Block. Draw 1 card.",
                CardKind.Skill,
                1,
                new[]
                {
                    new CardEffectSpec(CardEffectType.GainBlock, 8),
                    new CardEffectSpec(CardEffectType.DrawCards, 1)
                }),
            new CardDefinition(
                "quick_slash",
                "Quick Slash",
                "Deal 7 damage. Draw 1 card.",
                CardKind.Attack,
                1,
                new[]
                {
                    new CardEffectSpec(CardEffectType.DealDamage, 7),
                    new CardEffectSpec(CardEffectType.DrawCards, 1)
                })
        };

        return definitions.ToDictionary(d => d.Id, d => d, StringComparer.Ordinal);
    }
}
