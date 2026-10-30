using Photon.Pun;
using UnityEngine;

namespace Peak;

public class Action_RaycastSpawnSomething : ItemAction
{
	public Transform rayOrigin;

	public LayerMask hitLayerMask;

	public float maxDistance;

	public float backOffFromHitDistance;

	public GameObject prefabToSpawn;

	private RaycastHit raycastResult;

	public bool setShootableBasedOnRaycast;

	public QueryTriggerInteraction queryTriggerInteraction;

	private bool hitRecentRaycast;

	private void FixedUpdate()
	{
		hitRecentRaycast = Physics.Raycast(rayOrigin.position, MainCamera.instance.transform.forward, out raycastResult, maxDistance, hitLayerMask, queryTriggerInteraction);
		if (setShootableBasedOnRaycast)
		{
			item.UIData.isShootable = hitRecentRaycast;
		}
	}

	public override void RunAction()
	{
		PhotonNetwork.Instantiate(position: (!hitRecentRaycast) ? (rayOrigin.position + MainCamera.instance.transform.forward * (maxDistance - backOffFromHitDistance)) : (raycastResult.point - MainCamera.instance.transform.forward * backOffFromHitDistance), prefabName: prefabToSpawn.gameObject.name, rotation: Quaternion.identity, group: 0);
	}
}
