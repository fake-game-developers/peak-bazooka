using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace Peak.Bazooka;

/// <summary>
/// Applies carry weight and host-synced magazine size to each spawned bazooka.
/// </summary>
internal sealed class BazookaInstanceSetup : MonoBehaviour
{
    internal static readonly HashSet<BazookaInstanceSetup> Active = new();

    internal Item? Item { get; private set; }

    private void Awake()
    {
        Item = GetComponent<Item>();
        Plugin.StripItemAntigrav(gameObject, immediate: false);
        ApplyLocal();
    }

    private void Start()
    {
        ApplyLocal();
        if (!PhotonNetwork.InRoom || PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(HostEnsureMagazine());
        }
    }

    internal void ApplyLocal()
    {
        if (Item == null)
        {
            return;
        }

        Item.carryWeight = Plugin.CarryWeight;
        if (Plugin.InfiniteShots)
        {
            Item.totalUses = -1;
            Item.UIData.hideFuel = true;
            return;
        }

        Item.totalUses = Plugin.ShotCount;
        Item.UIData.hideFuel = false;
    }

    private IEnumerator HostEnsureMagazine()
    {
        yield return null;
        if (Item == null)
        {
            yield break;
        }

        ApplyLocal();
        if (Plugin.InfiniteShots)
        {
            GetComponent<Action_Ammo>()?.ApplyInfinite();
            yield break;
        }

        if (Item.HasData(DataEntryKey.ItemUses))
        {
            OptionableIntItemData existing = Item.GetData<OptionableIntItemData>(DataEntryKey.ItemUses);
            if (existing.HasData && existing.Value >= 0)
            {
                yield break;
            }
        }

        GetComponent<Action_Ammo>()?.EnsureHostMagazine();
    }

    private void OnEnable() => Active.Add(this);

    private void OnDisable() => Active.Remove(this);

    private void OnDestroy() => Active.Remove(this);
}
