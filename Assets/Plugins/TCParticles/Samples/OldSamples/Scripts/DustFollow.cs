using UnityEngine;
using System.Collections;

public class DustFollow : MonoBehaviour {

	public GameObject follow;

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
		Vector3 pos = transform.position;
		pos.z = follow.transform.position.z;
		transform.position = pos;
	}
}
