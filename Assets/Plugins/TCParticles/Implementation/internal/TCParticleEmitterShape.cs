using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace TC.Internal {
	[Serializable]
	public class ParticleEmitterShape {
		// Shape to emit from
		public EmitShapes shape = EmitShapes.Sphere;

		//Sphere emission
		public MinMax radius = MinMax.Constant(5.0f);

		//Cube emission
		public Vector3 cubeSize = Vector3.one;

		//Mesh emission settings
		public Mesh emitMesh;
		public Texture texture;
		public bool normalizeArea = true;
		[Range(0, 3)] public int uvChannel;
		public bool spawnOnMeshSurface = true;

		//Cone emission
		[Range(0.0f, 89.9f)] public float coneAngle;
		public float coneHeight;
		public float coneRadius;

		//Ring emission
		public float ringOuterRadius;
		public float ringRadius;

		//Line emission
		public float lineLength;
		public float lineRadius;

		// Velocity vectors
		public StartDirection startDirectionType = StartDirection.Normal;
		public Vector3 startDirectionVector;
		public float startDirectionRandomAngle;

		// Point cloud emission
		public PointCloudData pointCloud;

		ComputeBuffer m_emitProtoBuffer;

		[NonSerialized]
		ParticleProto[] m_toEmitBuffer;

		int m_toEmitListCount;
		Vector4 m_emitUseVelSizeColorPos;

		public void SetMeshData(ComputeShader cs, int kern, ref ParticleEmitterData emitter) {
			if (emitMesh == null) {
				return;
			}

			var buffer = TCParticleGlobalManager.GetMeshBuffer(emitMesh, uvChannel);
			uint onSurface;

			if (!spawnOnMeshSurface) {
				onSurface = 0;
			} else {
				onSurface = normalizeArea ? (uint) 1 : 2;
			}

			emitter.MeshVertLen = (uint) buffer.count;
			emitter.OnSurface = onSurface;
			cs.SetBuffer(kern, "emitFaces", buffer);

			if (texture != null) {
				cs.SetTexture(kern, "_MeshTexture", texture);
			} else {
				cs.SetTexture(kern, "_MeshTexture", Texture2D.whiteTexture);
			}
		}

		public void UpdateListData(ComputeShader cs, int kern) {
			//Make sure we always have some buffer
			if (m_emitProtoBuffer == null || m_emitProtoBuffer.count < m_toEmitListCount) {
				if (m_emitProtoBuffer != null) {
					m_emitProtoBuffer.Release();
				}

				m_emitProtoBuffer = new ComputeBuffer(Mathf.Max(1, m_toEmitListCount), ParticleProto.Stride);
			}

			cs.SetBuffer(kern, "emitList", m_emitProtoBuffer);

			if (m_toEmitBuffer != null) {
				Profiler.BeginSample("Upload Particle Prototype data");
				cs.SetFloat(SID._UseEmitList, 1.0f);
				m_emitProtoBuffer.SetData(m_toEmitBuffer, 0, 0, m_toEmitListCount);
				cs.SetVector(SID._UseVelSizeColorPos, m_emitUseVelSizeColorPos);
				m_toEmitBuffer = null;
				Profiler.EndSample();
			} else {
				cs.SetFloat(SID._UseEmitList, 0.0f);
			}
		}

		public void SetPointCloudData(ComputeShader cs, int kern, int count, ref ParticleEmitterData emitter) {
			if (pointCloud == null) {
				return;
			}

			var particlePrototypes = pointCloud.GetPrototypes(count);
			SetPrototypeEmission(particlePrototypes, count, true, false, false, true);
		}

		public void ReleaseData() {
			if (m_emitProtoBuffer != null) {
				m_emitProtoBuffer.Release();
			}
		}

		internal void SetPrototypeEmission(ParticleProto[] prototypes, int count, bool useColor, bool useSize, bool useVelocity, bool usePosition) {
			m_toEmitBuffer = prototypes;
			m_emitUseVelSizeColorPos = new Vector4(useVelocity ? 1 : 0, useSize ? 1 : 0, useColor ? 1 : 0, usePosition ? 1 : 0);
			m_toEmitListCount = count;
		}
	}
}