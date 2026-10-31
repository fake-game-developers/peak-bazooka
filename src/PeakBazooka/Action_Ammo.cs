using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace Peak.Bazooka;

public class Action_Ammo : ItemAction
{
    /// <summary>
    /// Host (or offline) only: spend one shot if any remain and broadcast the new count.
    /// Infinite magazines (<see cref="Plugin.ShotCount"/> 0) always succeed.
    /// </summary>
    internal bool TrySpendOne()
    {
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
        {
            return false;
        }

        if (Plugin.InfiniteShots)
        {
            ApplyInfinite();
            return true;
        }

        if (!item.HasData(DataEntryKey.ItemUses))
        {
            EnsureHostMagazine();
        }

        OptionableIntItemData data = item.GetData<OptionableIntItemData>(DataEntryKey.ItemUses);
        if (!data.HasData)
        {
            EnsureHostMagazine();
            data = item.GetData<OptionableIntItemData>(DataEntryKey.ItemUses);
        }

        if (!data.HasData || data.Value <= 0)
        {
            BroadcastUses(0);
            return false;
        }

        data.Value--;
        BroadcastUses(data.Value);
        return true;
    }

    internal bool HasAmmo()
    {
        if (Plugin.InfiniteShots || item.totalUses <= 0)
        {
            return true;
        }

        if (!item.HasData(DataEntryKey.ItemUses))
        {
            return item.totalUses > 0;
        }

        OptionableIntItemData data = item.GetData<OptionableIntItemData>(DataEntryKey.ItemUses);
        if (!data.HasData)
        {
            return item.totalUses > 0;
        }

        return data.Value < 0 || data.Value > 0;
    }

    /// <summary>Host (or offline) writes the magazine size into instance data when it is still unset.</summary>
    internal void EnsureHostMagazine()
    {
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        if (Plugin.InfiniteShots)
        {
            ApplyInfinite();
            return;
        }

        int count = Plugin.ShotCount;
        item.totalUses = count;
        item.UIData.hideFuel = false;
        OptionableIntItemData data = item.GetData<OptionableIntItemData>(DataEntryKey.ItemUses);
        if (data.HasData && data.Value >= 0)
        {
            return;
        }

        data.HasData = true;
        data.Value = count;
        item.SetUseRemainingPercentage(1f);
        if (PhotonNetwork.InRoom && item.photonView != null)
        {
            item.photonView.RPC(nameof(ApplyUsesRPC), RpcTarget.All, data.Value, count);
        }
    }

    internal void ApplyInfinite()
    {
        item.totalUses = -1;
        item.UIData.hideFuel = true;
        OptionableIntItemData data = item.GetData<OptionableIntItemData>(DataEntryKey.ItemUses);
        data.HasData = false;
        data.Value = -1;
        item.SetUseRemainingPercentage(1f);
        BazookaAmmoUI.Refresh();
    }

    private void BroadcastUses(int remaining)
    {
        int capacity = Mathf.Max(item.totalUses, Plugin.ShotCount);
        item.totalUses = capacity;
        if (!PhotonNetwork.InRoom)
        {
            ApplyUses(remaining, capacity);
            return;
        }

        item.photonView.RPC(nameof(ApplyUsesRPC), RpcTarget.All, remaining, capacity);
    }

    [PunRPC]
    public void ApplyUsesRPC(int remaining, int capacity, PhotonMessageInfo info)
    {
        if (PhotonNetwork.InRoom && (info.Sender == null || !info.Sender.IsMasterClient))
        {
            return;
        }

        ApplyUses(remaining, capacity);
    }

    private void ApplyUses(int remaining, int capacity)
    {
        if (capacity <= 0)
        {
            ApplyInfinite();
            return;
        }

        item.totalUses = Mathf.Max(1, capacity);
        item.UIData.hideFuel = false;
        OptionableIntItemData data = item.GetData<OptionableIntItemData>(DataEntryKey.ItemUses);
        data.HasData = true;
        data.Value = Mathf.Max(0, remaining);
        item.SetUseRemainingPercentage(data.Value / (float)item.totalUses);
        SyncHolderSlotUses(data.Value);
        BazookaAmmoUI.Refresh();
    }

    private void SyncHolderSlotUses(int remaining)
    {
        Character? holder = character != null ? character : item.holderCharacter;
        if (holder == null || holder.player == null || item.data == null)
        {
            return;
        }

        ItemSlot? slot = FindSlotHolding(holder.player, item.data.guid);
        if (slot?.data == null)
        {
            return;
        }

        if (slot.data.TryGetDataEntry(DataEntryKey.ItemUses, out OptionableIntItemData slotUses))
        {
            slotUses.HasData = true;
            slotUses.Value = remaining;
            return;
        }

        slot.data.RegisterEntry(DataEntryKey.ItemUses, new OptionableIntItemData
        {
            HasData = true,
            Value = remaining,
        });
    }

    private static ItemSlot? FindSlotHolding(Player player, System.Guid guid)
    {
        foreach (ItemSlot slot in player.itemSlots)
        {
            if (slot?.data != null && slot.data.guid == guid)
            {
                return slot;
            }
        }

        ItemSlot? temporary = player.tempFullSlot;
        return temporary?.data != null && temporary.data.guid == guid ? temporary : null;
    }
}
