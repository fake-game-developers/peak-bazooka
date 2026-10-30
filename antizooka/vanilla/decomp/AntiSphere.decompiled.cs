using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Photon.Pun;
using UnityEngine;

namespace Peak;

public class AntiSphere : MonoBehaviour
{
	private List<Character> ACTIVE_CHARACTERS = new List<Character>();

	private List<Item> ACTIVE_ITEMS = new List<Item>();

	public float antiGravForceRagdoll = 10f;

	public float antiGravForceItem = 1f;

	public float antiGravEnterForce = 100f;

	public float antiGravExitForce = 100f;

	public float antiGravRagdollForceMult = 0.2f;

	public bool hasLifetime;

	public float exitForceTime = 1f;

	public float lifetime = 10f;

	private float _life;

	private bool _dying;

	private Coroutine reduceRoutine;

	public AnimationCurve antiGravForceCurve;

	public float reduceRate = 0.5f;

	public float fallTime = 0.5f;

	public float staminaGainRate = 0.1f;

	public SphereCollider coll;

	public SFX_Instance bubbleEnter;

	public SFX_Instance bubbleExit;

	public AudioSource audioSource;

	private void OnTriggerEnter(Collider other)
	{
		Item component;
		if (CharacterRagdoll.TryGetCharacterFromCollider(other, out var character))
		{
			if (!ACTIVE_CHARACTERS.Contains(character))
			{
				if (reduceRoutine != null)
				{
					StopCoroutine(reduceRoutine);
				}
				Vector3 normalized = (transform.position - character.Center).normalized;
				StartCoroutine(AddEnterExitForce(character, normalized, antiGravEnterForce));
				bubbleEnter.Play(character.Center);
				ACTIVE_CHARACTERS.Add(character);
				character.data.outOfStaminaFor = 0f;
				if (character == Character.localCharacter)
				{
					GUIManager.instance.StartAnti();
				}
			}
		}
		else if (other.attachedRigidbody.TryGetComponent<Item>(out component) && !ACTIVE_ITEMS.Contains(component) && component.itemState == ItemState.Ground)
		{
			ACTIVE_ITEMS.Add(component);
		}
	}

	private void FixedUpdate()
	{
		if (hasLifetime)
		{
			_life += Time.fixedDeltaTime;
			if (_life > lifetime && !_dying)
			{
				Die();
			}
		}
		for (int num = ACTIVE_CHARACTERS.Count - 1; num >= 0; num--)
		{
			Character character = ACTIVE_CHARACTERS[num];
			if (character == null || character.data == null || character.data.dead || character.data.isCarried)
			{
				ACTIVE_CHARACTERS.Remove(character);
				if (character == Character.localCharacter)
				{
					GUIManager.instance.EndAnti();
				}
			}
			else if (Vector3.Distance(transform.position, character.Center) > coll.radius * transform.lossyScale.x + 1f)
			{
				ACTIVE_CHARACTERS.Remove(character);
				if (character == Character.localCharacter)
				{
					GUIManager.instance.EndAnti();
				}
			}
			else
			{
				if (character.IsLocal && !character.data.isClimbing)
				{
					character.AddStamina(Time.fixedDeltaTime * staminaGainRate);
				}
				character.data.timeInAntigrav += Time.fixedDeltaTime;
				if (character.data.isClimbing)
				{
					if (character.data.sinceClimbJump > 0.5f)
					{
						character.refs.climbing.playerSlide += Vector2.up * 400f * Mathf.Clamp01(Mathf.Pow(character.data.outOfStaminaFor * 0.15f, 2f)) * Time.fixedDeltaTime;
					}
				}
				else if (character.data.fallSeconds > 0f)
				{
					character.AddForce(Vector3.up * antiGravForceRagdoll);
				}
				else
				{
					character.AddForce(Vector3.up * antiGravForceCurve.Evaluate(character.data.timeInAntigrav));
				}
				character.data.sinceGrounded = Mathf.Min(character.data.sinceGrounded, 0.5f);
			}
		}
		foreach (Item aCTIVE_ITEM in ACTIVE_ITEMS)
		{
			if (aCTIVE_ITEM == null || aCTIVE_ITEM.rig == null)
			{
				ACTIVE_ITEMS.Remove(aCTIVE_ITEM);
			}
			else if ((bool)aCTIVE_ITEM.antigrav)
			{
				aCTIVE_ITEM.antigrav.enabled = false;
			}
			else
			{
				aCTIVE_ITEM.rig.AddForce(-Physics.gravity * 1.2f, ForceMode.Acceleration);
			}
		}
	}

	private void Die()
	{
		_dying = true;
		transform.DOScale(Vector3.zero, Mathf.Min(lifetime, 2f)).SetEase(Ease.InCubic);
		Invoke("Delete", Mathf.Min(lifetime, 2f));
	}

	private void Delete()
	{
		for (int num = ACTIVE_CHARACTERS.Count - 1; num >= 0; num--)
		{
			Character character = ACTIVE_CHARACTERS[num];
			ACTIVE_CHARACTERS.Remove(character);
			if (character == Character.localCharacter)
			{
				GUIManager.instance.EndAnti();
			}
		}
		if (TryGetComponent<AudioReverbZone>(out var component))
		{
			component.enabled = false;
		}
		if ((bool)audioSource)
		{
			audioSource.DOFade(0f, 1f).OnComplete(() =>
			{
				audioSource.enabled = false;
				coll.enabled = false;
			});
		}
		if (TryGetComponent<PhotonView>(out var component2) && component2.AmOwner)
		{
			PhotonNetwork.Destroy(gameObject);
		}
	}

	private void OnTriggerExit(Collider other)
	{
		Item component;
		if (CharacterRagdoll.TryGetCharacterFromCollider(other, out var character))
		{
			if (ACTIVE_CHARACTERS.Contains(character))
			{
				ACTIVE_CHARACTERS.Remove(character);
				character.data.outOfStaminaFor = 0f;
				character.data.sinceJump = 0f;
				Vector3 normalized = ((character.Center - transform.position).normalized + Vector3.up).normalized;
				Vector3 normalized2 = new Vector3(character.GetBodypart(BodypartType.Head).transform.forward.x, 0f, character.GetBodypart(BodypartType.Head).transform.forward.z).normalized;
				Vector3 normalized3 = new Vector3(normalized.x, 0f, normalized.z).normalized;
				float num = Vector3.Dot(normalized2, normalized3);
				normalized = Vector3.Lerp(new Vector3(normalized.x * 0.33f, normalized.y, normalized.z * 0.33f), normalized, (num + 1f) * 0.5f);
				StartCoroutine(AddEnterExitForce(character, normalized, antiGravExitForce));
				bubbleExit.Play(character.Center);
				reduceRoutine = StartCoroutine(ReduceAntigravRoutine(character));
				if (character == Character.localCharacter)
				{
					GUIManager.instance.EndAnti();
				}
			}
		}
		else if (other.attachedRigidbody.TryGetComponent<Item>(out component) && ACTIVE_ITEMS.Contains(component) && component.itemState == ItemState.Ground)
		{
			ACTIVE_ITEMS.Remove(component);
			if ((bool)component.antigrav)
			{
				component.antigrav.enabled = true;
			}
		}
	}

	private IEnumerator ReduceAntigravRoutine(Character character)
	{
		while (character.data.timeInAntigrav > 0f)
		{
			if (character.data.timeInAntigrav > 1f)
			{
				character.data.timeInAntigrav = 1f;
			}
			character.data.timeInAntigrav -= Time.fixedDeltaTime * reduceRate;
			yield return new WaitForFixedUpdate();
		}
		character.data.timeInAntigrav = 0f;
	}

	private IEnumerator AddEnterExitForce(Character character, Vector3 exitDir, float force)
	{
		float t = 0f;
		while (t < exitForceTime)
		{
			t += Time.fixedDeltaTime;
			character.AddForce(exitDir * force * (1f - t) * Mathf.Lerp(antiGravRagdollForceMult, 1f, character.data.currentRagdollControll));
			yield return new WaitForFixedUpdate();
		}
	}
}
