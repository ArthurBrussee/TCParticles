using UnityEngine;

public interface TCParticleManager
{
	void DispatchExtensionKernel(ComputeShader extension, string kernelName);
}