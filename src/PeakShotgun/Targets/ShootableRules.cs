using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Peak.Shotgun;

/// <summary>
/// Shootable target toggles, packed so the host can publish them as one room property.
/// Wire format: never renumber a bit, only append.
/// </summary>
[Flags]
internal enum ShootTargets
{
    None = 0,
    Zombies = 1 << 0,
    Mandrake = 1 << 1,
    Beetles = 1 << 2,
    Spiders = 1 << 3,
    Spores = 1 << 4,
    Scorpions = 1 << 5,
    Dynamite = 1 << 6,
    Scouts = 1 << 7,
    Scoutmaster = 1 << 8,
    Condors = 1 << 9,
    Antlions = 1 << 10,
    Frogs = 1 << 11,
    Beehives = 1 << 12,
    Ticks = 1 << 13,
    Jellyfish = 1 << 14,
    Coconuts = 1 << 15,
    Tumbleweeds = 1 << 16,
    BeeSwarms = 1 << 17,
    BingBong = 1 << 18,
    Balloons = 1 << 19,
}

/// <summary>One <c>[Targets]</c> switch: whether a rocket affects this kind of target. On by default; host's value.</summary>
internal sealed class ShootableRule
{
    private readonly Func<int>? hitsToDefeat;

    private ConfigEntry<bool>? entry;

    internal ShootableRule(string key, ShootTargets flag, string effect, Func<int>? hitsToDefeat = null)
    {
        Key = key;
        Flag = flag;
        Effect = effect;
        this.hitsToDefeat = hitsToDefeat;
    }

    /// <summary>Config key under <c>[Targets]</c>.</summary>
    internal string Key { get; }

    internal ShootTargets Flag { get; }

    /// <summary>What a rocket does to this target (the config description).</summary>
    internal string Effect { get; }

    internal bool LocalEnabled => entry?.Value ?? true;

    /// <summary>The host's switch in multiplayer, this player's offline.</summary>
    internal bool Enabled => (HostConfigSync.Shootables & Flag) != 0;

    /// <summary>Rockets to defeat it. Counted by the host, so the host's value applies.</summary>
    internal int HitsToDefeat => Mathf.Max(1, hitsToDefeat?.Invoke() ?? 1);

    internal void Bind(ConfigFile config) =>
        entry = config.Bind(ShootableRules.Section, Key, true, $"Rockets {Effect}. Host's value in multiplayer.");
}

/// <summary>
/// Every rocket target. To add one: append a <see cref="ShootTargets"/> bit, add a rule here, and (for a world
/// target) a <see cref="ShotTarget"/> in <see cref="ShotTargetRegistry"/>.
/// </summary>
internal static class ShootableRules
{
    internal const string Section = "Targets";

    internal const int DefaultScoutmasterHits = 3;

    private static ConfigEntry<int>? scoutmasterHits;

    internal static readonly ShootableRule Scouts = new(
        "Scouts", ShootTargets.Scouts, "injure and knock down other scouts (Standard mode only; TF2 mode never hurts scouts)");

    internal static readonly ShootableRule Zombies = new(
        "Zombies", ShootTargets.Zombies, "knock out zombies");

    internal static readonly ShootableRule Scoutmaster = new(
        "Scoutmaster",
        ShootTargets.Scoutmaster,
        "knock down Scoutmaster Myres; after ScoutmasterHits rockets he is gone for good",
        () => scoutmasterHits?.Value ?? DefaultScoutmasterHits);

    internal static readonly ShootableRule Spiders = new(
        "Spiders", ShootTargets.Spiders, "kill spiders (they drop whoever they were holding)");

    internal static readonly ShootableRule Beetles = new(
        "Beetles", ShootTargets.Beetles, "kill beetles");

    internal static readonly ShootableRule Scorpions = new(
        "Scorpions", ShootTargets.Scorpions, "kill scorpions");

    internal static readonly ShootableRule Frogs = new(
        "Frogs", ShootTargets.Frogs, "kill Gloom frogs (they drop cooked frog legs)");

    internal static readonly ShootableRule Condors = new(
        "Condors", ShootTargets.Condors, "kill Caldera condors (they drop whoever they were carrying)");

    internal static readonly ShootableRule Antlions = new(
        "Antlions", ShootTargets.Antlions, "kill Mesa antlions");

    internal static readonly ShootableRule Ticks = new(
        "Ticks", ShootTargets.Ticks, "knock ticks off scouts");

    internal static readonly ShootableRule Jellyfish = new(
        "Jellyfish", ShootTargets.Jellyfish, "smash Shore jellyfish");

    internal static readonly ShootableRule BeeSwarms = new(
        "BeeSwarms", ShootTargets.BeeSwarms, "scatter bee swarms");

    internal static readonly ShootableRule Beehives = new(
        "Beehives", ShootTargets.Beehives, "smash beehives (honeycomb drops, bees get angry)");

    internal static readonly ShootableRule Mandrake = new(
        "Mandrakes", ShootTargets.Mandrake, "destroy mandrakes");

    internal static readonly ShootableRule Spores = new(
        "Spores", ShootTargets.Spores, "break spore bombs and clear spore clouds");

    internal static readonly ShootableRule Dynamite = new(
        "Dynamite", ShootTargets.Dynamite, "set off dynamite");

    internal static readonly ShootableRule Coconuts = new(
        "Coconuts", ShootTargets.Coconuts, "knock coconuts off palm trees");

    internal static readonly ShootableRule Tumbleweeds = new(
        "Tumbleweeds", ShootTargets.Tumbleweeds, "push Mesa tumbleweeds");

    internal static readonly ShootableRule BingBong = new(
        "BingBong", ShootTargets.BingBong, "make Bing Bong squeak and fly");

    internal static readonly ShootableRule Balloons = new(
        "Balloons", ShootTargets.Balloons, "pop balloons");

    internal static readonly IReadOnlyList<ShootableRule> All =
    [
        Scouts,
        Zombies,
        Scoutmaster,
        Spiders,
        Beetles,
        Scorpions,
        Frogs,
        Condors,
        Antlions,
        Ticks,
        Jellyfish,
        BeeSwarms,
        Beehives,
        Mandrake,
        Spores,
        Dynamite,
        Coconuts,
        Tumbleweeds,
        BingBong,
        Balloons,
    ];

    internal static void Bind(ConfigFile config)
    {
        foreach (ShootableRule rule in All)
        {
            rule.Bind(config);
            if (rule == Scoutmaster)
            {
                scoutmasterHits = config.Bind(Section, "ScoutmasterHits", DefaultScoutmasterHits, new ConfigDescription(
                    "Rockets it takes to get rid of Scoutmaster Myres. Each earlier rocket knocks him down. Host's value in multiplayer.",
                    new AcceptableValueRange<int>(1, 10)));
            }
        }
    }

    /// <summary>This player's switches, as the host publishes them.</summary>
    internal static ShootTargets LocalShootables
    {
        get
        {
            ShootTargets targets = ShootTargets.None;
            foreach (ShootableRule rule in All)
            {
                if (rule.LocalEnabled)
                {
                    targets |= rule.Flag;
                }
            }

            return targets;
        }
    }
}
