using BattleCity.Characters;
using Godot;

namespace BattleCity.Ui;

/// <summary>
/// Bottom-centre prompt for the nearest usable interactable ("Enter", "Duel").
/// Follows <see cref="PlayerController.PromptChanged"/>; the device-aware icon
/// comes with the Input wrapper later.
/// </summary>
public partial class InteractionPrompt : CanvasLayer
{
    private Label? _label;
    private PlayerController? _controller;

    public string Text => _label?.Text ?? string.Empty;

    public override void _Ready()
    {
        Layer = 40;
        _label = new Label
        {
            Name = "Prompt",
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
        };
        _label.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _label.AnchorLeft = 0.5f;
        _label.AnchorRight = 0.5f;
        _label.AnchorTop = 1.0f;
        _label.AnchorBottom = 1.0f;
        _label.OffsetLeft = -200.0f;
        _label.OffsetRight = 200.0f;
        _label.OffsetTop = -260.0f;
        _label.OffsetBottom = -220.0f;
        _label.GrowHorizontal = Control.GrowDirection.Both;
        _label.GrowVertical = Control.GrowDirection.Begin;
        _label.AddThemeFontSizeOverride("font_size", 22);
        AddChild(_label);
    }

    public void Bind(PlayerController? controller)
    {
        if (_controller is not null)
        {
            _controller.PromptChanged -= OnPromptChanged;
        }

        _controller = controller;
        if (_controller is not null)
        {
            _controller.PromptChanged += OnPromptChanged;
        }

        OnPromptChanged(string.Empty);
    }

    private void OnPromptChanged(string prompt)
    {
        if (_label is null)
        {
            return;
        }

        _label.Visible = prompt.Length > 0;
        _label.Text = prompt.Length > 0 ? $"[E]  {prompt}" : string.Empty;
    }
}
