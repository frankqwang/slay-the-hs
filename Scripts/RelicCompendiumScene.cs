using Godot;
using System;
using System.Linq;

public partial class RelicCompendiumScene : Control
{
    private VBoxContainer _content = null!;

    public override void _Ready()
    {
        var backButton = GetNode<Button>("%BackButton");
        _content = GetNode<VBoxContainer>("%RelicContent");
        backButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");

        BuildCompendium();
    }

    private void BuildCompendium()
    {
        foreach (Node child in _content.GetChildren())
        {
            child.QueueFree();
        }

        var rarityOrder = new[] { "Starter", "Common", "Uncommon", "Rare", "Boss" };
        var grouped = RelicData.GroupByRarity();

        foreach (var rarity in rarityOrder)
        {
            if (!grouped.TryGetValue(rarity, out var relics) || relics.Count == 0)
            {
                continue;
            }

            AddSectionTitle(rarity, relics.Count);
            foreach (var relic in relics.OrderBy(r => r.Archetype, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            {
                AddRelicRow(relic);
            }
        }
    }

    private void AddSectionTitle(string rarity, int count)
    {
        var title = new Label
        {
            Text = $"【{rarity}】 共 {count} 个"
        };
        title.AddThemeFontSizeOverride("font_size", 30);
        _content.AddChild(title);
        _content.AddChild(new HSeparator());
    }

    private void AddRelicRow(RelicData relic)
    {
        var row = new PanelContainer { CustomMinimumSize = new Vector2(0, 88) };
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 12);

        var icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(52, 52),
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Texture = GD.Load<Texture2D>(CombatVisualCatalog.GetRelicIconPath(relic.Id))
        };

        var textBox = new VBoxContainer();
        textBox.SizeFlagsHorizontal = (int)Control.SizeFlags.ExpandFill;

        var name = new Label { Text = $"{relic.Name}  ·  [{relic.Archetype}]" };
        name.AddThemeFontSizeOverride("font_size", 22);
        textBox.AddChild(name);

        textBox.AddChild(new Label
        {
            Text = relic.Description,
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(1f, 1f, 1f, 0.86f)
        });

        line.AddChild(icon);
        line.AddChild(textBox);
        row.AddChild(line);
        _content.AddChild(row);
    }
}
