using Photon.Pun;
using UnityEngine;

public class Action_Spawn : ItemAction
{
	public GameObject objectToSpawn;

	public Transform spawnPoint;

	public override void RunAction()
	{
		if (spawnPoint == null)
		{
			spawnPoint = transform;
		}
		item.photonView.RPC("RPCSpawn", RpcTarget.All, spawnPoint.transform.position, spawnPoint.transform.rotation);
	}

	[PunRPC]
	public void RPCSpawn(Vector3 position, Quaternion rotation)
	{
		Object.Instantiate(objectToSpawn, position, rotation);
	}
}
