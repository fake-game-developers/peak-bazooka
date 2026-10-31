using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zorro.Core;

namespace Peak.Bazooka;

/// <summary>
/// Host-only: when <see cref="Plugin.GiveBazookaOnSpawn"/> is on, each scout gets one bazooka in hand
/// once per run after the climb starts (Shore and later — never at the Airport), including late joiners.
/// Uses PEAK's <c>SpawnItemInHand</c> flow (same as Challenge Mode starting gear).
/// </summary>
internal static class StartingBazookaGiver
{
    private static readonly HashSet<int> GivenActorNumbers = new();

    private static string lastRunKey = "";

    internal static IEnumerator WhenPlayersAreReady()
    {
        var wait = new WaitForSecondsRealtime(0.5f);
        while (true)
        {
            yield return wait;

            if (!Plugin.GiveBazookaOnSpawn)
            {
                continue;
            }

            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || LoadingScreenHandler.loading)
            {
                continue;
            }

            // Airport lobby only — wait until the climb map is up.
            if (IsAirport() || !MapHandler.ExistsAndInitialized)
            {
                continue;
            }

            if (Plugin.BazookaPrefab == null)
            {
                continue;
            }

            string runKey = CurrentRunKey();
            if (runKey != lastRunKey)
            {
                lastRunKey = runKey;
                GivenActorNumbers.Clear();
            }

            foreach (Character character in EnumeratePlayers())
            {
                TryGive(character);
            }
        }
    }

    private static bool IsAirport() =>
        SceneManager.GetActiveScene().name == "Airport";

    private static void TryGive(Character character)
    {
        if (character == null || character.isBot || character.isZombie)
        {
            return;
        }

        PhotonView? view = character.photonView;
        if (view == null || !view.IsOwnerActive)
        {
            return;
        }

        int actor = view.OwnerActorNr;
        if (GivenActorNumbers.Contains(actor))
        {
            return;
        }

        if (character.refs?.items == null || character.data == null)
        {
            return;
        }

        // Match Challenge Mode: wait out the beach wake / warp before putting a gun in hand.
        if (character.data.passedOutOnTheBeach > 0f || character.warping || !character.data.isGrounded)
        {
            return;
        }

        if (AlreadyHasBazooka(character))
        {
            GivenActorNumbers.Add(actor);
            return;
        }

        Item? prefabItem = Plugin.BazookaPrefab!.GetComponent<Item>();
        if (prefabItem != null && !prefabItem.IsValidToSpawn())
        {
            GivenActorNumbers.Add(actor);
            Plugin.Log.LogInfo(
                $"Skipped starting bazooka for actor {actor}: bazooka is disabled in this run's item settings.");
            return;
        }

        try
        {
            // Prefab name is "PeakBazooka:Bazooka" after PEAKLib registration.
            character.refs.items.SpawnItemInHand(Plugin.BazookaPrefab.name);
            GivenActorNumbers.Add(actor);
            Plugin.Log.LogInfo($"Gave starting bazooka to actor {actor} ({character.characterName}).");
        }
        catch (System.Exception exception)
        {
            Plugin.Log.LogError($"Could not give starting bazooka to actor {actor}: {exception.Message}");
        }
    }

    private static bool AlreadyHasBazooka(Character character)
    {
        Item? held = character.data?.currentItem;
        if (held != null && held.GetComponent<Action_Gun>() != null)
        {
            return true;
        }

        Player? player = character.player;
        if (player?.itemSlots == null)
        {
            return false;
        }

        foreach (ItemSlot? slot in player.itemSlots)
        {
            if (slot != null && !slot.IsEmpty() && slot.prefab != null && slot.prefab.GetComponent<Action_Gun>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Character> EnumeratePlayers()
    {
        if (PlayerHandler.Exists)
        {
            List<Character>? fromHandler = PlayerHandler.GetAllPlayerCharacters();
            if (fromHandler != null && fromHandler.Count > 0)
            {
                return fromHandler;
            }
        }

        return Character.AllCharacters;
    }

    private static string CurrentRunKey()
    {
        System.Guid run = RunManager.Instance != null ? RunManager.Instance.RunId : System.Guid.Empty;
        return run == System.Guid.Empty ? "" : run.ToString("N");
    }
}
