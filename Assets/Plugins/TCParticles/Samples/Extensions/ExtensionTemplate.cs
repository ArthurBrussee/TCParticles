using UnityEngine;
using TC;

//Small example of how to create extensions for TC Particle systems
public class ExtensionTemplate : MonoBehaviour {
	//The system to apply the extension computation too
	public TCParticleSystem System;

	//The compute shader to use for the extension
	public ComputeShader Extension;

	//The amount to accelerate particles
	[Range(0.0f, 10.0f)] public float AccelSpeed = 1.0f;


	void Update() {
		//Bind own custom variables to compute shader
		Extension.SetFloat("AccelSpeed", AccelSpeed);

		//Then use the DispatchExtensionKernel API.
		//This API will setup the correct state and fire of a thread for each particle
		System.Manager.DispatchExtensionKernel(Extension, Extension.FindKernel("MyExtensionKernel"));
	}
}