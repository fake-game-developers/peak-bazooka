using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Peak;
using Peak.Afflictions;
using PEAKLib.Core;
using PEAKLib.Items;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zorro.Core;

namespace Peak.Shotgun;

[BepInAutoPlugin("PeakBazooka")]
[BepInDependency(ItemsPlugin.Id)]
[BepInDependency(CorePlugin.Id)]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static Plugin Instance { get; private set; } = null!;

    internal static GameObject? ShotgunPrefab { get; private set; }

    internal static GameObject? ExplosionPrefab { get; private set; }

    private static ConfigEntry<int>? shots;

    /// <summary>Host-synced magazine size. 0 = infinite.</summary>
    internal static int ShotCount => HostConfigSync.ShotCount;

    internal static int LocalConfiguredShots => Mathf.Max(0, shots?.Value ?? 8);

    internal static bool InfiniteShots => ShotCount <= 0;

    private static ConfigEntry<float>? recoil;

    internal static float Recoil => Mathf.Clamp(recoil?.Value ?? 6f, 0f, 10f);

    private static ConfigEntry<int>? carryWeight;

    internal const int DefaultCarryWeight = 4;

    internal const int MaxCarryWeight = 20;

    internal static int CarryWeight =>
        Mathf.Clamp(carryWeight?.Value ?? DefaultCarryWeight, 0, MaxCarryWeight);

    private static ConfigEntry<float>? explosionRadius;

    /// <summary>Vanilla dynamite <see cref="ExplosionEffect.explosionRadius"/>.</summary>
    internal const float DynamiteExplosionRadius = 5f;

    /// <summary>Old default hit sphere. Much bigger than the 0.3-scale fireball.</summary>
    private const float OldDefaultExplosionRadius = 6f;

    internal const float DefaultExplosionVisualScale = 0.3f;

    internal const float DefaultExplosionRadius = DynamiteExplosionRadius * DefaultExplosionVisualScale;

    /// <summary>Hit radius in metres. The fireball is scaled to this same size.</summary>
    internal static float ExplosionRadius =>
        Mathf.Clamp(explosionRadius?.Value ?? DefaultExplosionRadius, 1f, 20f);

    private static ConfigEntry<float>? explosionVolume;

    /// <summary>Explosion SFX volume multiplier. 1 = vanilla dynamite loudness.</summary>
    internal static float ExplosionVolume => Mathf.Clamp(explosionVolume?.Value ?? 0.2f, 0f, 1f);

    internal static float ExplosionShake => 4f * (ExplosionRadius / DefaultExplosionRadius);

    private static ConfigEntry<bool>? explosionOneShot;

    internal static bool LocalExplosionOneShot => explosionOneShot?.Value ?? true;

    /// <summary>Host-synced: one rocket defeats a shootable instead of counting as a single pellet.</summary>
    internal static bool ExplosionOneShot => HostConfigSync.ExplosionOneShot;

    internal static int ExplosionHits =>
        ExplosionOneShot ? HitsToDefeatSettings.MaxHits : 1;

    private static ConfigEntry<float>? rocketSpeed;

    internal static float RocketSpeed => Mathf.Clamp(rocketSpeed?.Value ?? 38f, 8f, 80f);

    private static ConfigEntry<float>? selfDamage;

    /// <summary>Injury 0–1 from your own blast. 0 when <see cref="TF2RocketJump"/> is on.</summary>
    internal static float SelfDamage =>
        TF2RocketJump ? 0f : Mathf.Clamp(selfDamage?.Value ?? 0.05f, 0f, 1f);

    private static ConfigEntry<float>? selfKnockback;

    /// <summary>Point-blank self-launch speed in m/s. Falls off with distance. 0 = no rocket jump.</summary>
    internal static float SelfKnockback => Mathf.Clamp(selfKnockback?.Value ?? 16f, 0f, 40f);

    private static ConfigEntry<float>? selfKnockbackUp;

    /// <summary>Extra upward bias on the self-launch so a floor shot pops you up instead of sliding.</summary>
    internal static float SelfKnockbackUp => Mathf.Clamp(selfKnockbackUp?.Value ?? 0.75f, 0f, 2f);

    private static ConfigEntry<bool>? tf2RocketJump;

    /// <summary>TF2 soldier: WASD rocket jump, no ragdoll, no self-Injury.</summary>
    internal static bool TF2RocketJump => tf2RocketJump?.Value ?? false;

    /// <summary>Own-blast ragdoll. Off when <see cref="TF2RocketJump"/> is on.</summary>
    internal static bool SelfRagdoll => !TF2RocketJump;

    private static ConfigEntry<bool>? debugMode;

    internal static bool DebugMode => debugMode?.Value ?? false;

    private static ConfigEntry<bool>? giveShotgunOnSpawn;

    internal static bool GiveShotgunOnSpawn => giveShotgunOnSpawn?.Value ?? false;

    internal const int GuaranteedLuggageShotguns = 2;

    internal const int MaxShotgunsPerBiome = 4;

    internal static readonly SpawnPool AllLuggagePools =
        SpawnPool.LuggageBeach
        | SpawnPool.LuggageJungle
        | SpawnPool.LuggageTundra
        | SpawnPool.LuggageCaldera
        | SpawnPool.LuggageClimber
        | SpawnPool.LuggageAncient
        | SpawnPool.LuggageCursed
        | SpawnPool.LuggageMesa
        | SpawnPool.LuggageRoots
        | SpawnPool.LuggageGloom
        | SpawnPool.LuggageCitadel
        | SpawnPool.LuggageClown;

    internal static SpawnPool ShotgunLuggagePools => AllLuggagePools;

    private DateTime lastConfigWriteUtc;
    private float nextConfigCheck;

    private void Awake()
    {
        Instance = this;
        Log = Logger;
        shots = Config.Bind("Bazooka", "Shots", 8, new ConfigDescription(
            "Rockets in each bazooka. 0 = infinite. Host's value is used in multiplayer.",
            new AcceptableValueRange<int>(0, 999)));
        recoil = Config.Bind("Bazooka", "Recoil", 6f, new ConfigDescription(
            "Metres per second the shooter is pushed back, opposite the aim, on each shot. 0 = no recoil.",
            new AcceptableValueRange<float>(0f, 10f)));
        carryWeight = Config.Bind("Bazooka", "CarryWeight", DefaultCarryWeight, new ConfigDescription(
            "Stamina-bar weight of a carried bazooka, in PEAK item units. 1 = 2.5. Default 4 = 10 on the bar.",
            new AcceptableValueRange<int>(0, MaxCarryWeight)));
        explosionRadius = Config.Bind("Bazooka", "ExplosionRadius", DefaultExplosionRadius, new ConfigDescription(
            "Blast radius in metres. Hits and the fireball are this big.",
            new AcceptableValueRange<float>(1f, 20f)));
        explosionVolume = Config.Bind("Bazooka", "ExplosionVolume", 0.2f, new ConfigDescription(
            "Explosion loudness vs vanilla dynamite. Takes effect on the next blast; no restart.",
            new AcceptableValueRange<float>(0f, 1f)));
        explosionOneShot = Config.Bind(
            "Bazooka",
            "ExplosionOneShot",
            true,
            "One rocket knocks out everyone in the blast. When false, a rocket is one hit (zombies take two, Scoutmaster four). Host's value in multiplayer.");
        rocketSpeed = Config.Bind("Bazooka", "RocketSpeed", 38f, new ConfigDescription(
            "Rocket flight speed in metres per second.",
            new AcceptableValueRange<float>(8f, 80f)));
        selfDamage = Config.Bind("Bazooka", "SelfDamage", 0.05f, new ConfigDescription(
            "Injury from your own blast (0–1). Ignored when TF2RocketJump is on (then self-damage is 0). Other scouts still take full blast injury.",
            new AcceptableValueRange<float>(0f, 1f)));
        selfKnockback = Config.Bind("Bazooka", "SelfKnockback", 16f, new ConfigDescription(
            "How hard your own blast launches you, in metres per second at point-blank. Falls off to zero at ExplosionRadius. 0 = no rocket jump.",
            new AcceptableValueRange<float>(0f, 40f)));
        selfKnockbackUp = Config.Bind("Bazooka", "SelfKnockbackUp", 0.75f, new ConfigDescription(
            "Extra upward pull on a self-blast so shooting the ground pops you up instead of sideways.",
            new AcceptableValueRange<float>(0f, 2f)));
        tf2RocketJump = Config.Bind(
            "Bazooka",
            "TF2RocketJump",
            false,
            "TF2 soldier rocket jump: WASD in the air, no ragdoll, no self-Injury (and no fall Injury from the jump). Next blast; no restart.");
        giveShotgunOnSpawn = Config.Bind(
            "Bazooka",
            "GiveBazookaOnSpawn",
            false,
            "Host only: give every scout one bazooka in hand when they spawn into the climb or join mid-run (Shore and later — not at the Airport).");
        debugMode = Config.Bind("Debug", "EnableDebugMode", false, "Host spawns test zombies, a bazooka and luggage near the player at the Airport and on the Shore.");
        MigrateSelfRagdollToTf2();
        MigrateExplosionRadiusToMatchVisual();
        DropRetiredConfigKeys();
        GameCompat.PatchAll(new Harmony(Name ?? "PeakBazooka"));
        RunStartupCheck("game name check", GameCompat.CheckNamedMembers);
        RunStartupCheck("shot targets", ShotTargetRegistry.Initialize);
        gameObject.AddComponent<HostConfigSync>();
        StartCoroutine(WaitForAntiZooka());
        StartCoroutine(StartingShotgunGiver.WhenPlayersAreReady());
        shoreSpawnRoutine = StartCoroutine(ShoreTestSpawns.WhenTheShoreIsReady());
        SceneManager.sceneLoaded += OnSceneLoaded;
        lastConfigWriteUtc = SafeConfigWriteTime();
        Log.LogInfo($"Plugin {Name} is loaded.");
    }

    /// <summary>
    /// BepInEx keeps unbound keys and writes them back. Drop shotgun leftovers.
    /// </summary>
    private void DropRetiredConfigKeys()
    {
        object? bag = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(Config)
            ?? AccessTools.Field(typeof(ConfigFile), "orphanedEntries")?.GetValue(Config);
        if (bag is not Dictionary<ConfigDefinition, string> leftover || leftover.Count == 0)
        {
            return;
        }

        var drop = new List<ConfigDefinition>();
        foreach (ConfigDefinition key in leftover.Keys)
        {
            if (key.Section is "HitsToDefeat" or "Shootables"
                || key.Key is "EnableRecoil" or "CanSpawnOnAnyBiome" or "SelfRagdoll" or "ExplosionVisualScale")
            {
                drop.Add(key);
            }
        }

        if (drop.Count == 0)
        {
            return;
        }

        foreach (ConfigDefinition key in drop)
        {
            leftover.Remove(key);
        }

        Config.Save();
        lastConfigWriteUtc = SafeConfigWriteTime();
    }

    /// <summary>
    /// <c>SelfRagdoll = false</c> was the old TF2 switch. Copy it onto <see cref="TF2RocketJump"/> then drop the key.
    /// </summary>
    private void MigrateSelfRagdollToTf2()
    {
        object? bag = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(Config)
            ?? AccessTools.Field(typeof(ConfigFile), "orphanedEntries")?.GetValue(Config);
        if (bag is not Dictionary<ConfigDefinition, string> leftover)
        {
            return;
        }

        ConfigDefinition? selfRagdollKey = null;
        string? raw = null;
        foreach (KeyValuePair<ConfigDefinition, string> pair in leftover)
        {
            if (pair.Key.Key == "SelfRagdoll")
            {
                selfRagdollKey = pair.Key;
                raw = pair.Value;
                break;
            }
        }

        if (selfRagdollKey == null)
        {
            return;
        }

        leftover.Remove(selfRagdollKey);
        bool oldWasAirControl = raw != null
            && (raw.IndexOf("false", StringComparison.OrdinalIgnoreCase) >= 0 || raw.Trim() == "0");
        if (oldWasAirControl && tf2RocketJump != null && !tf2RocketJump.Value)
        {
            tf2RocketJump.Value = true;
            Log.LogInfo("Migrated SelfRagdoll = false to TF2RocketJump = true.");
        }

        Config.Save();
        lastConfigWriteUtc = SafeConfigWriteTime();
    }

    /// <summary>
    /// Old default was a 6m hit sphere around a ~1.5m fireball. If the player never changed
    /// <c>ExplosionRadius</c>, shrink it to match their <c>ExplosionVisualScale</c> (then that key is dropped).
    /// Custom radii are kept; the fireball now scales to them.
    /// </summary>
    private void MigrateExplosionRadiusToMatchVisual()
    {
        if (explosionRadius == null || !Mathf.Approximately(explosionRadius.Value, OldDefaultExplosionRadius))
        {
            return;
        }

        float visual = DefaultExplosionVisualScale;
        object? bag = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(Config)
            ?? AccessTools.Field(typeof(ConfigFile), "orphanedEntries")?.GetValue(Config);
        if (bag is Dictionary<ConfigDefinition, string> leftover)
        {
            foreach (KeyValuePair<ConfigDefinition, string> pair in leftover)
            {
                if (pair.Key.Key != "ExplosionVisualScale")
                {
                    continue;
                }

                if (float.TryParse(
                    pair.Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float parsed))
                {
                    visual = Mathf.Clamp(parsed, 0.1f, 1f);
                }

                break;
            }
        }

        float matched = Mathf.Clamp(DynamiteExplosionRadius * visual, 1f, 20f);
        explosionRadius.Value = matched;
        Log.LogInfo($"ExplosionRadius {OldDefaultExplosionRadius} → {matched} so the blast matches the fireball.");
        Config.Save();
        lastConfigWriteUtc = SafeConfigWriteTime();
    }

    private static void RunStartupCheck(string what, Action check)
    {
        try
        {
            check();
        }
        catch (Exception e)
        {
            Log.LogWarning($"Startup {what} failed ({e.GetType().Name}: {e.Message}).");
        }
    }

    private DateTime SafeConfigWriteTime()
    {
        try
        {
            return File.GetLastWriteTimeUtc(Config.ConfigFilePath);
        }
        catch (Exception)
        {
            return DateTime.MinValue;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < nextConfigCheck)
        {
            return;
        }

        nextConfigCheck = Time.unscaledTime + 0.5f;
        DateTime write = SafeConfigWriteTime();
        if (write == lastConfigWriteUtc)
        {
            return;
        }

        ReloadConfigFromDisk("cfg changed");
    }

    private void ReloadConfigFromDisk(string reason)
    {
        try
        {
            lastConfigWriteUtc = SafeConfigWriteTime();
            Config.Reload();
            DropRetiredConfigKeys();
            ApplyLuggageSpawnConfig();
            HostConfigSync.SyncFromLocalRole(reason);
            Log.LogInfo(
                $"Config reloaded ({reason}); shots = {(InfiniteShots ? "infinite" : ShotCount.ToString())}, TF2RocketJump = {TF2RocketJump}.");
        }
        catch (Exception e)
        {
            Log.LogWarning($"Config reload failed ({reason}): {e.Message}");
        }
    }

    internal static void ApplyShotCountConfig()
    {
        foreach (ShotgunInstanceSetup setup in ShotgunInstanceSetup.Active)
        {
            setup?.ApplyLocal();
            Action_Ammo? ammo = setup?.GetComponent<Action_Ammo>();
            if (InfiniteShots)
            {
                ammo?.ApplyInfinite();
            }
            else
            {
                ammo?.EnsureHostMagazine();
            }
        }

        if (ShotgunPrefab != null)
        {
            Item? prefabItem = ShotgunPrefab.GetComponent<Item>();
            if (prefabItem != null)
            {
                prefabItem.totalUses = InfiniteShots ? -1 : ShotCount;
                prefabItem.UIData.hideFuel = InfiniteShots;
            }
        }

        ShotgunAmmoUI.Refresh();
        ApplyCarryWeightConfig();
    }

    internal static void ApplyCarryWeightConfig()
    {
        int weight = CarryWeight;
        if (ShotgunPrefab != null)
        {
            Item? prefabItem = ShotgunPrefab.GetComponent<Item>();
            if (prefabItem != null)
            {
                prefabItem.carryWeight = weight;
            }
        }

        foreach (ShotgunInstanceSetup setup in ShotgunInstanceSetup.Active)
        {
            if (setup?.Item == null)
            {
                continue;
            }

            setup.Item.carryWeight = weight;
        }
    }

    private Coroutine? shoreSpawnRoutine;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
        {
            ShotgunCombat.ClearWorldHitCounts();
        }

        ReloadConfigFromDisk("map load");

        if (shoreSpawnRoutine != null)
        {
            StopCoroutine(shoreSpawnRoutine);
        }

        shoreSpawnRoutine = StartCoroutine(ShoreTestSpawns.WhenTheShoreIsReady());

        if (mode == LoadSceneMode.Single)
        {
            ShotgunLootLedger.OnMapLoaded();
        }
    }

    private IEnumerator WaitForAntiZooka()
    {
        var wait = new WaitForSecondsRealtime(0.5f);
        while (ShotgunPrefab == null)
        {
            try
            {
                CreateFromAntiZooka();
            }
            catch (Exception exception)
            {
                Log.LogWarning($"Could not create the bazooka yet: {exception.Message}");
            }

            if (ShotgunPrefab != null)
            {
                yield break;
            }

            yield return wait;
        }
    }

    private bool buildingShotgun;

    private bool reportedMissingAntiZooka;

    internal void CreateFromAntiZooka()
    {
        if (ShotgunPrefab != null || buildingShotgun)
        {
            return;
        }

        ItemDatabase? database = SingletonAsset<ItemDatabase>.Instance;
        if (database?.Objects == null || database.Objects.Count == 0)
        {
            return;
        }

        Item? source = FindAntiZooka(database);
        if (source == null)
        {
            if (!reportedMissingAntiZooka)
            {
                reportedMissingAntiZooka = true;
                Log.LogError($"The item catalog has {database.Objects.Count} items, and none is the AntiZooka.");
            }

            return;
        }

        buildingShotgun = true;
        try
        {
            BuildBazookaFromAntiZooka(source, database);
        }
        finally
        {
            buildingShotgun = false;
        }
    }

    private void BuildBazookaFromAntiZooka(Item source, ItemDatabase database)
    {
        Log.LogInfo($"Building the bazooka from {source.name}.");
        CacheExplosionPrefab(database);

        bool sourceWasActive = source.gameObject.activeSelf;
        if (sourceWasActive)
        {
            source.gameObject.SetActive(false);
        }

        GameObject gunObject = Instantiate(source.gameObject);
        if (sourceWasActive)
        {
            source.gameObject.SetActive(true);
        }

        gunObject.name = "Bazooka";
        gunObject.SetActive(false);
        DontDestroyOnLoad(gunObject);

        Transform spawn = gunObject.transform;
        Action_RaycastSpawnSomething? raySpawn = gunObject.GetComponent<Action_RaycastSpawnSomething>();
        if (raySpawn != null && raySpawn.rayOrigin != null)
        {
            spawn = raySpawn.rayOrigin;
        }

        foreach (Action_RaycastSpawnSomething extra in gunObject.GetComponents<Action_RaycastSpawnSomething>())
        {
            DestroyImmediate(extra);
        }

        foreach (Action_ReduceUses extra in gunObject.GetComponents<Action_ReduceUses>())
        {
            DestroyImmediate(extra);
        }

        StripItemAntigrav(gunObject, immediate: true);

        Action_Antizooka? juice = gunObject.GetComponent<Action_Antizooka>();
        if (juice != null)
        {
            juice.OnCastFinished = false;
            juice.OnPressed = false;
            juice.OnHeld = false;
            juice.OnReleased = false;
        }

        Item item = gunObject.GetComponent<Item>();
        item.usingTimePrimary = 0f;
        item.showUseProgress = false;
        item.totalUses = InfiniteShots ? -1 : ShotCount;
        item.carryWeight = CarryWeight;
        item.UIData.itemName = ShotgunText.ShotgunItemName;
        ApplyIcon(item, "Peak.Shotgun.icons.BazookaIcon.png", "Bazooka");
        item.UIData.hideFuel = InfiniteShots;
        item.UIData.hasMainInteract = true;
        item.UIData.mainInteractPrompt = ShotgunText.ShootPrompt;

        Action_Gun gun = gunObject.AddComponent<Action_Gun>();
        gun.maxDistance = 80f;
        gun.pelletRadius = 0.2f;
        gun.fireRate = 0.8f;
        gun.spread = 0f;
        gun.pelletCount = 1;
        gun.OnHeld = false;
        gun.OnPressed = true;
        gun.OnCastFinished = false;
        gun.spawnTransform = spawn;
        gun.afflictionsOnHit =
        [
            new Affliction_AdjustStatus(CharacterAfflictions.STATUSTYPE.Injury, 0.45f, 1f),
        ];
        if (gunObject.TryGetComponent(out ItemUseFeedback feedback))
        {
            feedback.sfxUsed = null;
        }

        gunObject.AddComponent<Action_Ammo>();
        gunObject.AddComponent<ShotgunInstanceSetup>();

        LootData loot = gunObject.GetComponent<LootData>() ?? gunObject.AddComponent<LootData>();
        loot.banInSolo = false;
        loot.rarityOverrides.Clear();
        ApplyLuggageSpawnConfig(loot);
        LootData.AllSpawnWeightData = null;

        ShotgunText.Inject();
        new ItemContent(item).Register(ModDefinition.GetOrCreate(Info));
        ShotgunPrefab = gunObject;
        Log.LogInfo(
            $"Registered the bazooka for all biomes' luggage ({GuaranteedLuggageShotguns} random Roots suitcases always forced).");
    }

    private static void CacheExplosionPrefab(ItemDatabase database)
    {
        if (ExplosionPrefab != null)
        {
            return;
        }

        for (int i = 0; i < database.Objects.Count; i++)
        {
            Item candidate = database.Objects[i];
            Dynamite? dynamite = candidate != null ? candidate.GetComponent<Dynamite>() : null;
            if (dynamite != null && dynamite.explosionPrefab != null)
            {
                ExplosionPrefab = dynamite.explosionPrefab;
                Log.LogInfo($"Explosion VFX uses {dynamite.explosionPrefab.name}.");
                return;
            }
        }

        Log.LogWarning("No dynamite explosion prefab was found. Blasts will still deal damage.");
    }

    /// <summary>
    /// AntiZooka floats when dropped via <see cref="Antigrav"/>. The bazooka is a normal item — strip that.
    /// </summary>
    internal static void StripItemAntigrav(GameObject gunObject, bool immediate)
    {
        foreach (Antigrav extra in gunObject.GetComponents<Antigrav>())
        {
            extra.enabled = false;
            extra.intensity = 0f;
            if (immediate)
            {
                DestroyImmediate(extra);
            }
            else
            {
                Destroy(extra);
            }
        }

        foreach (Action_ApplyAntigrav extra in gunObject.GetComponents<Action_ApplyAntigrav>())
        {
            extra.enabled = false;
            if (immediate)
            {
                DestroyImmediate(extra);
            }
            else
            {
                Destroy(extra);
            }
        }

        Item? item = gunObject.GetComponent<Item>();
        if (item != null)
        {
            item.antigrav = null;
        }
    }

    internal static void ApplyLuggageSpawnConfig(LootData? loot = null)
    {
        if (loot == null && ShotgunPrefab != null)
        {
            loot = ShotgunPrefab.GetComponent<LootData>();
        }

        if (loot == null)
        {
            return;
        }

        loot.spawnLocations = ShotgunLuggagePools;
        loot.Rarity = Rarity.RidiculouslyRare;
        LootData.AllSpawnWeightData = null;
    }

    private static void ApplyIcon(Item item, string resourceName, string displayName)
    {
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            Log.LogWarning($"{displayName} icon resource was not found.");
            return;
        }

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(memory.ToArray()))
        {
            Log.LogWarning($"{displayName} icon could not be decoded.");
            return;
        }

        texture.name = displayName;
        texture.wrapMode = TextureWrapMode.Clamp;
        item.UIData.icon = texture;
    }

    private static Item? FindAntiZooka(ItemDatabase database)
    {
        for (int i = 0; i < database.Objects.Count; i++)
        {
            Item candidate = database.Objects[i];
            if (candidate != null && candidate.GetComponent<Action_Antizooka>() != null)
            {
                return candidate;
            }
        }

        return null;
    }
}
