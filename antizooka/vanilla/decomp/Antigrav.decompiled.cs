using UnityEngine;

[RequireComponent(typeof(Item))]
public class Antigrav : MonoBehaviour
{
	private Item item;

	public float intensity = 1f;

	private void Start()
	{
		item = GetComponent<Item>();
		item.antigrav = this;
	}

	private void FixedUpdate()
	{
		if (item.itemState == ItemState.Ground)
		{
			item.rig.AddForce(-Physics.gravity * intensity, ForceMode.Acceleration);
		}
	}
}
