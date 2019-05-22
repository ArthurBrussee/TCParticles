using System;
using System.Linq;
using UnityEngine;

namespace TC.EditorIntegration {
	[Serializable]
	public class TCNoiseForceVisualize {
		public PreviewModeEnum PreviewMode;
		public float PreviewSlice = 0.5f;

		TCForce m_target;

		Mesh arrowMesh;
		Material arrowMat;
		ComputeBuffer arrowBuffer;

		public enum PreviewModeEnum {
			Stability,
			MagnitudeX,
			MagnitudeY,
			MagnitueZ
		}

		public TCNoiseForceVisualize(TCForce target) {
			m_target = target;


			arrowMat = Resources.Load("Editor/ArrowMat", typeof(Material)) as Material;
			arrowMesh = Resources.Load("Editor/Arrow1", typeof(Mesh)) as Mesh;

			var triangles = arrowMesh.triangles;
			var vertices = arrowMesh.vertices;

			arrowBuffer = new ComputeBuffer(triangles.Length, 12);
			arrowBuffer.SetData(triangles.Select(tri => vertices[tri]).ToArray());
		}

		public void Release() {
			arrowBuffer.Release();
		}

		public void DrawTurbulencePreview() {
			arrowMat.SetFloat("_Slice", PreviewSlice);
			arrowMat.SetInt("_PreviewMode", (int) PreviewMode);
			arrowMat.SetInt("_Resolution", m_target.resolution);

			arrowMat.SetTexture("_ForceTexture", m_target.CurrentForceVolume);
			arrowMat.SetMatrix("_ModelMatrix", Matrix4x4.TRS(m_target.transform.position, m_target.transform.rotation, m_target.noiseExtents));
			arrowMat.SetBuffer("vertices", arrowBuffer);

			arrowMat.SetPass(1);
			Graphics.DrawProceduralNow(MeshTopology.Triangles, arrowBuffer.count, m_target.resolution * m_target.resolution);
		}
	}
}