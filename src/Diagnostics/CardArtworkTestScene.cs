using System;
using System.IO;
using System.Linq;
using BattleCity.Core;
using BattleCity.DuelScene;
using Godot;

namespace BattleCity.Diagnostics;

/// <summary>Offline resolver regression tests, isolated from the user's downloaded files.</summary>
public partial class CardArtworkTestScene : Node
{
    private int _passCount;
    private int _failCount;

    public override void _Ready()
    {
        string directory = Path.Combine(Path.GetTempPath(), "battle-city-art-" + Guid.NewGuid());
        bool previousUseLocalArt = CardArtwork.UseLocalArt;
        Directory.CreateDirectory(directory);
        try
        {
            const string id = "airknight_parshath";
            string path = Path.Combine(directory, id + ".png");
            var fallback = ResourceLoader.Load<Texture2D>($"{Paths.CardArt}/{id}.png");
            Check(CardArtwork.LoadFromDirectory(id, directory) == fallback, "missing override uses generated art");
            using var image = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
            image.Fill(Colors.Magenta);
            Check(image.SavePng(path) == Error.Ok, "fixture written");
            Check(CardArtwork.LoadFromDirectory(id, directory)?.GetImage().GetPixel(0, 0) == Colors.Magenta,
                "unimported PNG takes precedence");
            File.WriteAllText(path, "not a PNG");
            Check(CardArtwork.LoadFromDirectory(id, directory) == fallback, "corrupt override falls back");
            File.Delete(path);
            Check(CardArtwork.LoadFromDirectory(id, directory) == fallback, "removed override returns to generated art");
            Check(CardArtwork.LoadFromDirectory(id, null) == fallback, "export/no override directory uses generated art");
            Check(CardArtwork.LoadFromDirectory("nonexistent_test_card", directory) is null, "missing both sources remains supported");
            CardArtwork.UseLocalArt = false;
            Check(CardArtwork.Load(id) == fallback, "diagnostic opt-out uses generated art");
            CardArtwork.UseLocalArt = previousUseLocalArt;
            if (!previousUseLocalArt)
            {
                Check(CardArtwork.Load(id) == fallback, "command-line opt-out uses generated art");
            }
            string local = ProjectSettings.GlobalizePath($"{Paths.LocalCardArt}/{id}.png");
            if (previousUseLocalArt && File.Exists(local))
            {
                using var expected = new Image();
                Check(expected.Load(local) == Error.Ok, "download decodes");
                Check(CardArtwork.Load(id)!.GetImage().GetData().SequenceEqual(expected.GetData()),
                    "production resolver uses actual downloaded pixels");
            }
        }
        catch (Exception ex)
        {
            Check(false, "unexpected exception: " + ex);
        }
        finally
        {
            CardArtwork.UseLocalArt = previousUseLocalArt;
            Directory.Delete(directory, true);
        }

        GD.Print($"CardArtworkTest summary: {_passCount} pass, {_failCount} fail");
        GetTree().Quit(_failCount == 0 ? 0 : 1);
    }

    private void Check(bool condition, string label)
    {
        if (!condition)
        {
            _failCount++;
            GD.Print("CardArtworkTest FAIL: " + label);
            return;
        }

        _passCount++;
        GD.Print("CardArtworkTest PASS: " + label);
    }
}
