using Godot;

public partial class MainMenu : Control
{
    private Button _startButton = null!;
    private Button _quitButton = null!;
    private Button _languageButton = null!;
    private Button _cardEditorButton = null!;
    private Button _relicCompendiumButton = null!;

    public override void _Ready()
    {
        _startButton = GetNode<Button>("%StartButton");
        _quitButton = GetNode<Button>("%QuitButton");
        _languageButton = GetNode<Button>("%LanguageButton");
        _cardEditorButton = GetNode<Button>("%CardEditorButton");
        _relicCompendiumButton = GetNode<Button>("%RelicCompendiumButton");

        _startButton.Pressed += OnStartPressed;
        _quitButton.Pressed += OnQuitPressed;
        _languageButton.Pressed += OnLanguagePressed;
        _cardEditorButton.Pressed += OnCardEditorPressed;
        _relicCompendiumButton.Pressed += OnRelicCompendiumPressed;

        RefreshLanguageButtonText();
    }

    private void OnStartPressed()
    {
        var state = GetNode<GameState>("/root/GameState");
        state.StartNewRun();
        GetTree().ChangeSceneToFile("res://Scenes/MapScene.tscn");
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

    private void OnCardEditorPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/CardEditorScene.tscn");
    }

    private void OnRelicCompendiumPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/RelicCompendiumScene.tscn");
    }

    private void OnLanguagePressed()
    {
        LocalizationSettings.ToggleLanguage();
        RefreshLanguageButtonText();
    }

    private void RefreshLanguageButtonText()
    {
        _languageButton.Text = LocalizationSettings.LanguageButtonText();
    }
}
