using System;
using System.IO;
using BattleCity.Core;
using Godot;

namespace BattleCity.DuelScene;

/// <summary>Optional, unimported checkout artwork; packaged generated art is always the fallback.</summary>
public static class CardArtwork
{
    private static readonly byte[] _pngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    /// <summary>Set before composing any faces; diagnostics disable overrides for safe evidence captures.</summary>
    public static bool UseLocalArt { get; set; } = !Array.Exists(OS.GetCmdlineUserArgs(), arg => arg == "--generated-art");

    // Exports deliberately use generated art: downloaded originals are not packaged.
    public static Texture2D? Load(string cardId) => LoadFromDirectory(cardId,
        UseLocalArt && OS.HasFeature("editor") ? ProjectSettings.GlobalizePath(Paths.LocalCardArt) : null);

    internal static Texture2D? LoadFromDirectory(string cardId, string? directory)
    {
        if (directory is not null)
        {
            string path = Path.Combine(directory, cardId + ".png");
            try
            {
                if (File.Exists(path))
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    if (!bytes.AsSpan().StartsWith(_pngSignature))
                    {
                        GD.PushWarning($"Ignoring local card artwork with no PNG signature: {path}; using generated art.");
                    }
                    else
                    {
                        using var image = new Image();
                        Error error = image.LoadPngFromBuffer(bytes);
                        if (error == Error.Ok && !image.IsEmpty())
                        {
                            return ImageTexture.CreateFromImage(image);
                        }

                        GD.PushWarning($"Ignoring invalid local card artwork: {path} ({error}); using generated art.");
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                GD.PushWarning($"Cannot read local card artwork: {path}; using generated art. {ex.Message}");
            }
        }

        string fallback = $"{Paths.CardArt}/{cardId}.png";
        return ResourceLoader.Exists(fallback) ? ResourceLoader.Load<Texture2D>(fallback) : null;
    }
}
