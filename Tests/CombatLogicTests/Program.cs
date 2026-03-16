using System;
using System.Collections.Generic;

internal static class Program
{
    private static int Main()
    {
        var tests = new List<(string Name, Action Run)>
        {
            ("Vulnerable multiplier rounds up", TestVulnerableRounding),
            ("No vulnerable keeps base damage", TestNoVulnerable),
            ("Block fully absorbs hit", TestFullBlock),
            ("Block partially absorbs hit", TestPartialBlock),
            ("Strength and relic bonus stack", TestStrengthAndFlatBonus),
            ("Damage never drops below zero", TestClampToZero),
            ("Turn-1 relic bonuses apply once", TestTurnOneRelicBonuses),
            ("Non-turn-1 has no opening relic bonus", TestLaterTurnNoOpeningBonus),
            ("End of round decrements statuses", TestEndOfRoundStatusDecay),
            ("Hand cards move to discard", TestMoveHandToDiscard),
            ("Draw stops at hand limit", TestDrawStopsAtHandLimit),
            ("Draw reshuffles discard when needed", TestDrawReshufflesDiscard),
            ("Draw halts when no cards available", TestDrawHaltsWhenNoCards),
            ("Normal intent values stay in expected ranges", TestNormalIntentRanges),
            ("Elite intent values stay in expected ranges", TestEliteIntentRanges),
            ("Elite attack rate is higher than normal", TestEliteAttackRateHigher),
            ("Normal encounter roster scales by floor", TestNormalEncounterRoster),
            ("Elite encounter roster uses sentinel archetype", TestEliteEncounterRoster),
            ("Card catalog starter/reward pools are config-driven", TestCardCatalogPools),
            ("Card effects preserve expected sequence", TestCardEffectSequence),
            ("Enemy catalog rules match encounter expectations", TestEnemyCatalogRules)
        };

        var failed = 0;
        foreach (var test in tests)
        {
            try
            {
                test.Run();
                Console.WriteLine($"[PASS] {test.Name}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"[FAIL] {test.Name}: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {tests.Count}, Failed: {failed}");
        return failed == 0 ? 0 : 1;
    }

    private static void TestVulnerableRounding()
    {
        var result = CombatResolver.ResolveHit(
            baseDamage: 5,
            attackerStrength: 0,
            targetVulnerable: 1,
            targetBlock: 0,
            targetHp: 50);
        ExpectEqual(8, result.FinalDamage, nameof(result.FinalDamage));
        ExpectEqual(42, result.RemainingHp, nameof(result.RemainingHp));
    }

    private static void TestNoVulnerable()
    {
        var result = CombatResolver.ResolveHit(
            baseDamage: 7,
            attackerStrength: 0,
            targetVulnerable: 0,
            targetBlock: 0,
            targetHp: 30);
        ExpectEqual(7, result.FinalDamage, nameof(result.FinalDamage));
        ExpectEqual(23, result.RemainingHp, nameof(result.RemainingHp));
    }

    private static void TestFullBlock()
    {
        var result = CombatResolver.ResolveHit(
            baseDamage: 6,
            attackerStrength: 0,
            targetVulnerable: 0,
            targetBlock: 10,
            targetHp: 25);
        ExpectEqual(6, result.FinalDamage, nameof(result.FinalDamage));
        ExpectEqual(6, result.Blocked, nameof(result.Blocked));
        ExpectEqual(0, result.Taken, nameof(result.Taken));
        ExpectEqual(4, result.RemainingBlock, nameof(result.RemainingBlock));
        ExpectEqual(25, result.RemainingHp, nameof(result.RemainingHp));
    }

    private static void TestPartialBlock()
    {
        var result = CombatResolver.ResolveHit(
            baseDamage: 12,
            attackerStrength: 0,
            targetVulnerable: 0,
            targetBlock: 5,
            targetHp: 40);
        ExpectEqual(12, result.FinalDamage, nameof(result.FinalDamage));
        ExpectEqual(5, result.Blocked, nameof(result.Blocked));
        ExpectEqual(7, result.Taken, nameof(result.Taken));
        ExpectEqual(0, result.RemainingBlock, nameof(result.RemainingBlock));
        ExpectEqual(33, result.RemainingHp, nameof(result.RemainingHp));
    }

    private static void TestStrengthAndFlatBonus()
    {
        var result = CombatResolver.ResolveHit(
            baseDamage: 6,
            attackerStrength: 2,
            targetVulnerable: 1,
            targetBlock: 0,
            targetHp: 40,
            flatBonus: 1);
        ExpectEqual(14, result.FinalDamage, nameof(result.FinalDamage));
        ExpectEqual(26, result.RemainingHp, nameof(result.RemainingHp));
    }

    private static void TestClampToZero()
    {
        var result = CombatResolver.ResolveHit(
            baseDamage: -2,
            attackerStrength: -5,
            targetVulnerable: 0,
            targetBlock: 0,
            targetHp: 10);
        ExpectEqual(0, result.FinalDamage, nameof(result.FinalDamage));
        ExpectEqual(10, result.RemainingHp, nameof(result.RemainingHp));
    }

    private static void TestTurnOneRelicBonuses()
    {
        var result = TurnFlowResolver.ResolvePlayerTurnStart(
            turn: 1,
            maxEnergy: 3,
            hasLantern: true,
            hasAnchor: true);
        ExpectEqual(4, result.Energy, nameof(result.Energy));
        ExpectEqual(8, result.PlayerBlock, nameof(result.PlayerBlock));
    }

    private static void TestLaterTurnNoOpeningBonus()
    {
        var result = TurnFlowResolver.ResolvePlayerTurnStart(
            turn: 2,
            maxEnergy: 3,
            hasLantern: true,
            hasAnchor: true);
        ExpectEqual(3, result.Energy, nameof(result.Energy));
        ExpectEqual(0, result.PlayerBlock, nameof(result.PlayerBlock));
    }

    private static void TestEndOfRoundStatusDecay()
    {
        var result = TurnFlowResolver.ResolveEndOfRoundStatuses(
            playerVulnerable: 2,
            enemyVulnerable: 1);
        ExpectEqual(1, result.PlayerVulnerable, nameof(result.PlayerVulnerable));
        ExpectEqual(0, result.EnemyVulnerable, nameof(result.EnemyVulnerable));
    }

    private static void TestMoveHandToDiscard()
    {
        var hand = new List<string> { "strike", "defend", "bash" };
        var discard = new List<string> { "old" };
        TurnFlowResolver.MoveHandToDiscard(hand, discard);

        ExpectEqual(0, hand.Count, "hand.Count");
        ExpectEqual(4, discard.Count, "discard.Count");
        ExpectEqual("old", discard[0], "discard[0]");
        ExpectEqual("strike", discard[1], "discard[1]");
        ExpectEqual("defend", discard[2], "discard[2]");
        ExpectEqual("bash", discard[3], "discard[3]");
    }

    private static void TestDrawStopsAtHandLimit()
    {
        var rng = new Random(1);
        var drawPile = new List<string> { "a", "b", "c" };
        var discardPile = new List<string>();
        var hand = new List<string> { "h1", "h2" };

        var result = DeckFlowResolver.DrawIntoHand(
            drawPile,
            discardPile,
            hand,
            drawCount: 3,
            handLimit: 3,
            rng);

        ExpectEqual(true, result.HandLimitReached, nameof(result.HandLimitReached));
        ExpectEqual(1, result.DrawnCards.Count, "result.DrawnCards.Count");
        ExpectEqual(3, hand.Count, "hand.Count");
        ExpectEqual(2, drawPile.Count, "drawPile.Count");
    }

    private static void TestDrawReshufflesDiscard()
    {
        var rng = new Random(42);
        var drawPile = new List<string> { "top" };
        var discardPile = new List<string> { "x", "y", "z" };
        var hand = new List<string>();

        var result = DeckFlowResolver.DrawIntoHand(
            drawPile,
            discardPile,
            hand,
            drawCount: 3,
            handLimit: 10,
            rng);

        ExpectEqual(false, result.HandLimitReached, nameof(result.HandLimitReached));
        ExpectEqual(1, result.ReshuffleCount, nameof(result.ReshuffleCount));
        ExpectEqual(3, result.DrawnCards.Count, "result.DrawnCards.Count");
        ExpectEqual(3, hand.Count, "hand.Count");
        ExpectEqual(1, drawPile.Count, "drawPile.Count");
        ExpectEqual(0, discardPile.Count, "discardPile.Count");
        ExpectEqual("top", hand[0], "hand[0]");
    }

    private static void TestDrawHaltsWhenNoCards()
    {
        var rng = new Random(7);
        var drawPile = new List<string>();
        var discardPile = new List<string>();
        var hand = new List<string>();

        var result = DeckFlowResolver.DrawIntoHand(
            drawPile,
            discardPile,
            hand,
            drawCount: 5,
            handLimit: 10,
            rng);

        ExpectEqual(0, result.DrawnCards.Count, "result.DrawnCards.Count");
        ExpectEqual(0, result.ReshuffleCount, nameof(result.ReshuffleCount));
        ExpectEqual(false, result.HandLimitReached, nameof(result.HandLimitReached));
    }

    private static void TestNormalIntentRanges()
    {
        var rng = new Random(1337);
        for (var i = 0; i < 5000; i++)
        {
            var intent = IntentResolver.RollEnemyIntent(isElite: false, rng);
            switch (intent.Type)
            {
                case EnemyIntentType.Attack:
                    ExpectInRange(intent.Value, 6, 10, "normal attack value");
                    break;
                case EnemyIntentType.Defend:
                    ExpectEqual(6, intent.Value, "normal defend value");
                    break;
                case EnemyIntentType.Buff:
                    ExpectEqual(2, intent.Value, "normal buff value");
                    break;
            }
        }
    }

    private static void TestEliteIntentRanges()
    {
        var rng = new Random(2025);
        for (var i = 0; i < 5000; i++)
        {
            var intent = IntentResolver.RollEnemyIntent(isElite: true, rng);
            switch (intent.Type)
            {
                case EnemyIntentType.Attack:
                    ExpectInRange(intent.Value, 9, 14, "elite attack value");
                    break;
                case EnemyIntentType.Defend:
                    ExpectEqual(10, intent.Value, "elite defend value");
                    break;
                case EnemyIntentType.Buff:
                    ExpectEqual(3, intent.Value, "elite buff value");
                    break;
            }
        }
    }

    private static void TestEliteAttackRateHigher()
    {
        const int sampleSize = 20000;
        var normalRng = new Random(11);
        var eliteRng = new Random(11);
        var normalAttack = 0;
        var eliteAttack = 0;

        for (var i = 0; i < sampleSize; i++)
        {
            if (IntentResolver.RollEnemyIntent(isElite: false, normalRng).Type == EnemyIntentType.Attack)
            {
                normalAttack++;
            }

            if (IntentResolver.RollEnemyIntent(isElite: true, eliteRng).Type == EnemyIntentType.Attack)
            {
                eliteAttack++;
            }
        }

        var normalRate = normalAttack / (double)sampleSize;
        var eliteRate = eliteAttack / (double)sampleSize;
        ExpectInRange(normalRate, 0.55, 0.65, "normal attack rate");
        ExpectInRange(eliteRate, 0.65, 0.75, "elite attack rate");
        if (eliteRate <= normalRate)
        {
            throw new InvalidOperationException(
                $"elite attack rate should be higher than normal: elite={eliteRate:F3}, normal={normalRate:F3}");
        }
    }

    private static void TestNormalEncounterRoster()
    {
        var early = EnemyEncounterBuilder.BuildEncounter(MapNodeType.NormalBattle, floor: 1);
        ExpectEqual(2, early.Count, "early.Count");
        ExpectEqual("Cultist A", early[0].Name, "early[0].Name");
        ExpectEqual(43, early[0].Hp, "early[0].Hp");

        var later = EnemyEncounterBuilder.BuildEncounter(MapNodeType.NormalBattle, floor: 3);
        ExpectEqual(3, later.Count, "later.Count");
        ExpectEqual("Cultist C", later[2].Name, "later[2].Name");
        ExpectEqual(50, later[2].Hp, "later[2].Hp");
    }

    private static void TestEliteEncounterRoster()
    {
        var roster = EnemyEncounterBuilder.BuildEncounter(MapNodeType.EliteBattle, floor: 4);
        ExpectEqual(2, roster.Count, "roster.Count");
        ExpectEqual("Elite Sentinel A", roster[0].Name, "roster[0].Name");
        ExpectEqual("elite_sentinel", roster[0].VisualId, "roster[0].VisualId");
        ExpectEqual(118, roster[0].Hp, "roster[0].Hp");
        ExpectEqual(2, roster[0].Strength, "roster[0].Strength");
    }

    private static void TestCardCatalogPools()
    {
        var starter = CardData.StarterDeckIds();
        ExpectEqual(11, starter.Count, "starter.Count");
        ExpectEqual("bash", starter[10], "starter[10]");

        var rewards = CardData.RewardPoolIds();
        ExpectEqual(6, rewards.Count, "rewards.Count");
        ExpectEqual("heavy_slash", rewards[0], "rewards[0]");
    }

    private static void TestCardEffectSequence()
    {
        var bash = CardData.CreateById("bash");
        ExpectEqual(2, bash.Effects.Count, "bash.Effects.Count");
        ExpectEqual(true, bash.Effects[0].Type == CardEffectType.DealDamage, "bash.effects[0].type");
        ExpectEqual(true, bash.Effects[1].Type == CardEffectType.ApplyVulnerable, "bash.effects[1].type");

        var quickSlash = CardData.CreateById("quick_slash");
        ExpectEqual(2, quickSlash.Effects.Count, "quickSlash.Effects.Count");
        ExpectEqual(true, quickSlash.Effects[0].Type == CardEffectType.DealDamage, "quickSlash.effects[0].type");
        ExpectEqual(true, quickSlash.Effects[1].Type == CardEffectType.DrawCards, "quickSlash.effects[1].type");
        ExpectEqual(7, quickSlash.Damage, "quickSlash.Damage");
        ExpectEqual(1, quickSlash.DrawCount, "quickSlash.DrawCount");
    }

    private static void TestEnemyCatalogRules()
    {
        var normalRules = EnemyCatalog.GetEncounterRules(MapNodeType.NormalBattle);
        ExpectEqual(3, normalRules.Count, "normalRules.Count");
        ExpectEqual("cultist", normalRules[0].ArchetypeId, "normalRules[0].ArchetypeId");

        var eliteRules = EnemyCatalog.GetEncounterRules(MapNodeType.EliteBattle);
        ExpectEqual(2, eliteRules.Count, "eliteRules.Count");
        ExpectEqual("elite_sentinel", eliteRules[0].ArchetypeId, "eliteRules[0].ArchetypeId");

        var sentinel = EnemyCatalog.GetArchetype("elite_sentinel");
        ExpectEqual("Elite Sentinel", sentinel.DisplayName, "sentinel.DisplayName");
        ExpectEqual(3, sentinel.StrengthByFloor(6), "sentinel.StrengthByFloor(6)");
    }

    private static void ExpectEqual(int expected, int actual, string label)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
        }
    }

    private static void ExpectEqual(string expected, string actual, string label)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
        }
    }

    private static void ExpectEqual(bool expected, bool actual, string label)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
        }
    }

    private static void ExpectInRange(int value, int min, int max, string label)
    {
        if (value < min || value > max)
        {
            throw new InvalidOperationException($"{label}: expected in [{min}, {max}], got {value}");
        }
    }

    private static void ExpectInRange(double value, double min, double max, string label)
    {
        if (value < min || value > max)
        {
            throw new InvalidOperationException($"{label}: expected in [{min}, {max}], got {value:F4}");
        }
    }
}
