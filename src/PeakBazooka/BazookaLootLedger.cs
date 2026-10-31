using System;
using System.Collections.Generic;
using System.IO;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

namespace Peak.Bazooka;

/// <summary>
/// Per-run bazooka loot bookkeeping: which Roots suitcases are marked for a guaranteed bazooka, how many
/// bazookas each suitcase yielded (for the per-biome cap), and the seat pose of each seated gun.
/// <para>
/// Keyed by <c>RunManager.RunId</c> and by a stable suitcase key (scene + scene view id), so it survives a
/// quicksave reload (same run id, same scene view ids) and host migration. The host publishes it as a room
/// property after every change, so a new host continues from it, and saves it to disk so a reload or a new
/// session of the same run keeps it.
/// </para>
/// </summary>
internal static class BazookaLootLedger
{
    internal const string RoomLootKey = "PeakBazooka_Loot";

    private const int MaxSavedRuns = 20;

    private static LootRunState? state;

    private static string FilePath => Path.Combine(BepInEx.Paths.ConfigPath, "PeakBazooka.loot.json");

    [Serializable]
    private sealed class LootLedgerFile
    {
        public List<LootRunState> runs = new();
    }

    [Serializable]
    private sealed class LootRunState
    {
        public string runId = "";
        public bool assigned;
        public List<string> chosen = new();
        public List<BazookaLuggage> bazookas = new();
        public List<SeatPose> poses = new();
    }

    [Serializable]
    private sealed class BazookaLuggage
    {
        public string luggage = "";
        public int biome;
        public int count;
    }

    [Serializable]
    private sealed class SeatPose
    {
        public string luggage = "";
        public Vector3 position;
        public Quaternion rotation;
    }

    /// <summary>Stable across clients and reloads: luggage are scene objects with fixed scene view ids.</summary>
    internal static string KeyOf(Luggage luggage)
    {
        string scene = luggage.gameObject.scene.name;
        int viewId = luggage.photonView != null ? luggage.photonView.ViewID : 0;
        if (viewId != 0)
        {
            return $"{scene}#{viewId}";
        }

        Vector3 p = luggage.transform.position;
        return $"{scene}@{Mathf.RoundToInt(p.x * 10f)},{Mathf.RoundToInt(p.y * 10f)},{Mathf.RoundToInt(p.z * 10f)}";
    }

    internal static bool Assigned => Current().assigned;

    internal static int ChosenCount => Current().chosen.Count;

    internal static bool IsChosen(Luggage luggage) => Current().chosen.Contains(KeyOf(luggage));

    internal static void MarkAssigned(IEnumerable<Luggage> chosen)
    {
        LootRunState run = Current();
        run.assigned = true;
        run.chosen.Clear();
        foreach (Luggage luggage in chosen)
        {
            run.chosen.Add(KeyOf(luggage));
        }

        Commit();
    }

    /// <summary>Bazookas already counted in <paramref name="biome"/>, not counting <paramref name="except"/>'s own.</summary>
    internal static int CountInBiome(SpawnPool biome, Luggage? except)
    {
        string? skip = except != null ? KeyOf(except) : null;
        int total = 0;
        foreach (BazookaLuggage entry in Current().bazookas)
        {
            if (entry.biome == (int)biome && entry.luggage != skip)
            {
                total += entry.count;
            }
        }

        return total;
    }

    /// <summary>
    /// Records how many bazookas this suitcase now yields. A suitcase re-rolled after a reload replaces its
    /// earlier count instead of adding to it.
    /// </summary>
    internal static void SetBazookas(Luggage luggage, SpawnPool biome, int count)
    {
        LootRunState run = Current();
        string key = KeyOf(luggage);
        int index = run.bazookas.FindIndex(entry => entry.luggage == key);
        if (count <= 0)
        {
            if (index < 0)
            {
                return;
            }

            run.bazookas.RemoveAt(index);
        }
        else if (index >= 0)
        {
            if (run.bazookas[index].count == count && run.bazookas[index].biome == (int)biome)
            {
                return;
            }

            run.bazookas[index].count = count;
            run.bazookas[index].biome = (int)biome;
        }
        else
        {
            run.bazookas.Add(new BazookaLuggage { luggage = key, biome = (int)biome, count = count });
        }

        Commit();
    }

    /// <summary>A bazooka restored from a save counts against the cap even if this ledger never saw its roll.</summary>
    internal static void NoteRestored(Luggage luggage, SpawnPool biome)
    {
        string key = KeyOf(luggage);
        if (!Current().bazookas.Exists(entry => entry.luggage == key))
        {
            SetBazookas(luggage, biome, 1);
        }
    }

    internal static void SavePose(Luggage luggage, Vector3 position, Quaternion rotation)
    {
        LootRunState run = Current();
        string key = KeyOf(luggage);
        SeatPose? pose = run.poses.Find(entry => entry.luggage == key);
        if (pose == null)
        {
            run.poses.Add(new SeatPose { luggage = key, position = position, rotation = rotation });
        }
        else
        {
            pose.position = position;
            pose.rotation = rotation;
        }

        Commit();
    }

    internal static bool TryGetPose(Luggage luggage, out Vector3 position, out Quaternion rotation)
    {
        string key = KeyOf(luggage);
        SeatPose? pose = Current().poses.Find(entry => entry.luggage == key);
        position = pose?.position ?? default;
        rotation = pose?.rotation ?? Quaternion.identity;
        return pose != null;
    }

    /// <summary>
    /// After a full map load, drop bookkeeping made before the run had an id so it cannot leak into the next
    /// run. A ledger with a real run id stays; <see cref="Current"/> switches when the run id changes.
    /// </summary>
    internal static void OnMapLoaded()
    {
        if (state != null && state.runId.Length == 0)
        {
            state = null;
        }
    }

    /// <summary>New host: continue from the room's ledger and save it to this machine.</summary>
    internal static void OnBecameHost()
    {
        if (CurrentRunKey().Length != 0)
        {
            Current();
            Commit();
        }
    }

    internal static void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (PhotonNetwork.IsMasterClient
            || !changed.TryGetValue(RoomLootKey, out object? raw)
            || raw is not string json)
        {
            return;
        }

        LootRunState? received = Parse(json);
        if (received != null && received.runId == CurrentRunKey())
        {
            state = received;
        }
    }

    private static string CurrentRunKey()
    {
        Guid run = RunManager.Instance != null ? RunManager.Instance.RunId : Guid.Empty;
        return run == Guid.Empty ? "" : run.ToString("N");
    }

    private static LootRunState Current()
    {
        string key = CurrentRunKey();
        if (state != null && state.runId == key)
        {
            return state;
        }

        if (state != null && state.runId.Length == 0 && key.Length != 0)
        {
            // Rolled before the run id arrived (it is set shortly after the run starts): same run.
            state.runId = key;
            Commit();
            return state;
        }

        state = (key.Length != 0 ? FromRoom(key) ?? FromDisk(key) : null) ?? new LootRunState { runId = key };
        return state;
    }

    private static LootRunState? FromRoom(string key)
    {
        if (!PhotonNetwork.InRoom
            || PhotonNetwork.CurrentRoom?.CustomProperties == null
            || !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomLootKey, out object? raw)
            || raw is not string json)
        {
            return null;
        }

        LootRunState? run = Parse(json);
        return run != null && run.runId == key ? run : null;
    }

    private static LootRunState? FromDisk(string key)
    {
        return ReadFile().runs.Find(run => run.runId == key);
    }

    /// <summary>Host (or offline) publishes to the room and saves to disk. Clients only mirror.</summary>
    private static void Commit()
    {
        if (state == null || (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient))
        {
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { RoomLootKey, JsonUtility.ToJson(state) } });
        }

        if (state.runId.Length == 0)
        {
            return;
        }

        LootLedgerFile file = ReadFile();
        file.runs.RemoveAll(run => run.runId == state.runId);
        file.runs.Add(state);
        if (file.runs.Count > MaxSavedRuns)
        {
            file.runs.RemoveRange(0, file.runs.Count - MaxSavedRuns);
        }

        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(file));
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not save bazooka loot ledger: {e.Message}");
        }
    }

    private static LootLedgerFile ReadFile()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                LootLedgerFile? file = JsonUtility.FromJson<LootLedgerFile>(File.ReadAllText(FilePath));
                if (file?.runs != null)
                {
                    return file;
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Could not read bazooka loot ledger: {e.Message}");
        }

        return new LootLedgerFile();
    }

    private static LootRunState? Parse(string json)
    {
        try
        {
            return JsonUtility.FromJson<LootRunState>(json);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Ignored malformed bazooka loot ledger from the room: {e.Message}");
            return null;
        }
    }
}
