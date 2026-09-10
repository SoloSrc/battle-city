using System.Threading.Tasks;
using Godot;

namespace BattleCity.Core;

/// <summary>
/// Full-screen black fade used by <see cref="Game"/> for scene transitions and
/// the loss return. Built in code so it needs no scene; lives above every other layer.
/// </summary>
public partial class ScreenFade : CanvasLayer
{
    [Export(PropertyHint.Range, "0,2,0.05,suffix:s")]
    public float Duration { get; set; } = 0.25f;

    /// <summary>True from the start of a fade-out until the end of the following fade-in.</summary>
    public bool IsCovered => _rect is not null && _rect.Color.A > 0.999f;

    private ColorRect? _rect;
    private Tween? _tween;

    public override void _Ready()
    {
        Layer = 100;
        _rect = new ColorRect
        {
            Name = "Black",
            Color = new Color(0.0f, 0.0f, 0.0f, 0.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_rect);
    }

    public Task FadeOutAsync() => FadeToAsync(1.0f);

    public Task FadeInAsync() => FadeToAsync(0.0f);

    /// <summary>Jumps to fully covered or fully clear without animating.</summary>
    public void Set(bool covered)
    {
        _tween?.Kill();
        if (_rect is not null)
        {
            _rect.Color = new Color(0.0f, 0.0f, 0.0f, covered ? 1.0f : 0.0f);
        }
    }

    private async Task FadeToAsync(float alpha)
    {
        if (_rect is null)
        {
            return;
        }

        _tween?.Kill();
        if (Duration <= 0.0f || Mathf.IsEqualApprox(_rect.Color.A, alpha))
        {
            _rect.Color = new Color(0.0f, 0.0f, 0.0f, alpha);
            return;
        }

        _tween = CreateTween();
        _tween.TweenProperty(_rect, "color:a", alpha, Duration);
        await ToSignal(_tween, Tween.SignalName.Finished);
    }
}
