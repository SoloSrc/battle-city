using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace BattleCity.Characters;

/// <summary>
/// Glue between the shared-rig animation carrier (architecture.md §6.2) and a
/// loaded body: copies clips, strips the Blender <c>anim_</c> prefix, points
/// bone tracks at the body's skeleton, and injects call-method events from data.
/// </summary>
public static class AnimationRetarget
{
    private const string ActionPrefix = "anim_";

    /// <summary>Blender action <c>anim_walk</c> becomes clip <c>walk</c> (architecture §6.3).</summary>
    public static string ClipName(string animationName)
    {
        return animationName.StartsWith(ActionPrefix, StringComparison.Ordinal)
            ? animationName[ActionPrefix.Length..]
            : animationName;
    }

    /// <summary>
    /// Copies every clip of <paramref name="source"/> into a new library, with
    /// bone tracks rewritten to <paramref name="skeletonPath"/> (relative to the
    /// destination player's root) and method tracks pointed at that root.
    /// </summary>
    public static AnimationLibrary CopyClips(AnimationPlayer source, NodePath skeletonPath)
    {
        var library = new AnimationLibrary();
        foreach (StringName libraryName in source.GetAnimationLibraryList())
        {
            AnimationLibrary sourceLibrary = source.GetAnimationLibrary(libraryName);
            foreach (StringName animationName in sourceLibrary.GetAnimationList())
            {
                var animation = (Animation)sourceLibrary.GetAnimation(animationName).Duplicate(true);
                RetargetTracks(animation, skeletonPath);
                library.AddAnimation(ClipName(animationName.ToString()), animation);
            }
        }

        return library;
    }

    public static void RetargetTracks(Animation animation, NodePath skeletonPath)
    {
        string skeleton = skeletonPath.ToString();
        for (int i = 0; i < animation.GetTrackCount(); i++)
        {
            NodePath path = animation.TrackGetPath(i);
            switch (animation.TrackGetType(i))
            {
                case Animation.TrackType.Position3D:
                case Animation.TrackType.Rotation3D:
                case Animation.TrackType.Scale3D:
                    if (path.GetSubNameCount() > 0)
                    {
                        string bone = path.GetSubName(path.GetSubNameCount() - 1);
                        animation.TrackSetPath(i, new NodePath($"{skeleton}:{bone}"));
                    }

                    break;
                case Animation.TrackType.Method:
                    animation.TrackSetPath(i, new NodePath("."));
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Reads <c>data/rig/animation_events.json</c>: clip name → event name → seconds.
    /// Returns an empty table (and pushes an error) when the file is malformed.
    /// </summary>
    public static Dictionary<string, Dictionary<string, float>> LoadEventTable(string path)
    {
        var table = new Dictionary<string, Dictionary<string, float>>();
        if (!FileAccess.FileExists(path))
        {
            return table;
        }

        try
        {
            using var doc = JsonDocument.Parse(FileAccess.GetFileAsString(path));
            foreach (JsonProperty clip in doc.RootElement.EnumerateObject())
            {
                if (clip.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var events = new Dictionary<string, float>();
                foreach (JsonProperty entry in clip.Value.EnumerateObject())
                {
                    if (entry.Value.ValueKind == JsonValueKind.Number)
                    {
                        events[entry.Name] = entry.Value.GetSingle();
                    }
                }

                table[clip.Name] = events;
            }
        }
        catch (JsonException e)
        {
            GD.PushError($"{path} is not valid JSON: {e.Message}");
        }

        return table;
    }

    /// <summary>
    /// Adds one method track per clip calling <see cref="AnimationEvents.EventMethod"/>
    /// on the animation root with the event name. Returns the events injected.
    /// </summary>
    public static List<string> InjectEvents(AnimationLibrary library, Dictionary<string, Dictionary<string, float>> table)
    {
        var injected = new List<string>();
        foreach ((string clip, Dictionary<string, float> events) in table)
        {
            if (!library.HasAnimation(clip) || events.Count == 0)
            {
                continue;
            }

            Animation animation = library.GetAnimation(clip);
            int track = animation.AddTrack(Animation.TrackType.Method);
            animation.TrackSetPath(track, new NodePath("."));
            foreach ((string name, float time) in events)
            {
                var key = new Godot.Collections.Dictionary
                {
                    ["method"] = AnimationEvents.EventMethod,
                    ["args"] = new Godot.Collections.Array { name },
                };
                animation.TrackInsertKey(track, Mathf.Clamp(time, 0.0f, animation.Length), key);
                injected.Add($"{clip}:{name}@{time.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}");
            }
        }

        return injected;
    }

    /// <summary>Event names present on method tracks of a clip.</summary>
    public static List<string> MethodEvents(Animation animation)
    {
        var events = new List<string>();
        for (int track = 0; track < animation.GetTrackCount(); track++)
        {
            if (animation.TrackGetType(track) != Animation.TrackType.Method)
            {
                continue;
            }

            for (int key = 0; key < animation.TrackGetKeyCount(track); key++)
            {
                string name = animation.MethodTrackGetName(track, key).ToString();
                if (name == AnimationEvents.EventMethod)
                {
                    Godot.Collections.Array args = animation.MethodTrackGetParams(track, key);
                    name = args.Count > 0 ? args[0].AsString() : name;
                }

                if (!events.Contains(name))
                {
                    events.Add(name);
                }
            }
        }

        return events;
    }

    /// <summary>
    /// Ensures every clip in <paramref name="required"/> exists, aliasing missing
    /// ones to a delivered stand-in per <paramref name="fallbacks"/>. Returns the
    /// aliases made, as "missing→standin".
    /// </summary>
    public static List<string> FillMissingClips(
        AnimationLibrary library, IEnumerable<string> required, IReadOnlyDictionary<string, string> fallbacks)
    {
        var aliases = new List<string>();
        foreach (string clip in required)
        {
            if (library.HasAnimation(clip))
            {
                continue;
            }

            string candidate = clip;
            var visited = new HashSet<string>();
            while (!library.HasAnimation(candidate) && fallbacks.TryGetValue(candidate, out string? next) && visited.Add(candidate))
            {
                candidate = next;
            }

            if (!library.HasAnimation(candidate))
            {
                continue;
            }

            // A copy, so events injected for the missing clip do not leak into the stand-in.
            library.AddAnimation(clip, (Animation)library.GetAnimation(candidate).Duplicate(true));
            aliases.Add($"{clip}→{candidate}");
        }

        return aliases;
    }

    /// <summary>A short, empty, looping clip so the tree can run with no animations at all.</summary>
    public static Animation EmptyLoop(float length = 1.0f)
    {
        return new Animation { Length = length, LoopMode = Animation.LoopModeEnum.Linear };
    }

    /// <summary>Reads <c>data/rig/disk_mount.json</c> for one body type; identity when absent.</summary>
    public static Transform3D LoadDiskMountOffset(string path, string bodyType)
    {
        if (!FileAccess.FileExists(path))
        {
            return Transform3D.Identity;
        }

        try
        {
            using var doc = JsonDocument.Parse(FileAccess.GetFileAsString(path));
            if (!doc.RootElement.TryGetProperty(bodyType, out JsonElement body))
            {
                return Transform3D.Identity;
            }

            Vector3 position = ReadVector(body, "position");
            Vector3 rotation = ReadVector(body, "rotation_degrees");
            var basis = Basis.FromEuler(new Vector3(
                Mathf.DegToRad(rotation.X), Mathf.DegToRad(rotation.Y), Mathf.DegToRad(rotation.Z)));
            return new Transform3D(basis, position);
        }
        catch (JsonException e)
        {
            GD.PushError($"{path} is not valid JSON: {e.Message}");
            return Transform3D.Identity;
        }
    }

    private static Vector3 ReadVector(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out JsonElement array) || array.ValueKind != JsonValueKind.Array || array.GetArrayLength() != 3)
        {
            return Vector3.Zero;
        }

        return new Vector3(array[0].GetSingle(), array[1].GetSingle(), array[2].GetSingle());
    }
}
