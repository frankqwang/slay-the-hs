using System;
using System.Collections.Generic;
using System.IO;

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
            ("Default normal intent values stay in expected ranges", TestDefaultNormalIntentRanges),
            ("Default elite intent values stay in expected ranges", TestDefaultEliteIntentRanges),
            ("Shaman opens with buff in groups", TestShamanOpener),
            ("Guard opens with defend", TestGuardOpener),
            ("Brute buffs every third turn", TestBruteTurnCycle),
            ("Elite sentinel follows 3-turn pattern", TestEliteSentinelTurnPattern),
            ("Normal encounter roster scales by floor", TestNormalEncounterRoster),
            ("Elite encounter roster scales by floor", TestEliteEncounterRoster),
            ("Card effects aggregate into legacy fields", TestCardEffectsAggregateLegacyFields),
            ("CreateById returns independent card instances", TestCreateByIdReturnsIndependentInstances),
            ("Card pools only contain known card ids", TestCardPoolsContainKnownCardIds),
            ("Enemy catalog contains configured encounter rules", TestEnemyCatalogRuleCoverage),
            ("Card effect pipeline preserves execution order", TestCardEffectPipelineOrder),
            ("Card effect pipeline handles new buff effects", TestCardEffectPipelineExtendedEffects),
            ("Card text uses real line breaks", TestCardTextUsesLineBreaks),
            ("Card description supports language toggle", TestCardDescriptionLanguageToggle),
            ("Relic catalog exposes extended relic ids", TestRelicCatalogCoverage),
            ("Potion catalog exposes potion ids", TestPotionCatalogCoverage),
            ("New build cards resolve from catalog", TestNewBuildCardsResolve),
            ("Card catalog validation rejects duplicate ids", TestCardCatalogValidationRejectsDuplicates),
            ("Card catalog validation rejects unknown pool references", TestCardCatalogValidationRejectsUnknownPoolRefs),
            ("Card catalog save/load roundtrip preserves entries", TestCardCatalogSaveLoadRoundtrip),
            ("Card catalog validation requires strike fallback", TestCardCatalogValidationRequiresStrike)
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
        var result = CombatResolver.ResolveHit(5, 0, 1, 0, 50);
        ExpectEqual(8, result.FinalDamage, nameof(result.FinalDamage));
        ExpectEqual(42, result.RemainingHp, nameof(result.RemainingHp));
    }

    private static void TestNoVulnerable()
    {
        var result = CombatResolver.ResolveHit(7, 0, 0, 0, 30);
        ExpectEqual(7, result.FinalDamage, nameof(result.FinalDamage));
        ExpectEqual(23, result.RemainingHp, nameof(result.RemainingHp));
    }

    private static void TestFullBlock()
    {
        var result = CombatResolver.ResolveHit(6, 0, 0, 10, 25);
        ExpectEqual(6, result.FinalDamage, nameof(result.FinalDamage));
        ExpectEqual(6, result.Blocked, nameof(result.Blocked));
        ExpectEqual(0, result.Taken, nameof(result.Taken));
        ExpectEqual(4, result.RemainingBlock, nameof(result.RemainingBlock));
        ExpectEqual(25, result.RemainingHp, nameof(result.RemainingHp));
    }

    private static void TestPartialBlock()
    {
        var result = CombatResolver.ResolveHit(12, 0, 0, 5, 40);
        ExpectEqual(12, result.FinalDamage, nameof(result.FinalDamage));
        ExpectEqual(5, result.Blocked, nameof(result.Blocked));
        ExpectEqual(7, result.Taken, nameof(result.Taken));
        ExpectEqual(0, result.RemainingBlock, nameof(result.RemainingBlock));
        ExpectEqual(33, result.RemainingHp, nameof(result.RemainingHp));
    }

    private static void TestStrengthAndFlatBonus()
    {
        var result = CombatResolver.ResolveHit(6, 2, 1, 0, 40, flatBonus: 1);
        ExpectEqual(14, result.FinalDamage, nameof(result.FinalDamage));
        ExpectEqual(26, result.RemainingHp, nameof(result.RemainingHp));
    }

    private static void TestClampToZero()
    {
        var result = CombatResolver.ResolveHit(-2, -5, 0, 0, 10);
        ExpectEqual(0, result.FinalDamage, nameof(result.FinalDamage));
        ExpectEqual(10, result.RemainingHp, nameof(result.RemainingHp));
    }

    private static void TestTurnOneRelicBonuses()
    {
        var result = TurnFlowResolver.ResolvePlayerTurnStart(1, 3, hasLantern: true, hasAnchor: true);
        ExpectEqual(4, result.Energy, nameof(result.Energy));
        ExpectEqual(8, result.PlayerBlock, nameof(result.PlayerBlock));
    }

    private static void TestLaterTurnNoOpeningBonus()
    {
        var result = TurnFlowResolver.ResolvePlayerTurnStart(2, 3, hasLantern: true, hasAnchor: true);
        ExpectEqual(3, result.Energy, nameof(result.Energy));
        ExpectEqual(0, result.PlayerBlock, nameof(result.PlayerBlock));
    }

    private static void TestEndOfRoundStatusDecay()
    {
        var result = TurnFlowResolver.ResolveEndOfRoundStatuses(2, 1);
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

        var result = DeckFlowResolver.DrawIntoHand(drawPile, discardPile, hand, drawCount: 3, handLimit: 3, rng);

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

        var result = DeckFlowResolver.DrawIntoHand(drawPile, discardPile, hand, drawCount: 3, handLimit: 10, rng);

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
        var result = DeckFlowResolver.DrawIntoHand(new List<string>(), new List<string>(), new List<string>(), drawCount: 5, handLimit: 10, rng);

        ExpectEqual(0, result.DrawnCards.Count, "result.DrawnCards.Count");
        ExpectEqual(0, result.ReshuffleCount, nameof(result.ReshuffleCount));
        ExpectEqual(false, result.HandLimitReached, nameof(result.HandLimitReached));
    }

    private static void TestDefaultNormalIntentRanges()
    {
        var rng = new Random(1337);
        for (var i = 0; i < 5000; i++)
        {
            var intent = IntentResolver.RollEnemyIntent(new EnemyUnit { ArchetypeId = "cultist" }, new List<EnemyUnit> { new EnemyUnit { ArchetypeId = "cultist", Hp = 1 } }, isElite: false, turn: 1, rng);
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

    private static void TestDefaultEliteIntentRanges()
    {
        var rng = new Random(2025);
        for (var i = 0; i < 5000; i++)
        {
            var intent = IntentResolver.RollEnemyIntent(new EnemyUnit { ArchetypeId = "unknown_elite" }, new List<EnemyUnit> { new EnemyUnit { ArchetypeId = "unknown_elite", Hp = 1 } }, isElite: true, turn: 1, rng);
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

    private static void TestShamanOpener()
    {
        var shaman = new EnemyUnit { ArchetypeId = "cultist_shaman", Hp = 1 };
        var allies = new List<EnemyUnit>
        {
            shaman,
            new EnemyUnit { ArchetypeId = "cultist", Hp = 1 },
            new EnemyUnit { ArchetypeId = "cultist_guard", Hp = 1 }
        };

        var intent = IntentResolver.RollEnemyIntent(shaman, allies, isElite: false, turn: 1, new Random(9));
        ExpectEqual(EnemyIntentType.Buff, intent.Type, "shaman opener type");
        ExpectEqual(1, intent.Value, "shaman opener value");
    }

    private static void TestGuardOpener()
    {
        var guard = new EnemyUnit { ArchetypeId = "cultist_guard", Hp = 1 };
        var allies = new List<EnemyUnit>
        {
            guard,
            new EnemyUnit { ArchetypeId = "cultist", Hp = 1 }
        };

        var intent = IntentResolver.RollEnemyIntent(guard, allies, isElite: false, turn: 1, new Random(21));
        ExpectEqual(EnemyIntentType.Defend, intent.Type, "guard opener type");
        ExpectEqual(6, intent.Value, "guard opener value");
    }

    private static void TestBruteTurnCycle()
    {
        var brute = new EnemyUnit { ArchetypeId = "cultist_brute", Hp = 1 };
        var allies = new List<EnemyUnit> { brute };

        var turn3 = IntentResolver.RollEnemyIntent(brute, allies, isElite: false, turn: 3, new Random(5));
        ExpectEqual(EnemyIntentType.Buff, turn3.Type, "brute turn3 type");
        ExpectEqual(2, turn3.Value, "brute turn3 value");
    }

    private static void TestEliteSentinelTurnPattern()
    {
        var sentinel = new EnemyUnit { ArchetypeId = "elite_sentinel", Hp = 1 };
        var allies = new List<EnemyUnit> { sentinel };

        var turn1 = IntentResolver.RollEnemyIntent(sentinel, allies, isElite: true, turn: 1, new Random(3));
        var turn2 = IntentResolver.RollEnemyIntent(sentinel, allies, isElite: true, turn: 2, new Random(3));
        var turn3 = IntentResolver.RollEnemyIntent(sentinel, allies, isElite: true, turn: 3, new Random(3));

        ExpectEqual(EnemyIntentType.Attack, turn1.Type, "sentinel turn1 type");
        ExpectInRange(turn1.Value, 10, 15, "sentinel turn1 value");
        ExpectEqual(EnemyIntentType.Defend, turn2.Type, "sentinel turn2 type");
        ExpectEqual(12, turn2.Value, "sentinel turn2 value");
        ExpectEqual(EnemyIntentType.Buff, turn3.Type, "sentinel turn3 type");
        ExpectEqual(3, turn3.Value, "sentinel turn3 value");
    }

    private static void TestNormalEncounterRoster()
    {
        var early = EnemyEncounterBuilder.BuildEncounter(MapNodeType.NormalBattle, floor: 1);
        ExpectEqual(2, early.Count, "early.Count");
        ExpectEqual("Guard A", early[0].Name, "early[0].Name");
        ExpectEqual(47, early[0].Hp, "early[0].Hp");

        var later = EnemyEncounterBuilder.BuildEncounter(MapNodeType.NormalBattle, floor: 6);
        ExpectEqual(5, later.Count, "later.Count");
        ExpectEqual("Brute E", later[4].Name, "later[4].Name");
        ExpectEqual(104, later[4].Hp, "later[4].Hp");
    }

    private static void TestEliteEncounterRoster()
    {
        var roster = EnemyEncounterBuilder.BuildEncounter(MapNodeType.EliteBattle, floor: 4);
        ExpectEqual(3, roster.Count, "roster.Count");
        ExpectEqual("Elite Sentinel A", roster[0].Name, "roster[0].Name");
        ExpectEqual("elite_sentinel", roster[0].VisualId, "roster[0].VisualId");
        ExpectEqual(118, roster[0].Hp, "roster[0].Hp");
        ExpectEqual(2, roster[0].Strength, "roster[0].Strength");
    }

    private static void TestCardEffectsAggregateLegacyFields()
    {
        var card = CardData.CreateById("quick_slash");

        ExpectEqual(2, card.Effects.Count, "card.Effects.Count");
        ExpectEqual(7, card.Damage, nameof(card.Damage));
        ExpectEqual(0, card.Block, nameof(card.Block));
        ExpectEqual(0, card.ApplyVulnerable, nameof(card.ApplyVulnerable));
        ExpectEqual(1, card.DrawCount, nameof(card.DrawCount));
        ExpectEqual(true, card.HasEffect(CardEffectType.Damage), "card.HasEffect(Damage)");
        ExpectEqual(true, card.HasEffect(CardEffectType.DrawCards), "card.HasEffect(DrawCards)");
    }

    private static void TestCreateByIdReturnsIndependentInstances()
    {
        var a = CardData.CreateById("strike");
        var b = CardData.CreateById("strike");

        if (ReferenceEquals(a, b))
        {
            throw new InvalidOperationException("CreateById should return independent instances for duplicate cards in deck/hand.");
        }
    }

    private static void TestCardPoolsContainKnownCardIds()
    {
        foreach (var id in CardData.StarterDeckIds())
        {
            var card = CardData.CreateById(id);
            ExpectEqual(id, card.Id, "starter id resolution");
        }

        foreach (var id in CardData.RewardPoolIds())
        {
            var card = CardData.CreateById(id);
            ExpectEqual(id, card.Id, "reward id resolution");
        }
    }

    private static void TestEnemyCatalogRuleCoverage()
    {
        var catalog = EnemyEncounterCatalog.Load();
        ExpectEqual(true, catalog.EncounterMembersByType.ContainsKey(MapNodeType.NormalBattle), "normal encounter config exists");
        ExpectEqual(true, catalog.EncounterMembersByType.ContainsKey(MapNodeType.EliteBattle), "elite encounter config exists");
        ExpectEqual(true, catalog.ArchetypesById.ContainsKey("cultist"), "cultist archetype exists");
    }

    private static void TestCardEffectPipelineOrder()
    {
        var card = new CardData(
            id: "combo",
            name: "Combo",
            description: "Deal 4. Apply 2 Vulnerable. Draw 1.",
            descriptionZh: "造成4点伤害。施加2层易伤。抽1张牌。",
            kind: CardKind.Attack,
            cost: 1,
            effects: new List<CardEffectData>
            {
                new(CardEffectType.Damage, CardEffectTarget.SelectedEnemy, amount: 4),
                new(CardEffectType.ApplyVulnerable, CardEffectTarget.SelectedEnemy, amount: 2),
                new(CardEffectType.DrawCards, CardEffectTarget.Player, amount: 1)
            });

        var log = new List<string>();
        var result = CardEffectPipeline.Execute(card, new RecordingEffectExecutor(log));

        ExpectEqual(2, log.Count, "pipeline action count");
        ExpectEqual("Damage:4", log[0], "pipeline first action");
        ExpectEqual("Vulnerable:2", log[1], "pipeline second action");
        ExpectEqual(1, result.DrawCount, "pipeline draw count");
    }

    private static void TestCardEffectPipelineExtendedEffects()
    {
        var card = new CardData(
            id: "support",
            name: "Support",
            description: "Gain 2 Strength. Gain 1 Energy. Heal 3.",
            descriptionZh: "获得2点力量。获得1点能量。回复3点生命。",
            kind: CardKind.Skill,
            cost: 1,
            effects: new List<CardEffectData>
            {
                new(CardEffectType.GainStrength, CardEffectTarget.Player, amount: 2),
                new(CardEffectType.GainEnergy, CardEffectTarget.Player, amount: 1),
                new(CardEffectType.Heal, CardEffectTarget.Player, amount: 3)
            });

        var runtime = new CountingRuntime();
        var result = CardEffectPipeline.Execute(card, runtime);

        ExpectEqual(1, runtime.GainStrengthCount, "GainStrengthCount");
        ExpectEqual(1, runtime.GainEnergyCount, "GainEnergyCount");
        ExpectEqual(1, runtime.HealCount, "HealCount");
        ExpectEqual(0, result.DrawCount, "extended pipeline draw count");
    }

    private static void TestCardTextUsesLineBreaks()
    {
        LocalizationSettings.SetLanguage(GameLanguage.En);
        var card = CardData.CreateById("strike");
        var text = card.ToCardText();
        ExpectEqual(true, text.Contains("\n"), "card text includes newline");
        ExpectEqual(false, text.Contains("\\n"), "card text should not include escaped literal \\\\n");
    }

    private static void TestCardDescriptionLanguageToggle()
    {
        var card = CardData.CreateById("strike");

        LocalizationSettings.SetLanguage(GameLanguage.En);
        ExpectEqual("Deal 6 damage.", card.GetLocalizedDescription(), "english description");

        LocalizationSettings.SetLanguage(GameLanguage.ZhHans);
        ExpectEqual("造成6点伤害。", card.GetLocalizedDescription(), "chinese description");

        LocalizationSettings.SetLanguage(GameLanguage.En);
    }

    private static void TestRelicCatalogCoverage()
    {
        foreach (var relicId in RelicData.AllRelicIds())
        {
            var relic = RelicData.CreateById(relicId);
            ExpectEqual(relicId, relic.Id, "relic id resolution");
            ExpectEqual(false, string.IsNullOrWhiteSpace(relic.Name), "relic name non-empty");
        }
    }

    private static void TestPotionCatalogCoverage()
    {
        foreach (var potionId in PotionData.AllPotionIds())
        {
            var potion = PotionData.CreateById(potionId);
            ExpectEqual(potionId, potion.Id, "potion id resolution");
            ExpectEqual(false, string.IsNullOrWhiteSpace(potion.Name), "potion name non-empty");
        }
    }

    private static void TestNewBuildCardsResolve()
    {
        var newBuildCards = new[]
        {
            "berserker_form",
            "overclock",
            "crushing_blow",
            "chain_lightning",
            "meditate",
            "fortress_stance"
        };

        foreach (var cardId in newBuildCards)
        {
            var card = CardData.CreateById(cardId);
            ExpectEqual(cardId, card.Id, "new build card id resolution");
            if (card.Effects.Count == 0)
            {
                throw new InvalidOperationException($"new build card should have effects: {cardId}");
            }
        }
    }

    private static void TestCardCatalogValidationRejectsDuplicates()
    {
        var catalog = new CardCatalogData
        {
            Cards = new List<CardEntryData>
            {
                new()
                {
                    Id = "strike",
                    Name = "Strike",
                    Kind = nameof(CardKind.Attack),
                    Cost = 1,
                    Effects = new List<CardEffectEntryData>
                    {
                        new()
                        {
                            Type = nameof(CardEffectType.Damage),
                            Target = nameof(CardEffectTarget.SelectedEnemy),
                            Amount = 6,
                            Repeat = 1
                        }
                    }
                },
                new()
                {
                    Id = "strike",
                    Name = "Other",
                    Kind = nameof(CardKind.Attack),
                    Cost = 1,
                    Effects = new List<CardEffectEntryData>()
                }
            }
        };

        var errors = CardCatalogPersistence.Validate(catalog);
        ExpectEqual(true, errors.Exists(e => e.Contains("duplicate card id")), "duplicate id error exists");
    }

    private static void TestCardCatalogValidationRejectsUnknownPoolRefs()
    {
        var catalog = new CardCatalogData
        {
            Cards = new List<CardEntryData>
            {
                new()
                {
                    Id = "strike",
                    Name = "Strike",
                    Kind = nameof(CardKind.Attack),
                    Cost = 1,
                    Effects = new List<CardEffectEntryData>
                    {
                        new()
                        {
                            Type = nameof(CardEffectType.Damage),
                            Target = nameof(CardEffectTarget.SelectedEnemy),
                            Amount = 6,
                            Repeat = 1
                        }
                    }
                }
            },
            StarterDeck = new List<string> { "ghost_card" },
            RewardPool = new List<string> { "ghost_card" }
        };

        var errors = CardCatalogPersistence.Validate(catalog);
        ExpectEqual(true, errors.Exists(e => e.Contains("starterDeck references unknown id")), "starterDeck unknown id error exists");
        ExpectEqual(true, errors.Exists(e => e.Contains("rewardPool references unknown id")), "rewardPool unknown id error exists");
    }


    private static void TestCardCatalogSaveLoadRoundtrip()
    {
        var catalog = new CardCatalogData
        {
            Cards = new List<CardEntryData>
            {
                new()
                {
                    Id = "strike",
                    Name = "Strike",
                    Kind = nameof(CardKind.Attack),
                    Cost = 1,
                    Description = "Deal 6 damage.",
                    DescriptionZh = "造成6点伤害。",
                    Effects = new List<CardEffectEntryData>
                    {
                        new()
                        {
                            Type = nameof(CardEffectType.Damage),
                            Target = nameof(CardEffectTarget.SelectedEnemy),
                            Amount = 6,
                            Repeat = 1
                        }
                    }
                }
            },
            StarterDeck = new List<string> { "strike" },
            RewardPool = new List<string> { "strike" }
        };

        var path = Path.Combine(Path.GetTempPath(), $"cards_test_{Guid.NewGuid():N}.json");
        try
        {
            CardCatalogPersistence.SaveToFile(path, catalog);
            var loaded = CardCatalogPersistence.LoadFromFile(path);
            ExpectEqual(1, loaded.Cards.Count, "roundtrip card count");
            ExpectEqual("strike", loaded.Cards[0].Id, "roundtrip card id");
            ExpectEqual(1, loaded.StarterDeck.Count, "roundtrip starterDeck count");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static void TestCardCatalogValidationRequiresStrike()
    {
        var catalog = new CardCatalogData
        {
            Cards = new List<CardEntryData>
            {
                new()
                {
                    Id = "defend",
                    Name = "Defend",
                    Kind = nameof(CardKind.Skill),
                    Cost = 1,
                    Effects = new List<CardEffectEntryData>
                    {
                        new()
                        {
                            Type = nameof(CardEffectType.GainBlock),
                            Target = nameof(CardEffectTarget.Player),
                            Amount = 5,
                            Repeat = 1
                        }
                    }
                }
            }
        };

        var errors = CardCatalogPersistence.Validate(catalog);
        ExpectEqual(true, errors.Exists(e => e.Contains("required fallback card id missing")), "missing strike error exists");
    }

    private sealed class RecordingEffectExecutor : ICardEffectRuntime
    {
        private readonly List<string> _log;

        public RecordingEffectExecutor(List<string> log)
        {
            _log = log;
        }

        public void ExecuteDamage(CardData card, CardEffectData effect)
        {
            _log.Add($"Damage:{effect.Amount}");
        }

        public void ExecuteGainBlock(CardData card, CardEffectData effect)
        {
            _log.Add($"Block:{effect.Amount}");
        }

        public void ExecuteApplyVulnerable(CardData card, CardEffectData effect)
        {
            _log.Add($"Vulnerable:{effect.Amount}");
        }

        public void ExecuteGainStrength(CardData card, CardEffectData effect)
        {
            _log.Add($"Strength:{effect.Amount}");
        }

        public void ExecuteGainEnergy(CardData card, CardEffectData effect)
        {
            _log.Add($"Energy:{effect.Amount}");
        }

        public void ExecuteHeal(CardData card, CardEffectData effect)
        {
            _log.Add($"Heal:{effect.Amount}");
        }
    }

    private sealed class CountingRuntime : ICardEffectRuntime
    {
        public int GainStrengthCount;
        public int GainEnergyCount;
        public int HealCount;

        public void ExecuteDamage(CardData card, CardEffectData effect) { }
        public void ExecuteGainBlock(CardData card, CardEffectData effect) { }
        public void ExecuteApplyVulnerable(CardData card, CardEffectData effect) { }

        public void ExecuteGainStrength(CardData card, CardEffectData effect)
        {
            GainStrengthCount++;
        }

        public void ExecuteGainEnergy(CardData card, CardEffectData effect)
        {
            GainEnergyCount++;
        }

        public void ExecuteHeal(CardData card, CardEffectData effect)
        {
            HealCount++;
        }
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
