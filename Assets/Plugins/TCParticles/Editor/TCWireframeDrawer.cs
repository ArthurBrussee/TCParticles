using UnityEngine;

public class TCWireframeDrawer {
	readonly Material m_lineMat;
	Vector3[] m_lines;

	public TCWireframeDrawer(Mesh mesh) {
		m_lineMat =
			new Material(Shader.Find("Hidden/Wireframe")) {
				hideFlags = HideFlags.HideAndDontSave,
			};

		BuildLineMesh(mesh);
	}


	void BuildLineMesh(Mesh mesh) {
		if (mesh == null) {
			return;
		}

		var vertices = mesh.vertices;
		var triangles = mesh.triangles;

		m_lines = new Vector3[triangles.Length];
		for (int i = 0; i < triangles.Length; i += 3) {
			m_lines[i + 0] = vertices[triangles[i]];
			m_lines[i + 1] = vertices[triangles[i + 1]];
			m_lines[i + 2] = vertices[triangles[i + 2]];
		}
	}

	public void UpdateMesh(Mesh mesh) {
		BuildLineMesh(mesh);
	}

	public void DrawWireMesh(Transform trans) {
		if (m_lines == null || m_lines.Length == 0) {
			return;
		}

		m_lineMat.SetPass(0);

		GL.PushMatrix();
		GL.MultMatrix(Matrix4x4.TRS(trans.position, trans.rotation,
			trans.localScale));
		GL.Begin(GL.LINES);
		GL.Color(new Color(1.0f, 1.0f, 1.0f, 0.4f));

		for (int i = 0; i < m_lines.Length / 3; i++) {
			GL.Vertex(m_lines[i * 3]);
			GL.Vertex(m_lines[i * 3 + 1]);

			GL.Vertex(m_lines[i * 3 + 1]);
			GL.Vertex(m_lines[i * 3 + 2]);

			GL.Vertex(m_lines[i * 3 + 2]);
			GL.Vertex(m_lines[i * 3]);
		}

		GL.End();
		GL.PopMatrix();
	}

	public void Release() {
		Object.DestroyImmediate(m_lineMat);
	}
}