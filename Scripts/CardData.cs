using System.Collections.Generic;
using System.Linq;

public enum CardKind
{
    Attack,
    Skill
}

public sealed class CardData
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public CardKind Kind { get; }
    public int Cost { get; }
    public int Damage { get; }
    public int Block { get; }
    public int ApplyVulnerable { get; }
    public int DrawCount { get; }
    public IReadOnlyList<CardEffectSpec> Effects { get; }

    public CardData(
        string id,
        string name,
        string description,
        CardKind kind,
        int cost,
        int damage,
        int block,
        int applyVulnerable,
        int drawCount,
        IReadOnlyList<CardEffectSpec>? effects = null)
    {
        Id = id;
        Name = name;
        Description = description;
        Kind = kind;
        Cost = cost;
        Damage = damage;
        Block = block;
        ApplyVulnerable = applyVulnerable;
        DrawCount = drawCount;
        Effects = effects ?? new List<CardEffectSpec>();
    }

    public string ToCardText()
    {
        return $"{Name}\\nCost: {Cost}\\n{Description}";
    }

    public static CardData CreateById(string id)
    {
        var definition = CardCatalog.GetDefinition(id);
        var effects = definition.Effects.ToList();

        var damage = SumEffectValue(effects, CardEffectType.DealDamage);
        var block = SumEffectValue(effects, CardEffectType.GainBlock);
        var vulnerable = SumEffectValue(effects, CardEffectType.ApplyVulnerable);
        var draw = SumEffectValue(effects, CardEffectType.DrawCards);

        return new CardData(
            definition.Id,
            definition.Name,
            definition.Description,
            definition.Kind,
            definition.Cost,
            damage,
            block,
            vulnerable,
            draw,
            effects);
    }

    public static List<string> StarterDeckIds()
    {
        return CardCatalog.StarterDeckIds();
    }

    public static List<string> RewardPoolIds()
    {
        return CardCatalog.RewardPoolIds();
    }

    private static int SumEffectValue(IEnumerable<CardEffectSpec> effects, CardEffectType type)
    {
        var total = 0;
        foreach (var effect in effects)
        {
            if (effect.Type == type)
            {
                total += effect.Value;
            }
        }

        return total;
    }
}
