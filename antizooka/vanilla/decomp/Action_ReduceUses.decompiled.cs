using Photon.Pun;

public class Action_ReduceUses : ItemAction
{
	public bool consumeOnFullyUsed;

	public override void RunAction()
	{
		item.photonView.RPC("ReduceUsesRPC", RpcTarget.All);
		if ((bool)character && character.IsLocal)
		{
			OptionableIntItemData data = item.GetData<OptionableIntItemData>(DataEntryKey.ItemUses);
			if (data.HasData && data.Value == 0 && consumeOnFullyUsed)
			{
				item.StartCoroutine(item.ConsumeDelayed());
			}
		}
	}

	[PunRPC]
	public void ReduceUsesRPC()
	{
		OptionableIntItemData data = item.GetData<OptionableIntItemData>(DataEntryKey.ItemUses);
		if (data.HasData && data.Value > 0)
		{
			data.Value--;
			if (item.totalUses > 0)
			{
				item.SetUseRemainingPercentage((float)data.Value / (float)item.totalUses);
			}
		}
	}
}
