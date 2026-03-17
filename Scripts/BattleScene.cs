using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattleScene : Control
{
    private enum EnemyAnimState
    {
        Idle,
        Hit,
        Dying
    }

    private const int MaxEnergy = 3;
    private const int HandLimit = 10;

    private readonly Random _rng = new();
    private readonly List<CardData> _drawPile = new();
    private readonly List<CardData> _discardPile = new();
    private readonly List<CardData> _hand = new();
    private readonly Dictionary<CardData, CardView> _handViews = new();
    private readonly Stack<CardView> _cardViewPool = new();

    private Label _enemyHpLabel = null!;
    private Label _enemyBlockLabel = null!;
    private Label _enemyStatusLabel = null!;
    private Label _enemyIntentLabel = null!;
    private Label _enemyNameLabel = null!;
    private Label _turnLabel = null!;
    private Label _energyLabel = null!;
    private Label _topHpLabel = null!;
    private Label _handCountLabel = null!;
    private Label _relicBarLabel = null!;
    private RichTextLabel _logText = null!;
    private HBoxContainer _relicIcons = null!;

    private Control _mainMargin = null!;
    private Control _handContainer = null!;
    private Control _enemyDropArea = null!;
    private Label _dropHintLabel = null!;
    private GridContainer _enemyRosterGrid = null!;
    private Label _enemyIntentListLabel = null!;
    private readonly Dictionary<int, Control> _enemyCardTargetByIndex = new();
    private readonly Dictionary<int, Button> _enemyCardButtonByIndex = new();
    private readonly PackedScene _enemyCardScene = GD.Load<PackedScene>("res://Scenes/EnemyCardView.tscn");
    private int _hoverEnemyIndex = -1;
    private Control _playerPanel = null!;
    private PlayerCardView _playerCardView = null!;
    private Control _enemyPanel = null!;
    private Control _turnBanner = null!;
    private Label _turnBannerLabel = null!;
    private Control _drawAnchor = null!;
    private Control _effectsLayer = null!;
    private ColorRect _arenaFarBg = null!;
    private ColorRect _arenaMidBg = null!;
    private ColorRect _arenaFrontFog = null!;
    private PanelContainer _keywordTooltip = null!;
    private RichTextLabel _keywordTooltipText = null!;

    private Button _endTurnButton = null!;

    private readonly StyleBoxFlat _dropNormalStyle = new();
    private readonly StyleBoxFlat _dropHotStyle = new();

    private int _turn = 1;
    private int _playerHp;
    private int _playerMaxHp;
    private int _playerBlock;
    private int _playerStrength;
    private int _playerVulnerable;
    private readonly PlayerUnit _player = new();

    private readonly List<EnemyUnit> _enemies = new();
    private int _selectedEnemyIndex;
    private bool _isElite;

    private int _energy;
    private bool _battleEnded;
    private int _inputLockDepth;
    private string _relicUiSignature = string.Empty;
    private readonly Dictionary<string, PanelContainer> _relicChipById = new();
    private readonly Dictionary<string, ColorRect> _relicTriggerDotById = new();
    private readonly HashSet<string> _triggeredRelicsThisTurn = new();
    private readonly Dictionary<string, Texture2D> _iconCache = new();
    private Vector2 _playerPanelBasePos;
    private Vector2 _enemyDropAreaBasePos;
    private Vector2 _enemyDropAreaBaseScale;
    private Vector2 _enemyShadowBaseSize;
    private Vector2 _playerShadowBaseSize;
    private ColorRect _enemyShadow = null!;
    private ColorRect _playerShadow = null!;
    private float _animTime;
    private EnemyAnimState _enemyAnimState = EnemyAnimState.Idle;
    private float _enemyAnimTimer;
    private bool _enemyEntrancePlayed;
    private float _playerPunchX;
    private float _enemyPunchX;
    private bool _deferredHandLayoutPending;
    private bool _dropZoneHighlighted;
    private float _hitStopTimer;
    private Line2D _dragGuide = null!;
    private Line2D _dragArrowHead = null!;
    private CardView _draggingCard = null!;
    private float _dragGuidePulseTime;
    private CanvasLayer _overlayCanvas = null!;

    private CardView _hoveredCard = null!;
    private GameState _state = null!;
    public Action<string> UiSfxRequested = _ => { };
    private const float HoverSwitchDeadzone = 18f;

    public override void _Ready()
    {
        _enemyNameLabel = GetNode<Label>("%EnemyNameLabel");
        _enemyHpLabel = GetNode<Label>("%EnemyHpLabel");
        _enemyBlockLabel = GetNode<Label>("%EnemyBlockLabel");
        _enemyStatusLabel = GetNode<Label>("%EnemyStatusLabel");
        _enemyIntentLabel = GetNode<Label>("%EnemyIntentLabel");
        _turnLabel = GetNode<Label>("%TurnLabel");
        _energyLabel = GetNode<Label>("%EnergyLabel");
        _topHpLabel = GetNode<Label>("%TopHpLabel");
        _handCountLabel = GetNode<Label>("%HandCountLabel");
        _relicBarLabel = GetNode<Label>("%RelicBarLabel");
        _relicIcons = GetNode<HBoxContainer>("%RelicIcons");
        _logText = GetNode<RichTextLabel>("%LogText");

        _mainMargin = GetNode<Control>("%MainMargin");
        _handContainer = GetNode<Control>("%HandContainer");
        _enemyDropArea = GetNode<Control>("%EnemyDropArea");
        _dropHintLabel = GetNode<Label>("%DropHintLabel");
        _playerPanel = GetNode<Control>("%PlayerPanel");
        _playerCardView = GetNode<PlayerCardView>("%PlayerCardView");
        _enemyPanel = GetNode<Control>("%EnemyPanel");
        _turnBanner = GetNode<Control>("%TurnBanner");
        _turnBannerLabel = GetNode<Label>("%TurnBannerLabel");
        _drawAnchor = GetNode<Control>("%DrawAnchor");
        _effectsLayer = GetNode<Control>("%EffectsLayer");
        _arenaFarBg = GetNode<ColorRect>("%ArenaFarBg");
        _arenaMidBg = GetNode<ColorRect>("%ArenaMidBg");
        _arenaFrontFog = GetNode<ColorRect>("%ArenaFrontFog");
        _keywordTooltip = GetNode<PanelContainer>("%KeywordTooltip");
        _keywordTooltipText = GetNode<RichTextLabel>("%KeywordTooltipText");
        _enemyShadow = GetNode<ColorRect>("MainMargin/MainVBox/Arena/EnemyShadow");
        _playerShadow = GetNode<ColorRect>("MainMargin/MainVBox/Arena/PlayerShadow");
        _overlayCanvas = new CanvasLayer { Layer = 20 };
        AddChild(_overlayCanvas);
        _keywordTooltip.Reparent(_overlayCanvas);
        _keywordTooltip.TopLevel = false;
        _keywordTooltip.ZIndex = 100;
        _keywordTooltip.MouseFilter = MouseFilterEnum.Ignore;

        _endTurnButton = GetNode<Button>("%EndTurnButton");
        SetupDragGuide();
        SetupEnemyQuickUi();

        SetupDropZoneStyles();

        _state = GetNode<GameState>("/root/GameState");
        if (_state.DeckCardIds.Count == 0)
        {
            _state.StartNewRun();
            _state.BeginEncounter(MapNodeType.NormalBattle);
        }

        _endTurnButton.Pressed += EndTurn;
        GetNode<Button>("%BackButton").Pressed += BackToMap;
        GetNode<Button>("%TestVictoryButton").Pressed += OnTestVictoryPressed;
        _handContainer.Resized += () => LayoutHandCards(false);

        SetupFromGameState();
        _playerPanelBasePos = _playerPanel.Position;
        _enemyDropAreaBasePos = _enemyDropArea.Position;
        _enemyDropAreaBaseScale = _enemyDropArea.Scale;
        _enemyShadowBaseSize = _enemyShadow.Size;
        _playerShadowBaseSize = _playerShadow.Size;

        Log("Battle start", "#cbd5e1");
        _ = StartBattleFlow();
    }

    public override void _Process(double delta)
    {
        if (_hitStopTimer > 0f)
        {
            _hitStopTimer -= (float)delta;
            return;
        }

        _animTime += (float)delta;

        var viewport = GetViewportRect().Size;
        if (viewport.X <= 1 || viewport.Y <= 1)
        {
            return;
        }

        var mouse = GetViewport().GetMousePosition();
        var nx = (mouse.X / viewport.X - 0.5f) * 2f;
        var ny = (mouse.Y / viewport.Y - 0.5f) * 2f;
        UpdateHandHoverFromMouse(mouse);

        if (IsInstanceValid(_dragGuide) && _dragGuide.Visible)
        {
            _dragGuidePulseTime += (float)delta;
            var pulse = 0.5f + 0.5f * Mathf.Sin(_dragGuidePulseTime * 9f);
            var alpha = 0.6f + pulse * 0.4f;
            var baseColor = _dragGuide.DefaultColor;
            _dragGuide.DefaultColor = new Color(baseColor.R, baseColor.G, baseColor.B, alpha);
            _dragArrowHead.DefaultColor = new Color(baseColor.R, baseColor.G, baseColor.B, alpha);
        }

        _arenaFarBg.Position = new Vector2(nx * -6f, ny * -4f);
        _arenaMidBg.Position = new Vector2(nx * -10f, ny * -6f);
        _arenaFrontFog.Position = new Vector2(nx * -14f, ny * -8f);

        var breathePlayer = Mathf.Sin(_animTime * 1.2f) * 2.2f;
        var breatheEnemy = Mathf.Sin(_animTime * 1.1f + 1.3f) * 2.8f;
        _playerPunchX = Mathf.Lerp(_playerPunchX, 0f, (float)delta * 16f);
        _enemyPunchX = Mathf.Lerp(_enemyPunchX, 0f, (float)delta * 16f);
        _playerPanel.Position = _playerPanelBasePos + new Vector2(_playerPunchX, breathePlayer);

        var enemyAnimOffset = Vector2.Zero;
        var enemyAnimScaleMul = 1f;
        switch (_enemyAnimState)
        {
            case EnemyAnimState.Hit:
                _enemyAnimTimer -= (float)delta;
                enemyAnimOffset = new Vector2(Mathf.Sin(_animTime * 60f) * 5f, -2f);
                enemyAnimScaleMul = 1.02f;
                if (_enemyAnimTimer <= 0f)
                {
                    _enemyAnimState = EnemyAnimState.Idle;
                }
                break;
            case EnemyAnimState.Dying:
                enemyAnimOffset = new Vector2(0f, 12f);
                enemyAnimScaleMul = 0.9f;
                break;
        }

        _enemyDropArea.Position = _enemyDropAreaBasePos + new Vector2(_enemyPunchX, breatheEnemy) + enemyAnimOffset;
        _enemyDropArea.Scale = _enemyDropAreaBaseScale * ((1f + Mathf.Sin(_animTime * 1.1f + 1.3f) * 0.01f) * enemyAnimScaleMul);

        var shadowScale = 1f + Mathf.Sin(_animTime * 1.1f + 1.3f) * 0.03f;
        _enemyShadow.Size = _enemyShadowBaseSize * shadowScale;
        _playerShadow.Size = _playerShadowBaseSize * (1f + Mathf.Sin(_animTime * 1.2f) * 0.02f);
    }

    private EnemyUnit CurrentEnemy
    {
        get
        {
            if (_enemies.Count == 0)
            {
                throw new InvalidOperationException("No enemies available.");
            }

            _selectedEnemyIndex = Mathf.Clamp(_selectedEnemyIndex, 0, _enemies.Count - 1);
            return _enemies[_selectedEnemyIndex];
        }
    }

    private int AliveEnemyCount()
    {
        var alive = 0;
        for (var i = 0; i < _enemies.Count; i++)
        {
            if (_enemies[i].IsAlive)
            {
                alive++;
            }
        }

        return alive;
    }

    private void SelectNextAliveEnemy()
    {
        if (_enemies.Count == 0)
        {
            return;
        }

        if (CurrentEnemy.IsAlive)
        {
            return;
        }

        for (var i = 0; i < _enemies.Count; i++)
        {
            if (_enemies[i].IsAlive)
            {
                _selectedEnemyIndex = i;
                return;
            }
        }
    }

    public override void _ExitTree()
    {
        foreach (var view in _cardViewPool)
        {
            if (IsInstanceValid(view))
            {
                view.QueueFree();
            }
        }
        _cardViewPool.Clear();
        _handViews.Clear();
    }

    private async Task StartBattleFlow()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _playerPanelBasePos = _playerPanel.Position;
        _enemyDropAreaBasePos = _enemyDropArea.Position;
        _enemyDropAreaBaseScale = _enemyDropArea.Scale;
        _enemyShadowBaseSize = _enemyShadow.Size;
        _playerShadowBaseSize = _playerShadow.Size;
        await PlayEnemyEntrance();
        await StartPlayerTurn();
    }

    private async Task PlayEnemyEntrance()
    {
        if (_enemyEntrancePlayed)
        {
            return;
        }

        _enemyEntrancePlayed = true;
        _enemyDropArea.Scale = _enemyDropAreaBaseScale * 0.86f;
        _enemyDropArea.Modulate = new Color(1, 1, 1, 0f);

        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(_enemyDropArea, "scale", _enemyDropAreaBaseScale, 0.22f);
        tween.Parallel().TweenProperty(_enemyDropArea, "modulate:a", 1f, 0.2f);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private void SetupDropZoneStyles()
    {
        _dropNormalStyle.BgColor = new Color("2b3445");
        _dropNormalStyle.BorderWidthLeft = 2;
        _dropNormalStyle.BorderWidthTop = 2;
        _dropNormalStyle.BorderWidthRight = 2;
        _dropNormalStyle.BorderWidthBottom = 2;
        _dropNormalStyle.BorderColor = new Color("4b5563");
        _dropNormalStyle.CornerRadiusTopLeft = 8;
        _dropNormalStyle.CornerRadiusTopRight = 8;
        _dropNormalStyle.CornerRadiusBottomLeft = 8;
        _dropNormalStyle.CornerRadiusBottomRight = 8;

        _dropHotStyle.BgColor = new Color("1f4d3b");
        _dropHotStyle.BorderWidthLeft = 2;
        _dropHotStyle.BorderWidthTop = 2;
        _dropHotStyle.BorderWidthRight = 2;
        _dropHotStyle.BorderWidthBottom = 2;
        _dropHotStyle.BorderColor = new Color("34d399");
        _dropHotStyle.CornerRadiusTopLeft = 8;
        _dropHotStyle.CornerRadiusTopRight = 8;
        _dropHotStyle.CornerRadiusBottomLeft = 8;
        _dropHotStyle.CornerRadiusBottomRight = 8;

        SetDropZoneHighlight(false);
    }

    private void SetupEnemyQuickUi()
    {
        var dropVBox = _enemyDropArea.GetNodeOrNull<VBoxContainer>("DropVBox");
        if (dropVBox != null)
        {
            _enemyRosterGrid = _enemyDropArea.GetNodeOrNull<GridContainer>("DropVBox/EnemyRosterGrid");
            if (_enemyRosterGrid == null)
            {
                _enemyRosterGrid = new GridContainer
                {
                    Name = "EnemyRosterGrid",
                    Columns = 2,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                };
                dropVBox.AddChild(_enemyRosterGrid);
                dropVBox.MoveChild(_enemyRosterGrid, 1);
            }
            _enemyRosterGrid.AddThemeConstantOverride("h_separation", 6);
            _enemyRosterGrid.AddThemeConstantOverride("v_separation", 6);
        }

        _enemyPanel.Visible = false;
        _dropHintLabel.Visible = false;

        var enemyInfo = _enemyPanel.GetNodeOrNull<VBoxContainer>("EnemyInfo");
        if (enemyInfo == null)
        {
            return;
        }

        _enemyIntentListLabel = new Label
        {
            Name = "EnemyIntentListLabel",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color("a5b4fc"),
            Text = "Intents: -",
            Visible = false
        };
        enemyInfo.AddChild(_enemyIntentListLabel);
    }

    private void SetupFromGameState()
    {
        _playerHp = _state.PlayerHp;
        _playerMaxHp = _state.MaxHp;
        _player.Name = "Player";

        _enemies.Clear();
        _selectedEnemyIndex = 0;
        _enemies.AddRange(EnemyEncounterBuilder.BuildEncounter(_state.PendingEncounterType, _state.Floor));
        _isElite = _state.PendingEncounterType == MapNodeType.EliteBattle;

        UpdateEnemySelectionUi();
        SyncEnemyVisualFromSelection();

        _drawPile.Clear();
        _discardPile.Clear();
        _hand.Clear();

        _drawPile.AddRange(_state.CreateDeckCards());
        DeckFlowResolver.ShuffleInPlace(_drawPile, _rng);
    }

    private async Task StartPlayerTurn()
    {
        if (_battleEnded)
        {
            return;
        }

        PushInputLock();
        ClearRelicTurnMarkers();
        await ShowTurnBanner("Player Turn", new Color("38bdf8"));

        var hasLantern = _state.HasRelic("lantern");
        var hasAnchor = _state.HasRelic("anchor");
        var turnStart = TurnFlowResolver.ResolvePlayerTurnStart(_turn, MaxEnergy, hasLantern, hasAnchor);

        _energy = turnStart.Energy;

        if (_state.HasRelic("ember_ring"))
        {
            _energy += 1;
            Log("Ember Ring grants +1 energy", "#fb923c");
            FlashRelic("ember_ring");
        }
        if (_turn == 1 && hasLantern)
        {
            Log("Lantern grants +1 energy", "#facc15");
            FlashRelic("lantern");
        }

        _playerBlock = turnStart.PlayerBlock;

        if (_state.HasRelic("iron_shell"))
        {
            _playerBlock += 3;
            Log("Iron Shell grants 3 block", "#93c5fd");
            FlashRelic("iron_shell");
        }
        if (_turn == 1 && hasAnchor)
        {
            Log("Anchor grants 8 block", "#60a5fa");
            FlashRelic("anchor");
        }

        await DrawCards(5);
        RollEnemyIntent();

        RefreshUi();
        PopInputLock();
    }

    private void RollEnemyIntent()
    {
        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (!enemy.IsAlive)
            {
                continue;
            }

            var intent = IntentResolver.RollEnemyIntent(enemy, _enemies, _isElite, _turn, _rng);
            enemy.IntentType = intent.Type;
            enemy.IntentValue = intent.Value;
        }

        Log($"Turn {_turn}: Enemy intents prepared", "#94a3b8");
    }

    private string IntentText(EnemyUnit enemy)
    {
        return enemy.IntentType switch
        {
            EnemyIntentType.Attack => $"Attack {enemy.IntentValue + enemy.Strength}",
            EnemyIntentType.Defend => $"Gain {enemy.IntentValue} Block",
            EnemyIntentType.Buff => $"Gain {enemy.IntentValue} Strength",
            _ => "-"
        };
    }

    private string IntentCompactText(EnemyUnit enemy)
    {
        return enemy.IntentType switch
        {
            EnemyIntentType.Attack => $"ATK {enemy.IntentValue + enemy.Strength}",
            EnemyIntentType.Defend => $"BLK {enemy.IntentValue}",
            EnemyIntentType.Buff => $"STR +{enemy.IntentValue}",
            _ => "-"
        };
    }

    private Color IntentTint(EnemyIntentType type)
    {
        return type switch
        {
            EnemyIntentType.Attack => new Color("fca5a5"),
            EnemyIntentType.Defend => new Color("93c5fd"),
            EnemyIntentType.Buff => new Color("d8b4fe"),
            _ => new Color("cbd5e1")
        };
    }

    private void UpdateEnemySelectionUi()
    {
        if (!IsInstanceValid(_enemyRosterGrid))
        {
            return;
        }

        _enemyRosterGrid.Columns = _enemies.Count >= 3 ? 3 : 2;
        _enemyCardTargetByIndex.Clear();
        _enemyCardButtonByIndex.Clear();
        foreach (Node child in _enemyRosterGrid.GetChildren())
        {
            _enemyRosterGrid.RemoveChild(child);
            child.QueueFree();
        }

        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            var enemyIndex = i;
            var targetMode = IsInstanceValid(_draggingCard) && CardRequiresEnemyTarget(_draggingCard.Card);
            var isHoveredTarget = targetMode && _hoverEnemyIndex == i;
            var isSelectedTarget = i == _selectedEnemyIndex;
            var selectableTarget = enemy.IsAlive && targetMode;

            var cardButton = _enemyCardScene.Instantiate<EnemyCardView>();
            var visual = CombatVisualCatalog.GetEnemyVisual(enemy.VisualId);
            _enemyRosterGrid.AddChild(cardButton);
            cardButton.Configure(
                enemy,
                IntentCompactText(enemy),
                IntentText(enemy),
                IntentTint(enemy.IntentType),
                LoadTextureCached(visual.PortraitPath),
                visual.StageTint,
                isSelectedTarget,
                isHoveredTarget,
                selectableTarget,
                IsInputLocked());

            cardButton.Pressed += () =>
            {
                _selectedEnemyIndex = enemyIndex;
                SyncEnemyVisualFromSelection();
                RefreshUi();
            };

            _enemyCardTargetByIndex[enemyIndex] = cardButton.EffectTarget();
            _enemyCardButtonByIndex[enemyIndex] = cardButton;
        }
    }

    private void SyncEnemyVisualFromSelection()
    {
        if (_enemies.Count == 0)
        {
            return;
        }

        SelectNextAliveEnemy();
    }

    private Control EnemyEffectTarget(int enemyIndex)
    {
        if (_enemyCardTargetByIndex.TryGetValue(enemyIndex, out var target) && IsInstanceValid(target))
        {
            return target;
        }

        return _enemyDropArea;
    }

    private bool TryGetEnemyIndexAt(Vector2 mouseGlobal, out int enemyIndex)
    {
        enemyIndex = -1;
        foreach (var kv in _enemyCardButtonByIndex)
        {
            var idx = kv.Key;
            var button = kv.Value;
            if (!_enemies[idx].IsAlive || !IsInstanceValid(button))
            {
                continue;
            }

            var hitRect = button.GetGlobalRect().Grow(-6f);
            if (_enemyCardTargetByIndex.TryGetValue(idx, out var target) && IsInstanceValid(target))
            {
                hitRect = target.GetGlobalRect().Grow(10f);
            }

            if (hitRect.HasPoint(mouseGlobal))
            {
                enemyIndex = idx;
                return true;
            }
        }

        return false;
    }

    private void EndTurn()
    {
        _ = EndTurnAsync();
    }

    private async Task EndTurnAsync()
    {
        if (_battleEnded || IsInputLocked())
        {
            return;
        }

        EmitUiSfx("turn_end");
        PushInputLock();

        TurnFlowResolver.MoveHandToDiscard(_hand, _discardPile);

        await RenderHand();

        await ShowTurnBanner("Enemy Turn", new Color("f87171"));

        // Enemy block expires when their turn begins.
        for (var i = 0; i < _enemies.Count; i++)
        {
            _enemies[i].Block = TurnFlowResolver.ResolveEnemyTurnStartBlock(_enemies[i].Block);
        }
        ExecuteEnemyTurn();
        if (_battleEnded)
        {
            RefreshUi();
            PopInputLock();
            return;
        }

        _turn += 1;
        TickStatuses();
        PopInputLock();
        await StartPlayerTurn();
    }

    private void ExecuteEnemyTurn()
    {
        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            if (!enemy.IsAlive)
            {
                continue;
            }

            switch (enemy.IntentType)
            {
                case EnemyIntentType.Attack:
                {
                    var resolution = CombatResolver.ResolveHit(
                        enemy.IntentValue,
                        enemy.Strength,
                        _playerVulnerable,
                        _playerBlock,
                        _playerHp);
                    _playerBlock = resolution.RemainingBlock;
                    _playerHp = resolution.RemainingHp;
                    Log($"{enemy.Name} attacks {resolution.FinalDamage}, blocked {resolution.Blocked}, took {resolution.Taken}", "#f87171");
                    if (resolution.Taken > 0)
                    {
                        TriggerHitStop(0.045f);
                        var playerEffectTarget = _playerCardView.EffectTarget();
                        SpawnFloatingText(playerEffectTarget, $"-{resolution.Taken}", new Color("fca5a5"));
                        SpawnSlashEffect(playerEffectTarget, new Color("fecaca"));
                        FlashPanel(_playerPanel, new Color(1f, 0.5f, 0.5f, 1f));
                        PunchPanel(_playerPanel, -8f);
                        PulseImpact(_playerPanel, 1.045f);
                    }

                    break;
                }
                case EnemyIntentType.Defend:
                    enemy.Block += enemy.IntentValue;
                    Log($"{enemy.Name} gains {enemy.IntentValue} Block", "#60a5fa");
                    SpawnFloatingText(_enemyPanel, $"+{enemy.IntentValue} Block", new Color("93c5fd"));
                    if (i == _selectedEnemyIndex)
                    {
                        SpawnShieldEffect(EnemyEffectTarget(i), new Color("93c5fd"));
                    }
                    break;
                case EnemyIntentType.Buff:
                    enemy.Strength += enemy.IntentValue;
                    Log($"{enemy.Name} gains {enemy.IntentValue} Strength", "#c084fc");
                    SpawnFloatingText(_enemyPanel, $"+{enemy.IntentValue} STR", new Color("d8b4fe"));
                    if (i == _selectedEnemyIndex)
                    {
                        SpawnRuneEffect(EnemyEffectTarget(i), new Color("d8b4fe"));
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (_playerHp <= 0)
            {
                _playerHp = 0;
                _battleEnded = true;
                Log("Defeat", "#ef4444");
                GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
                return;
            }
        }
    }

    private void TickStatuses()
    {
        _playerVulnerable = Math.Max(_playerVulnerable - 1, 0);
        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            enemy.Vulnerable = Math.Max(enemy.Vulnerable - 1, 0);
        }
    }

    private async Task DrawCards(int count)
    {
        var drawResult = DeckFlowResolver.DrawIntoHand(
            _drawPile,
            _discardPile,
            _hand,
            count,
            HandLimit,
            _rng);

        if (drawResult.HandLimitReached)
        {
            Log("Hand is full", "#f59e0b");
        }
        for (var i = 0; i < drawResult.ReshuffleCount; i++)
        {
            Log("Shuffled discard into draw pile", "#94a3b8");
        }

        var entering = new HashSet<CardData>(drawResult.DrawnCards);

        await RenderHand(entering);
    }

    private async Task<bool> TrySpendAndApplyCard(CardData card)
    {
        if (_battleEnded || !_hand.Contains(card))
        {
            return false;
        }

        if (CardRequiresEnemyTarget(card) && !CurrentEnemy.IsAlive)
        {
            Log("Target is already defeated", "#f59e0b");
            return false;
        }

        if (card.Cost > _energy)
        {
            Log($"Not enough energy for {card.Name}", "#f59e0b");
            return false;
        }

        _energy -= card.Cost;

        var relicAttackBonus = _state.HasRelic("whetstone") ? 1 : 0;
        var effectExecutor = new BattleCardEffectExecutor(this, relicAttackBonus);
        var effectResult = CardEffectPipeline.Execute(card, effectExecutor);

        _hand.Remove(card);
        _discardPile.Add(card);

        if (effectResult.DrawCount > 0)
        {
            Log($"Play {card.Name}: draw {effectResult.DrawCount}", "#93c5fd");
            await DrawCards(effectResult.DrawCount);
        }
        else
        {
            await RenderHand();
        }

        if (CurrentEnemy.Hp <= 0)
        {
            CurrentEnemy.Hp = 0;
            Log($"{CurrentEnemy.Name} defeated", "#34d399");
            SelectNextAliveEnemy();
            SyncEnemyVisualFromSelection();
            if (AliveEnemyCount() <= 0)
            {
                await OnVictoryAsync();
                return true;
            }
        }

        RefreshUi();
        return true;
    }

    private void OnCardDropAttempt(CardView view, Vector2 mouseGlobal)
    {
        _ = OnCardDropAttemptAsync(view, mouseGlobal);
    }

    private async Task OnCardDropAttemptAsync(CardView view, Vector2 mouseGlobal)
    {
        _draggingCard = null;
        _hoverEnemyIndex = -1;
        view.LockPositionWhileDragging = false;
        SetDragGuideVisible(false);

        if (_battleEnded || IsInputLocked())
        {
            SetDropZoneHighlight(false);
            UpdateEnemySelectionUi();
            if (IsInstanceValid(view))
            {
                await view.AnimateBackToHand();
            }

            LayoutHandCards(true);
            return;
        }

        SetDropZoneHighlight(false);

        var requiresEnemyTarget = CardRequiresEnemyTarget(view.Card);
        if (!requiresEnemyTarget)
        {
            PushInputLock();
            var playedSelf = await TrySpendAndApplyCard(view.Card);
            if (!playedSelf && IsInstanceValid(view))
            {
                EmitUiSfx("card_cancel");
                await view.AnimateBackToHand();
                LayoutHandCards(true);
            }
            else if (playedSelf)
            {
                EmitUiSfx("card_play");
            }
            PopInputLock();
            return;
        }

        if (!TryGetEnemyIndexAt(mouseGlobal, out var targetEnemyIndex))
        {
            var selectedAlive = _selectedEnemyIndex >= 0
                && _selectedEnemyIndex < _enemies.Count
                && _enemies[_selectedEnemyIndex].IsAlive;
            var droppedInEnemyZone = _enemyDropArea.GetGlobalRect().Grow(8f).HasPoint(mouseGlobal);
            if (selectedAlive && droppedInEnemyZone)
            {
                targetEnemyIndex = _selectedEnemyIndex;
            }
            else
            {
                EmitUiSfx("card_cancel");
                UpdateEnemySelectionUi();
                await view.AnimateBackToHand();
                LayoutHandCards(true);
                return;
            }
        }

        _selectedEnemyIndex = targetEnemyIndex;
        SyncEnemyVisualFromSelection();
        UpdateEnemySelectionUi();

        if (!CurrentEnemy.IsAlive)
        {
            Log("Select a living enemy target", "#f59e0b");
            EmitUiSfx("error");
            UpdateEnemySelectionUi();
            await view.AnimateBackToHand();
            LayoutHandCards(true);
            return;
        }

        if (view.Card.Cost > _energy)
        {
            Log($"Not enough energy for {view.Card.Name}", "#f59e0b");
            EmitUiSfx("error");
            UpdateEnemySelectionUi();
            await view.AnimateBackToHand();
            LayoutHandCards(true);
            return;
        }

        PushInputLock();

        var dropRect = EnemyEffectTarget(_selectedEnemyIndex).GetGlobalRect();
        var fromPos = view.GlobalPosition + view.Size * 0.5f;
        var target = new Vector2(
            dropRect.Position.X + dropRect.Size.X * 0.5f - view.Size.X * 0.5f,
            dropRect.Position.Y + dropRect.Size.Y * 0.5f - view.Size.Y * 0.5f);

        await view.AnimateToTarget(target);

        SpawnCardTrail(fromPos, target + view.Size * 0.5f);
        var played = await TrySpendAndApplyCard(view.Card);
        if (!played && IsInstanceValid(view))
        {
            EmitUiSfx("card_cancel");
            await view.AnimateBackToHand();
            LayoutHandCards(true);
        }
        else if (played)
        {
            EmitUiSfx("card_play");
        }
        PopInputLock();
    }

    private void OnCardClicked(CardView view)
    {
        _ = OnCardClickedAsync(view);
    }

    private async Task OnCardClickedAsync(CardView view)
    {
        if (_battleEnded || IsInputLocked())
        {
            return;
        }

        if (CardRequiresEnemyTarget(view.Card))
        {
            if (!CurrentEnemy.IsAlive)
            {
                Log("Select a living enemy target", "#f59e0b");
                return;
            }
            Log($"Drag {view.Card.Name} to enemy to play", "#94a3b8");
            EmitUiSfx("ui_hint");
            if (IsInstanceValid(view))
            {
                await view.AnimateBackToHand(0.08f);
            }
            return;
        }

        PushInputLock();
        var toRect = _enemyDropArea.GetGlobalRect();
        var fromPos = view.GlobalPosition + view.Size * 0.5f;
        var toPos = new Vector2(toRect.Position.X + toRect.Size.X * 0.5f, toRect.Position.Y + toRect.Size.Y * 0.5f);
        if (view.Card.Cost <= _energy)
        {
            SpawnCardTrail(fromPos, toPos);
        }
        if (!await TrySpendAndApplyCard(view.Card) && IsInstanceValid(view))
        {
            EmitUiSfx("card_cancel");
            await view.AnimateBackToHand();
            LayoutHandCards(true);
        }
        else
        {
            EmitUiSfx("card_play");
        }

        PopInputLock();
    }

    private void OnCardDragMoved(CardView card, Vector2 mouseGlobal)
    {
        if (!CardRequiresEnemyTarget(card.Card))
        {
            SetDropZoneHighlight(false);
            SetDragGuideVisible(false);
            return;
        }

        var previousHover = _hoverEnemyIndex;
        var hot = TryGetEnemyIndexAt(mouseGlobal, out var hoverEnemyIndex);
        if (hot && hoverEnemyIndex != _selectedEnemyIndex)
        {
            _selectedEnemyIndex = hoverEnemyIndex;
            SyncEnemyVisualFromSelection();
        }
        _hoverEnemyIndex = hot ? hoverEnemyIndex : -1;

        if (previousHover != _hoverEnemyIndex || hot)
        {
            UpdateEnemySelectionUi();
        }

        SetDropZoneHighlight(hot);
        UpdateDragGuide(card, mouseGlobal);
    }

    private void OnCardDragStarted(CardView card)
    {
        _hoveredCard = null;
        _draggingCard = card;
        _hoverEnemyIndex = -1;
        var requiresTarget = CardRequiresEnemyTarget(card.Card);
        card.LockPositionWhileDragging = requiresTarget;
        HideKeywordTooltip();
        SetDropZoneHighlight(false);
        SetDragGuideVisible(requiresTarget);
        if (requiresTarget)
        {
            UpdateDragGuide(card, card.GlobalPosition + card.Size * 0.5f);
            card.Scale = new Vector2(1.1f, 1.1f);
            UpdateEnemySelectionUi();
        }
        EmitUiSfx("card_grab");
    }

    private void OnCardDragEnded(CardView card)
    {
        _draggingCard = null;
        _hoverEnemyIndex = -1;
        card.LockPositionWhileDragging = false;
        SetDragGuideVisible(false);
        SetDropZoneHighlight(false);
        UpdateEnemySelectionUi();
    }

    private void OnCardHoverChanged(CardView card, bool hovered)
    {
        // Hover is driven by mouse-position sampling in _Process to avoid
        // enter/exit thrash while cards scale and overlap.
    }

    private bool CardRequiresEnemyTarget(CardData card)
    {
        return card.Effects.Any(effect =>
            (effect.Type == CardEffectType.Damage || effect.Type == CardEffectType.ApplyVulnerable)
            && effect.Target == CardEffectTarget.SelectedEnemy);
    }

    private sealed class BattleCardEffectExecutor : ICardEffectRuntime
    {
        private readonly BattleScene _scene;
        private readonly int _relicAttackBonus;

        public BattleCardEffectExecutor(BattleScene scene, int relicAttackBonus)
        {
            _scene = scene;
            _relicAttackBonus = relicAttackBonus;
        }

        public void ExecuteDamage(CardData card, CardEffectData effect)
        {
            _scene.ResolveDamageCardEffect(card, effect, _relicAttackBonus);
        }

        public void ExecuteGainBlock(CardData card, CardEffectData effect)
        {
            _scene.ApplyGainBlockEffect(card, effect);
        }

        public void ExecuteApplyVulnerable(CardData card, CardEffectData effect)
        {
            _scene.ResolveApplyVulnerableEffect(card, effect);
        }

        public void ExecuteGainStrength(CardData card, CardEffectData effect)
        {
            _scene.ApplyGainStrengthEffect(card, effect);
        }

        public void ExecuteGainEnergy(CardData card, CardEffectData effect)
        {
            _scene.ApplyGainEnergyEffect(card, effect);
        }

        public void ExecuteHeal(CardData card, CardEffectData effect)
        {
            _scene.ApplyHealEffect(card, effect);
        }
    }

    private void ApplyGainBlockEffect(CardData card, CardEffectData effect)
    {
        if (effect.Target != CardEffectTarget.Player || effect.Amount <= 0)
        {
            return;
        }

        _playerBlock += effect.Amount;
        Log($"Play {card.Name}: gain {effect.Amount} Block", "#60a5fa");
        var playerEffectTarget = _playerCardView.EffectTarget();
        SpawnFloatingText(playerEffectTarget, $"+{effect.Amount}", new Color("93c5fd"));
        SpawnShieldEffect(playerEffectTarget, new Color("93c5fd"));
    }


    private void ApplyGainStrengthEffect(CardData card, CardEffectData effect)
    {
        if (effect.Target != CardEffectTarget.Player || effect.Amount <= 0)
        {
            return;
        }

        _playerStrength += effect.Amount;
        var playerEffectTarget = _playerCardView.EffectTarget();
        Log($"Play {card.Name}: gain {effect.Amount} Strength", "#c084fc");
        SpawnFloatingText(playerEffectTarget, $"STR+{effect.Amount}", new Color("d8b4fe"));
        SpawnRuneEffect(playerEffectTarget, new Color("d8b4fe"));
    }

    private void ApplyGainEnergyEffect(CardData card, CardEffectData effect)
    {
        if (effect.Target != CardEffectTarget.Player || effect.Amount <= 0)
        {
            return;
        }

        _energy += effect.Amount;
        var playerEffectTarget = _playerCardView.EffectTarget();
        Log($"Play {card.Name}: gain {effect.Amount} Energy", "#fde68a");
        SpawnFloatingText(playerEffectTarget, $"EN+{effect.Amount}", new Color("fde68a"));
    }

    private void ApplyHealEffect(CardData card, CardEffectData effect)
    {
        if (effect.Target != CardEffectTarget.Player || effect.Amount <= 0)
        {
            return;
        }

        var before = _playerHp;
        _playerHp = Math.Min(_playerHp + effect.Amount, _playerMaxHp);
        var gained = _playerHp - before;
        if (gained <= 0)
        {
            return;
        }

        var playerEffectTarget = _playerCardView.EffectTarget();
        Log($"Play {card.Name}: heal {gained}", "#86efac");
        SpawnFloatingText(playerEffectTarget, $"+{gained} HP", new Color("86efac"));
    }

    private void ResolveDamageCardEffect(CardData card, CardEffectData effect, int relicAttackBonus)
    {
        if (effect.Amount <= 0)
        {
            return;
        }

        if (effect.Target == CardEffectTarget.AllEnemies)
        {
            for (var enemyIndex = 0; enemyIndex < _enemies.Count; enemyIndex++)
            {
                if (_enemies[enemyIndex].IsAlive)
                {
                    ApplyDamageToEnemy(enemyIndex, card, effect, relicAttackBonus);
                }
            }
            return;
        }

        ApplyDamageToEnemy(_selectedEnemyIndex, card, effect, relicAttackBonus);
    }

    private void ResolveApplyVulnerableEffect(CardData card, CardEffectData effect)
    {
        if (effect.Amount <= 0)
        {
            return;
        }

        if (effect.Target == CardEffectTarget.AllEnemies)
        {
            for (var enemyIndex = 0; enemyIndex < _enemies.Count; enemyIndex++)
            {
                if (_enemies[enemyIndex].IsAlive)
                {
                    ApplyVulnerableToEnemy(enemyIndex, card.Name, effect.Amount);
                }
            }
            return;
        }

        ApplyVulnerableToEnemy(_selectedEnemyIndex, card.Name, effect.Amount);
    }

    private void ApplyDamageToEnemy(int enemyIndex, CardData card, CardEffectData effect, int relicAttackBonus)
    {
        var targetEnemy = _enemies[enemyIndex];
        if (!targetEnemy.IsAlive)
        {
            return;
        }

        var strength = effect.UseAttackerStrength ? _playerStrength : 0;
        var vulnerable = effect.UseTargetVulnerable ? targetEnemy.Vulnerable : 0;
        var flatBonus = effect.FlatBonus + relicAttackBonus;

        var resolution = CombatResolver.ResolveHit(
            effect.Amount,
            strength,
            vulnerable,
            targetEnemy.Block,
            targetEnemy.Hp,
            flatBonus);

        targetEnemy.Block = resolution.RemainingBlock;
        targetEnemy.Hp = resolution.RemainingHp;

        var effectTarget = EnemyEffectTarget(enemyIndex);
        Log($"Play {card.Name}: damage {resolution.FinalDamage} ({resolution.Taken} HP)", "#f87171");
        if (resolution.Taken > 0)
        {
            TriggerHitStop(0.045f);
            SpawnFloatingText(effectTarget, $"-{resolution.Taken}", new Color("fda4af"));
            SpawnSlashEffect(effectTarget, new Color("fda4af"));
            TriggerEnemyHit();
            FlashPanel(effectTarget, new Color(1f, 0.55f, 0.55f, 1f));
            PunchPanel(effectTarget, 8f);
            PulseImpact(effectTarget, 1.05f);
        }
    }

    private void ApplyVulnerableToEnemy(int enemyIndex, string cardName, int amount)
    {
        var enemy = _enemies[enemyIndex];
        if (!enemy.IsAlive)
        {
            return;
        }

        enemy.Vulnerable += amount;
        var effectTarget = EnemyEffectTarget(enemyIndex);

        Log($"Play {cardName}: apply {amount} Vulnerable", "#c084fc");
        SpawnFloatingText(effectTarget, $"VUL+{amount}", new Color("d8b4fe"));
        SpawnRuneEffect(effectTarget, new Color("d8b4fe"));
    }

    private void SetDropZoneHighlight(bool highlight)
    {
        if (_dropZoneHighlighted == highlight)
        {
            return;
        }

        _dropZoneHighlighted = highlight;
        if (!IsInstanceValid(_enemyRosterGrid))
        {
            _enemyDropArea.AddThemeStyleboxOverride("panel", highlight ? _dropHotStyle : _dropNormalStyle);
        }
        _dropHintLabel.Modulate = highlight ? new Color("bbf7d0") : new Color("d1d5db");
    }

    private void FlashPanel(Control panel, Color flashColor)
    {
        if (!IsInstanceValid(panel))
        {
            return;
        }

        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Quad);
        tween.TweenProperty(panel, "modulate", flashColor, 0.07f);
        tween.TweenProperty(panel, "modulate", Colors.White, 0.16f);
    }

    private void TriggerEnemyHit()
    {
        if (_enemyAnimState == EnemyAnimState.Dying)
        {
            return;
        }

        _enemyAnimState = EnemyAnimState.Hit;
        _enemyAnimTimer = 0.14f;
    }

    private async Task TriggerEnemyDeath()
    {
        if (_enemyAnimState == EnemyAnimState.Dying)
        {
            return;
        }

        _enemyAnimState = EnemyAnimState.Dying;

        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(_enemyDropArea, "modulate:a", 0.15f, 0.28f);
        tween.Parallel().TweenProperty(_enemyDropArea, "scale", _enemyDropAreaBaseScale * 0.78f, 0.28f);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private void SpawnSlashEffect(Control target, Color color)
    {
        var rect = new ColorRect
        {
            Color = color,
            Size = new Vector2(86, 10),
            TopLevel = true,
            ZIndex = 180
        };
        _effectsLayer.AddChild(rect);

        var area = target.GetGlobalRect();
        rect.GlobalPosition = new Vector2(area.Position.X + area.Size.X * 0.5f - 43f, area.Position.Y + area.Size.Y * 0.45f);
        rect.RotationDegrees = -22f;

        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Quad);
        tween.TweenProperty(rect, "scale", new Vector2(1.3f, 1f), 0.12f);
        tween.Parallel().TweenProperty(rect, "modulate:a", 0f, 0.18f);
        tween.Finished += () =>
        {
            if (IsInstanceValid(rect))
            {
                rect.QueueFree();
            }
        };
    }

    private void SpawnShieldEffect(Control target, Color color)
    {
        var ring = new ColorRect
        {
            Color = new Color(color.R, color.G, color.B, 0.36f),
            Size = new Vector2(58, 58),
            TopLevel = true,
            ZIndex = 180
        };
        _effectsLayer.AddChild(ring);

        var area = target.GetGlobalRect();
        ring.GlobalPosition = new Vector2(area.Position.X + area.Size.X * 0.5f - 29f, area.Position.Y + area.Size.Y * 0.5f - 29f);

        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(ring, "scale", new Vector2(1.5f, 1.5f), 0.22f);
        tween.Parallel().TweenProperty(ring, "modulate:a", 0f, 0.22f);
        tween.Finished += () =>
        {
            if (IsInstanceValid(ring))
            {
                ring.QueueFree();
            }
        };
    }

    private void SpawnRuneEffect(Control target, Color color)
    {
        var rune = new Label
        {
            Text = "✦",
            Modulate = color,
            TopLevel = true,
            ZIndex = 180
        };
        _effectsLayer.AddChild(rune);

        var area = target.GetGlobalRect();
        rune.GlobalPosition = new Vector2(area.Position.X + area.Size.X * 0.5f - 6f, area.Position.Y + area.Size.Y * 0.5f - 8f);

        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Quad);
        tween.TweenProperty(rune, "global_position", rune.GlobalPosition + new Vector2(0f, -28f), 0.26f);
        tween.Parallel().TweenProperty(rune, "modulate:a", 0f, 0.26f);
        tween.Finished += () =>
        {
            if (IsInstanceValid(rune))
            {
                rune.QueueFree();
            }
        };
    }

    private void PunchPanel(Control panel, float offsetX)
    {
        if (panel == _playerPanel)
        {
            _playerPunchX += offsetX;
            return;
        }

        _enemyPunchX += offsetX;
    }

    private async void ShakeMain(float intensity, int steps)
    {
        if (!IsInstanceValid(_mainMargin))
        {
            return;
        }

        var original = _mainMargin.Position;
        for (var i = 0; i < steps; i++)
        {
            var x = (float)(_rng.NextDouble() * 2.0 - 1.0) * intensity;
            var y = (float)(_rng.NextDouble() * 2.0 - 1.0) * intensity * 0.5f;
            _mainMargin.Position = original + new Vector2(x, y);
            await ToSignal(GetTree().CreateTimer(0.012f), SceneTreeTimer.SignalName.Timeout);
        }

        _mainMargin.Position = original;
        // Cards are positioned in global space; after screen shake, force a re-layout
        // so they snap back to the correct fan positions.
        LayoutHandCards(false);
    }

    private async Task ShowTurnBanner(string text, Color tint)
    {
        _turnBannerLabel.Text = text;
        _turnBanner.Modulate = new Color(tint, 0f);
        _turnBanner.Visible = true;
        _turnBanner.Position = new Vector2(_turnBanner.Position.X, 20);

        var tweenIn = CreateTween();
        tweenIn.SetEase(Tween.EaseType.Out);
        tweenIn.SetTrans(Tween.TransitionType.Cubic);
        tweenIn.TweenProperty(_turnBanner, "position:y", 34f, 0.15f);
        tweenIn.Parallel().TweenProperty(_turnBanner, "modulate:a", 1f, 0.15f);
        await ToSignal(tweenIn, Tween.SignalName.Finished);

        await ToSignal(GetTree().CreateTimer(0.18f), SceneTreeTimer.SignalName.Timeout);

        var tweenOut = CreateTween();
        tweenOut.SetEase(Tween.EaseType.Out);
        tweenOut.SetTrans(Tween.TransitionType.Cubic);
        tweenOut.TweenProperty(_turnBanner, "modulate:a", 0f, 0.2f);
        await ToSignal(tweenOut, Tween.SignalName.Finished);
        _turnBanner.Visible = false;
    }

    private void SpawnFloatingText(Control target, string text, Color color)
    {
        var label = new Label
        {
            Text = text,
            Modulate = color,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TopLevel = true,
            ZIndex = 200
        };

        _effectsLayer.AddChild(label);

        var targetRect = target.GetGlobalRect();
        var start = new Vector2(targetRect.Position.X + targetRect.Size.X * 0.5f - 40f, targetRect.Position.Y + 18f);
        label.GlobalPosition = start;

        var isDamage = text.StartsWith("-");
        var value = 0;
        if (isDamage)
        {
            int.TryParse(text.Replace("-", string.Empty), out value);
        }
        var isCritStyle = isDamage && value >= 12;
        label.Scale = isCritStyle ? new Vector2(1.25f, 1.25f) : Vector2.One;
        label.AddThemeFontSizeOverride("font_size", isCritStyle ? 28 : (isDamage ? 23 : 20));
        if (isCritStyle)
        {
            label.AddThemeColorOverride("font_color", new Color("fecaca"));
        }
        else if (isDamage)
        {
            label.AddThemeColorOverride("font_color", new Color("fca5a5"));
        }
        else if (text.Contains("Block", StringComparison.OrdinalIgnoreCase) || text.StartsWith("+"))
        {
            label.AddThemeColorOverride("font_color", new Color("93c5fd"));
        }

        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Back);
        tween.TweenProperty(label, "scale", label.Scale * (isCritStyle ? 1.15f : 1.07f), 0.12f);
        tween.Parallel().TweenProperty(label, "global_position", start + new Vector2(0f, -18f), 0.12f);
        tween.TweenProperty(label, "scale", isCritStyle ? new Vector2(1.12f, 1.12f) : Vector2.One, 0.1f);
        tween.Parallel().TweenProperty(label, "global_position", start + new Vector2(0f, -42f), 0.34f);
        tween.Parallel().TweenProperty(label, "modulate:a", 0f, 0.34f);
        tween.Finished += () =>
        {
            if (IsInstanceValid(label))
            {
                label.QueueFree();
            }
        };
    }

    private void SpawnCardTrail(Vector2 from, Vector2 to)
    {
        var dir = to - from;
        var len = Math.Max(dir.Length(), 1f);
        var trail = new ColorRect
        {
            Color = new Color("7dd3fc"),
            Size = new Vector2(len, 3),
            TopLevel = true,
            ZIndex = 170
        };
        _effectsLayer.AddChild(trail);
        trail.GlobalPosition = from;
        trail.Rotation = dir.Angle();

        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Quad);
        tween.TweenProperty(trail, "modulate:a", 0f, 0.18f);
        tween.Parallel().TweenProperty(trail, "scale:y", 0.2f, 0.18f);
        tween.Finished += () =>
        {
            if (IsInstanceValid(trail))
            {
                trail.QueueFree();
            }
        };
    }

    private void PulseImpact(Control target, float peakScale)
    {
        if (!IsInstanceValid(target))
        {
            return;
        }

        var originalScale = target.Scale;
        target.PivotOffset = target.Size * 0.5f;

        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(target, "scale", originalScale * peakScale, 0.06f);
        tween.TweenProperty(target, "scale", originalScale, 0.11f);
    }

    private async Task OnVictoryAsync()
    {
        _battleEnded = true;
        PushInputLock();
        await TriggerEnemyDeath();
        Log("Victory", "#22c55e");

        _state.PlayerHp = _playerHp;
        var hpBeforeResolve = _state.PlayerHp;
        var hpAfterCharm = _state.HasRelic("charm") ? Math.Min(hpBeforeResolve + 5, _state.MaxHp) : hpBeforeResolve;
        var charmHeal = hpAfterCharm - hpBeforeResolve;
        var hpAfterBloodVial = _state.HasRelic("blood_vial") ? Math.Min(hpAfterCharm + 2, _state.MaxHp) : hpAfterCharm;
        var bloodVialHeal = hpAfterBloodVial - hpAfterCharm;

        _state.ResolveBattleVictory();

        if (charmHeal > 0)
        {
            Log($"Lucky Charm heals {charmHeal} HP", "#86efac");
            FlashRelic("charm");
        }

        if (bloodVialHeal > 0)
        {
            Log($"Blood Vial heals {bloodVialHeal} HP", "#fca5a5");
            FlashRelic("blood_vial");
        }

        GetTree().ChangeSceneToFile("res://Scenes/RewardScene.tscn");
    }

    private async Task RenderHand(HashSet<CardData> entering = null)
    {
        var previousViews = new Dictionary<CardData, CardView>(_handViews);
        _handViews.Clear();
        var entrants = new List<CardView>();

        for (var i = 0; i < _hand.Count; i++)
        {
            var card = _hand[i];
            CardView cardView;
            if (previousViews.TryGetValue(card, out var existingView) && IsInstanceValid(existingView))
            {
                cardView = existingView;
                previousViews.Remove(card);
            }
            else
            {
                cardView = AcquireCardView();
            }

            cardView.Setup(card);
            cardView.SetPlayable(!IsInputLocked() && card.Cost <= _energy);
            if (cardView.GetParent() != _handContainer)
            {
                _handContainer.AddChild(cardView);
            }
            _handContainer.MoveChild(cardView, i);
            _handViews[card] = cardView;

            if (entering != null && entering.Contains(card))
            {
                entrants.Add(cardView);
            }
        }

        foreach (var stale in previousViews.Values)
        {
            RecycleCardView(stale);
        }
        previousViews.Clear();

        if (IsInstanceValid(_hoveredCard) && !_handViews.ContainsValue(_hoveredCard))
        {
            _hoveredCard = null;
            HideKeywordTooltip();
        }

        LayoutHandCards(false);

        if (entrants.Count == 0)
        {
            return;
        }

        var drawRect = _drawAnchor.GetGlobalRect();
        var basePos = new Vector2(drawRect.Position.X + drawRect.Size.X * 0.5f, drawRect.Position.Y + drawRect.Size.Y * 0.5f);
        var tasks = new List<Task>();
        for (var i = 0; i < entrants.Count; i++)
        {
            var offset = new Vector2(i * 8f, -i * 6f);
            tasks.Add(AnimateDrawEntry(entrants[i], basePos + offset, i));
        }

        await Task.WhenAll(tasks);
        LayoutHandCards(true);
    }

    private CardView AcquireCardView()
    {
        CardView cardView;
        while (_cardViewPool.Count > 0)
        {
            cardView = _cardViewPool.Pop();
            if (!IsInstanceValid(cardView))
            {
                continue;
            }

            cardView.Visible = true;
            cardView.PrepareForReuse();
            return cardView;
        }

        cardView = new CardView();
        cardView.DropAttempted += OnCardDropAttempt;
        cardView.Clicked += OnCardClicked;
        cardView.DragMoved += OnCardDragMoved;
        cardView.DragStarted += OnCardDragStarted;
        cardView.DragEnded += OnCardDragEnded;
        cardView.HoverChanged += OnCardHoverChanged;
        return cardView;
    }

    private void RecycleCardView(CardView cardView)
    {
        if (!IsInstanceValid(cardView))
        {
            return;
        }

        if (_hoveredCard == cardView)
        {
            _hoveredCard = null;
            HideKeywordTooltip();
        }

        cardView.PrepareForReuse();
        cardView.Visible = false;
        if (cardView.GetParent() == _handContainer)
        {
            _handContainer.RemoveChild(cardView);
        }
        _cardViewPool.Push(cardView);
    }

    private void LayoutHandCards(bool animate)
    {
        var cards = new List<CardView>();
        foreach (Node node in _handContainer.GetChildren())
        {
            if (node is CardView card)
            {
                cards.Add(card);
            }
        }

        var count = cards.Count;
        if (count == 0)
        {
            HideKeywordTooltip();
            return;
        }

        CardView hovered = null;
        if (IsInstanceValid(_hoveredCard))
        {
            hovered = _hoveredCard;
        }

        var hoveredIndex = -1;
        var anyDragging = false;
        for (var i = 0; i < count; i++)
        {
            if (cards[i].IsDragging)
            {
                anyDragging = true;
            }
            if (hovered != null && cards[i] == hovered && !cards[i].IsDragging)
            {
                hoveredIndex = i;
            }
        }

        var width = _handContainer.Size.X;
        var baseY = _handContainer.GlobalPosition.Y + _handContainer.Size.Y - 230f;
        var spread = Mathf.Min(1000f, 170f * Math.Max(count - 1, 1));
        var startX = _handContainer.GlobalPosition.X + width * 0.5f - spread * 0.5f - 90f;

        for (var i = 0; i < count; i++)
        {
            var t = count == 1 ? 0.5f : i / (float)(count - 1);
            var x = startX + spread * t;
            var normalized = t * 2f - 1f;
            var centerDepth = 1f - Mathf.Abs(normalized);
            var curveY = -(1f - Mathf.Pow(Mathf.Abs(normalized), 1.35f)) * 22f;
            var rot = normalized * 13f;

            if (!anyDragging && hoveredIndex >= 0)
            {
                var distance = Math.Abs(i - hoveredIndex);
                var direction = i < hoveredIndex ? -1f : 1f;
                if (i == hoveredIndex)
                {
                    curveY += 2f;
                    rot *= 0.22f;
                }
                else if (distance == 1)
                {
                    x += direction * 20f;
                    rot *= 0.65f;
                    curveY -= 2f;
                }
                else if (distance == 2)
                {
                    x += direction * 8f;
                }
            }

            var pos = new Vector2(x, baseY + curveY);
            var scale = Vector2.One;
            var finalRot = rot;

            if (!anyDragging && hoveredIndex == i)
            {
                pos.Y -= 30f;
                scale = new Vector2(1.08f, 1.08f);
                finalRot = 0f;
            }

            cards[i].ZIndex = hoveredIndex == i ? 2000 : i;
            cards[i].SetPose(pos, finalRot, scale, animate);
            cards[i].SetFocusState(hoveredIndex == i, hoveredIndex >= 0 && hoveredIndex != i && !anyDragging);
        }
    }

    private void UpdateHandHoverFromMouse(Vector2 mouseGlobal)
    {
        if (!IsInstanceValid(_handContainer) || _battleEnded || IsInputLocked() || _draggingCard != null)
        {
            SetHoveredCard(null);
            return;
        }

        var handRect = _handContainer.GetGlobalRect().Grow(20f);
        if (!handRect.HasPoint(mouseGlobal))
        {
            SetHoveredCard(null);
            return;
        }

        var cards = new List<CardView>();
        foreach (Node node in _handContainer.GetChildren())
        {
            if (node is CardView card && !card.IsDragging)
            {
                cards.Add(card);
            }
        }
        if (cards.Count == 0)
        {
            SetHoveredCard(null);
            return;
        }

        CardView best = null;
        var bestScore = float.MaxValue;
        foreach (var card in cards)
        {
            var rect = card.GetGlobalRect().Grow(8f);
            if (!rect.HasPoint(mouseGlobal))
            {
                continue;
            }

            var center = card.GlobalPosition + card.Size * 0.5f;
            var score = center.DistanceTo(mouseGlobal);
            if (score < bestScore)
            {
                bestScore = score;
                best = card;
            }
        }

        if (best == null)
        {
            SetHoveredCard(null);
            return;
        }

        if (IsInstanceValid(_hoveredCard) && _hoveredCard == best)
        {
            return;
        }

        if (IsInstanceValid(_hoveredCard))
        {
            var currentCenter = _hoveredCard.GlobalPosition + _hoveredCard.Size * 0.5f;
            var currentScore = currentCenter.DistanceTo(mouseGlobal);
            if (currentScore <= bestScore + HoverSwitchDeadzone)
            {
                return;
            }
        }

        SetHoveredCard(best);
    }

    private void SetHoveredCard(CardView next)
    {
        if (_hoveredCard == next)
        {
            return;
        }

        _hoveredCard = next;
        if (IsInstanceValid(_hoveredCard))
        {
            EmitUiSfx("card_hover");
            ShowKeywordTooltip(_hoveredCard);
        }
        else
        {
            HideKeywordTooltip();
        }

        RequestHandLayout(true);
    }

    private async void OnTestVictoryPressed()
    {
        if (_battleEnded)
        {
            return;
        }

        Log("[TEST] Trigger instant victory", "#facc15");
        await OnVictoryAsync();
    }

    private void BackToMap()
    {
        if (IsInputLocked())
        {
            return;
        }

        _state.PlayerHp = _playerHp;
        GetTree().ChangeSceneToFile("res://Scenes/MapScene.tscn");
    }

    private void RefreshUi()
    {
        if (_enemies.Count == 0)
        {
            return;
        }

        SelectNextAliveEnemy();
        SyncEnemyVisualFromSelection();
        var enemy = CurrentEnemy;

        _player.Hp = _playerHp;
        _player.MaxHp = _playerMaxHp;
        _player.Block = _playerBlock;
        _player.Strength = _playerStrength;
        _player.Vulnerable = _playerVulnerable;
        _playerCardView.Configure(_player, IsInputLocked());

        _enemyNameLabel.Text = $"{enemy.Name} ({_selectedEnemyIndex + 1}/{_enemies.Count})";
        _enemyHpLabel.Text = $"Enemy HP: {enemy.Hp}";
        _enemyBlockLabel.Text = $"Enemy Block: {enemy.Block}";
        _enemyStatusLabel.Text = $"Enemy Status: STR {enemy.Strength}, VUL {enemy.Vulnerable}";
        _enemyIntentLabel.Text = _battleEnded || !enemy.IsAlive ? "Intent: -" : $"Intent: {IntentText(enemy)}";
        if (IsInstanceValid(_enemyIntentListLabel))
        {
            var intentParts = new List<string>();
            for (var i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                var status = e.IsAlive ? IntentText(e) : "Defeated";
                intentParts.Add($"{i + 1}.{e.Name}: {status}");
            }

            _enemyIntentListLabel.Text = $"Intents: {string.Join(" | ", intentParts)}";
        }
        _topHpLabel.Text = $"HP {_playerHp}/{_playerMaxHp}";
        _turnLabel.Text = $"Turn {_turn}";
        _energyLabel.Text = $"Energy {_energy}/{MaxEnergy}";
        _handCountLabel.Text = $"Hand: {_hand.Count} | Draw: {_drawPile.Count} | Discard: {_discardPile.Count} | Enemies: {AliveEnemyCount()}";
        _relicBarLabel.Text = BuildRelicBarText();
        RefreshRelicIcons();
        UpdateEnemySelectionUi();
        UpdateInputControls();
        RefreshCardPlayableStates();
    }

    private string BuildRelicBarText()
    {
        if (_state.RelicIds.Count == 0)
        {
            return "Relics: None";
        }

        var names = _state.RelicIds.Select(id => RelicData.CreateById(id).Name);
        return $"Relics: {string.Join(" | ", names)}";
    }

    private void RefreshRelicIcons()
    {
        var signature = string.Join("|", _state.RelicIds);
        if (signature == _relicUiSignature)
        {
            return;
        }

        _relicUiSignature = signature;

        foreach (Node child in _relicIcons.GetChildren())
        {
            child.QueueFree();
        }
        _relicChipById.Clear();
        _relicTriggerDotById.Clear();
        foreach (var relicId in _state.RelicIds)
        {
            var relic = RelicData.CreateById(relicId);
            var chip = new PanelContainer
            {
                CustomMinimumSize = new Vector2(48, 28),
                TooltipText = relic.ToRelicText()
            };
            var chipStyle = new StyleBoxFlat
            {
                BgColor = new Color("1b2936"),
                BorderColor = new Color("6ea0c8"),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6
            };
            chip.AddThemeStyleboxOverride("panel", chipStyle);

            var icon = new TextureRect
            {
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
                Texture = LoadTextureCached(CombatVisualCatalog.GetRelicIconPath(relicId))
            };
            chip.AddChild(icon);

            var triggerDot = new ColorRect
            {
                Color = new Color("facc15"),
                CustomMinimumSize = new Vector2(8, 8),
                Size = new Vector2(8, 8),
                Visible = _triggeredRelicsThisTurn.Contains(relicId)
            };
            triggerDot.Position = new Vector2(38, 2);
            chip.AddChild(triggerDot);

            _relicIcons.AddChild(chip);
            _relicChipById[relicId] = chip;
            _relicTriggerDotById[relicId] = triggerDot;
        }
    }

    private void ShowKeywordTooltip(CardView card)
    {
        var lines = new List<string>();
        if (card.Card.Damage > 0)
        {
            lines.Add("[color=#fda4af]Damage[/color]: reduced by enemy Block.");
        }

        if (card.Card.Block > 0)
        {
            lines.Add("[color=#93c5fd]Block[/color]: prevents incoming damage this turn.");
        }

        if (card.Card.ApplyVulnerable > 0)
        {
            lines.Add("[color=#e9d5ff]Vulnerable[/color]: target takes 50% more damage.");
        }

        if (card.Card.DrawCount > 0)
        {
            lines.Add("[color=#a5f3fc]Draw[/color]: draw extra cards now.");
        }

        if (card.Card.HasEffect(CardEffectType.GainStrength))
        {
            lines.Add("[color=#d8b4fe]Strength[/color]: increases your attack damage.");
        }

        if (card.Card.HasEffect(CardEffectType.GainEnergy))
        {
            lines.Add("[color=#fde68a]Energy[/color]: adds extra energy this turn.");
        }

        if (card.Card.HasEffect(CardEffectType.Heal))
        {
            lines.Add("[color=#86efac]Heal[/color]: restore HP, up to max HP.");
        }

        if (lines.Count == 0)
        {
            lines.Add("[color=#cbd5e1]No keywords.[/color]");
        }

        _keywordTooltipText.Text = string.Join("\n", lines);
        var pos = card.GlobalPosition + new Vector2(card.Size.X + 12f, -10f);
        var viewport = GetViewportRect().Size;
        var tipSize = _keywordTooltip.Size;
        if (tipSize.X < 10f || tipSize.Y < 10f)
        {
            tipSize = new Vector2(270f, 150f);
        }
        pos.X = Mathf.Clamp(pos.X, 8f, viewport.X - tipSize.X - 8f);
        pos.Y = Mathf.Clamp(pos.Y, 8f, viewport.Y - tipSize.Y - 8f);
        _keywordTooltip.Position = pos;
        _keywordTooltip.Visible = true;
    }

    private void HideKeywordTooltip()
    {
        _keywordTooltip.Visible = false;
    }

    private void FlashRelic(string relicId)
    {
        if (!_relicChipById.TryGetValue(relicId, out var chip) || !IsInstanceValid(chip))
        {
            return;
        }

        _triggeredRelicsThisTurn.Add(relicId);
        if (_relicTriggerDotById.TryGetValue(relicId, out var dot) && IsInstanceValid(dot))
        {
            dot.Visible = true;
        }

        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(chip, "modulate", new Color("fef08a"), 0.08f);
        tween.TweenProperty(chip, "modulate", Colors.White, 0.18f);
    }

    private void ClearRelicTurnMarkers()
    {
        if (_triggeredRelicsThisTurn.Count == 0)
        {
            return;
        }

        _triggeredRelicsThisTurn.Clear();
        foreach (var kv in _relicTriggerDotById)
        {
            if (IsInstanceValid(kv.Value))
            {
                kv.Value.Visible = false;
            }
        }
    }

    private Texture2D LoadTextureCached(string path)
    {
        if (_iconCache.TryGetValue(path, out var texture) && IsInstanceValid(texture))
        {
            return texture;
        }

        var loaded = GD.Load<Texture2D>(path);
        if (loaded != null)
        {
            _iconCache[path] = loaded;
            return loaded;
        }

        return GD.Load<Texture2D>("res://icon.svg");
    }

    private void Log(string line, string colorHex = "#cbd5e1")
    {
        _logText.AppendText($"[color={colorHex}]{line}[/color]\n");
        _logText.ScrollToLine(Math.Max(_logText.GetLineCount() - 1, 0));
    }

    private bool IsInputLocked()
    {
        return _battleEnded || _inputLockDepth > 0;
    }

    private void PushInputLock()
    {
        _inputLockDepth += 1;
        UpdateInputControls();
    }

    private void PopInputLock()
    {
        _inputLockDepth = Math.Max(0, _inputLockDepth - 1);
        UpdateInputControls();
    }

    private void UpdateInputControls()
    {
        _endTurnButton.Disabled = IsInputLocked();
        RefreshCardPlayableStates();
    }

    private void TriggerHitStop(float duration)
    {
        _hitStopTimer = Math.Max(_hitStopTimer, duration);
    }

    private void SetupDragGuide()
    {
        _dragGuide = new Line2D
        {
            Width = 3f,
            DefaultColor = new Color("7dd3fc"),
            Antialiased = true,
            TopLevel = true,
            Visible = false,
            ZIndex = 1000
        };
        _dragArrowHead = new Line2D
        {
            Width = 3f,
            DefaultColor = new Color("7dd3fc"),
            Antialiased = true,
            TopLevel = true,
            Visible = false,
            ZIndex = 1001
        };
        _overlayCanvas.AddChild(_dragGuide);
        _overlayCanvas.AddChild(_dragArrowHead);
    }

    private void SetDragGuideVisible(bool visible)
    {
        if (!IsInstanceValid(_dragGuide) || !IsInstanceValid(_dragArrowHead))
        {
            return;
        }

        _dragGuide.Visible = visible;
        _dragArrowHead.Visible = visible;
        if (!visible)
        {
            _dragGuide.ClearPoints();
            _dragArrowHead.ClearPoints();
            _dragGuidePulseTime = 0f;
        }
    }

    private void UpdateDragGuide(CardView card, Vector2 mouseGlobal)
    {
        if (!IsInstanceValid(card) || !IsInstanceValid(_dragGuide) || !IsInstanceValid(_dragArrowHead) || !CardRequiresEnemyTarget(card.Card))
        {
            SetDragGuideVisible(false);
            return;
        }

        var cardCenter = card.GlobalPosition + card.Size * 0.5f;
        var target = mouseGlobal;
        var active = TryGetEnemyIndexAt(mouseGlobal, out var hoverEnemyIndex);
        if (active)
        {
            var rect = EnemyEffectTarget(hoverEnemyIndex).GetGlobalRect();
            target = rect.Position + rect.Size * 0.5f;
        }
        var controlLift = Mathf.Clamp(cardCenter.DistanceTo(target) * 0.22f, 54f, 140f);
        var control = (cardCenter + target) * 0.5f + new Vector2(0f, -controlLift);

        _dragGuide.ClearPoints();
        const int segments = 20;
        for (var i = 0; i <= segments; i++)
        {
            var t = i / (float)segments;
            var a = cardCenter.Lerp(control, t);
            var b = control.Lerp(target, t);
            var p = a.Lerp(b, t);
            _dragGuide.AddPoint(p);
        }

        var color = active ? new Color("86efac") : new Color("7dd3fc");
        _dragGuide.DefaultColor = color;
        _dragGuide.Width = active ? 4.5f : 3f;

        var tHead = 0.94f;
        var headA = cardCenter.Lerp(control, tHead);
        var headB = control.Lerp(target, tHead);
        var from = headA.Lerp(headB, tHead);
        var tangent = (target - from).Normalized();
        if (tangent.Length() < 0.01f)
        {
            tangent = Vector2.Right;
        }
        var normal = new Vector2(-tangent.Y, tangent.X);
        var headLen = active ? 20f : 16f;
        var headWidth = active ? 11f : 9f;
        var tip = target;
        var left = tip - tangent * headLen + normal * headWidth;
        var right = tip - tangent * headLen - normal * headWidth;

        _dragArrowHead.ClearPoints();
        _dragArrowHead.AddPoint(left);
        _dragArrowHead.AddPoint(tip);
        _dragArrowHead.AddPoint(right);
        _dragArrowHead.DefaultColor = color;
        _dragArrowHead.Width = active ? 4.5f : 3f;
        _dragGuide.Visible = true;
        _dragArrowHead.Visible = true;
    }

    private async Task AnimateDrawEntry(CardView card, Vector2 from, int index)
    {
        var delay = 0.026f * index;
        if (delay > 0f)
        {
            await ToSignal(GetTree().CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);
        }

        EmitUiSfx("card_draw");
        await card.AnimateFromDraw(from);
    }

    private void EmitUiSfx(string cue)
    {
        UiSfxRequested(cue);
    }

    private void RefreshCardPlayableStates()
    {
        foreach (Node node in _handContainer.GetChildren())
        {
            if (node is CardView cardView)
            {
                cardView.SetPlayable(!IsInputLocked() && cardView.Card.Cost <= _energy);
            }
        }
    }

    private void RequestHandLayout(bool animate)
    {
        if (animate)
        {
            LayoutHandCards(true);
            return;
        }

        if (_deferredHandLayoutPending)
        {
            return;
        }

        _deferredHandLayoutPending = true;
        CallDeferred(nameof(DeferredHandLayout));
    }

    private void DeferredHandLayout()
    {
        _deferredHandLayoutPending = false;
        LayoutHandCards(false);
    }

}
