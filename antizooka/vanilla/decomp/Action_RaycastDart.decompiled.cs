using Peak.Afflictions;
using Photon.Pun;
using UnityEngine;

public class Action_RaycastDart : ItemAction
{
	public float maxDistance;

	public float dartCollisionSize;

	[SerializeReference]
	public Affliction[] afflictionsOnHit;

	public Transform spawnTransform;

	public GameObject dartVFX;

	private HelperFunctions.LayerType layerMaskType;

	private RaycastHit lineHit;

	private RaycastHit[] sphereHits;

	public SFX_Instance shotSFX;

	public override void RunAction()
	{
		FireDart();
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere(spawnTransform.position, dartCollisionSize);
	}

	private void FireDart()
	{
		if ((bool)shotSFX)
		{
			shotSFX.Play(transform.position);
		}
		Physics.Raycast(spawnTransform.position, MainCamera.instance.transform.forward, out lineHit, maxDistance, HelperFunctions.terrainMapMask, QueryTriggerInteraction.Ignore);
		if (!lineHit.collider)
		{
			lineHit.distance = maxDistance;
			lineHit.point = spawnTransform.position + MainCamera.instance.transform.forward * maxDistance;
		}
		sphereHits = Physics.SphereCastAll(spawnTransform.position, dartCollisionSize, MainCamera.instance.transform.forward, lineHit.distance, LayerMask.GetMask("Character"), QueryTriggerInteraction.Ignore);
		RaycastHit[] array = sphereHits;
		for (int i = 0; i < array.Length; i++)
		{
			RaycastHit raycastHit = array[i];
			if ((bool)raycastHit.collider)
			{
				Character componentInParent = raycastHit.collider.GetComponentInParent<Character>();
				if ((bool)componentInParent && componentInParent != character)
				{
					DartImpact(componentInParent, spawnTransform.position, raycastHit.point);
					return;
				}
			}
		}
		DartImpact(null, spawnTransform.position, lineHit.point);
	}

	private void DartImpact(Character hitCharacter, Vector3 origin, Vector3 endpoint)
	{
		if ((bool)hitCharacter)
		{
			photonView.RPC("RPC_DartImpact", RpcTarget.All, hitCharacter.photonView.ViewID, origin, endpoint);
		}
		else
		{
			photonView.RPC("RPC_DartImpact", RpcTarget.All, -1, origin, endpoint);
		}
	}

	[PunRPC]
	private void RPC_DartImpact(int characterID, Vector3 origin, Vector3 endpoint)
	{
		if (characterID != -1)
		{
			Character component = PhotonNetwork.GetPhotonView(characterID).gameObject.GetComponent<Character>();
			if (component != null && component.photonView.IsMine)
			{
				Affliction[] array = afflictionsOnHit;
				foreach (Affliction affliction in array)
				{
					component.refs.afflictions.AddAffliction(affliction);
				}
			}
		}
		Object.Instantiate(dartVFX, endpoint, Quaternion.identity);
		GamefeelHandler.instance.AddPerlinShakeProximity(endpoint, 5f);
	}
}
