using UnityEngine;
using System.Collections;

public class Projectile : MonoBehaviour {
	
	public float life;

	public GameObject spawnObject;
	public float spawnDelay;

	IEnumerator Death()
	{
		yield return new WaitForSeconds(life);
		Destroy(gameObject);
	}

	IEnumerator Spawn()
	{
		if (spawnObject == null)
			yield break;

		yield return new WaitForSeconds(spawnDelay);
		Instantiate(spawnObject, transform.position, Quaternion.identity);
	}

	void Start () {
		StartCoroutine(Death());
		StartCoroutine(Spawn());
	}

}
