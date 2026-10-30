using System.Collections;
using UnityEngine;

public class ExplosionEffect : MonoBehaviour
{
	private static readonly int k_Random = Shader.PropertyToID("_Random");

	public float speed = 1f;

	public GameObject explosionOrb;

	public float baseScale = 1f;

	public float explosionRadius = 5f;

	public float spawnRadiusFactor = 0.8f;

	public float childSizeFactor = 0.9f;

	public float minDelay = 0.4f;

	public float maxDelay = 0.5f;

	public float minSpeed = 0.75f;

	public float maxSpeed = 1.25f;

	private ExplosionOrb[] explosionPoints;

	public int explosionPointCount = 4;

	public int subExplosionPointCount = 2;

	private void Start()
	{
		GetPoints();
		ExplosionOrb[] array = explosionPoints;
		foreach (ExplosionOrb item in array)
		{
			StartCoroutine(IExplode(item));
		}
		IEnumerator IExplode(ExplosionOrb explosionOrb)
		{
			yield return new WaitForSeconds(explosionOrb.delay / (speed * explosionOrb.speed));
			GameObject gameObject = Object.Instantiate(this.explosionOrb, explosionOrb.position, HelperFunctions.GetRandomRotationWithUp(explosionOrb.direction));
			gameObject.GetComponentInChildren<Animator>().speed = speed * explosionOrb.speed;
			gameObject.transform.localScale = Vector3.one * (explosionOrb.size * baseScale);
			MeshRenderer componentInChildren = gameObject.GetComponentInChildren<MeshRenderer>();
			MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
			componentInChildren.GetPropertyBlock(materialPropertyBlock);
			materialPropertyBlock.SetFloat(k_Random, Random.value);
			componentInChildren.SetPropertyBlock(materialPropertyBlock);
		}
	}

	private void GetPoints()
	{
		int num = 1 + explosionPointCount + subExplosionPointCount * explosionPointCount;
		explosionPoints = new ExplosionOrb[num];
		explosionPoints[0] = new ExplosionOrb(transform.position, Vector3.up);
		int num2 = 0;
		float size = childSizeFactor * childSizeFactor;
		for (int i = 0; i < explosionPointCount; i++)
		{
			num2++;
			Vector3 vector = Random.onUnitSphere * explosionRadius * spawnRadiusFactor;
			vector.y = Mathf.Abs(vector.y);
			explosionPoints[num2] = new ExplosionOrb(transform.position + vector, vector, Random.Range(minDelay, maxDelay), childSizeFactor, Random.Range(minSpeed, maxSpeed));
			int num3 = num2;
			for (int j = 0; j < subExplosionPointCount; j++)
			{
				num2++;
				Vector3 position = explosionPoints[num3].position;
				vector = Random.onUnitSphere * explosionRadius * explosionPoints[num3].size * spawnRadiusFactor;
				vector.y = Mathf.Abs(vector.y);
				explosionPoints[num2] = new ExplosionOrb(position + vector, vector, explosionPoints[num3].delay + Random.Range(minDelay, maxDelay), size, explosionPoints[num3].speed + Random.Range(minSpeed, maxSpeed));
			}
		}
	}

	public void OnDrawGizmosSelected()
	{
		if (explosionPoints != null && explosionPoints.Length != 0)
		{
			ExplosionOrb[] array = explosionPoints;
			for (int i = 0; i < array.Length; i++)
			{
				ExplosionOrb explosionOrb = array[i];
				Gizmos.color = Color.red;
				Gizmos.DrawWireSphere(explosionOrb.position, explosionRadius * explosionOrb.size);
			}
		}
	}
}
