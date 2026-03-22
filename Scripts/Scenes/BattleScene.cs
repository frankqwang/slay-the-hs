using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

[Tool]
public partial class BattleScene : Control
{
    private readonly record struct HandCardPose(Vector2 LocalPosition, float RotationDegrees, Vector2 Scale, int ZIndex);
    private readonly record struct HandDebugMarker(Vector2 CardCenter, Vector2 PivotCenter, int Index);

    private sealed class HandDebugSnapshot
    {
        public Rect2 HandRect { get; init; }
        public Vector2 CenterTop { get; init; }
        public Vector2 CenterBottom { get; init; }
        public Vector2 PivotCenter { get; init; }
        public Vector2[] ArcPoints { get; init; } = Array.Empty<Vector2>();
        public List<HandDebugMarker> Markers { get; init; } = new();
        public string Info { get; init; } = string.Empty;
    }

    [Tool]
    private sealed partial class HandDebugOverlay : Control
    {
        private HandDebugSnapshot _snapshot = null;

        public void SetSnapshot(HandDebugSnapshot snapshot)
        {
            _snapshot = snapshot;
            Visible = snapshot != null;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_snapshot == null)
            {
                return;
            }

            DrawRect(_snapshot.HandRect, new Color(0.35f, 0.9f, 1f, 0.12f), false, 2f);
            DrawLine(_snapshot.CenterTop, _snapshot.CenterBottom, new Color(0.2f, 1f, 0.45f, 0.9f), 2f);
            DrawCircle(_snapshot.PivotCenter, 5f, new Color(0.2f, 1f, 0.45f, 1f));
            if (_snapshot.ArcPoints.Length >= 2)
            {
                DrawPolyline(_snapshot.ArcPoints, new Color(0.2f, 0.95f, 1f, 0.95f), 2f);
            }

            var font = ThemeDB.FallbackFont;
            var fontSize = 12;
            foreach (var marker in _snapshot.Markers)
            {
                DrawCircle(marker.CardCenter, 4f, new Color(1f, 0.28f, 0.28f, 1f));
                DrawCircle(marker.PivotCenter, 4f, new Color(1f, 0.85f, 0.2f, 1f));
                DrawLine(marker.CardCenter, marker.PivotCenter, new Color(1f, 0.75f, 0.2f, 0.55f), 1.5f);
                if (font != null)
                {
                    DrawString(font, marker.PivotCenter + new Vector2(8f, -8f), marker.Index.ToString(), HorizontalAlignment.Left, -1f, fontSize, new Color(1f, 1f, 1f, 0.95f));
                }
            }

            if (font != null && !string.IsNullOrEmpty(_snapshot.Info))
            {
                DrawString(font, _snapshot.HandRect.Position + new Vector2(10f, 18f), _snapshot.Info, HorizontalAlignment.Left, -1f, fontSize, new Color(0.92f, 0.98f, 1f, 0.95f));
            }
        }
    }

    private enum EnemyAnimState
    {
        Idle,
        Hit,
        Dying
    }

    private const int MaxEnergy = 3;
    private const int HandLimit = 10;
    private const int HoveredHandZIndex = 100;

    [ExportGroup("Hand Layout")]
    [Export(PropertyHint.Range, "0,60,0.5")]
    private float _handArcAngleMinDegrees = 9f;
    [Export(PropertyHint.Range, "0,60,0.5")]
    private float _handArcAngleMaxDegrees = 17f;
    [Export(PropertyHint.Range, "200,3000,1")]
    private float _handArcRadiusMin = 720f;
    [Export(PropertyHint.Range, "200,3000,1")]
    private float _handArcRadiusMax = 1080f;
    [Export(PropertyHint.Range, "-20,20,0.1")]
    private float _handArcPhaseOffsetDegrees = 0f;
    [Export(PropertyHint.Range, "-400,400,1")]
    private float _handArcCenterOffsetX = 0f;
    [Export(PropertyHint.Range, "-80,120,1")]
    private float _handBottomPadding = 6f;
    [Export(PropertyHint.Range, "0.5,1.2,0.01")]
    private float _handPivotYOffsetFactor = 0.92f;
    [Export(PropertyHint.Range, "0,80,1")]
    private float _handHoverLift = 34f;
    [Export(PropertyHint.Range, "0,80,1")]
    private float _handHoverNeighborPush = 24f;
    [Export(PropertyHint.Range, "0,40,1")]
    private float _handHoverSecondaryPush = 10f;

    [ExportGroup("Editor Preview")]
    [Export]
    private bool _editorPreviewHand = true;
    [Export(PropertyHint.Range, "1,10,1")]
    private int _editorPreviewCardCount = 5;
    [Export]
    private string _editorPreviewCardIds = "meteor_shower,reaper_touch,bone_shrapnel,soul_siphon,phoenix_cycle";

    [ExportGroup("Hand Debug")]
    [Export]
    private bool _showHandDebugOverlay = false;

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
    private Button _backButton = null!;
    private Button _testVictoryButton = null!;
    private Button _settingsButton = null!;
    private Control _settingsModal = null!;
    private OptionButton _resolutionOption = null!;
    private OptionButton _maxFpsOption = null!;
    private CheckBox _vsyncCheckBox = null!;
    private CheckBox _fpsCounterCheckBox = null!;
    private HSlider _masterVolumeSlider = null!;
    private HSlider _musicVolumeSlider = null!;
    private Label _settingsTitle = null!;
    private Label _resolutionLabel = null!;
    private Label _maxFpsLabel = null!;
    private Label _vsyncLabel = null!;
    private Label _fpsCounterLabelText = null!;
    private Label _masterVolumeLabel = null!;
    private Label _musicVolumeLabel = null!;
    private Button _settingsCloseButton = null!;
    private Label _logTitleLabel = null!;
    private readonly int[] _fpsCaps = { 0, 30, 60, 120, 144, 165, 240 };
    private List<Vector2I> _windowSizes = new();

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
    private HandDebugOverlay _handDebugOverlay = null!;

    private CardView _hoveredCard = null!;
    private GameState _state = null!;
    public Action<string> UiSfxRequested = _ => { };
    private bool IsFastMode => _state != null && _state.ExternalFastMode;
    private const float HoverSwitchDeadzone = 18f;
    private string _editorPreviewSignature = string.Empty;
    private bool _editorPreviewLayoutPending;
    private bool _editorPreviewRefreshPending;

    public override void _Ready()
    {
        EnsureOverlayCanvas();
        EnsureHandDebugOverlay();
        if (Engine.IsEditorHint())
        {
            SetupEditorPreview();
            return;
        }

        GetNode<GameState>("/root/GameState").SetUiPhase("battle");
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
        _keywordTooltip.Reparent(_overlayCanvas);
        _keywordTooltip.TopLevel = false;
        _keywordTooltip.ZIndex = 100;
        _keywordTooltip.MouseFilter = MouseFilterEnum.Ignore;

        _endTurnButton = GetNode<Button>("%EndTurnButton");
        _backButton = GetNode<Button>("%BackButton");
        _testVictoryButton = GetNode<Button>("%TestVictoryButton");
        _settingsButton = GetNode<Button>("%SettingsButton");
        _settingsModal = GetNode<Control>("%SettingsModal");
        _resolutionOption = GetNode<OptionButton>("%ResolutionOption");
        _maxFpsOption = GetNode<OptionButton>("%MaxFpsOption");
        _vsyncCheckBox = GetNode<CheckBox>("%VsyncCheckBox");
        _fpsCounterCheckBox = GetNode<CheckBox>("%FpsCounterCheckBox");
        _masterVolumeSlider = GetNode<HSlider>("%MasterVolumeSlider");
        _musicVolumeSlider = GetNode<HSlider>("%MusicVolumeSlider");
        _settingsTitle = GetNode<Label>("%SettingsTitle");
        _resolutionLabel = GetNode<Label>("%ResolutionLabel");
        _maxFpsLabel = GetNode<Label>("%MaxFpsLabel");
        _vsyncLabel = GetNode<Label>("%VsyncLabel");
        _fpsCounterLabelText = GetNode<Label>("%FpsCounterLabelText");
        _masterVolumeLabel = GetNode<Label>("%MasterVolumeLabel");
        _musicVolumeLabel = GetNode<Label>("%MusicVolumeLabel");
        _settingsCloseButton = GetNode<Button>("%SettingsCloseButton");
        _logTitleLabel = GetNode<Label>("LogOverlay/LogMargin/LogVBox/LogTitle");

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
        _settingsButton.Pressed += OnOpenSettingsPressed;
        _settingsCloseButton.Pressed += OnCloseSettingsPressed;
        SetupSettingsUi();
        _backButton.Pressed += BackToMap;
        _testVictoryButton.Pressed += OnTestVictoryPressed;
        _handContainer.Resized += () => LayoutHandCards(false);
        LocalizationSettings.LanguageChanged += OnLanguageChanged;

        SetupFromGameState();
        _playerPanelBasePos = _playerPanel.Position;
        _enemyDropAreaBasePos = _enemyDropArea.Position;
        _enemyDropAreaBaseScale = _enemyDropArea.Scale;
        _enemyShadowBaseSize = _enemyShadow.Size;
        _playerShadowBaseSize = _playerShadow.Size;

        RefreshBattleStaticText();
        Log(LocalizationService.Get("log.battle.start", "Battle start"), "#cbd5e1");
        _ = StartBattleFlow();
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
        {
            UpdateEditorPreview();
            RequestEditorPreviewLayout();
            RefreshHandDebugOverlay();
            return;
        }

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

        RefreshHandDebugOverlay();
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

    private void SetupSettingsUi()
    {
        PopulateResolutionOptions();
        PopulateMaxFpsOptions();

        _resolutionOption.ItemSelected += OnResolutionSelected;
        _maxFpsOption.ItemSelected += OnMaxFpsSelected;
        _vsyncCheckBox.Toggled += OnVsyncToggled;
        _fpsCounterCheckBox.Toggled += OnFpsCounterToggled;
        _masterVolumeSlider.ValueChanged += OnMasterVolumeChanged;
        _musicVolumeSlider.ValueChanged += OnMusicVolumeChanged;

        var settings = AppSettings.Instance;
        _vsyncCheckBox.ButtonPressed = settings.VSyncEnabled;
        _fpsCounterCheckBox.ButtonPressed = settings.ShowFpsCounter;
        _masterVolumeSlider.Value = settings.MasterVolumePercent;
        _musicVolumeSlider.Value = settings.MusicVolumePercent;

        _settingsModal.Visible = false;
        RefreshSettingsText();
    }

    private void RefreshSettingsText()
    {
        _settingsButton.Text = LocalizationService.Get("ui.battle.settings", "Settings");
        _settingsTitle.Text = LocalizationService.Get("ui.battle.settings", "Settings");
        _resolutionLabel.Text = LocalizationService.Get("ui.battle.settings_resolution", "Resolution");
        _maxFpsLabel.Text = LocalizationService.Get("ui.battle.settings_max_fps", "Max FPS");
        _vsyncLabel.Text = LocalizationService.Get("ui.battle.settings_vsync", "VSync");
        _fpsCounterLabelText.Text = LocalizationService.Get("ui.battle.settings_fps_counter", "Show FPS");
        _masterVolumeLabel.Text = LocalizationService.Get("ui.battle.settings_master_volume", "Master Volume");
        _musicVolumeLabel.Text = LocalizationService.Get("ui.battle.settings_music_volume", "Music Volume");
        _settingsCloseButton.Text = LocalizationService.Get("ui.common.close", "Close");
        if (_maxFpsOption.ItemCount > 0)
        {
            _maxFpsOption.SetItemText(0, LocalizationService.Get("ui.options.max_fps.unlimited", "Unlimited"));
        }
    }

    private void PopulateResolutionOptions()
    {
        _resolutionOption.Clear();
        _windowSizes = BuildSupportedResolutionList();
        for (var i = 0; i < _windowSizes.Count; i++)
        {
            var size = _windowSizes[i];
            _resolutionOption.AddItem($"{size.X} x {size.Y}", i);
        }

        var selectedIndex = _windowSizes.FindIndex(s => s == AppSettings.Instance.WindowSize);
        _resolutionOption.Select(selectedIndex >= 0 ? selectedIndex : 0);
    }

    private void PopulateMaxFpsOptions()
    {
        _maxFpsOption.Clear();
        for (var i = 0; i < _fpsCaps.Length; i++)
        {
            var cap = _fpsCaps[i];
            _maxFpsOption.AddItem(cap <= 0 ? "Unlimited" : cap.ToString(), i);
        }

        var currentCap = AppSettings.Instance.MaxFps;
        var index = Array.IndexOf(_fpsCaps, currentCap);
        _maxFpsOption.Select(index >= 0 ? index : 0);
    }

    private static List<Vector2I> BuildSupportedResolutionList()
    {
        var screen = DisplayServer.WindowGetCurrentScreen();
        var screenSize = DisplayServer.ScreenGetSize(screen);
        var presets = new[]
        {
            new Vector2I(1024, 576), new Vector2I(1152, 648), new Vector2I(1280, 720),
            new Vector2I(1280, 800), new Vector2I(1366, 768), new Vector2I(1600, 900),
            new Vector2I(1920, 1080), new Vector2I(2560, 1440), new Vector2I(3440, 1440), new Vector2I(3840, 2160)
        };

        var unique = new HashSet<Vector2I>();
        foreach (var preset in presets)
        {
            if (preset.X <= screenSize.X && preset.Y <= screenSize.Y)
            {
                unique.Add(preset);
            }
        }

        unique.Add(AppSettings.Instance.WindowSize);

        return unique.OrderBy(s => s.X * s.Y).ThenBy(s => s.X).ThenBy(s => s.Y).ToList();
    }

    private void OnOpenSettingsPressed()
    {
        _settingsModal.Visible = true;
        RefreshSettingsText();
    }

    private void OnCloseSettingsPressed()
    {
        _settingsModal.Visible = false;
    }

    private void OnResolutionSelected(long index)
    {
        if (index < 0 || index >= _windowSizes.Count)
        {
            return;
        }

        AppSettings.Instance.SetWindowSize(_windowSizes[(int)index]);
    }

    private void OnMaxFpsSelected(long index)
    {
        if (index < 0 || index >= _fpsCaps.Length)
        {
            return;
        }

        AppSettings.Instance.SetMaxFps(_fpsCaps[(int)index]);
    }

    private void OnVsyncToggled(bool enabled)
    {
        AppSettings.Instance.SetVSyncEnabled(enabled);
    }

    private void OnFpsCounterToggled(bool enabled)
    {
        AppSettings.Instance.SetShowFpsCounter(enabled);
    }

    private void OnMasterVolumeChanged(double value)
    {
        AppSettings.Instance.SetMasterVolumePercent((float)value);
    }

    private void OnMusicVolumeChanged(double value)
    {
        AppSettings.Instance.SetMusicVolumePercent((float)value);
    }

    private void RefreshBattleStaticText()
    {
        RefreshSettingsText();
        _endTurnButton.Text = LocalizationService.Get("ui.battle.end_turn", "End Turn");
        _backButton.Text = LocalizationService.Get("ui.battle.back_to_map", "Back To Map");
        _testVictoryButton.Text = LocalizationService.Get("ui.battle.test_victory_button", "Test Victory");
        _logTitleLabel.Text = LocalizationService.Get("ui.battle.log_title", "Action Log");
        _turnBannerLabel.Text = LocalizationService.Get("ui.battle.turn_player", "Player Turn");
    }

    private void OnLanguageChanged()
    {
        RefreshBattleStaticText();
        RefreshUi();
    }

    public override void _ExitTree()
    {
        if (Engine.IsEditorHint())
        {
            ClearEditorPreviewHand();
            return;
        }

        LocalizationSettings.LanguageChanged -= OnLanguageChanged;
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

        if (IsFastMode)
        {
            _enemyDropArea.Scale = _enemyDropAreaBaseScale;
            _enemyDropArea.Modulate = Colors.White;
            return;
        }

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
            Text = $"{LocalizationService.Get("ui.battle.intents", "Intents")}: -",
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
        await ShowTurnBanner(LocalizationService.Get("ui.battle.turn_player", "Player Turn"), new Color("38bdf8"));

        var hasLantern = _state.HasRelic("lantern");
        var hasAnchor = _state.HasRelic("anchor");
        var turnStart = TurnFlowResolver.ResolvePlayerTurnStart(_turn, MaxEnergy, hasLantern, hasAnchor);

        _energy = turnStart.Energy;

        if (_state.HasRelic("ember_ring"))
        {
            _energy += 1;
            Log(LocalizationService.Get("log.battle.ember_ring", "Ember Ring grants +1 energy"), "#fb923c");
            FlashRelic("ember_ring");
        }
        if (_turn == 1 && hasLantern)
        {
            Log(LocalizationService.Get("log.battle.lantern", "Lantern grants +1 energy"), "#facc15");
            FlashRelic("lantern");
        }

        _playerBlock = turnStart.PlayerBlock;

        if (_state.HasRelic("iron_shell"))
        {
            _playerBlock += 3;
            Log(LocalizationService.Get("log.battle.iron_shell", "Iron Shell grants 3 block"), "#93c5fd");
            FlashRelic("iron_shell");
        }
        if (_turn == 1 && hasAnchor)
        {
            Log(LocalizationService.Get("log.battle.anchor", "Anchor grants 8 block"), "#60a5fa");
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

        Log(LocalizationService.Format("log.battle.turn_intents_prepared", "Turn {0}: Enemy intents prepared", _turn), "#94a3b8");
    }

    private string IntentText(EnemyUnit enemy)
    {
        return enemy.IntentType switch
        {
            EnemyIntentType.Attack => LocalizationService.Format("ui.battle.intent.attack", "Attack {0}", enemy.IntentValue + enemy.Strength),
            EnemyIntentType.Defend => LocalizationService.Format("ui.battle.intent.defend", "Gain {0} Block", enemy.IntentValue),
            EnemyIntentType.Buff => LocalizationService.Format("ui.battle.intent.strength", "Gain {0} Strength", enemy.IntentValue),
            _ => "-"
        };
    }

    private string IntentCompactText(EnemyUnit enemy)
    {
        return enemy.IntentType switch
        {
            EnemyIntentType.Attack => LocalizationService.Format("ui.battle.intent.attack_short", "ATK {0}", enemy.IntentValue + enemy.Strength),
            EnemyIntentType.Defend => LocalizationService.Format("ui.battle.intent.defend_short", "BLK {0}", enemy.IntentValue),
            EnemyIntentType.Buff => LocalizationService.Format("ui.battle.intent.buff_short", "STR +{0}", enemy.IntentValue),
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

        await ShowTurnBanner(LocalizationService.Get("ui.battle.turn_enemy", "Enemy Turn"), new Color("f87171"));

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
                    Log(LocalizationService.Format("log.battle.enemy_attack", "{0} attacks {1}, blocked {2}, took {3}", CombatVisualCatalog.GetLocalizedEnemyName(enemy.ArchetypeId, enemy.Name), resolution.FinalDamage, resolution.Blocked, resolution.Taken), "#f87171");
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
                    Log(LocalizationService.Format("log.battle.enemy_gain_block", "{0} gains {1} Block", CombatVisualCatalog.GetLocalizedEnemyName(enemy.ArchetypeId, enemy.Name), enemy.IntentValue), "#60a5fa");
                    SpawnFloatingText(_enemyPanel, $"+{enemy.IntentValue} Block", new Color("93c5fd"));
                    if (i == _selectedEnemyIndex)
                    {
                        SpawnShieldEffect(EnemyEffectTarget(i), new Color("93c5fd"));
                    }
                    break;
                case EnemyIntentType.Buff:
                    enemy.Strength += enemy.IntentValue;
                    Log(LocalizationService.Format("log.battle.enemy_gain_strength", "{0} gains {1} Strength", CombatVisualCatalog.GetLocalizedEnemyName(enemy.ArchetypeId, enemy.Name), enemy.IntentValue), "#c084fc");
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
                    Log(LocalizationService.Get("log.battle.defeat", "Defeat"), "#ef4444");
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
            Log(LocalizationService.Get("log.battle.hand_full", "Hand is full"), "#f59e0b");
        }
        for (var i = 0; i < drawResult.ReshuffleCount; i++)
        {
            Log(LocalizationService.Get("log.battle.shuffle_discard", "Shuffled discard into draw pile"), "#94a3b8");
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
            Log(LocalizationService.Get("log.battle.target_defeated", "Target is already defeated"), "#f59e0b");
            return false;
        }

        if (card.Cost > _energy)
        {
            Log(LocalizationService.Format("log.battle.not_enough_energy", "Not enough energy for {0}", card.GetLocalizedName()), "#f59e0b");
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
            Log(LocalizationService.Format("log.battle.play_draw", "Play {0}: draw {1}", card.GetLocalizedName(), effectResult.DrawCount), "#93c5fd");
            await DrawCards(effectResult.DrawCount);
        }
        else
        {
            await RenderHand();
        }

        if (CurrentEnemy.Hp <= 0)
        {
            CurrentEnemy.Hp = 0;
            Log(LocalizationService.Format("log.battle.enemy_defeated", "{0} defeated", CombatVisualCatalog.GetLocalizedEnemyName(CurrentEnemy.ArchetypeId, CurrentEnemy.Name)), "#34d399");
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
            var selfImpactCenter = view.GlobalPosition + view.Size * 0.5f;
            var playedSelf = await TrySpendAndApplyCard(view.Card);
            if (!playedSelf && IsInstanceValid(view))
            {
                EmitUiSfx("card_cancel");
                await view.AnimateBackToHand();
                LayoutHandCards(true);
            }
            else if (playedSelf)
            {
                SpawnCardPlayImpact(selfImpactCenter, view.Card.Kind);
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
            Log(LocalizationService.Get("log.battle.select_living_target", "Select a living enemy target"), "#f59e0b");
            EmitUiSfx("error");
            UpdateEnemySelectionUi();
            await view.AnimateBackToHand();
            LayoutHandCards(true);
            return;
        }

        if (view.Card.Cost > _energy)
        {
            Log(LocalizationService.Format("log.battle.not_enough_energy", "Not enough energy for {0}", view.Card.GetLocalizedName()), "#f59e0b");
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

        var impactCenter = target + view.Size * 0.5f;
        SpawnCardTrail(fromPos, impactCenter);
        var played = await TrySpendAndApplyCard(view.Card);
        if (!played && IsInstanceValid(view))
        {
            EmitUiSfx("card_cancel");
            await view.AnimateBackToHand();
            LayoutHandCards(true);
        }
        else if (played)
        {
            SpawnCardPlayImpact(impactCenter, view.Card.Kind);
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
                Log(LocalizationService.Get("log.battle.select_living_target", "Select a living enemy target"), "#f59e0b");
                return;
            }
            Log(LocalizationService.Format("log.battle.drag_to_play", "Drag {0} to enemy to play", view.Card.GetLocalizedName()), "#94a3b8");
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
        var playedClick = await TrySpendAndApplyCard(view.Card);
        if (!playedClick && IsInstanceValid(view))
        {
            EmitUiSfx("card_cancel");
            await view.AnimateBackToHand();
            LayoutHandCards(true);
        }
        else if (playedClick)
        {
            SpawnCardPlayImpact(toPos, view.Card.Kind);
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

    public BattleSnapshot BuildBattleSnapshot()
    {
        var snapshot = new BattleSnapshot
        {
            Turn = _turn,
            Energy = _energy,
            MaxEnergy = MaxEnergy,
            BattleEnded = _battleEnded,
            InputLocked = IsInputLocked(),
            DrawPileCount = _drawPile.Count,
            DiscardPileCount = _discardPile.Count,
            SelectedEnemyIndex = _selectedEnemyIndex,
            Player = new PlayerBattleSnapshot
            {
                Hp = _playerHp,
                MaxHp = _playerMaxHp,
                Block = _playerBlock,
                Strength = _playerStrength,
                Vulnerable = _playerVulnerable
            }
        };

        for (var i = 0; i < _hand.Count; i++)
        {
            var card = _hand[i];
            snapshot.Hand.Add(new CardSnapshot
            {
                HandIndex = i,
                CardId = card.Id,
                Name = card.Name,
                Cost = card.Cost,
                RequiresEnemyTarget = CardRequiresEnemyTarget(card),
                IsPlayable = !_battleEnded && !IsInputLocked() && card.Cost <= _energy,
                Description = card.GetLocalizedDescription()
            });
        }

        for (var i = 0; i < _enemies.Count; i++)
        {
            var enemy = _enemies[i];
            snapshot.Enemies.Add(new EnemyBattleSnapshot
            {
                EnemyIndex = i,
                ArchetypeId = enemy.ArchetypeId,
                Name = enemy.Name,
                Hp = enemy.Hp,
                MaxHp = enemy.MaxHp,
                Block = enemy.Block,
                Strength = enemy.Strength,
                Vulnerable = enemy.Vulnerable,
                IsAlive = enemy.IsAlive,
                IsSelected = i == _selectedEnemyIndex,
                IntentType = enemy.IntentType.ToString(),
                IntentValue = enemy.IntentValue,
                IntentText = _battleEnded || !enemy.IsAlive ? "-" : IntentText(enemy)
            });
        }

        return snapshot;
    }

    public List<LegalActionSnapshot> BuildLegalActions()
    {
        var actions = new List<LegalActionSnapshot>
        {
            new()
            {
                Kind = "start_new_run",
                Label = "Start a new map run"
            },
            new()
            {
                Kind = "start_battle_test_run",
                Label = "Start a battle test run"
            }
        };

        if (_battleEnded || IsInputLocked())
        {
            return actions;
        }

        actions.Add(new LegalActionSnapshot
        {
            Kind = "end_turn",
            Label = "End the current turn"
        });

        for (var i = 0; i < _hand.Count; i++)
        {
            var card = _hand[i];
            if (card.Cost > _energy)
            {
                continue;
            }

            var parameters = new Dictionary<string, object?>
            {
                ["handIndex"] = i,
                ["cardId"] = card.Id
            };
            var label = $"Play {card.Name} from hand index {i}";
            if (CardRequiresEnemyTarget(card))
            {
                var targets = new List<int>();
                for (var enemyIndex = 0; enemyIndex < _enemies.Count; enemyIndex++)
                {
                    if (_enemies[enemyIndex].IsAlive)
                    {
                        targets.Add(enemyIndex);
                    }
                }

                parameters["targetEnemyIndices"] = targets;
                if (targets.Count == 0)
                {
                    continue;
                }
            }

            actions.Add(new LegalActionSnapshot
            {
                Kind = "play_card",
                Label = label,
                Parameters = parameters
            });
        }

        return actions;
    }

    public async Task<string?> TryPlayCardExternallyAsync(int? handIndex, string? cardId, int? targetEnemyIndex)
    {
        if (_battleEnded || IsInputLocked())
        {
            return "Battle input is currently locked.";
        }

        var resolvedHandIndex = ResolveHandIndex(handIndex, cardId);
        if (resolvedHandIndex < 0 || resolvedHandIndex >= _hand.Count)
        {
            return "Requested card is not in hand.";
        }

        var card = _hand[resolvedHandIndex];
        if (CardRequiresEnemyTarget(card))
        {
            if (!targetEnemyIndex.HasValue)
            {
                return "This card requires 'targetEnemyIndex'.";
            }

            if (targetEnemyIndex.Value < 0 || targetEnemyIndex.Value >= _enemies.Count)
            {
                return "targetEnemyIndex is out of range.";
            }

            if (!_enemies[targetEnemyIndex.Value].IsAlive)
            {
                return "The selected enemy is already defeated.";
            }

            _selectedEnemyIndex = targetEnemyIndex.Value;
            SyncEnemyVisualFromSelection();
        }

        PushInputLock();
        try
        {
            var played = await TrySpendAndApplyCard(card);
            if (!played)
            {
                return "Card play failed.";
            }

            if (CardRequiresEnemyTarget(card))
            {
                var er = EnemyEffectTarget(_selectedEnemyIndex).GetGlobalRect();
                SpawnCardPlayImpact(er.Position + er.Size * 0.5f, card.Kind);
            }
            else
            {
                var pr = _playerCardView.EffectTarget().GetGlobalRect();
                SpawnCardPlayImpact(pr.Position + pr.Size * 0.5f, card.Kind);
            }

            return null;
        }
        finally
        {
            PopInputLock();
        }
    }

    public async Task<string?> TryEndTurnExternallyAsync()
    {
        if (_battleEnded || IsInputLocked())
        {
            return "Battle input is currently locked.";
        }

        await EndTurnAsync();
        return null;
    }

    private int ResolveHandIndex(int? handIndex, string? cardId)
    {
        if (handIndex.HasValue && handIndex.Value >= 0 && handIndex.Value < _hand.Count)
        {
            return handIndex.Value;
        }

        if (!string.IsNullOrWhiteSpace(cardId))
        {
            for (var i = 0; i < _hand.Count; i++)
            {
                if (string.Equals(_hand[i].Id, cardId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return -1;
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
        Log(LocalizationService.Format("log.battle.play_gain_block", "Play {0}: gain {1} Block", card.GetLocalizedName(), effect.Amount), "#60a5fa");
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
        Log(LocalizationService.Format("log.battle.play_gain_strength", "Play {0}: gain {1} Strength", card.GetLocalizedName(), effect.Amount), "#c084fc");
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
        Log(LocalizationService.Format("log.battle.play_gain_energy", "Play {0}: gain {1} Energy", card.GetLocalizedName(), effect.Amount), "#fde68a");
        SpawnFloatingText(playerEffectTarget, $"EN+{effect.Amount}", new Color("fde68a"));
        SpawnEnergyRadialBurst(playerEffectTarget);
        if (IsInstanceValid(_energyLabel))
        {
            PulseImpact(_energyLabel, 1.12f);
        }
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
        Log(LocalizationService.Format("log.battle.play_heal", "Play {0}: heal {1}", card.GetLocalizedName(), gained), "#86efac");
        SpawnFloatingText(playerEffectTarget, $"+{gained} HP", new Color("86efac"));
        SpawnHealSparkles(playerEffectTarget);
        PulseImpact(_playerPanel, 1.035f);
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
        Log(LocalizationService.Format("log.battle.play_damage", "Play {0}: damage {1} ({2} HP)", card.GetLocalizedName(), resolution.FinalDamage, resolution.Taken), "#f87171");
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

        Log(LocalizationService.Format("log.battle.play_vulnerable", "Play {0}: apply {1} Vulnerable", cardName, amount), "#c084fc");
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

        if (IsFastMode)
        {
            _enemyDropArea.Modulate = new Color(1f, 1f, 1f, 0.15f);
            _enemyDropArea.Scale = _enemyDropAreaBaseScale * 0.78f;
            return;
        }

        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(_enemyDropArea, "modulate:a", 0.15f, 0.28f);
        tween.Parallel().TweenProperty(_enemyDropArea, "scale", _enemyDropAreaBaseScale * 0.78f, 0.28f);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private void SpawnSlashEffect(Control target, Color color)
    {
        SpawnSlashStripe(target, color, new Vector2(88f, 11f), -21f);
        SpawnSlashStripe(target, new Color(color.R, color.G, color.B, color.A * 0.72f), new Vector2(62f, 8f), 38f);
    }

    private void SpawnSlashStripe(Control target, Color color, Vector2 size, float rotationDegrees)
    {
        var rect = new ColorRect
        {
            Color = color,
            Size = size,
            TopLevel = true,
            ZIndex = 180
        };
        _effectsLayer.AddChild(rect);

        var area = target.GetGlobalRect();
        var cx = area.Position.X + area.Size.X * 0.5f;
        var cy = area.Position.Y + area.Size.Y * 0.45f;
        rect.GlobalPosition = new Vector2(cx - size.X * 0.5f, cy - size.Y * 0.5f);
        rect.RotationDegrees = rotationDegrees;
        rect.PivotOffset = size * 0.5f;

        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Quad);
        tween.TweenProperty(rect, "scale", new Vector2(1.38f, 1f), 0.11f);
        tween.Parallel().TweenProperty(rect, "modulate:a", 0f, 0.19f);
        tween.Finished += () =>
        {
            if (IsInstanceValid(rect))
            {
                rect.QueueFree();
            }
        };
    }

    private void SpawnHealSparkles(Control target)
    {
        if (IsFastMode)
        {
            return;
        }

        var area = target.GetGlobalRect();
        var center = new Vector2(area.Position.X + area.Size.X * 0.5f, area.Position.Y + area.Size.Y * 0.42f);
        var baseColor = new Color("86efac");

        for (var i = 0; i < 7; i++)
        {
            var spark = new ColorRect
            {
                Color = new Color(baseColor.R, baseColor.G, baseColor.B, 0.88f),
                Size = new Vector2(5f, 5f),
                TopLevel = true,
                ZIndex = 182
            };
            _effectsLayer.AddChild(spark);

            var ox = (float)(_rng.NextDouble() * 56.0 - 28.0);
            var oy = (float)(_rng.NextDouble() * 18.0 - 6.0);
            spark.GlobalPosition = center + new Vector2(ox - 2.5f, oy - 2.5f);

            var tween = CreateTween();
            tween.SetEase(Tween.EaseType.Out);
            tween.SetTrans(Tween.TransitionType.Cubic);
            var rise = 36f + (float)(_rng.NextDouble() * 22.0);
            var drift = (float)(_rng.NextDouble() * 24.0 - 12.0);
            tween.TweenProperty(spark, "global_position", spark.GlobalPosition + new Vector2(drift, -rise), 0.38f);
            tween.Parallel().TweenProperty(spark, "modulate:a", 0f, 0.38f);
            tween.Parallel().TweenProperty(spark, "rotation_degrees", (float)(_rng.NextDouble() * 80.0 - 40.0), 0.38f);
            var captured = spark;
            tween.Finished += () =>
            {
                if (IsInstanceValid(captured))
                {
                    captured.QueueFree();
                }
            };
        }
    }

    private void SpawnEnergyRadialBurst(Control target)
    {
        if (IsFastMode)
        {
            return;
        }

        var area = target.GetGlobalRect();
        var center = new Vector2(area.Position.X + area.Size.X * 0.5f, area.Position.Y + area.Size.Y * 0.48f);
        var tint = new Color("fde68a");

        var core = new ColorRect
        {
            Color = new Color(tint.R, tint.G, tint.B, 0.55f),
            Size = new Vector2(36f, 36f),
            TopLevel = true,
            ZIndex = 181
        };
        _effectsLayer.AddChild(core);
        core.GlobalPosition = center - core.Size * 0.5f;
        core.PivotOffset = core.Size * 0.5f;

        var coreTween = CreateTween();
        coreTween.SetEase(Tween.EaseType.Out);
        coreTween.SetTrans(Tween.TransitionType.Cubic);
        coreTween.TweenProperty(core, "scale", new Vector2(2.4f, 2.4f), 0.2f);
        coreTween.Parallel().TweenProperty(core, "modulate:a", 0f, 0.22f);
        coreTween.Finished += () =>
        {
            if (IsInstanceValid(core))
            {
                core.QueueFree();
            }
        };

        for (var i = 0; i < 6; i++)
        {
            var ray = new ColorRect
            {
                Color = new Color(tint.R, tint.G, tint.B, 0.65f),
                Size = new Vector2(46f, 4f),
                TopLevel = true,
                ZIndex = 180
            };
            _effectsLayer.AddChild(ray);
            ray.GlobalPosition = center - new Vector2(0f, ray.Size.Y * 0.5f);
            ray.PivotOffset = new Vector2(0f, ray.Size.Y * 0.5f);
            ray.RotationDegrees = i * 60f;

            var rt = CreateTween();
            rt.SetEase(Tween.EaseType.Out);
            rt.SetTrans(Tween.TransitionType.Quad);
            rt.TweenProperty(ray, "scale:x", 1.65f, 0.16f);
            rt.Parallel().TweenProperty(ray, "modulate:a", 0f, 0.2f);
            var capturedRay = ray;
            rt.Finished += () =>
            {
                if (IsInstanceValid(capturedRay))
                {
                    capturedRay.QueueFree();
                }
            };
        }
    }

    private void SpawnDrawPileFlash()
    {
        if (IsFastMode || !IsInstanceValid(_drawAnchor))
        {
            return;
        }

        var area = _drawAnchor.GetGlobalRect();
        var center = area.Position + area.Size * 0.5f;
        var ring = new ColorRect
        {
            Color = new Color(0.49f, 0.83f, 0.99f, 0.28f),
            Size = new Vector2(48f, 48f),
            TopLevel = true,
            ZIndex = 165
        };
        _effectsLayer.AddChild(ring);
        ring.GlobalPosition = center - ring.Size * 0.5f;
        ring.PivotOffset = ring.Size * 0.5f;

        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(ring, "scale", new Vector2(2.1f, 2.1f), 0.24f);
        tween.Parallel().TweenProperty(ring, "modulate:a", 0f, 0.26f);
        tween.Finished += () =>
        {
            if (IsInstanceValid(ring))
            {
                ring.QueueFree();
            }
        };
    }

    private void SpawnCardPlayImpact(Vector2 globalCenter, CardKind kind)
    {
        if (IsFastMode)
        {
            return;
        }

        var primary = kind == CardKind.Attack ? new Color("fb923c") : new Color("7dd3fc");
        var glow = kind == CardKind.Attack ? new Color("fecaca") : new Color("bae6fd");

        var core = new ColorRect
        {
            Color = new Color(primary.R, primary.G, primary.B, 0.62f),
            Size = new Vector2(28f, 28f),
            TopLevel = true,
            ZIndex = 178
        };
        _effectsLayer.AddChild(core);
        core.GlobalPosition = globalCenter - core.Size * 0.5f;
        core.PivotOffset = core.Size * 0.5f;

        var coreTween = CreateTween();
        coreTween.SetEase(Tween.EaseType.Out);
        coreTween.SetTrans(Tween.TransitionType.Back);
        coreTween.TweenProperty(core, "scale", new Vector2(2.15f, 2.15f), 0.14f);
        coreTween.Parallel().TweenProperty(core, "modulate:a", 0f, 0.2f);
        coreTween.Finished += () =>
        {
            if (IsInstanceValid(core))
            {
                core.QueueFree();
            }
        };

        var ring = new ColorRect
        {
            Color = new Color(glow.R, glow.G, glow.B, 0.35f),
            Size = new Vector2(40f, 40f),
            TopLevel = true,
            ZIndex = 177
        };
        _effectsLayer.AddChild(ring);
        ring.GlobalPosition = globalCenter - ring.Size * 0.5f;
        ring.PivotOffset = ring.Size * 0.5f;

        var ringTween = CreateTween();
        ringTween.SetEase(Tween.EaseType.Out);
        ringTween.SetTrans(Tween.TransitionType.Cubic);
        ringTween.TweenProperty(ring, "scale", new Vector2(2.45f, 2.45f), 0.2f);
        ringTween.Parallel().TweenProperty(ring, "modulate:a", 0f, 0.22f);
        ringTween.Finished += () =>
        {
            if (IsInstanceValid(ring))
            {
                ring.QueueFree();
            }
        };

        for (var i = 0; i < 9; i++)
        {
            var spark = new ColorRect
            {
                Color = new Color(primary.R, primary.G, primary.B, 0.78f),
                Size = new Vector2(6f, 6f),
                TopLevel = true,
                ZIndex = 179
            };
            _effectsLayer.AddChild(spark);
            spark.GlobalPosition = globalCenter - spark.Size * 0.5f;
            spark.PivotOffset = spark.Size * 0.5f;
            var angle = i * (Mathf.Tau / 9f);
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            var st = CreateTween();
            st.SetEase(Tween.EaseType.Out);
            st.SetTrans(Tween.TransitionType.Quad);
            st.TweenProperty(spark, "global_position", globalCenter - spark.Size * 0.5f + dir * (42f + (float)(_rng.NextDouble() * 14.0)), 0.18f);
            st.Parallel().TweenProperty(spark, "modulate:a", 0f, 0.2f);
            st.Parallel().TweenProperty(spark, "rotation_degrees", (float)(_rng.NextDouble() * 240.0 - 120.0), 0.2f);
            var captured = spark;
            st.Finished += () =>
            {
                if (IsInstanceValid(captured))
                {
                    captured.QueueFree();
                }
            };
        }
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

        if (IsFastMode)
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
        if (IsFastMode)
        {
            _turnBanner.Visible = false;
            return;
        }

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
        Log(LocalizationService.Get("log.battle.victory", "Victory"), "#22c55e");

        _state.PlayerHp = _playerHp;
        var hpBeforeResolve = _state.PlayerHp;
        var hpAfterCharm = _state.HasRelic("charm") ? Math.Min(hpBeforeResolve + 5, _state.MaxHp) : hpBeforeResolve;
        var charmHeal = hpAfterCharm - hpBeforeResolve;
        var hpAfterBloodVial = _state.HasRelic("blood_vial") ? Math.Min(hpAfterCharm + 2, _state.MaxHp) : hpAfterCharm;
        var bloodVialHeal = hpAfterBloodVial - hpAfterCharm;

        _state.ResolveBattleVictory();
        _state.SetUiPhase("reward");

        if (charmHeal > 0)
        {
            Log(LocalizationService.Format("log.battle.lucky_charm", "Lucky Charm heals {0} HP", charmHeal), "#86efac");
            FlashRelic("charm");
        }

        if (bloodVialHeal > 0)
        {
            Log(LocalizationService.Format("log.battle.blood_vial", "Blood Vial heals {0} HP", bloodVialHeal), "#fca5a5");
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

        if (IsFastMode)
        {
            LayoutHandCards(true);
            return;
        }

        SpawnDrawPileFlash();

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
        var cards = CollectHandCards();

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
        var localPoses = CalculateHandCardPoses(cards, hovered, out var hoveredIndex, out var anyDragging);
        var handGlobal = _handContainer.GlobalPosition;
        var isEditorPreview = Engine.IsEditorHint();
        for (var i = 0; i < count; i++)
        {
            var pose = localPoses[i];
            cards[i].SetPivotYOffsetFactor(_handPivotYOffsetFactor);
            cards[i].ZIndex = pose.ZIndex;
            if (isEditorPreview)
            {
                cards[i].Position = pose.LocalPosition;
                cards[i].RotationDegrees = pose.RotationDegrees;
                cards[i].Scale = pose.Scale;
            }
            else
            {
                cards[i].SetPose(handGlobal + pose.LocalPosition, pose.RotationDegrees, pose.Scale, animate);
            }
            cards[i].SetFocusState(hoveredIndex == i, hoveredIndex >= 0 && hoveredIndex != i && !anyDragging);
        }

        RefreshHandDebugOverlay();
    }

    private List<HandCardPose> CalculateHandCardPoses(List<CardView> cards, CardView hovered, out int hoveredIndex, out bool anyDragging)
    {
        hoveredIndex = -1;
        anyDragging = false;

        var count = cards.Count;
        var cardSize = cards[0].CustomMinimumSize;
        var cardHalfWidth = cardSize.X * 0.5f;
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

        GetHandArcMetrics(count, cardSize, out var centerX, out var pivotOffset, out var pivotBaseY, out var maxAngle, out var radius, out var startAngle, out var angleStep);
        var poses = new List<HandCardPose>(count);

        for (var i = 0; i < count; i++)
        {
            var angle = count <= 1 ? 0f : startAngle + angleStep * i;
            angle += Mathf.DegToRad(_handArcPhaseOffsetDegrees);
            var pivotX = centerX + Mathf.Sin(angle) * radius;
            var pivotY = pivotBaseY + (1f - Mathf.Cos(angle)) * radius;
            var rot = Mathf.RadToDeg(angle);

            if (!anyDragging && hoveredIndex >= 0)
            {
                var distance = Math.Abs(i - hoveredIndex);
                var direction = i < hoveredIndex ? -1f : 1f;
                if (i == hoveredIndex)
                {
                    rot *= 0.22f;
                }
                else if (distance == 1)
                {
                    pivotX += direction * _handHoverNeighborPush;
                    rot *= 0.65f;
                }
                else if (distance == 2)
                {
                    pivotX += direction * _handHoverSecondaryPush;
                }
            }

            var pivotPosition = new Vector2(pivotX, pivotY);
            var scale = Vector2.One;
            var finalRot = rot;
            if (!anyDragging && hoveredIndex == i)
            {
                pivotPosition.Y -= _handHoverLift;
                scale = new Vector2(1.08f, 1.08f);
                finalRot = 0f;
            }

            var pos = ConvertPivotToTopLeft(pivotPosition, pivotOffset, finalRot, scale);
            poses.Add(new HandCardPose(pos, finalRot, scale, hoveredIndex == i ? HoveredHandZIndex : i));
        }

        return poses;
    }

    private List<CardView> CollectHandCards()
    {
        var cards = new List<CardView>();
        if (!IsInstanceValid(_handContainer))
        {
            return cards;
        }

        foreach (Node node in _handContainer.GetChildren())
        {
            if (node is CardView card)
            {
                cards.Add(card);
            }
        }

        return cards;
    }

    private void GetHandArcMetrics(int count, Vector2 cardSize, out float centerX, out Vector2 pivotOffset, out float pivotBaseY, out float maxAngle, out float radius, out float startAngle, out float angleStep)
    {
        centerX = _handContainer.Size.X * 0.5f + _handArcCenterOffsetX;
        pivotOffset = new Vector2(cardSize.X * 0.5f, cardSize.Y * _handPivotYOffsetFactor);
        pivotBaseY = _handContainer.Size.Y - (cardSize.Y - pivotOffset.Y) - _handBottomPadding;
        pivotBaseY += cardSize.Y * 0.25f;
        var spreadFactor = Mathf.Clamp((count - 1) / 7f, 0f, 1f);
        maxAngle = Mathf.DegToRad(Mathf.Lerp(_handArcAngleMinDegrees, _handArcAngleMaxDegrees, spreadFactor));
        if (count >= 5)
        {
            maxAngle *= 1.25f;
        }

        radius = Mathf.Lerp(_handArcRadiusMax, _handArcRadiusMin, spreadFactor);
        // Few cards used to keep almost the full radius while only spanning a small arc, so pivots sat near
        // the left/right extremes of a huge circle (~2*R*sin(θ) with large R). Tighten radius until the
        // hand grows so 2–3 cards cluster toward the middle instead of hugging the hand panel edges.
        if (count >= 2)
        {
            var fewCardRadiusMul = Mathf.Clamp(0.28f + (count - 2) * 0.12f, 0.28f, 1f);
            radius *= fewCardRadiusMul;
        }

        startAngle = -maxAngle;
        angleStep = count <= 1 ? 0f : (maxAngle * 2f) / (count - 1);
    }

    private static Vector2 ConvertPivotToTopLeft(Vector2 pivotPosition, Vector2 pivotOffset, float rotationDegrees, Vector2 scale)
    {
        var uniformScale = (scale.X + scale.Y) * 0.5f;
        var scaledPivot = pivotOffset * uniformScale;
        var rotatedPivot = scaledPivot.Rotated(Mathf.DegToRad(rotationDegrees));
        return pivotPosition - rotatedPivot;
    }

    private void DrawHandDebugArc(Vector2 handOrigin, float centerX, float pivotBaseY, float radius, float startAngle, float maxAngle)
    {
        const int segments = 48;
        var points = new Vector2[segments + 1];
        for (var i = 0; i <= segments; i++)
        {
            var t = i / (float)segments;
            var angle = Mathf.Lerp(startAngle, maxAngle, t);
            var x = centerX + Mathf.Sin(angle) * radius;
            var y = pivotBaseY + (1f - Mathf.Cos(angle)) * radius;
            points[i] = handOrigin + new Vector2(x, y);
        }

        DrawPolyline(points, new Color(0.2f, 0.95f, 1f, 0.95f), 2f);
    }

    private void EnsureHandDebugOverlay()
    {
        if (IsInstanceValid(_handDebugOverlay))
        {
            return;
        }

        _handDebugOverlay = new HandDebugOverlay
        {
            Name = "HandDebugOverlay",
            MouseFilter = MouseFilterEnum.Ignore,
            TopLevel = false,
            ZIndex = 1
        };
        _overlayCanvas.AddChild(_handDebugOverlay);
    }

    private void EnsureOverlayCanvas()
    {
        if (IsInstanceValid(_overlayCanvas))
        {
            return;
        }

        _overlayCanvas = new CanvasLayer { Layer = 50 };
        AddChild(_overlayCanvas);
    }

    private void RefreshHandDebugOverlay()
    {
        if (!IsInstanceValid(_handDebugOverlay))
        {
            return;
        }

        _handDebugOverlay.Position = Vector2.Zero;
        _handDebugOverlay.Size = GetViewportRect().Size;

        if (!_showHandDebugOverlay || !IsInstanceValid(_handContainer))
        {
            _handDebugOverlay.SetSnapshot(null);
            return;
        }

        var cards = CollectHandCards();
        if (cards.Count == 0)
        {
            _handDebugOverlay.SetSnapshot(null);
            return;
        }

        var hovered = IsInstanceValid(_hoveredCard) ? _hoveredCard : null;
        var cardSize = cards[0].CustomMinimumSize;
        GetHandArcMetrics(cards.Count, cardSize, out var centerX, out var pivotOffset, out var pivotBaseY, out var maxAngle, out var radius, out var startAngle, out _);

        var handOrigin = _handContainer.GetGlobalTransformWithCanvas().Origin;
        var arcPoints = BuildHandDebugArcPoints(handOrigin, centerX, pivotBaseY, radius, startAngle, maxAngle);
        var markers = new List<HandDebugMarker>(cards.Count);
        for (var i = 0; i < cards.Count; i++)
        {
            var cardTransform = cards[i].GetGlobalTransformWithCanvas();
            var cardCenter = cardTransform * (cardSize * 0.5f);
            var cardPivot = cardTransform * pivotOffset;
            markers.Add(new HandDebugMarker(cardCenter, cardPivot, i));
        }

        var snapshot = new HandDebugSnapshot
        {
            HandRect = new Rect2(handOrigin, _handContainer.Size),
            CenterTop = handOrigin + new Vector2(centerX, 0f),
            CenterBottom = handOrigin + new Vector2(centerX, _handContainer.Size.Y),
            PivotCenter = handOrigin + new Vector2(centerX, pivotBaseY),
            ArcPoints = arcPoints,
            Markers = markers,
            Info = $"radius {radius:0}  angle {Mathf.RadToDeg(maxAngle):0.0}  centerX {centerX:0}"
        };
        _handDebugOverlay.SetSnapshot(snapshot);
    }

    private Vector2[] BuildHandDebugArcPoints(Vector2 handOrigin, float centerX, float pivotBaseY, float radius, float startAngle, float maxAngle)
    {
        const int segments = 48;
        var points = new Vector2[segments + 1];
        for (var i = 0; i <= segments; i++)
        {
            var t = i / (float)segments;
            var angle = Mathf.Lerp(startAngle, maxAngle, t);
            var x = centerX + Mathf.Sin(angle) * radius;
            var y = pivotBaseY + (1f - Mathf.Cos(angle)) * radius;
            points[i] = handOrigin + new Vector2(x, y);
        }

        return points;
    }

    private void UpdateHandHoverFromMouse(Vector2 mouseGlobal)
    {
        if (!IsInstanceValid(_handContainer) || _battleEnded || IsInputLocked() || _draggingCard != null)
        {
            SetHoveredCard(null);
            return;
        }

        var handRect = _handContainer.GetGlobalRect().Grow(20f);
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

        var basePoses = CalculateHandCardPoses(cards, hovered: null, out _, out _);
        var slotCenters = BuildHandHoverSlotCenters(cards, basePoses);
        var hoverIndex = ResolveHandHoverIndex(mouseGlobal, slotCenters, cards[0].CustomMinimumSize, handRect);
        if (hoverIndex < 0)
        {
            SetHoveredCard(null);
            return;
        }

        var next = cards[hoverIndex];
        if (IsInstanceValid(_hoveredCard) && _hoveredCard == next)
        {
            return;
        }

        SetHoveredCard(next);
    }

    private List<Vector2> BuildHandHoverSlotCenters(List<CardView> cards, List<HandCardPose> poses)
    {
        var centers = new List<Vector2>(cards.Count);
        var handGlobal = _handContainer.GlobalPosition;
        for (var i = 0; i < cards.Count; i++)
        {
            centers.Add(GetHandPoseGlobalCenter(handGlobal, cards[i].CustomMinimumSize, poses[i]));
        }

        return centers;
    }

    private Vector2 GetHandPoseGlobalCenter(Vector2 handGlobal, Vector2 cardSize, HandCardPose pose)
    {
        var scale = (pose.Scale.X + pose.Scale.Y) * 0.5f;
        var pivotOffset = new Vector2(cardSize.X * 0.5f, cardSize.Y * _handPivotYOffsetFactor);
        var rotation = Mathf.DegToRad(pose.RotationDegrees);
        var topLeft = handGlobal + pose.LocalPosition;
        var pivotPosition = topLeft + (pivotOffset * scale).Rotated(rotation);
        var centerOffset = ((cardSize * 0.5f) - pivotOffset) * scale;
        return pivotPosition + centerOffset.Rotated(rotation);
    }

    private static int ResolveHandHoverIndex(Vector2 mouseGlobal, List<Vector2> slotCenters, Vector2 cardSize, Rect2 handRect)
    {
        if (slotCenters.Count == 0)
        {
            return -1;
        }

        var minTop = float.MaxValue;
        var maxBottom = float.MinValue;
        foreach (var center in slotCenters)
        {
            minTop = Math.Min(minTop, center.Y - cardSize.Y * 0.72f);
            maxBottom = Math.Max(maxBottom, center.Y + cardSize.Y * 0.48f);
        }

        if (mouseGlobal.Y < minTop || mouseGlobal.Y > maxBottom)
        {
            return -1;
        }

        if (slotCenters.Count == 1)
        {
            var halfWidth = cardSize.X * 0.58f;
            return Math.Abs(mouseGlobal.X - slotCenters[0].X) <= halfWidth ? 0 : -1;
        }

        var leftGap = slotCenters[1].X - slotCenters[0].X;
        var rightGap = slotCenters[^1].X - slotCenters[^2].X;
        var leftBoundary = slotCenters[0].X - Math.Max(leftGap * 0.6f, cardSize.X * 0.22f);
        var rightBoundary = slotCenters[^1].X + Math.Max(rightGap * 0.6f, cardSize.X * 0.22f);

        leftBoundary = Math.Max(leftBoundary, handRect.Position.X - 4f);
        rightBoundary = Math.Min(rightBoundary, handRect.End.X + 4f);
        if (mouseGlobal.X < leftBoundary || mouseGlobal.X > rightBoundary)
        {
            return -1;
        }

        for (var i = 0; i < slotCenters.Count - 1; i++)
        {
            var boundary = (slotCenters[i].X + slotCenters[i + 1].X) * 0.5f;
            if (mouseGlobal.X < boundary)
            {
                return i;
            }
        }

        return slotCenters.Count - 1;
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

        Log(LocalizationService.Get("log.battle.test_victory", "[TEST] Trigger instant victory"), "#facc15");
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

        var localizedEnemyName = CombatVisualCatalog.GetLocalizedEnemyName(enemy.ArchetypeId, enemy.Name);
        _enemyNameLabel.Text = $"{localizedEnemyName} ({_selectedEnemyIndex + 1}/{_enemies.Count})";
        _enemyHpLabel.Text = LocalizationService.Format("ui.battle.enemy_hp", "Enemy HP: {0}", enemy.Hp);
        _enemyBlockLabel.Text = LocalizationService.Format("ui.battle.enemy_block", "Enemy Block: {0}", enemy.Block);
        _enemyStatusLabel.Text = LocalizationService.Format("ui.battle.enemy_status", "Enemy Status: STR {0}, VUL {1}", enemy.Strength, enemy.Vulnerable);
        var intentLabel = LocalizationService.Get("ui.battle.intent_label", "Intent");
        _enemyIntentLabel.Text = _battleEnded || !enemy.IsAlive
            ? $"{intentLabel}: -"
            : $"{intentLabel}: {IntentText(enemy)}";
        if (IsInstanceValid(_enemyIntentListLabel))
        {
            var intentParts = new List<string>();
            for (var i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                var status = e.IsAlive ? IntentText(e) : LocalizationService.Get("ui.status.defeated", "Defeated");
                var enemyName = CombatVisualCatalog.GetLocalizedEnemyName(e.ArchetypeId, e.Name);
                intentParts.Add($"{i + 1}.{enemyName}: {status}");
            }

            _enemyIntentListLabel.Text = $"{LocalizationService.Get("ui.battle.intents", "Intents")}: {string.Join(" | ", intentParts)}";
        }
        _topHpLabel.Text = LocalizationService.Format("ui.player_status.hp", "HP {0}/{1}", _playerHp, _playerMaxHp);
        _turnLabel.Text = LocalizationService.Format("ui.battle.turn", "Turn {0}", _turn);
        _energyLabel.Text = LocalizationService.Format("ui.battle.energy", "Energy {0}/{1}", _energy, MaxEnergy);
        _handCountLabel.Text = LocalizationService.Format(
            "ui.battle.hand_status",
            "Hand: {0} | Draw: {1} | Discard: {2} | Enemies: {3}",
            _hand.Count,
            _drawPile.Count,
            _discardPile.Count,
            AliveEnemyCount());
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
            return LocalizationService.Get("ui.battle.no_relic", "Relics: None");
        }

        var names = _state.RelicIds.Select(id => RelicData.CreateById(id).LocalizedName);
        return LocalizationService.Format("ui.battle.relics", "Relics: {0}", string.Join(" | ", names));
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
        if (IsFastMode)
        {
            return;
        }

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

    private void SetupEditorPreview()
    {
        _handContainer = GetNodeOrNull<Control>("%HandContainer");
        if (!IsInstanceValid(_handContainer))
        {
            return;
        }

        _handContainer.Resized += RequestEditorPreviewLayout;
        RequestEditorPreviewRefresh();
    }

    private void UpdateEditorPreview(bool forceRefresh = false)
    {
        if (!Engine.IsEditorHint() || !IsInstanceValid(_handContainer))
        {
            return;
        }

        if (_handContainer.Size.X <= 1f || _handContainer.Size.Y <= 1f)
        {
            RequestEditorPreviewRefresh();
            return;
        }

        var signature = string.Join("|",
            _editorPreviewHand,
            _editorPreviewCardCount,
            _editorPreviewCardIds,
            _handArcAngleMinDegrees,
            _handArcAngleMaxDegrees,
            _handArcRadiusMin,
            _handArcRadiusMax,
            _handBottomPadding,
            _handPivotYOffsetFactor,
            _handHoverLift,
            _handHoverNeighborPush,
            _handHoverSecondaryPush,
            _showHandDebugOverlay,
            _handContainer.Size.X,
            _handContainer.Size.Y);

        if (!forceRefresh && signature == _editorPreviewSignature)
        {
            return;
        }

        _editorPreviewSignature = signature;

        if (!_editorPreviewHand)
        {
            ClearEditorPreviewHand();
            return;
        }

        RebuildEditorPreviewHand();
    }

    private void RebuildEditorPreviewHand()
    {
        ClearEditorPreviewHand();

        var ids = _editorPreviewCardIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (ids.Count == 0)
        {
            ids.AddRange(new[] { "meteor_shower", "reaper_touch", "bone_shrapnel", "soul_siphon", "phoenix_cycle" });
        }

        var count = Mathf.Clamp(_editorPreviewCardCount, 1, HandLimit);
        for (var i = 0; i < count; i++)
        {
            var cardView = new CardView();
            cardView.SetDragEnabled(false);
            cardView.SetUseTopLevel(true);
            cardView.Setup(CardData.CreateById(ids[i % ids.Count]));
            _handContainer.AddChild(cardView);
        }

        RequestEditorPreviewLayout();
    }

    private void ClearEditorPreviewHand()
    {
        if (!IsInstanceValid(_handContainer))
        {
            return;
        }

        foreach (Node child in _handContainer.GetChildren())
        {
            if (child is CardView cardView)
            {
                cardView.QueueFree();
            }
        }
    }

    private void RequestEditorPreviewLayout()
    {
        if (!Engine.IsEditorHint() || _editorPreviewLayoutPending)
        {
            return;
        }

        _editorPreviewLayoutPending = true;
        CallDeferred(nameof(DeferredEditorPreviewLayout));
    }

    private void RequestEditorPreviewRefresh()
    {
        if (!Engine.IsEditorHint() || _editorPreviewRefreshPending)
        {
            return;
        }

        _editorPreviewRefreshPending = true;
        CallDeferred(nameof(DeferredEditorPreviewRefresh));
    }

    private void DeferredEditorPreviewRefresh()
    {
        _editorPreviewRefreshPending = false;
        if (!Engine.IsEditorHint() || !IsInstanceValid(_handContainer))
        {
            return;
        }

        UpdateEditorPreview(forceRefresh: true);
        RequestEditorPreviewLayout();
    }

    private void DeferredEditorPreviewLayout()
    {
        _editorPreviewLayoutPending = false;
        if (!Engine.IsEditorHint() || !IsInstanceValid(_handContainer))
        {
            return;
        }

        var cards = new List<CardView>();
        foreach (Node node in _handContainer.GetChildren())
        {
            if (node is CardView card)
            {
                cards.Add(card);
            }
        }

        if (cards.Count == 0)
        {
            return;
        }

        var localPoses = CalculateHandCardPoses(cards, null, out _, out _);
        var handGlobal = _handContainer.GlobalPosition;
        for (var i = 0; i < cards.Count; i++)
        {
            var pose = localPoses[i];
            cards[i].SetPivotYOffsetFactor(_handPivotYOffsetFactor);
            cards[i].SetPose(handGlobal + pose.LocalPosition, pose.RotationDegrees, pose.Scale, animate: false);
            cards[i].ZIndex = pose.ZIndex;
        }

        RefreshHandDebugOverlay();
    }

}
