using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Generates a blobby sphere from a sphere mesh
/// </summary>
public class BlobGen : MonoBehaviour {
	[ContextMenu("Blobify")]
	void Blobify() {
		var meshFilter = GetComponent<MeshFilter>();
		var mesh = meshFilter.sharedMesh;

		var verts = mesh.vertices;
		var tris = mesh.GetTriangles(0);
		var normals = mesh.normals;
		var uvs = mesh.uv;

		var localToWorld = transform.localToWorldMatrix;
		
		for (int i = 0; i < verts.Length; ++i) {
			var worldSpace = localToWorld.MultiplyPoint(verts[i]);
			verts[i] += noise.pnoise(worldSpace * 5.0f, math.float3(0.2f, 0.2f, 0.2f)) * 0.2f * normals[i];

			verts[i] *= 5.0f; // make blob a bit bigger so we can see it better
		}

		var newMesh = new Mesh();

		newMesh.vertices = verts;
		newMesh.SetTriangles(tris, 0);
		newMesh.uv = uvs;
		
		newMesh.RecalculateBounds();
		newMesh.RecalculateNormals();
		newMesh.RecalculateTangents();
		
		meshFilter.sharedMesh = newMesh;
	}
}
