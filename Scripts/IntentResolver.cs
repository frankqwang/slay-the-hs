using System;
using System.Collections.Generic;

public readonly struct EnemyIntentRoll
{
    public EnemyIntentType Type { get; }
    public int Value { get; }

    public EnemyIntentRoll(EnemyIntentType type, int value)
    {
        Type = type;
        Value = value;
    }
}

public static class IntentResolver
{
    public static EnemyIntentRoll RollEnemyIntent(EnemyUnit enemy, IReadOnlyList<EnemyUnit> encounter, bool isElite, int turn, Random rng)
    {
        var alliesAlive = 0;
        for (var i = 0; i < encounter.Count; i++)
        {
            if (encounter[i].IsAlive)
            {
                alliesAlive++;
            }
        }

        return enemy.ArchetypeId switch
        {
            "cultist_shaman" => RollShamanIntent(turn, alliesAlive, rng),
            "cultist_guard" => RollGuardIntent(turn, alliesAlive, rng),
            "cultist_brute" => RollBruteIntent(turn, rng),
            "elite_sentinel" => RollEliteSentinelIntent(turn, rng),
            _ => RollDefaultIntent(isElite, rng)
        };
    }

    private static EnemyIntentRoll RollShamanIntent(int turn, int alliesAlive, Random rng)
    {
        if (turn == 1 && alliesAlive >= 2)
        {
            return new EnemyIntentRoll(EnemyIntentType.Buff, 1);
        }

        var roll = rng.Next(100);
        if (roll < 55)
        {
            return new EnemyIntentRoll(EnemyIntentType.Buff, alliesAlive >= 3 ? 2 : 1);
        }

        if (roll < 75)
        {
            return new EnemyIntentRoll(EnemyIntentType.Defend, 5);
        }

        return new EnemyIntentRoll(EnemyIntentType.Attack, rng.Next(4, 8));
    }

    private static EnemyIntentRoll RollGuardIntent(int turn, int alliesAlive, Random rng)
    {
        if (turn == 1)
        {
            return new EnemyIntentRoll(EnemyIntentType.Defend, alliesAlive >= 3 ? 7 : 6);
        }

        var roll = rng.Next(100);
        if (roll < 45)
        {
            return new EnemyIntentRoll(EnemyIntentType.Defend, 6);
        }

        if (roll < 80)
        {
            return new EnemyIntentRoll(EnemyIntentType.Attack, rng.Next(5, 9));
        }

        return new EnemyIntentRoll(EnemyIntentType.Buff, 1);
    }

    private static EnemyIntentRoll RollBruteIntent(int turn, Random rng)
    {
        if (turn % 3 == 0)
        {
            return new EnemyIntentRoll(EnemyIntentType.Buff, 2);
        }

        var roll = rng.Next(100);
        if (roll < 75)
        {
            return new EnemyIntentRoll(EnemyIntentType.Attack, rng.Next(8, 13));
        }

        return new EnemyIntentRoll(EnemyIntentType.Defend, 5);
    }

    private static EnemyIntentRoll RollEliteSentinelIntent(int turn, Random rng)
    {
        return turn % 3 switch
        {
            1 => new EnemyIntentRoll(EnemyIntentType.Attack, rng.Next(10, 16)),
            2 => new EnemyIntentRoll(EnemyIntentType.Defend, 12),
            _ => new EnemyIntentRoll(EnemyIntentType.Buff, 3)
        };
    }

    private static EnemyIntentRoll RollDefaultIntent(bool isElite, Random rng)
    {
        var roll = rng.Next(100);
        if (roll < (isElite ? 70 : 60))
        {
            return new EnemyIntentRoll(
                EnemyIntentType.Attack,
                isElite ? rng.Next(9, 15) : rng.Next(6, 11));
        }

        if (roll < 85)
        {
            return new EnemyIntentRoll(
                EnemyIntentType.Defend,
                isElite ? 10 : 6);
        }

        return new EnemyIntentRoll(
            EnemyIntentType.Buff,
            isElite ? 3 : 2);
    }
}
