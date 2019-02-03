using UnityEngine;
using System.Collections;
using TC;

public class PlasmaGun : MonoBehaviour {
	public GameObject plasmaBolt;
	public GameObject grenade;
	public GameObject firethrower;

	public float MoveSpeed = 0.3f;

	public enum Gun {
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

	void Awake() {
		cc  = GetComponent<CharacterController>();
		fireSystem = firethrower.GetComponent<TCParticleSystem>();
	}
	
	void Update() {
		if (fireSystem != null){
			fireSystem.Emitter.DoEmit = false;
		}

		if (Input.GetMouseButton(0) && canFire) {
			switch (selectedGun) {
				case Gun.PlasmaBolt:
					StartCoroutine(FirePlasma());
					break;

				case Gun.GrenadeLauncher:
					StartCoroutine(FireGrenade());
					break;

				case Gun.Firethrower:
					fireSystem.Emitter.DoEmit = true;
					break;
			}
		}

		var sp = MoveSpeed * Time.deltaTime;

		if (Input.GetKey(KeyCode.W)) {
			cc.Move(transform.forward * sp);
		}

		if (Input.GetKey(KeyCode.A)) {
			cc.Move(-transform.right * sp);
		}
		if (Input.GetKey(KeyCode.S)) {
			cc.Move(-transform.forward * sp);
		}
		if (Input.GetKey(KeyCode.D)) {
			cc.Move(transform.right * sp);
		}

		if (Input.GetKeyDown(KeyCode.Alpha1)) {
			selectedGun = Gun.PlasmaBolt;
		} else if (Input.GetKeyDown(KeyCode.Alpha2)) {
			selectedGun = Gun.GrenadeLauncher;
		} else if (Input.GetKeyDown(KeyCode.Alpha3)) {
			selectedGun = Gun.Firethrower;
		}

		float axis = Input.GetAxis("Mouse ScrollWheel");

		if (axis < 0 && selectedGun != Gun.PlasmaBolt){
			selectedGun--;
		} else if (axis > 0 && selectedGun != Gun.Firethrower){
			selectedGun++;
		}
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
