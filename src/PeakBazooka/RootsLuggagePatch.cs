using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Peak.Bazooka;

[HarmonyPatch(typeof(ItemDatabase), nameof(ItemDatabase.OnLoaded))]
internal static class ItemDatabasePatch
{
    [HarmonyPostfix]
    private static void CreateBazooka()
    {
        Plugin.Instance?.CreateFromAntiZooka();
    }
}

/// <summary>
/// Sets which <see cref="Spawner"/> is currently rolling loot so the bazooka inject
/// can target specific luggage instances.
/// </summary>
[HarmonyPatch(typeof(Spawner), nameof(Spawner.GetObjectsToSpawn))]
internal static class SpawnerLootContextPatch
{
    [HarmonyPrefix]
    private static void Begin(Spawner __instance) => RootsLuggagePatch.BeginSpawn(__instance);

    [HarmonyPostfix]
    private static void End() => RootsLuggagePatch.EndSpawn();
}

/// <summary>
/// Makes the bazooka an invalid loot roll while the current luggage's biome is at its cap, so
/// <c>LootData.GetRandomItems</c> fills the slot with something else instead of losing it.
/// </summary>
[HarmonyPatch(typeof(Item), nameof(Item.IsValidToSpawn))]
internal static class BazookaSpawnValidityPatch
{
    [HarmonyPostfix]
    private static void BlockAtCap(Item __instance, ref bool __result)
    {
        if (__result
            && Plugin.BazookaPrefab != null
            && __instance.gameObject == Plugin.BazookaPrefab
            && RootsLuggagePatch.BazookaBlockedForCurrentRoll())
        {
            __result = false;
        }
    }
}

/// <summary>
/// Roots: force <see cref="Plugin.GuaranteedLuggageBazookas"/> random suitcases (when the run allows the bazooka).
/// Every biome: hard-cap how many luggage can yield a bazooka this run. The bookkeeping lives in
/// <see cref="BazookaLootLedger"/>, so it survives quicksave reloads and host migration.
/// </summary>
[HarmonyPatch(typeof(LootData), nameof(LootData.GetRandomItems))]
internal static class RootsLuggagePatch
{
    private static Spawner? currentSpawner;

    // Set while re-rolling a blocked bazooka slot: the nested GetRandomItems must neither inject nor roll a bazooka.
    private static bool rollingReplacement;

    internal static void BeginSpawn(Spawner spawner) => currentSpawner = spawner;

    internal static void EndSpawn() => currentSpawner = null;

    /// <summary>
    /// Called from <see cref="BazookaSpawnValidityPatch"/>: while luggage rolls loot, the bazooka stops being a
    /// valid roll once its biome hit <see cref="Plugin.MaxBazookasPerBiome"/>, so the slot gets ordinary loot.
    /// </summary>
    internal static bool BazookaBlockedForCurrentRoll()
    {
        if (rollingReplacement)
        {
            return true;
        }

        if (currentSpawner is not Luggage luggage)
        {
            return false;
        }

        SpawnPool biome = LuggageBiomeKey(luggage, luggage.spawnPool);
        return BazookaLootLedger.CountInBiome(biome, except: luggage) >= Plugin.MaxBazookasPerBiome;
    }

    [HarmonyPostfix]
    private static void AfterLootRoll(
        SpawnPool spawnPool,
        int count,
        bool canRepeat,
        GameObject? fallback,
        ref List<GameObject>? __result)
    {
        if (rollingReplacement)
        {
            return;
        }

        TryGuaranteeRootsBazooka(spawnPool, count, ref __result);
        EnforcePerBiomeCap(spawnPool, canRepeat, fallback, ref __result);
    }

    private static void TryGuaranteeRootsBazooka(SpawnPool spawnPool, int count, ref List<GameObject>? __result)
    {
        // Forced rolls are always Roots-only; rare rolls can appear in any biome's luggage.
        if ((spawnPool & SpawnPool.LuggageRoots) == 0 || currentSpawner is not Luggage luggage)
        {
            return;
        }

        EnsureRandomLuggagePicked(luggage);
        if (!BazookaLootLedger.IsChosen(luggage))
        {
            return;
        }

        GameObject? bazooka = Plugin.BazookaPrefab;
        if (bazooka == null || count <= 0)
        {
            return;
        }

        if (__result == null)
        {
            __result = new List<GameObject>();
        }

        for (int i = 0; i < __result.Count; i++)
        {
            if (__result[i] == bazooka)
            {
                return;
            }
        }

        // The guarantee applies only while the run allows the bazooka (custom-run item settings, cap).
        Item? bazookaItem = bazooka.GetComponent<Item>();
        if (bazookaItem == null || !bazookaItem.IsValidToSpawn())
        {
            Plugin.Log.LogInfo(
                $"Skipped forced bazooka in Roots luggage '{luggage.name}': not valid to spawn in this run.");
            return;
        }

        // The spawner only fills `count` spots; a longer list silently drops its last item.
        if (__result.Count < count)
        {
            __result.Insert(0, bazooka);
        }
        else
        {
            __result[0] = bazooka;
        }

        Plugin.Log.LogInfo(
            $"Forced bazooka into random Roots luggage '{luggage.name}' "
            + $"({BazookaLootLedger.ChosenCount} marked this run).");
    }

    /// <summary>
    /// After loot (and Roots guarantees) are decided, count the bazookas against the biome cap. Bazookas past
    /// <see cref="Plugin.MaxBazookasPerBiome"/> are replaced with other loot from the same pool so the suitcase
    /// still spawns as many items as it rolled.
    /// </summary>
    private static void EnforcePerBiomeCap(
        SpawnPool spawnPool,
        bool canRepeat,
        GameObject? fallbackSpawn,
        ref List<GameObject>? __result)
    {
        GameObject? bazooka = Plugin.BazookaPrefab;
        if (__result == null || bazooka == null || currentSpawner is not Luggage luggage)
        {
            return;
        }

        SpawnPool biome = LuggageBiomeKey(luggage, spawnPool);
        if (!__result.Contains(bazooka))
        {
            // A suitcase re-rolled after a reload may no longer hold the bazooka it was counted for.
            BazookaLootLedger.SetBazookas(luggage, biome, 0);
            return;
        }

        int already = BazookaLootLedger.CountInBiome(biome, except: luggage);
        int allowed = Mathf.Max(0, Plugin.MaxBazookasPerBiome - already);
        int kept = 0;
        int blocked = 0;
        for (int i = 0; i < __result.Count;)
        {
            if (__result[i] != bazooka)
            {
                i++;
                continue;
            }

            if (kept < allowed)
            {
                kept++;
                i++;
                continue;
            }

            blocked++;
            GameObject? replacement = RollReplacement(spawnPool, canRepeat, fallbackSpawn, __result, bazooka);
            if (replacement == null)
            {
                __result.RemoveAt(i);
                continue;
            }

            __result[i] = replacement;
            i++;
        }

        if (blocked > 0)
        {
            Plugin.Log.LogInfo(
                $"Blocked {blocked} bazooka(s) in '{currentSpawner.name}' — biome {biome} already at cap "
                + $"{Plugin.MaxBazookasPerBiome} this run; replaced with other loot.");
        }

        BazookaLootLedger.SetBazookas(luggage, biome, kept);
        if (kept > 0)
        {
            Plugin.Log.LogInfo(
                $"Luggage '{currentSpawner.name}' biome {biome}: +{kept} bazooka "
                + $"({already + kept}/{Plugin.MaxBazookasPerBiome} this run).");
        }
    }

    /// <summary>Rolls one non-bazooka item from the same pool, preferring one the suitcase does not already hold.</summary>
    private static GameObject? RollReplacement(
        SpawnPool spawnPool,
        bool canRepeat,
        GameObject? fallbackSpawn,
        List<GameObject> current,
        GameObject bazooka)
    {
        List<GameObject>? roll;
        rollingReplacement = true;
        try
        {
            roll = LootData.GetRandomItems(spawnPool, current.Count + 1, canRepeat, fallbackSpawn);
        }
        finally
        {
            rollingReplacement = false;
        }

        if (roll == null)
        {
            return null;
        }

        GameObject? fallback = null;
        foreach (GameObject candidate in roll)
        {
            if (candidate == null || candidate == bazooka)
            {
                continue;
            }

            if (!current.Contains(candidate))
            {
                return candidate;
            }

            fallback ??= candidate;
        }

        return fallback;
    }

    internal static SpawnPool LuggageBiomeKey(Spawner spawner, SpawnPool rolledPool)
    {
        SpawnPool pool;
        try
        {
            pool = spawner.GetSpawnPool();
        }
        catch
        {
            pool = spawner.spawnPool;
        }

        SpawnPool luggage = pool & Plugin.AllLuggagePools;
        if (luggage == SpawnPool.None)
        {
            luggage = rolledPool & Plugin.AllLuggagePools;
        }

        return luggage == SpawnPool.None ? rolledPool : luggage;
    }

    /// <summary>
    /// Once per run: among Roots luggage still closed (plus the one opening now), pick
    /// <see cref="Plugin.GuaranteedLuggageBazookas"/> at random anywhere on the map.
    /// </summary>
    private static void EnsureRandomLuggagePicked(Luggage opening)
    {
        if (BazookaLootLedger.Assigned)
        {
            return;
        }

        var candidates = new List<Luggage>();
        foreach (Luggage luggage in Luggage.ALL_LUGGAGE)
        {
            if (UsesRootsPool(luggage))
            {
                candidates.Add(luggage);
            }
        }

        if (UsesRootsPool(opening) && !candidates.Contains(opening))
        {
            candidates.Add(opening);
        }

        if (candidates.Count == 0)
        {
            BazookaLootLedger.MarkAssigned(candidates);
            Plugin.Log.LogWarning("No Roots luggage found to mark for bazooka spawns.");
            return;
        }

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int take = Mathf.Min(Plugin.GuaranteedLuggageBazookas, candidates.Count);
        BazookaLootLedger.MarkAssigned(candidates.GetRange(0, take));

        Plugin.Log.LogInfo(
            $"Marked {take} random Roots luggage for a bazooka (of {candidates.Count} candidates).");
    }

    private static bool UsesRootsPool(Spawner spawner)
    {
        try
        {
            return (spawner.GetSpawnPool() & SpawnPool.LuggageRoots) != 0;
        }
        catch
        {
            return (spawner.spawnPool & SpawnPool.LuggageRoots) != 0;
        }
    }
}
