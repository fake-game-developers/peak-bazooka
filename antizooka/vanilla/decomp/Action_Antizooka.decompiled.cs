using System;
using System.Collections;
using DG.Tweening;
using Photon.Pun;
using UnityEngine;

namespace Peak;

public class Action_Antizooka : ItemAction
{
	public Vector3 recoilPos;

	public SFX_Instance sfx;

	public float shakeAmount;

	public Transform shell;

	public ParticleSystem muzzleFlash;

	public AudioSource chargeSFX;

	public Vector3 shellInitPos;

	private Tween shellShakeTween;

	protected override void OnEnable()
	{
		base.OnEnable();
		Item item = base.item;
		item.OnPrimaryStarted = (Action)Delegate.Combine(item.OnPrimaryStarted, new Action(PlayChargeFX));
		Item item2 = base.item;
		item2.OnPrimaryCancelled = (Action)Delegate.Combine(item2.OnPrimaryCancelled, new Action(StopChargeFX));
		shellInitPos = shell.transform.localPosition;
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		Item item = base.item;
		item.OnPrimaryStarted = (Action)Delegate.Remove(item.OnPrimaryStarted, new Action(PlayChargeFX));
		Item item2 = base.item;
		item2.OnPrimaryCancelled = (Action)Delegate.Remove(item2.OnPrimaryCancelled, new Action(StopChargeFX));
	}

	private void PlayChargeFX()
	{
		shellShakeTween = shell.DOShakePosition(item.usingTimePrimary, 0.1f, 50, 90f, snapping: false, fadeOut: false);
		chargeSFX.Play();
	}

	private void StopChargeFX()
	{
		if (shellShakeTween != null)
		{
			shellShakeTween.Kill();
		}
		shell.transform.localPosition = shellInitPos;
		chargeSFX.Stop();
	}

	public override void RunAction()
	{
		if (item.photonView.IsMine)
		{
			item.photonView.RPC("RPCA_Shoot", RpcTarget.All);
		}
	}

	[PunRPC]
	private void RPCA_Shoot()
	{
		GamefeelHandler.instance.AddPerlinShakeProximity(transform.position, shakeAmount, 0.3f);
		sfx.Play(item.transform.position);
		shell.localScale = Vector3.zero;
		muzzleFlash.Play();
		StartCoroutine(JuiceRoutine());
		IEnumerator JuiceRoutine()
		{
			item.defaultPos = recoilPos;
			shell.DOScale(Vector3.one, 1f).SetEase(Ease.OutBack).SetDelay(0.5f);
			shell.transform.localPosition = shellInitPos;
			float t = 0f;
			while (t < 1f)
			{
				t += Time.deltaTime * 4f;
				item.defaultPos = Vector3.Lerp(recoilPos, Vector3.zero, t);
				yield return null;
			}
		}
	}
}
