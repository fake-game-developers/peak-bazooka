using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class ScoutCannon : MonoBehaviour
{
	public float launchForce = 500f;

	public float itemLaunchForce = 500f;

	public float backpackLaunchForce = 5000f;

	public float fallFor = 1f;

	public float pullForce = 10f;

	public float pushForce = 10f;

	public bool lit;

	public float fireTime = 3f;

	public ParticleSystem litParticle;

	public ParticleSystem fireParticle;

	public GameObject fireSFX;

	public Animator anim;

	private MaterialPropertyBlock mpb;

	private PhotonView view;

	private Transform tube;

	private Transform entry;

	private Character target;

	private int targetID = -1;

	private List<Character> characters = new List<Character>();

	private MeshRenderer[] _mr;

	public bool holdOnFinish => false;

	private MeshRenderer[] meshRenderers
	{
		get
		{
			if (_mr == null)
			{
				_mr = tube.GetComponentsInChildren<MeshRenderer>();
			}
			return _mr;
		}
		set
		{
			_mr = value;
		}
	}

	private void FixedUpdate()
	{
		if (!PhotonNetwork.IsMasterClient)
		{
			return;
		}
		float num = 1f;
		float num2 = num;
		Character character = null;
		characters.Clear();
		characters.AddRange(Character.AllCharacters);
		characters.AddRange(Character.AllBotCharacters);
		foreach (Character character2 in characters)
		{
			float num3 = Vector3.Distance(character2.Center, entry.position);
			if (num3 < num2)
			{
				num2 = num3;
				character = character2;
			}
			if (!(num3 < num))
			{
				continue;
			}
			if (character2 == target)
			{
				if (character2.data.sinceJump < 0.5f)
				{
					continue;
				}
				List<Bodypart> partList = character2.refs.ragdoll.partList;
				if (character2.isBot && character2.data.fallSeconds == 0f)
				{
					character2.Fall(2f);
				}
				foreach (Bodypart item in partList)
				{
					BodypartType partType = item.partType;
					if (partType != BodypartType.Hand_L && partType != BodypartType.Hand_R && partType != BodypartType.Elbow_L && partType != BodypartType.Elbow_R && partType != BodypartType.Arm_L && partType != BodypartType.Arm_R)
					{
						Vector3 vector = item.transform.position - tube.position;
						vector = Vector3.Project(vector, tube.forward);
						Vector3 vector2 = tube.position + vector - item.transform.position;
						vector2 = Vector3.ClampMagnitude(vector2, 1f);
						item.Rig.AddForce(vector2 * pullForce, ForceMode.Acceleration);
						if (entry.InverseTransformPoint(item.transform.position).z < 0f && (bool)HelperFunctions.LineCheck(entry.position, item.transform.position, HelperFunctions.LayerType.Map).transform)
						{
							vector2 = entry.forward;
							item.Rig.AddForce(vector2 * pullForce, ForceMode.Acceleration);
						}
					}
				}
			}
			else if (HelperFunctions.LineCheck(tube.position, character2.Center, HelperFunctions.LayerType.Map).transform == null)
			{
				Vector3 vector3 = tube.position - character2.Center;
				vector3.Normalize();
				character2.AddForce(-vector3 * pushForce);
			}
		}
		if (target != character)
		{
			if (character == null)
			{
				view.RPC("RPCA_SetTarget", RpcTarget.All, -1);
			}
			else
			{
				view.RPC("RPCA_SetTarget", RpcTarget.All, character.refs.view.ViewID);
			}
		}
	}

	[PunRPC]
	private void RPCA_SetTarget(int setTargetID)
	{
		targetID = setTargetID;
		if (targetID == -1)
		{
			target = null;
		}
		else
		{
			target = PhotonNetwork.GetPhotonView(targetID).GetComponent<Character>();
		}
	}

	private void Awake()
	{
		tube = transform.Find("Cannon");
		entry = tube.Find("Entry");
		view = GetComponent<PhotonView>();
	}

	public void CancelCast(Character interactor)
	{
	}

	public void Light()
	{
		view.RPC("RPCA_Light", RpcTarget.All);
	}

	[PunRPC]
	public void RPCA_Light()
	{
		StartCoroutine(LightRoutine());
		IEnumerator LightRoutine()
		{
			lit = true;
			litParticle.Play();
			anim.Play("Light");
			yield return new WaitForSeconds(fireTime);
			anim.Play("Fire");
			fireParticle.Play();
			fireSFX.SetActive(value: true);
			if (view.IsMine)
			{
				FireTargets();
			}
			yield return new WaitForSeconds(fallFor);
			lit = false;
		}
	}

	private void FireTargets()
	{
		LaunchPlayers();
		LaunchItems();
	}

	private void LaunchPlayers()
	{
		List<Character> list = new List<Character>();
		if ((bool)target)
		{
			list.Add(target);
		}
		foreach (Character allCharacter in Character.AllCharacters)
		{
			if (!(Vector3.Distance(allCharacter.Center, entry.position) > 0.75f) && !(allCharacter == target))
			{
				list.Add(allCharacter);
			}
		}
		foreach (Character allBotCharacter in Character.AllBotCharacters)
		{
			if (!(Vector3.Distance(allBotCharacter.Center, entry.position) > 0.75f) && !(allBotCharacter == target))
			{
				list.Add(allBotCharacter);
			}
		}
		foreach (Character item in list)
		{
			view.RPC("RPCA_LaunchTarget", RpcTarget.All, item.refs.view.ViewID);
		}
	}

	private void LaunchItems()
	{
		Collider[] array = Physics.OverlapSphere(tube.position, 1f, HelperFunctions.GetMask(HelperFunctions.LayerType.AllPhysical));
		List<Item> list = new List<Item>();
		Collider[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			Item componentInParent = array2[i].GetComponentInParent<Item>();
			if ((bool)componentInParent && componentInParent.itemState == ItemState.Ground && !HelperFunctions.LineCheck(tube.position, componentInParent.Center(), HelperFunctions.LayerType.Map).transform && !list.Contains(componentInParent))
			{
				list.Add(componentInParent);
			}
		}
		foreach (Item item in list)
		{
			view.RPC("RPCA_LaunchItem", RpcTarget.All, item.photonView.ViewID);
		}
	}

	[PunRPC]
	public void RPCA_LaunchItem(int targetID)
	{
		PhotonView photonView = PhotonNetwork.GetPhotonView(targetID);
		if (photonView == null)
		{
			return;
		}
		Item component = photonView.GetComponent<Item>();
		if (!(component == null))
		{
			component.lastThrownTime = Time.time;
			GlobalEvents.TriggerItemThrown(component);
			if (component is Backpack)
			{
				component.rig.AddForce(tube.forward * backpackLaunchForce, ForceMode.VelocityChange);
			}
			else
			{
				component.rig.AddForce(tube.forward * itemLaunchForce, ForceMode.VelocityChange);
			}
		}
	}

	[PunRPC]
	public void RPCA_LaunchTarget(int targetID)
	{
		PhotonView photonView = PhotonNetwork.GetPhotonView(targetID);
		if (photonView == null)
		{
			return;
		}
		Character t = photonView.GetComponent<Character>();
		if (!(t == null))
		{
			if (t.IsLocal)
			{
				t.data.launchedByCannon = true;
			}
			t.RPCA_Fall(fallFor, 0f);
			t.AddForce(tube.forward * launchForce);
			StartCoroutine(ILaunch());
		}
		IEnumerator ILaunch()
		{
			float c = 0f;
			while (c < fallFor)
			{
				c += Time.deltaTime;
				if ((bool)t)
				{
					t.ClampSinceGrounded(1f);
				}
				yield return null;
			}
			if ((bool)t)
			{
				t.data.fallSeconds = 0f;
			}
			c = 0f;
			while (c < 4f)
			{
				c += Time.deltaTime;
				if ((bool)t)
				{
					t.ClampSinceGrounded(1f);
				}
				yield return null;
			}
			t.data.launchedByCannon = false;
		}
	}
}
