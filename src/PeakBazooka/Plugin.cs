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

namespace Peak.Bazooka;

[BepInAutoPlugin("PeakBazooka")]
[BepInDependency(ItemsPlugin.Id)]
[BepInDependency(CorePlugin.Id)]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static Plugin Instance { get; private set; } = null!;

    internal static GameObject? BazookaPrefab { get; private set; }

    internal static GameObject? ExplosionPrefab { get; private set; }

    private static ConfigEntry<int>? shots;

    /// <summary>Host-synced magazine size. 0 = infinite.</summary>
    internal static int ShotCount => HostConfigSync.ShotCount;

    internal static int LocalConfiguredShots => Mathf.Max(0, shots?.Value ?? 0);

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

    /// <summary>Old default hit sphere, from before the fireball was scaled to <c>ExplosionRadius</c>.</summary>
    private const float OldDefaultExplosionRadius = 6f;

    internal const float DefaultExplosionRadius = 3.5f;

    /// <summary>Fireball radius in metres. Hits reach <see cref="BlastReach"/>.</summary>
    internal static float ExplosionRadius =>
        Mathf.Clamp(explosionRadius?.Value ?? DefaultExplosionRadius, 1f, 20f);

    private static ConfigEntry<float>? explosionVolume;

    /// <summary>Explosion SFX volume multiplier. 1 = vanilla dynamite loudness.</summary>
    internal static float ExplosionVolume => Mathf.Clamp(explosionVolume?.Value ?? 0.5f, 0f, 1f);

    /// <summary>Camera shake near a blast: 9 at the default size, growing with the radius.</summary>
    internal static float ExplosionShake => 9f * (ExplosionRadius / DefaultExplosionRadius);

    private static ConfigEntry<float>? muzzleVelocity;

    /// <summary>How fast a rocket leaves the muzzle, m/s. With its slight drop and 6s burn, this sets its range.</summary>
    internal static float MuzzleVelocity => Mathf.Clamp(muzzleVelocity?.Value ?? 50f, 8f, 80f);

    private static ConfigEntry<BazookaMode>? mode;

    internal static BazookaMode LocalMode => mode?.Value ?? BazookaMode.Standard;

    /// <summary>Host-synced. Standard = a weapon; RocketJump = no scout damage, rocket jumps fly.</summary>
    internal static BazookaMode Mode => HostConfigSync.Mode;

    internal static bool RocketJumpMode => Mode == BazookaMode.RocketJump;

    private static ConfigEntry<float>? selfDamage;

    /// <summary>Standard mode: Injury 0–1 from your own blast at point-blank.</summary>
    internal static float SelfDamage => Mathf.Clamp(selfDamage?.Value ?? 0.25f, 0f, 1f);

    private static ConfigEntry<float>? selfKnockback;

    /// <summary>Standard mode: point-blank ragdoll launch speed in m/s. Falls off with distance. 0 = none.</summary>
    internal static float SelfKnockback => Mathf.Clamp(selfKnockback?.Value ?? 22f, 0f, 40f);

    private static ConfigEntry<float>? selfKnockbackUp;

    /// <summary>Standard mode: extra upward bias on the self-launch so a floor shot pops you up.</summary>
    internal static float SelfKnockbackUp => Mathf.Clamp(selfKnockbackUp?.Value ?? 0.75f, 0f, 2f);

    /// <summary>
    /// Hits reach at least this far. A small fireball as the hit sphere meant a near miss did nothing; the push
    /// reaches well past what you see, and is weakest at the edge.
    /// </summary>
    internal const float MinBlastReach = 6f;

    internal const float DefaultKnockback = 30f;

    internal const float MaxKnockback = 60f;

    private static ConfigEntry<float>? knockback;

    /// <summary>
    /// Speed in m/s a blast throws zombies and other scouts at its middle (40% at its edge). The shooter's value
    /// travels with each hit, so everyone is thrown alike.
    /// </summary>
    internal static float Knockback => Mathf.Clamp(knockback?.Value ?? DefaultKnockback, 0f, MaxKnockback);

    /// <summary><see cref="Knockback"/> relative to its default, for pushes on creatures and loose items.</summary>
    internal static float KnockbackScale => Knockback / DefaultKnockback;

    /// <summary>How far a rocket hits: <see cref="ExplosionRadius"/>, but at least <see cref="MinBlastReach"/>.</summary>
    internal static float BlastReach => Mathf.Max(ExplosionRadius, MinBlastReach);

    private static ConfigEntry<float>? jumpLaunchSpeed;

    /// <summary>RocketJump mode: point-blank launch speed in m/s. Half of it at the edge of the blast.</summary>
    internal static float JumpLaunchSpeed => Mathf.Clamp(jumpLaunchSpeed?.Value ?? 20f, 0f, RocketFlight.MaxSpeed);

    private static ConfigEntry<float>? jumpLaunchUp;

    internal static float JumpLaunchUp => Mathf.Clamp(jumpLaunchUp?.Value ?? 0.6f, 0f, 2f);

    private static ConfigEntry<float>? jumpAirControl;

    /// <summary>RocketJump mode: WASD acceleration in the air, m/s².</summary>
    internal static float JumpAirControl => Mathf.Clamp(jumpAirControl?.Value ?? 10f, 0f, 100f);

    private static ConfigEntry<float>? jumpGravity;

    /// <summary>RocketJump mode: gravity during a rocket jump, m/s².</summary>
    internal static float JumpGravity => Mathf.Clamp(jumpGravity?.Value ?? 10f, 1f, 40f);

    private static ConfigEntry<bool>? giveBazookaOnSpawn;

    internal static bool GiveBazookaOnSpawn => giveBazookaOnSpawn?.Value ?? false;

    internal const int GuaranteedLuggageBazookas = 2;

    internal const int MaxBazookasPerBiome = 4;

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

    internal static SpawnPool BazookaLuggagePools => AllLuggagePools;

    private DateTime lastConfigWriteUtc;
    private float nextConfigCheck;

    private void Awake()
    {
        Instance = this;
        Log = Logger;
        RenameOldModeValue();
        mode = Config.Bind(
            "Bazooka",
            "Mode",
            BazookaMode.Standard,
            "Standard: a weapon. Rockets injure and ragdoll scouts, and your own blast flings and hurts you. "
            + "RocketJump: rockets never hurt scouts (you or anyone else); your own blast is a rocket jump you steer with WASD in the air. "
            + "Creatures are hit in both modes. Host's value is used in multiplayer.");
        shots = Config.Bind("Bazooka", "Shots", 0, new ConfigDescription(
            "Rockets in each bazooka. 0 = infinite; any other number is a limited magazine with no refill. Host's value is used in multiplayer.",
            new AcceptableValueRange<int>(0, 999)));
        recoil = Config.Bind("Bazooka", "Recoil", 6f, new ConfigDescription(
            "Metres per second the shooter is pushed back, opposite the aim, on each shot. 0 = no recoil.",
            new AcceptableValueRange<float>(0f, 10f)));
        carryWeight = Config.Bind("Bazooka", "CarryWeight", DefaultCarryWeight, new ConfigDescription(
            "Stamina-bar weight of a carried bazooka, in PEAK item units. 1 = 2.5. Default 4 = 10 on the bar.",
            new AcceptableValueRange<int>(0, MaxCarryWeight)));
        explosionRadius = Config.Bind("Bazooka", "ExplosionRadius", DefaultExplosionRadius, new ConfigDescription(
            $"Fireball radius in metres. Hits reach this far, but at least {MinBlastReach}m.",
            new AcceptableValueRange<float>(1f, 20f)));
        explosionVolume = Config.Bind("Bazooka", "ExplosionVolume", 0.5f, new ConfigDescription(
            "Explosion loudness vs vanilla dynamite. Takes effect on the next blast; no restart.",
            new AcceptableValueRange<float>(0f, 1f)));
        muzzleVelocity = Config.Bind("Bazooka", "MuzzleVelocity", 50f, new ConfigDescription(
            "How fast a rocket leaves the muzzle, in metres per second. Rockets drop slightly and burn out after 6 seconds, "
            + "so this is also how far and how flat they fly: higher = straighter, longer shots; lower = a lobbed arc.",
            new AcceptableValueRange<float>(8f, 80f)));
        selfDamage = Config.Bind("Bazooka", "SelfDamage", 0.25f, new ConfigDescription(
            "Standard mode: Injury from your own blast at point-blank (0–1); half that at the edge of the blast. RocketJump mode: none. Other scouts take 0.4.",
            new AcceptableValueRange<float>(0f, 1f)));
        knockback = Config.Bind("Bazooka", "Knockback", DefaultKnockback, new ConfigDescription(
            "How hard a blast throws zombies, other scouts, creatures and loose items, in metres per second at its middle (40% at its edge). "
            + "Your own blast is SelfKnockback (Standard) or [RocketJump] LaunchSpeed. The shooter's value is used.",
            new AcceptableValueRange<float>(0f, MaxKnockback)));
        selfKnockback = Config.Bind("Bazooka", "SelfKnockback", 22f, new ConfigDescription(
            $"Standard mode: how hard your own blast flings you (ragdolled), in metres per second at point-blank. Falls off to zero at the edge of the blast ({MinBlastReach}m, or ExplosionRadius if bigger). 0 = none.",
            new AcceptableValueRange<float>(0f, 40f)));
        selfKnockbackUp = Config.Bind("Bazooka", "SelfKnockbackUp", 0.75f, new ConfigDescription(
            "Standard mode: extra upward pull on a self-blast so shooting the ground pops you up instead of sideways.",
            new AcceptableValueRange<float>(0f, 2f)));
        jumpLaunchSpeed = Config.Bind(RocketJumpSection, "LaunchSpeed", 20f, new ConfigDescription(
            $"Rocket jump speed in metres per second at point-blank; half that at the edge of the blast ({MinBlastReach}m, or ExplosionRadius if bigger). Rockets fired mid-air chain: one under you while falling cancels the fall, then launches you (speed capped at {RocketFlight.MaxSpeed}).",
            new AcceptableValueRange<float>(0f, RocketFlight.MaxSpeed)));
        jumpLaunchUp = Config.Bind(RocketJumpSection, "LaunchUp", 0.6f, new ConfigDescription(
            "Extra upward pull on a rocket jump. 0 = straight away from the blast.",
            new AcceptableValueRange<float>(0f, 2f)));
        jumpAirControl = Config.Bind(RocketJumpSection, "AirControl", 10f, new ConfigDescription(
            "How fast WASD steers or brakes you during a rocket jump, in m/s². Holding forward never pushes you past running speed.",
            new AcceptableValueRange<float>(0f, 100f)));
        jumpGravity = Config.Bind(RocketJumpSection, "Gravity", 10f, new ConfigDescription(
            "Gravity during a rocket jump, in m/s². Lower = floatier, longer jumps.",
            new AcceptableValueRange<float>(1f, 40f)));
        giveBazookaOnSpawn = Config.Bind(
            "Bazooka",
            "GiveBazookaOnSpawn",
            false,
            "Host only: give every scout one bazooka in hand when they spawn into the climb or join mid-run (Shore and later — not at the Airport).");
        ShootableRules.Bind(Config);
        MigrateOldRocketJumpSwitches();
        MigrateRenamedSetting("Bazooka", "RocketSpeed", muzzleVelocity);
        MigrateRenamedSetting(OldRocketJumpSection, "LaunchSpeed", jumpLaunchSpeed);
        MigrateRenamedSetting(OldRocketJumpSection, "LaunchUp", jumpLaunchUp);
        MigrateRenamedSetting(OldRocketJumpSection, "AirControl", jumpAirControl);
        MigrateRenamedSetting(OldRocketJumpSection, "Gravity", jumpGravity);
        MigrateExplosionRadiusToMatchVisual();
        DropRetiredConfigKeys();
        GameCompat.PatchAll(new Harmony(Name ?? "PeakBazooka"));
        RunStartupCheck("game name check", GameCompat.CheckNamedMembers);
        RunStartupCheck("shot targets", ShotTargetRegistry.Initialize);
        gameObject.AddComponent<HostConfigSync>();
        StartCoroutine(WaitForAntiZooka());
        StartCoroutine(StartingBazookaGiver.WhenPlayersAreReady());
        SceneManager.sceneLoaded += OnSceneLoaded;
        lastConfigWriteUtc = SafeConfigWriteTime();
        Log.LogInfo($"Plugin {Name} is loaded.");
    }

    /// <summary>
    /// BepInEx keeps unbound keys and writes them back. Drop keys retired in earlier versions.
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
            if (key.Section is "HitsToDefeat" or "Shootables" or "Debug"
                || key.Key is "EnableRecoil" or "CanSpawnOnAnyBiome" or "SelfRagdoll" or "TF2RocketJump" or "ExplosionVisualScale" or "ExplosionOneShot")
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

    /// <summary>Settings section of the rocket jump tuning; it was <c>[TF2]</c> when the mode was called TF2.</summary>
    internal const string RocketJumpSection = "RocketJump";

    private const string OldRocketJumpSection = "TF2";

    /// <summary>
    /// <c>Mode = TF2</c> is now <c>Mode = RocketJump</c> (same number on the wire). Rewrite the unbound value before
    /// <c>Config.Bind</c> parses it, or BepInEx would not know the name and fall back to Standard.
    /// </summary>
    private void RenameOldModeValue()
    {
        object? bag = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(Config)
            ?? AccessTools.Field(typeof(ConfigFile), "orphanedEntries")?.GetValue(Config);
        var key = new ConfigDefinition("Bazooka", "Mode");
        if (bag is Dictionary<ConfigDefinition, string> leftover && leftover.TryGetValue(key, out string raw)
            && raw.Trim().Equals("TF2", StringComparison.OrdinalIgnoreCase))
        {
            leftover[key] = nameof(BazookaMode.RocketJump);
            Log.LogInfo("Renamed Mode = TF2 to Mode = RocketJump.");
        }
    }

    /// <summary>Carries a player's value over from a setting's old name to <paramref name="renamed"/>, then drops the old key.</summary>
    private void MigrateRenamedSetting(string section, string oldKey, ConfigEntry<float>? renamed)
    {
        object? bag = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(Config)
            ?? AccessTools.Field(typeof(ConfigFile), "orphanedEntries")?.GetValue(Config);
        var old = new ConfigDefinition(section, oldKey);
        if (renamed == null || bag is not Dictionary<ConfigDefinition, string> leftover
            || !leftover.TryGetValue(old, out string raw))
        {
            return;
        }

        leftover.Remove(old);
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            renamed.Value = value;
            Log.LogInfo($"Moved {oldKey} = {value} to {renamed.Definition.Key}.");
        }

        Config.Save();
        lastConfigWriteUtc = SafeConfigWriteTime();
    }

    /// <summary>
    /// The old rocket jump switches were <c>TF2RocketJump = true</c> and, before that, <c>SelfRagdoll = false</c>.
    /// Either one turns on <see cref="BazookaMode.RocketJump"/>; both keys are dropped.
    /// </summary>
    private void MigrateOldRocketJumpSwitches()
    {
        object? bag = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(Config)
            ?? AccessTools.Field(typeof(ConfigFile), "orphanedEntries")?.GetValue(Config);
        if (bag is not Dictionary<ConfigDefinition, string> leftover)
        {
            return;
        }

        var found = new List<ConfigDefinition>();
        bool wantedRocketJump = false;
        foreach (KeyValuePair<ConfigDefinition, string> pair in leftover)
        {
            string raw = pair.Value?.Trim() ?? "";
            if (pair.Key.Key == "TF2RocketJump")
            {
                found.Add(pair.Key);
                wantedRocketJump |= raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1";
            }
            else if (pair.Key.Key == "SelfRagdoll")
            {
                found.Add(pair.Key);
                wantedRocketJump |= raw.Equals("false", StringComparison.OrdinalIgnoreCase) || raw == "0";
            }
        }

        if (found.Count == 0)
        {
            return;
        }

        foreach (ConfigDefinition key in found)
        {
            leftover.Remove(key);
        }

        if (wantedRocketJump && mode != null)
        {
            mode.Value = BazookaMode.RocketJump;
            Log.LogInfo("Migrated the old TF2RocketJump switch to Mode = RocketJump.");
        }

        Config.Save();
        lastConfigWriteUtc = SafeConfigWriteTime();
    }

    /// <summary>
    /// Old default was a 6m hit sphere around a ~1.5m fireball. If the player never changed <c>ExplosionRadius</c>,
    /// move it to the current default (the old <c>ExplosionVisualScale</c> key is dropped). Custom radii are kept;
    /// the fireball now scales to them.
    /// </summary>
    private void MigrateExplosionRadiusToMatchVisual()
    {
        if (explosionRadius == null || !Mathf.Approximately(explosionRadius.Value, OldDefaultExplosionRadius))
        {
            return;
        }

        float matched = DefaultExplosionRadius;
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
                $"Config reloaded ({reason}); shots = {(InfiniteShots ? "infinite" : ShotCount.ToString())}, Mode = {Mode}.");
        }
        catch (Exception e)
        {
            Log.LogWarning($"Config reload failed ({reason}): {e.Message}");
        }
    }

    internal static void ApplyShotCountConfig()
    {
        foreach (BazookaInstanceSetup setup in BazookaInstanceSetup.Active)
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

        if (BazookaPrefab != null)
        {
            Item? prefabItem = BazookaPrefab.GetComponent<Item>();
            if (prefabItem != null)
            {
                prefabItem.totalUses = InfiniteShots ? -1 : ShotCount;
                prefabItem.UIData.hideFuel = InfiniteShots;
            }
        }

        BazookaAmmoUI.Refresh();
        ApplyCarryWeightConfig();
    }

    internal static void ApplyCarryWeightConfig()
    {
        int weight = CarryWeight;
        if (BazookaPrefab != null)
        {
            Item? prefabItem = BazookaPrefab.GetComponent<Item>();
            if (prefabItem != null)
            {
                prefabItem.carryWeight = weight;
            }
        }

        foreach (BazookaInstanceSetup setup in BazookaInstanceSetup.Active)
        {
            if (setup?.Item == null)
            {
                continue;
            }

            setup.Item.carryWeight = weight;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
        {
            BazookaCombat.ClearWorldHitCounts();
        }

        ReloadConfigFromDisk("map load");

        if (mode == LoadSceneMode.Single)
        {
            BazookaLootLedger.OnMapLoaded();
        }
    }

    private IEnumerator WaitForAntiZooka()
    {
        var wait = new WaitForSecondsRealtime(0.5f);
        while (BazookaPrefab == null)
        {
            try
            {
                CreateFromAntiZooka();
            }
            catch (Exception exception)
            {
                Log.LogWarning($"Could not create the bazooka yet: {exception.Message}");
            }

            if (BazookaPrefab != null)
            {
                yield break;
            }

            yield return wait;
        }
    }

    private bool buildingBazooka;

    private bool reportedMissingAntiZooka;

    internal void CreateFromAntiZooka()
    {
        if (BazookaPrefab != null || buildingBazooka)
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

        buildingBazooka = true;
        try
        {
            BuildBazookaFromAntiZooka(source, database);
        }
        finally
        {
            buildingBazooka = false;
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
        BazookaPaint.Apply(gunObject);

        Action_Antizooka? juice = gunObject.GetComponent<Action_Antizooka>();
        Item held = gunObject.GetComponent<Item>();
        Log.LogInfo(
            $"Bazooka hold: defaultPos {held.defaultPos}, defaultForward {held.defaultForward}, "
            + $"shot recoil {(juice != null ? juice.recoilPos.ToString() : "none")} (the vanilla shot juice settles at (0, 0, 0)).");
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
        item.UIData.itemName = BazookaText.BazookaItemName;
        ApplyIcon(item, "Peak.Bazooka.icons.BazookaIcon.png", "Bazooka");
        item.UIData.hideFuel = InfiniteShots;
        item.UIData.hasMainInteract = true;
        item.UIData.mainInteractPrompt = BazookaText.ShootPrompt;

        Action_Gun gun = gunObject.AddComponent<Action_Gun>();
        gun.fireRate = 0.8f;
        gun.OnHeld = false;
        gun.OnPressed = true;
        gun.OnCastFinished = false;
        gun.spawnTransform = spawn;
        if (gunObject.TryGetComponent(out ItemUseFeedback feedback))
        {
            feedback.sfxUsed = null;
        }

        gunObject.AddComponent<Action_Ammo>();
        gunObject.AddComponent<BazookaInstanceSetup>();

        LootData loot = gunObject.GetComponent<LootData>() ?? gunObject.AddComponent<LootData>();
        loot.banInSolo = false;
        loot.rarityOverrides.Clear();
        ApplyLuggageSpawnConfig(loot);
        LootData.AllSpawnWeightData = null;

        BazookaText.Inject();
        new ItemContent(item).Register(ModDefinition.GetOrCreate(Info));
        BazookaPrefab = gunObject;
        Log.LogInfo(
            $"Registered the bazooka for all biomes' luggage ({GuaranteedLuggageBazookas} random Roots suitcases always forced).");
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
                ExplosionPrefab = BuildVisualOnlyExplosion(dynamite.explosionPrefab);
                return;
            }
        }

        Log.LogWarning("No dynamite explosion prefab was found. Blasts will still deal damage.");
    }

    /// <summary>
    /// Dynamite's explosion carries an <see cref="AOE"/>: dynamite-sized knockback, ragdoll and Injury on every
    /// scout in its own range, plus launched items, whatever size the fireball is drawn at. That is what made a
    /// small rocket hit like a nuke (and hurt scouts in RocketJump mode). Keep an inactive copy with only the visuals and
    /// sound; <see cref="BazookaShooter"/> applies the rocket's own blast.
    /// </summary>
    private static GameObject BuildVisualOnlyExplosion(GameObject source)
    {
        // Copy it inactive so no Awake / OnEnable (an AOE can fire on enable) runs before it is stripped.
        bool sourceWasActive = source.activeSelf;
        source.SetActive(false);
        GameObject template = Instantiate(source);
        source.SetActive(sourceWasActive);

        template.name = source.name + " (Bazooka)";
        DontDestroyOnLoad(template);
        foreach (AOE aoe in template.GetComponentsInChildren<AOE>(true))
        {
            DestroyImmediate(aoe);
        }

        foreach (Rigidbody rig in template.GetComponentsInChildren<Rigidbody>(true))
        {
            DestroyImmediate(rig);
        }

        foreach (Collider collider in template.GetComponentsInChildren<Collider>(true))
        {
            DestroyImmediate(collider);
        }

        var kept = new SortedSet<string>();
        foreach (Component component in template.GetComponentsInChildren<Component>(true))
        {
            if (component != null && component is not Transform)
            {
                kept.Add(component.GetType().Name);
            }
        }

        Log.LogInfo($"Explosion VFX uses {source.name} without its damage (kept: {string.Join(", ", kept)}).");

        var timers = new List<string>();
        RemoveAfter(template, timers);
        foreach (ExplosionEffect effect in template.GetComponentsInChildren<ExplosionEffect>(true))
        {
            // The fireball orbs are separate objects ExplosionEffect spawns from its own prefab, so removing the
            // explosion never removed them. Give the orb itself the timer (on a copy, unless it is part of this one).
            if (effect.explosionOrb == null)
            {
                continue;
            }

            if (!effect.explosionOrb.transform.IsChildOf(template.transform))
            {
                effect.explosionOrb = EffectCopy(effect.explosionOrb);
            }

            RemoveAfter(effect.explosionOrb, timers);
        }

        Log.LogInfo($"Explosion smoke is removed {ExplosionCleanupSeconds}s after the blast ({string.Join("; ", timers)}).");
        return template;
    }

    /// <summary>
    /// Everything an explosion leaves (smoke, fireball orbs) is deleted this long after the blast. Dynamite's smoke is
    /// sized for its own big blast and otherwise lingers as a dark smudge.
    /// </summary>
    internal const float ExplosionCleanupSeconds = 7f;

    private static GameObject? fxTemplateHolder;

    /// <summary>
    /// Copy of an effect prefab that can be changed without touching the game's own. It sits under an inactive holder
    /// so none of its scripts run, while <see cref="UnityEngine.Object.Instantiate(UnityEngine.Object)"/> still makes
    /// active copies.
    /// </summary>
    private static GameObject EffectCopy(GameObject prefab)
    {
        if (fxTemplateHolder == null)
        {
            fxTemplateHolder = new GameObject("PeakBazooka FX templates");
            fxTemplateHolder.SetActive(false);
            DontDestroyOnLoad(fxTemplateHolder);
        }

        GameObject copy = Instantiate(prefab, fxTemplateHolder.transform);
        copy.name = prefab.name;
        return copy;
    }

    /// <summary>Makes <paramref name="root"/> delete itself <see cref="ExplosionCleanupSeconds"/> after it appears.</summary>
    private static void RemoveAfter(GameObject root, List<string> timers)
    {
        RemoveAfterSeconds? remove = root.GetComponent<RemoveAfterSeconds>();
        if (remove == null)
        {
            remove = root.AddComponent<RemoveAfterSeconds>();
            timers.Add($"{root.name}: none before");
        }
        else
        {
            timers.Add($"{root.name}: was {remove.seconds:0.#}s{(remove.shrink ? " + shrink" : "")}");
        }

        remove.seconds = ExplosionCleanupSeconds;
        remove.shrink = false;
        remove.photonRemove = false;
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
        if (loot == null && BazookaPrefab != null)
        {
            loot = BazookaPrefab.GetComponent<LootData>();
        }

        if (loot == null)
        {
            return;
        }

        loot.spawnLocations = BazookaLuggagePools;
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

internal enum BazookaMode
{
    /// <summary>A weapon: rockets injure and ragdoll scouts, your own blast flings and hurts you.</summary>
    Standard,

    /// <summary>No scout damage. Your own blast is a rocket jump you steer in the air (after TF2's soldier).</summary>
    RocketJump,
}
