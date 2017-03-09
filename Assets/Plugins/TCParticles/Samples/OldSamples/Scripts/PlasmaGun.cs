using UnityEngine;
using System.Collections;

public class PlasmaGun : MonoBehaviour {
	public GameObject plasmaBolt;
	public GameObject grenade;
	public GameObject firethrower;

	public enum Gun
	{
		PlasmaBolt,
		GrenadeLauncher,
		Firethrower
	}

	public float plasmaRefireRate = 1.0f;
	public float plasmaShotPower;

	public float grenadeRefireRate = 0.5f;
	public float grenadeShotPower = 1.0f;
	public float grenadeVerticalPower = 1.0f;

	public Transform fireTransform;

	TCParticleSystem fireSystem;
	public Gun selectedGun;
	bool canFire = true;
	CharacterController cc;


	// Use this for initialization
	void Start () {
		cc  = GetComponent<CharacterController>();
		fireSystem = firethrower.GetComponent<TCParticleSystem>();
	}
	
	// Update is called once per frame
	void Update () {

		if (fireSystem != null)
			fireSystem.Emitter.DoEmit = false;

		if(Input.GetMouseButton(0) && canFire)
		{
			switch (selectedGun)
			{
				case Gun.PlasmaBolt:
					StartCoroutine(FirePlasma());
					break;

				case Gun.GrenadeLauncher:
					StartCoroutine(FireGrenade());
					break;
				
				case Gun.Firethrower:
					//firethrower.transform.position = fireTransform.position;
					//firethrower.transform.forward = Camera.main.transform.forward;
					fireSystem.Emitter.DoEmit = true;
					break;
			}
		}

		if (Input.GetKeyDown(KeyCode.Alpha1))
			selectedGun = Gun.PlasmaBolt;
		else if (Input.GetKeyDown(KeyCode.Alpha2))
			selectedGun = Gun.GrenadeLauncher;
		else if (Input.GetKeyDown(KeyCode.Alpha3))
			selectedGun = Gun.Firethrower;

		if (Input.GetAxis("Mouse ScrollWheel") < 0 && selectedGun != Gun.PlasmaBolt)
			selectedGun--;
		else if (Input.GetAxis("Mouse ScrollWheel") > 0 && selectedGun != Gun.Firethrower)
			selectedGun++;
	}

	IEnumerator FireGrenade()
	{
		canFire = false;
		GameObject gr = Instantiate(grenade, fireTransform.position + cc.velocity * Time.deltaTime, Quaternion.identity) as GameObject;
		gr.GetComponent<Rigidbody>().velocity = Camera.main.transform.forward * grenadeShotPower + cc.velocity + new Vector3(0.0f, grenadeVerticalPower, 0.0f);

		yield return new WaitForSeconds(grenadeRefireRate);

		canFire = true;
	}

	IEnumerator FirePlasma()
	{
		canFire = false;
		GameObject plasma = Instantiate(plasmaBolt, fireTransform.position + cc.velocity * Time.deltaTime, Quaternion.identity) as GameObject;
		plasma.GetComponent<Rigidbody>().velocity = Camera.main.transform.forward * plasmaShotPower + cc.velocity;
		
		yield return new WaitForSeconds(plasmaRefireRate);

		canFire = true;
	}
}
