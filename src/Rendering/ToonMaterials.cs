using System;
using System.Collections.Generic;
using BattleCity.Core;
using Godot;

namespace BattleCity.Rendering;

/// <summary>
/// Applies the shared cel material (<c>shaders/materials/toon.tres</c>, issue #27)
/// to every mesh surface whose imported material is named <c>toon_*</c>
/// (architecture.md §6.4). One <see cref="ShaderMaterial"/> is derived per
/// source material, carrying its colour and albedo texture; all other
/// parameters and the outline pass stay shared, so tuning the .tres tunes
/// every character, prop and kit piece at once. Called by <c>Character</c>,
/// <c>DuelDisk</c> and <c>Game</c> when a body, prop or level loads, and by
/// the diagnostic scenes.
/// </summary>
public static class ToonMaterials
{
    public const string Prefix = "toon_";

    private static ShaderMaterial? _base;
    private static readonly Dictionary<Material, ShaderMaterial> _derived = new();

    /// <summary>The shared material, loaded on first use; null when the resource is missing.</summary>
    public static ShaderMaterial? Base
    {
        get
        {
            if (_base is null && ResourceLoader.Exists(Paths.ToonMaterial))
            {
                _base = ResourceLoader.Load<ShaderMaterial>(Paths.ToonMaterial);
            }

            return _base;
        }
    }

    /// <summary>True for a material that the glTF importer named with the toon prefix.</summary>
    public static bool IsToonNamed(Material material) =>
        material.ResourceName.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>True for a material already derived from the shared toon material.</summary>
    public static bool IsToon(Material material) =>
        material is ShaderMaterial shader && Base is not null && shader.Shader == Base.Shader;

    /// <summary>
    /// Replaces every <c>toon_*</c> surface under <paramref name="root"/> with its
    /// derived toon material. Returns how many surfaces were converted; surfaces
    /// already converted and materials without the prefix are left alone.
    /// </summary>
    public static int Apply(Node root)
    {
        if (Base is null)
        {
            GD.PushError($"ToonMaterials: {Paths.ToonMaterial} is missing, toon_* materials stay as imported");
            return 0;
        }

        int converted = 0;
        foreach (MeshInstance3D instance in Meshes(root))
        {
            Mesh? mesh = instance.Mesh;
            if (mesh is null)
            {
                continue;
            }

            for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                Material? source = instance.GetActiveMaterial(surface);
                if (source is null || IsToon(source) || !IsToonNamed(source))
                {
                    continue;
                }

                instance.SetSurfaceOverrideMaterial(surface, Derive(source));
                converted++;
            }
        }

        return converted;
    }

    /// <summary>
    /// The toon material for <paramref name="source"/>: a copy of the shared one with
    /// the source's albedo colour and texture. Cached per source resource, so the
    /// same imported material always yields the same toon material.
    /// </summary>
    public static ShaderMaterial Derive(Material source)
    {
        if (_derived.TryGetValue(source, out ShaderMaterial? cached))
        {
            return cached;
        }

        var material = (ShaderMaterial)Base!.Duplicate();
        material.ResourceName = source.ResourceName;
        if (source is BaseMaterial3D standard)
        {
            material.SetShaderParameter(ToonParameters.Albedo, standard.AlbedoColor);
            if (standard.AlbedoTexture is not null)
            {
                material.SetShaderParameter(ToonParameters.AlbedoTexture, standard.AlbedoTexture);
            }
        }

        _derived[source] = material;
        return material;
    }

    private static IEnumerable<MeshInstance3D> Meshes(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            Node current = stack.Pop();
            if (current is MeshInstance3D instance)
            {
                yield return instance;
            }

            foreach (Node child in current.GetChildren())
            {
                stack.Push(child);
            }
        }
    }
}

/// <summary>Uniform names of <c>shaders/toon.gdshader</c> that code sets (the rest are artist tuning).</summary>
public static class ToonParameters
{
    public const string Albedo = "albedo";
    public const string AlbedoTexture = "albedo_texture";
}
