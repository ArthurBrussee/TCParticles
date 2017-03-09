#pragma strict
var myFusion : GameObject;
var myFusion2 : GameObject;
function Start () {

}

function Update () {
	if(Input.GetMouseButtonUp(0)){
		Instantiate(myFusion, Vector3.zero, Random.rotation);
	}
	
	if(Input.GetMouseButtonUp(1)){
		Instantiate(myFusion2, Vector3.zero, Random.rotation);
	}
}