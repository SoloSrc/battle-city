using System;
using System.Collections.Generic;
using System.Globalization;
using BattleCity.Core;
using BattleCity.Duel.Core.Model;
using Godot;

namespace BattleCity.DuelScene;

/// <summary>
/// Composes card faces (systems.md §6.2, <c>assets/source/cards/frame-layout.json</c>
/// revision 5): a 590 × 860 <see cref="SubViewport"/> draws the art in the
/// window with centered cover, the frame of the card's kind on top, the star
/// row, the attribute badge and the upright serif stats, or the Spell/Trap
/// badge. Each distinct card id is rendered once, baked to an
/// <see cref="ImageTexture"/> and the viewport freed; until the bake lands the
/// viewport texture itself is handed out. Headless runs keep the viewport
/// (there is nothing to read back).
/// </summary>
public partial class CardFaces : Node
{
    public const int Width = 590;
    public const int Height = 860;
    public const int ArtLeft = 10;
    public const int ArtTop = 10;
    public const int ArtWidth = 570;
    public const int ArtHeight = 610;
    public const int StarSize = 34;
    public const int StarStep = 37;
    public const int StarRowY = 686;
    public const int StarRowCenterX = 295;
    public const int StarRowRightMax = 467;
    public const int AttributeX = 524;
    public const int AttributeY = 686;
    public const int AttributeSize = 66;
    public const int AtkX = 156;
    public const int DefX = 434;
    public const int StatY = 785;
    public const int StatPlateWidth = 244;
    public const int StatPlateHeight = 94;
    public const int StatFontPx = 80;
    public const int BadgeX = 295;
    public const int BadgeY = 745;
    public const int BadgeSize = 72;

    /// <summary>Frames a bake waits after its viewport joined the tree before reading the texture back.</summary>
    private const int BakeFrames = 2;

    private static readonly Dictionary<string, Texture2D> _baked = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Texture2D> _loaded = new(StringComparer.Ordinal);
    private static Font? _statFont;

    [Signal]
    public delegate void FaceBakedEventHandler(string cardId, Texture2D texture);

    private readonly Dictionary<string, SubViewport> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _frames = new(StringComparer.Ordinal);

    /// <summary>Faces baked to plain textures so far (all runs).</summary>
    public static int BakedCount => _baked.Count;

    /// <summary>Viewports still waiting for their first frame.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>A face already baked to a plain texture.</summary>
    public static bool TryGetBaked(string cardId, out Texture2D? texture) => _baked.TryGetValue(cardId, out texture);

    /// <summary>Left edge and width of the star row for <paramref name="level"/> (§6.2): centered, shifted left once it would reach the attribute.</summary>
    public static (int Left, int Width) StarRow(int level)
    {
        int width = (Math.Max(1, level) - 1) * StarStep + StarSize;
        double center = Math.Min(StarRowCenterX, StarRowRightMax - width / 2.0);
        return ((int)Math.Round(center - width / 2.0), width);
    }

    /// <summary>The frame texture path for a card kind (Ritual is a layout fixture only; no card in the pool uses it).</summary>
    public static string FramePath(CardDefinition def)
    {
        ArgumentNullException.ThrowIfNull(def);
        string name = def.Kind switch
        {
            CardKind.Spell => "frame_spell",
            CardKind.Trap => "frame_trap",
            CardKind.Fusion => "frame_fusion",
            _ when def.Monster?.Category == MonsterCategory.Ritual => "frame_ritual",
            _ when def.Monster?.Category == MonsterCategory.Normal => "frame_normal",
            _ => "frame_effect",
        };
        return $"{Paths.CardFrames}/{name}.png";
    }

    /// <summary>The face of <paramref name="def"/>: the baked texture when ready, else a viewport texture that renders it this frame.</summary>
    public Texture2D Get(CardDefinition def)
    {
        ArgumentNullException.ThrowIfNull(def);
        if (_baked.TryGetValue(def.Id, out Texture2D? baked))
        {
            return baked;
        }

        if (!_pending.TryGetValue(def.Id, out SubViewport? viewport))
        {
            viewport = Compose(def);
            viewport.Name = def.Id;
            AddChild(viewport);
            _pending[def.Id] = viewport;
            _frames[def.Id] = 0;
        }

        return viewport.GetTexture();
    }

    /// <summary>Headless runs draw nothing, so faces stay as viewport textures instead of being read back.</summary>
    public static bool IsHeadless => DisplayServer.GetName() == "headless";

    public override void _Process(double delta)
    {
        if (_pending.Count == 0 || IsHeadless)
        {
            return;
        }

        var ready = new List<string>();
        foreach ((string id, SubViewport viewport) in _pending)
        {
            if (++_frames[id] < BakeFrames)
            {
                continue;
            }

            Image? image = viewport.GetTexture()?.GetImage();
            if (image is null || image.IsEmpty())
            {
                // Headless: nothing is drawn; the viewport stays as the face so the card still has a texture object.
                _frames[id] = int.MinValue;
                continue;
            }

            var texture = ImageTexture.CreateFromImage(image);
            _baked[id] = texture;
            ready.Add(id);
            EmitSignal(SignalName.FaceBaked, id, texture);
        }

        foreach (string id in ready)
        {
            _pending[id].QueueFree();
            _pending.Remove(id);
            _frames.Remove(id);
        }
    }

    /// <summary>Builds the viewport tree of one face; public so a layout fixture (level 12, Ritual) can be composed from an ad-hoc definition.</summary>
    public static SubViewport Compose(CardDefinition def)
    {
        ArgumentNullException.ThrowIfNull(def);
        var viewport = new SubViewport
        {
            Size = new Vector2I(Width, Height),
            TransparentBg = true,
            Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
        };

        var root = new Control { Size = new Vector2(Width, Height) };
        viewport.AddChild(root);

        Texture2D? art = LoadTexture($"{Paths.CardArt}/{def.Id}.png");
        if (art is not null)
        {
            // ExpandMode before Texture: a texture set first fixes the minimum size and the rect never shrinks to the layout size.
            root.AddChild(new TextureRect
            {
                Name = "Art",
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                ClipContents = true,
                Texture = art,
                Position = new Vector2(ArtLeft, ArtTop),
                Size = new Vector2(ArtWidth, ArtHeight),
            });
        }
        else
        {
            root.AddChild(new ColorRect
            {
                Name = "Art",
                Color = new Color(0.16f, 0.18f, 0.24f),
                Position = new Vector2(ArtLeft, ArtTop),
                Size = new Vector2(ArtWidth, ArtHeight),
            });
        }

        Texture2D? frame = LoadTexture(FramePath(def));
        if (frame is not null)
        {
            root.AddChild(new TextureRect
            {
                Name = "Frame",
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                Texture = frame,
                Size = new Vector2(Width, Height),
            });
        }

        if (def.Monster is { } monster)
        {
            (int left, _) = StarRow(monster.Level);
            Texture2D? star = LoadTexture($"{Paths.CardIcons}/star.png");
            for (int i = 0; i < monster.Level; i++)
            {
                root.AddChild(Icon($"Star{i + 1}", star, left + i * StarStep + StarSize / 2, StarRowY, StarSize));
            }

            root.AddChild(Icon("Attribute", LoadTexture($"{Paths.CardIcons}/attr_{monster.Attribute.ToString().ToLowerInvariant()}.png"), AttributeX, AttributeY, AttributeSize));
            root.AddChild(Stat("Atk", monster.Atk, AtkX));
            root.AddChild(Stat("Def", monster.Def, DefX));
        }
        else
        {
            string badge = def.IsTrap ? "st_trap" : "st_spell";
            root.AddChild(Icon("Badge", LoadTexture($"{Paths.CardIcons}/{badge}.png"), BadgeX, BadgeY, BadgeSize));
        }

        return viewport;
    }

    private static Control Icon(string name, Texture2D? texture, int centerX, int centerY, int size)
    {
        return new TextureRect
        {
            Name = name,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = texture,
            Position = new Vector2(centerX - size / 2, centerY - size / 2),
            Size = new Vector2(size, size),
        };
    }

    /// <summary>An upright serif number centered on its plate; the font's metrics center it vertically, the plate is never enlarged (§6.2).</summary>
    private static Label Stat(string name, int value, int centerX)
    {
        var label = new Label
        {
            Name = name,
            Text = value.ToString(CultureInfo.InvariantCulture),
            Position = new Vector2(centerX - StatPlateWidth / 2, StatY - StatPlateHeight / 2),
            Size = new Vector2(StatPlateWidth, StatPlateHeight),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ClipText = true,
        };
        label.AddThemeFontOverride("font", StatFont());
        label.AddThemeFontSizeOverride("font_size", StatFontPx);
        label.AddThemeColorOverride("font_color", new Color("090703"));
        return label;
    }

    private static Font StatFont()
    {
        _statFont ??= new SystemFont
        {
            FontNames = new[] { "DejaVu Serif", "Georgia", "Times New Roman", "Noto Serif", "serif" },
            FontItalic = false,
        };
        return _statFont;
    }

    private static Texture2D? LoadTexture(string path)
    {
        if (_loaded.TryGetValue(path, out Texture2D? texture))
        {
            return texture;
        }

        texture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        if (texture is not null)
        {
            _loaded[path] = texture;
        }

        return texture;
    }
}
