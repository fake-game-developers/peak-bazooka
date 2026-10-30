using System;
using UnityEngine;

public class ItemAction : ItemActionBase
{
	[SerializeField]
	public bool OnPressed;

	[SerializeField]
	public bool OnHeld;

	[SerializeField]
	public bool OnReleased;

	[SerializeField]
	public bool OnCastFinished;

	[SerializeField]
	public bool OnCancelled;

	public bool OnSecondaryCastFinished;

	public bool OnSecondaryPressed;

	public bool OnSecondaryHeld;

	public bool OnSecondaryCancelled;

	public bool OnConsumed;

	protected override void Subscribe()
	{
		if (OnPressed)
		{
			Item item = base.item;
			item.OnPrimaryStarted = (Action)Delegate.Combine(item.OnPrimaryStarted, new Action(RunAction));
		}
		if (OnHeld)
		{
			Item item2 = base.item;
			item2.OnPrimaryHeld = (Action)Delegate.Combine(item2.OnPrimaryHeld, new Action(RunAction));
		}
		if (OnCastFinished)
		{
			Item item3 = base.item;
			item3.OnPrimaryFinishedCast = (Action)Delegate.Combine(item3.OnPrimaryFinishedCast, new Action(RunAction));
		}
		if (OnSecondaryCastFinished)
		{
			Item item4 = base.item;
			item4.OnSecondaryFinishedCast = (Action)Delegate.Combine(item4.OnSecondaryFinishedCast, new Action(RunAction));
		}
		if (OnCancelled)
		{
			Item item5 = base.item;
			item5.OnPrimaryCancelled = (Action)Delegate.Combine(item5.OnPrimaryCancelled, new Action(RunAction));
		}
		if (OnConsumed)
		{
			Item item6 = base.item;
			item6.OnConsumed = (Action)Delegate.Combine(item6.OnConsumed, new Action(RunAction));
		}
		if (OnSecondaryPressed)
		{
			Item item7 = base.item;
			item7.OnSecondaryStarted = (Action)Delegate.Combine(item7.OnSecondaryStarted, new Action(RunAction));
		}
		if (OnSecondaryHeld)
		{
			Item item8 = base.item;
			item8.OnSecondaryHeld = (Action)Delegate.Combine(item8.OnSecondaryHeld, new Action(RunAction));
		}
		if (OnSecondaryCancelled)
		{
			Item item9 = base.item;
			item9.OnSecondaryCancelled = (Action)Delegate.Combine(item9.OnSecondaryCancelled, new Action(RunAction));
		}
	}

	protected override void Unsubscribe()
	{
		if (OnPressed)
		{
			Item item = base.item;
			item.OnPrimaryStarted = (Action)Delegate.Remove(item.OnPrimaryStarted, new Action(RunAction));
		}
		if (OnHeld)
		{
			Item item2 = base.item;
			item2.OnPrimaryHeld = (Action)Delegate.Remove(item2.OnPrimaryHeld, new Action(RunAction));
		}
		if (OnCastFinished)
		{
			Item item3 = base.item;
			item3.OnPrimaryFinishedCast = (Action)Delegate.Remove(item3.OnPrimaryFinishedCast, new Action(RunAction));
		}
		if (OnSecondaryCastFinished)
		{
			Item item4 = base.item;
			item4.OnSecondaryFinishedCast = (Action)Delegate.Remove(item4.OnSecondaryFinishedCast, new Action(RunAction));
		}
		if (OnCancelled)
		{
			Item item5 = base.item;
			item5.OnPrimaryCancelled = (Action)Delegate.Remove(item5.OnPrimaryCancelled, new Action(RunAction));
		}
		if (OnConsumed)
		{
			Item item6 = base.item;
			item6.OnConsumed = (Action)Delegate.Remove(item6.OnConsumed, new Action(RunAction));
		}
		if (OnSecondaryPressed)
		{
			Item item7 = base.item;
			item7.OnSecondaryStarted = (Action)Delegate.Remove(item7.OnSecondaryStarted, new Action(RunAction));
		}
		if (OnSecondaryHeld)
		{
			Item item8 = base.item;
			item8.OnSecondaryHeld = (Action)Delegate.Remove(item8.OnSecondaryHeld, new Action(RunAction));
		}
		if (OnSecondaryCancelled)
		{
			Item item9 = base.item;
			item9.OnSecondaryCancelled = (Action)Delegate.Remove(item9.OnSecondaryCancelled, new Action(RunAction));
		}
	}
}
