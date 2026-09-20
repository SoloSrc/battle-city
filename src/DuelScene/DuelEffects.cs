using System;
using System.Collections.Generic;
using BattleCity.Core;
using BattleCity.Rendering;
using Godot;

namespace BattleCity.DuelScene;

/// <summary>
/// The runtime side of the VFX hooks (systems.md §6.4) and the duel cues
/// (GDD §8): instantiates the artist's scenes under <c>vfx/</c> following
/// <c>vfx/README.md</c> (configure with world transforms, bind the card quad,
/// side colour, <c>finished</c> then self-free) and plays the WAVs under
/// <c>assets/audio/sfx</c>. A missing scene or cue is reported once and the
/// hook does nothing, so the presentation never depends on the assets.
/// Counts are kept for the acceptance scenes.
/// </summary>
public partial class DuelEffects : Node3D
{
    public const string CardMaterialise = "CardMaterialise";
    public const string CardSelected = "CardSelected";
    public const string SummonFlash = "SummonFlash";
    public const string AttackTrail = "AttackTrail";
    public const string HitPulse = "HitPulse";
    public const string Dissolve = "Dissolve";

    public static readonly IReadOnlyList<string> Effects = new[] { CardMaterialise, CardSelected, SummonFlash, AttackTrail, HitPulse, Dissolve };

    /// <summary>The GDD §8 duel cues delivered as <c>duel_&lt;cue&gt;.wav</c>.</summary>
    public static readonly IReadOnlyList<string> Cues = new[] { "draw", "summon", "set", "attack", "hit", "activate", "win", "lose" };

    /// <summary>Hologram edge colours of direction.md, used for the geometry effects of each side.</summary>
    public static readonly Color PlayerColor = new("79d8ff");
    public static readonly Color OpponentColor = new("ffa18b");

    private readonly Dictionary<string, PackedScene?> _scenes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AudioStreamPlayer?> _players = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _spawned = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _finished = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _played = new(StringComparer.Ordinal);

    /// <summary>Effects spawned and not yet finished (finite ones free themselves, <see cref="CardSelected"/> on <see cref="Stop"/>).</summary>
    public int Active { get; private set; }

    public IReadOnlyDictionary<string, int> Spawned => _spawned;

    public IReadOnlyDictionary<string, int> Finished => _finished;

    public IReadOnlyDictionary<string, int> SoundsPlayed => _played;

    public static Color ColorOf(HologramSide side) => side == HologramSide.Player ? PlayerColor : OpponentColor;

    /// <summary>Whether the scene of <paramref name="effect"/> exists; hooks fall back to their shader tweens when it does not.</summary>
    public bool Available(string effect) => Scene(effect) is not null;

    /// <summary>
    /// Instantiates <paramref name="effect"/> at <paramref name="from"/> (world), aimed at <paramref name="to"/> for the trail,
    /// bound to <paramref name="card"/> for the card-state effects, coloured for <paramref name="side"/>. Returns null when the scene is missing.
    /// </summary>
    public Node? Spawn(string effect, Transform3D from, Transform3D? to = null, HologramSide side = HologramSide.Player, GeometryInstance3D? card = null, Action? finished = null)
    {
        if (Scene(effect) is not { } scene)
        {
            return null;
        }

        Node node = scene.Instantiate();
        node.Set("autoplay", false);
        node.Set("color", ColorOf(side));
        AddChild(node);
        node.Call("configure", from, to ?? from);
        if (card is not null)
        {
            node.Call("bind_card", card);
        }

        Active++;
        _spawned[effect] = Count(_spawned, effect) + 1;
        node.Connect("finished", Callable.From(() =>
        {
            Active--;
            _finished[effect] = Count(_finished, effect) + 1;
            finished?.Invoke();
        }));
        node.Call("play");
        return node;
    }

    /// <summary>Stops a persistent effect (selection) or cuts a finite one short; safe on a freed node.</summary>
    public static void Stop(Node? effect)
    {
        if (effect is not null && IsInstanceValid(effect) && !effect.IsQueuedForDeletion())
        {
            effect.Call("stop");
        }
    }

    /// <summary>Plays a GDD §8 cue; unknown or missing cues are reported once.</summary>
    public void Play(string cue)
    {
        if (Player(cue) is { } player)
        {
            // The headless dummy audio driver does not drain playback resources reliably.
            // Still load the stream and count the cue so diagnostics validate event routing.
            if (!CardFaces.IsHeadless)
            {
                player.Play();
            }

            _played[cue] = Count(_played, cue) + 1;
        }
    }

    /// <summary>Stops every cue player. Headless runs never mix audio, so a playback left active leaks at exit; the acceptance scenes call this before they finish.</summary>
    public void Silence()
    {
        foreach (AudioStreamPlayer? player in _players.Values)
        {
            player?.Stop();
        }
    }

    private PackedScene? Scene(string effect)
    {
        if (_scenes.TryGetValue(effect, out PackedScene? cached))
        {
            return cached;
        }

        string path = $"{Paths.VfxRoot}/{effect}.tscn";
        PackedScene? scene = ResourceLoader.Exists(path) ? ResourceLoader.Load<PackedScene>(path) : null;
        if (scene is null)
        {
            GD.PushWarning($"DuelEffects: {path} is missing, the {effect} hook does nothing");
        }

        _scenes[effect] = scene;
        return scene;
    }

    private AudioStreamPlayer? Player(string cue)
    {
        if (_players.TryGetValue(cue, out AudioStreamPlayer? cached))
        {
            return cached;
        }

        string path = $"{Paths.DuelSfx}/duel_{cue}.wav";
        AudioStream? stream = ResourceLoader.Exists(path) ? ResourceLoader.Load<AudioStream>(path) : null;
        AudioStreamPlayer? player = null;
        if (stream is null)
        {
            GD.PushWarning($"DuelEffects: {path} is missing, the '{cue}' cue is silent");
        }
        else
        {
            player = new AudioStreamPlayer { Name = $"Sfx_{cue}", Stream = stream };
            AddChild(player);
        }

        _players[cue] = player;
        return player;
    }

    private static int Count(Dictionary<string, int> counts, string key) => counts.TryGetValue(key, out int n) ? n : 0;
}
