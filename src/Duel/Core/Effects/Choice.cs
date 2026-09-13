using System;
using System.Collections.Generic;

namespace BattleCity.Duel.Core.Effects;

/// <summary>
/// A structured prompt an effect puts to its controller (systems.md §5.4):
/// costs and targets are chosen from <see cref="Options"/>. Tier 1 effects
/// ask nothing; the prompt type exists so tier 2 effects and the UI share
/// one contract from the start.
/// </summary>
public sealed record Choice(string Prompt, IReadOnlyList<Guid> Options, int Min, int Max);
