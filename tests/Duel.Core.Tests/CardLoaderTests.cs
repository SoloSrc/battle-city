using System.IO;
using System.Linq;
using BattleCity.Duel.Core.Data;
using BattleCity.Duel.Core.Effects;
using BattleCity.Duel.Core.Model;
using Xunit;

namespace BattleCity.Duel.Core.Tests;

public class CardLoaderTests
{
    [Fact]
    public void LoadsTheCardPool()
    {
        CardLibrary library = new CardLoader().LoadDirectory(Cards.DataDirectory);

        Assert.Equal(79, library.Count);
        Assert.Equal(12, library.All.Count(c => c.IsVanilla));
        Assert.Equal(13, library.All.Count(c => c.Tier == 1));
        Assert.Equal(27, library.All.Count(c => c.Tier == 2));
        Assert.Equal(35, library.All.Count(c => c.Tier == 3));
        Assert.All(library.All.Where(c => c.Tier > 1), c => Assert.NotEmpty(c.Effects));

        // Every tier 1, 2 and 3 effect is implemented; the acceptance of issues #56 and #57.
        EffectRegistry registry = EffectRegistry.CreateDefault();
        Assert.All(library.All.Where(c => c.Tier <= 3).SelectMany(c => c.Effects), e => Assert.True(registry.Contains(e), $"effect '{e}' is not implemented"));

        CardDefinition elf = library["gemini_elf"];
        Assert.Equal("Gemini Elf", elf.Name);
        Assert.Equal(CardKind.Monster, elf.Kind);
        Assert.Equal(new MonsterStats("Spellcaster", MonsterAttribute.Earth, 4, 1900, 900, MonsterCategory.Normal), elf.Monster);
        Assert.Equal(3, elf.Limit);
        Assert.Equal(0, elf.Monster!.TributesRequired);

        CardDefinition skull = library["summoned_skull"];
        Assert.Equal(1, skull.Monster!.TributesRequired);

        CardDefinition pot = library["pot_of_greed"];
        Assert.Equal(CardKind.Spell, pot.Kind);
        Assert.Equal(SpellSubtype.Normal, pot.Spell!.Subtype);
        Assert.Equal(1, pot.Limit);
        Assert.Equal(new[] { PotOfGreedEffect.EffectId }, pot.Effects);
    }

    [Fact]
    public void FileNameMustMatchTheId()
    {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "wrong_name.json");
            File.Copy(Path.Combine(Cards.DataDirectory, "gemini_elf.json"), path);

            var error = Assert.Throws<CardDataException>(() => new CardLoader().LoadFile(path));
            Assert.Contains("does not match the file name", error.Message);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("""{"id":"x","name":"X","kind":"monster","monster":{"type":"Beast","attribute":"EARTH","level":4,"atk":1,"def":1,"category":"effect"},"tier":1,"limit":3,"effects":["not_yet"]}""", "tier 1 effect 'not_yet' is not implemented")]
    [InlineData("""{"id":"x","name":"X","kind":"monster","monster":{"type":"Beast","attribute":"EARTH","level":4,"atk":1,"def":1,"category":"effect"},"tier":2,"limit":3,"effects":["Not Yet"]}""", "effect id 'Not Yet' must be snake_case")]
    [InlineData("""{"id":"x","name":"X","kind":"monster","monster":{"type":"Beast","attribute":"EARTH","level":4,"atk":1,"def":1,"category":"normal"},"tier":1,"limit":3,"effects":["pot_of_greed"]}""", "a normal monster has no effects")]
    [InlineData("""{"id":"Bad Id","name":"X","kind":"spell","spell":{"subtype":"normal"},"tier":1,"limit":1}""", "'id' must be snake_case")]
    [InlineData("""{"id":"x","name":"X","kind":"ritual","tier":1,"limit":1}""", "'kind' has unknown value 'ritual'")]
    [InlineData("""{"id":"x","name":"X","kind":"monster","tier":1,"limit":1}""", "'monster' must be present")]
    [InlineData("""{"id":"x","name":"X","kind":"spell","spell":{"subtype":"normal"},"tier":5,"limit":1}""", "'tier' must be 1–4")]
    [InlineData("""{"id":"x","name":"X","kind":"spell","spell":{"subtype":"normal"},"tier":1,"limit":4}""", "'limit' must be 0–3")]
    [InlineData("""{"id":"x","name":"X","kind":"monster","monster":{"type":"Beast","attribute":"VOID","level":4,"atk":1,"def":1,"category":"normal"},"tier":1,"limit":3}""", "'monster.attribute' has unknown value 'VOID'")]
    [InlineData("""{"id":"x","name":"X","kind":"fusion","monster":{"type":"Beast","attribute":"EARTH","level":4,"atk":1,"def":1,"category":"fusion"},"tier":3,"limit":3,"materials":["a"]}""", "at least two 'materials'")]
    [InlineData("""not json""", "invalid JSON")]
    public void RejectsInvalidDocuments(string json, string expectedError)
    {
        var error = Assert.Throws<CardDataException>(() => new CardLoader().Parse(json));
        Assert.Contains(expectedError, error.Message);
    }

    [Fact]
    public void StubEffectsLoadButCannotBeDuelled()
    {
        CardLibrary library = new CardLoader().LoadDirectory(Cards.DataDirectory);
        CardDefinition jar = library["cyber_jar"];
        var deck = new Deck(Enumerable.Repeat(jar, 40).ToList());

        Assert.Equal(new[] { "cyber_jar" }, jar.Effects);
        var error = Assert.Throws<System.ArgumentException>(() => DuelEngine.Start(deck, deck));
        Assert.Contains("effects that are not implemented: cyber_jar", error.Message);
    }

    [Fact]
    public void RegistryRejectsUnknownEffects()
    {
        EffectRegistry registry = EffectRegistry.CreateDefault();

        Assert.True(registry.Contains(PotOfGreedEffect.EffectId));
        Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => registry.Create("cyber_jar"));
    }
}
