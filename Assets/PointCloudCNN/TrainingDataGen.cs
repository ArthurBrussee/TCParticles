using System.Collections.Generic;
using UnityEngine;

public class TrainingDataGen : MonoBehaviour {
	List<Vector3> vertices = new List<Vector3>();
	List<int> indices = new List<int>();
	List<Vector2> uvs = new List<Vector2>();

	
	void PushQuadVert(Vector3 basePos, float x, float y, float z) {
		indices.Add(vertices.Count);
		vertices.Add(basePos + new Vector3(x, y, z));
		uvs.Add(new Vector2(x * 0.5f + 0.5f, z * 0.5f + 0.5f));
	}

	[ContextMenu("Generate Training Mesh")]
	void GenerateMesh() {
		Mesh m = new Mesh();

		vertices.Clear();
		indices.Clear();

		for (int x = 0; x < 8; ++x) {
			for (int z = 0; z < 8; ++z) {
				Vector3 basePos = new Vector3(x * 2.0f, 0.0f, z * 2.0f);
				float height = Random.Range(-2.0f, 2.0f);

				PushQuadVert(basePos, 0.5f, height, 0.5f);
				PushQuadVert(basePos, 1.0f, 0.0f, -1.0f);
				PushQuadVert(basePos, -1.0f, 0.0f, -1.0f);
				
				
				PushQuadVert(basePos, 0.5f, height, 0.5f);
				PushQuadVert(basePos, 1.0f, 0.0f, 1.0f);
				PushQuadVert(basePos, 1.0f, 0.0f, -1.0f);
				
				
				PushQuadVert(basePos, 0.5f, height, 0.5f);
				PushQuadVert(basePos, -1.0f, 0.0f, 1.0f);
				PushQuadVert(basePos, 1.0f, 0.0f, 1.0f);
				
				PushQuadVert(basePos, 0.5f, height, 0.5f);
				PushQuadVert(basePos, -1.0f, 0.0f, -1.0f);
				PushQuadVert(basePos, -1.0f, 0.0f, 1.0f);
			}
		}

		m.SetVertices(vertices);
		m.SetTriangles(indices, 0);
		m.SetUVs(0, uvs);
		
		m.RecalculateBounds();
		m.RecalculateNormals();
		m.RecalculateTangents();

		GetComponent<MeshFilter>().sharedMesh = m;
	}
}
