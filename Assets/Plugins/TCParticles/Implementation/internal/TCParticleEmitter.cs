using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace TC.Internal {
	//Emitter class. Handles emitting of particles, streaming, and most of the properties.
	[Serializable]
	public class ParticleEmitter : ParticleComponent, TCParticleEmitter {
		#region ParticleEmitterInterface

		[SerializeField] ParticleEmitterShape pes = new ParticleEmitterShape();

		public EmitShapes Shape {
			get { return pes.shape; }
			set { pes.shape = value; }
		}

		public MinMax Radius {
			get { return pes.radius; }
		}

		public Vector3 CubeSize {
			get { return pes.cubeSize; }
			set { pes.cubeSize = value; }
		}

		public Mesh EmitMesh {
			get { return pes.emitMesh; }
			set { pes.emitMesh = value; }
		}

		public float ConeAngle {
			get { return pes.coneAngle; }
			set { pes.coneAngle = value; }
		}

		public float ConeHeight {
			get { return pes.coneHeight; }
			set { pes.coneHeight = value; }
		}

		public float ConeRadius {
			get { return pes.coneRadius; }
			set { pes.coneRadius = value; }
		}

		public float RingOuterRadius {
			get { return pes.ringOuterRadius; }
			set { pes.ringOuterRadius = value; }
		}

		public float RingRadius {
			get { return pes.ringRadius; }
			set { pes.ringRadius = value; }
		}

		public float LineLength {
			get { return pes.lineLength; }
			set { pes.lineLength = value; }
		}


		[SerializeField] private MinMaxRandom _speed = MinMaxRandom.Constant(3.0f);

		public MinMaxRandom Speed {
			get { return _speed; }
		}

		[SerializeField] private MinMaxRandom _energy = MinMaxRandom.Constant(3.0f);

		public MinMaxRandom Energy {
			get { return _energy; }
		}

		[SerializeField] private MinMaxRandom _size = MinMaxRandom.Constant(0.5f);

		public MinMaxRandom Size {
			get { return _size; }
		}

		[SerializeField] private MinMaxRandom _rotation = MinMaxRandom.Constant(0.0f);

		public MinMaxRandom Rotation {
			get { return _rotation; }
		}

		[SerializeField] private float _angularVelocity;

		public float AngularVelocity {
			get { return _angularVelocity; }
			set { _angularVelocity = value; }
		}

		[SerializeField] private Vector3Curve _constantForce = Vector3Curve.Zero();

		public Vector3Curve ConstantForce {
			get { return _constantForce; }
			set { _constantForce = value; }
		}

		[SerializeField] private float _emissionRate = 100.0f;

		public float EmissionRate {
			get { return _emissionRate; }
			set { _emissionRate = value; }
		}

		public enum EmissionTypeEnum {
			PerSecond,
			PerUnit
		}

		[SerializeField] private EmissionTypeEnum m_emissionType;

		public EmissionTypeEnum EmissionType {
			get { return m_emissionType; }
			set { m_emissionType = value; }
		}

		public int ParticleCount {
			get { return Manager.ParticleCount; }
		}

		[SerializeField] private AnimationCurve _sizeOverLifetime = AnimationCurve.Linear(0.0f, 1.0f, 1.0f, 1.0f);

		public AnimationCurve SizeOverLifetime {
			get { return _sizeOverLifetime; }
			set {
				_sizeOverLifetime = value;
				UpdateSizeOverLifetime();
			}
		}

		public float StartSizeMultiplier {
			get { return _sizeOverLifetime.Evaluate(0.0f); }

			set {
				_sizeOverLifetime.MoveKey(0, new Keyframe(0.0f, value));
				UpdateSizeOverLifetime();
			}
		}


		[SerializeField] private Vector3Curve _velocityOverLifetime = Vector3Curve.Zero();

		public Vector3Curve VelocityOverLifetime {
			get { return _velocityOverLifetime; }
			set {
				_velocityOverLifetime = value;
				UpdateVelocityOverLifetime();
			}
		}

		private Texture2D m_lifetimeTexture;

		[SerializeField] private bool emit = true;

		public bool DoEmit {
			get { return emit; }
			set { emit = value; }
		}

		public Color StartColor {
			get { return Renderer.ColourOverLifetime.Evaluate(0.0f); }
		}

		[SerializeField] [Range(0.0f, 1.0f)] private float _inheritVelocity;

		public float InheritVelocity {
			get { return _inheritVelocity; }
			set { _inheritVelocity = value; }
		}

		#endregion

		public StartDirectionType StartDirectionType {
			get { return pes.startDirectionType; }
			set { pes.startDirectionType = value; }
		}


		public Vector3 StartDirectionVector {
			get { return pes.startDirectionVector; }
			set { pes.startDirectionVector = value; }
		}


		public float StartDirectionRandomAngle {
			get { return pes.startDirectionRandomAngle; }
			set { pes.startDirectionRandomAngle = value; }
		}

		public Texture2D SizeOverLifetimeTexture {
			get { return m_lifetimeTexture; }
		}

		private bool m_doSizeOverLifetime;

		public bool DoSizeOverLifetime {
			get { return m_doSizeOverLifetime; }

		}

		[SerializeField] TCShapeEmitTag m_emitTag;

		[SerializeField] List<Burst> bursts = new List<Burst>();

		//last emit time. We can clear all particles if we haven't emitted in a while.
		float m_prevTime = -1.0f;
		//We can't clear particles with infinite life. 


		//Bursts sequences
		[Serializable]
		private class Burst {
			public int amount;
			public float time;
			public float life;
		}

		readonly Queue<Burst> m_burstsDone = new Queue<Burst>(100);
		List<TCShapeEmitter> m_shapeEmitters;

		Emitter[] m_emitSet;

		Vector3 m_emitPrevPos;
		Vector3 m_emitPrevSpeed;

		//Velocity 
		Vector3 m_prevPosition;
		Vector3 m_velocity = Vector3.zero;
		Quaternion m_lastRot;
		Vector3 m_lastScale;
		Matrix4x4 m_emitMatrix;
		Matrix4x4 m_emitRotationMatrix;

		ComputeBuffer m_emitBuffer;
		float m_femit;

		const int c_texDim = 64;

		static readonly Color[] Colors = new Color[c_texDim];


		// ReSharper disable NotAccessedField.Local
		struct Emitter {
			public Vector3 Pos;
			public Vector3 Vel;
			public Vector3 Accel;

			public float SizeMin;
			public float SizeMax;
			public float SpeedMin;
			public float SpeedMax;
			public float RotationMin;
			public float RotationMax;
			public uint Shape;
			//General parameters
			public float RadiusMax;
			public float RadiusMin;
			//BOX
			public Vector3 CubeSize;
			//CONE
			public float ConeHeight;
			public Vector3 ConePointUnder;
			//LINE
			public float LineLength;
			//MESH
			public uint MeshVertLen;
			public uint VelType;
			public float RandomAngle;
			public Vector3 StartSpeed;
			public float MassVariance;
			public uint Time;
			public uint EmitOffset;
			public Vector3 Scale;
			public uint OnSurface;
		};

		// ReSharper restore NotAccessedField.Local


		public ParticleEmitterShape GetEmitterShapeData() {
			return pes;
		}

		public override void Initialize() {
			InitializeProperties();
			//allocate the buffers and arrays and such
			m_lifetimeTexture = new Texture2D(64, 1, TextureFormat.RGBAHalf, false, true)
			{wrapMode = TextureWrapMode.Clamp, anisoLevel = 0};

			UpdateLifetimeTexture();

			m_emitBuffer = new ComputeBuffer(1, EmitterStride);
			m_emitSet = new Emitter[1];
			m_emitPrevPos = GetEmitPos(Transform);
			m_emitPrevSpeed = Vector3.zero;
		}

		public override void OnEnable() {
			m_emitPrevPos = GetEmitPos(Transform);
			m_emitPrevSpeed = Vector3.zero;
		}

		public void RegisterShape(TCShapeEmitter emitter) {
			if (!emitter.HasTag(m_emitTag)) {
				return;
			}

			if (m_shapeEmitters == null) {
				m_shapeEmitters = new List<TCShapeEmitter>();
			}

			m_shapeEmitters.Add(emitter);
		}

		public void RemoveShape(TCShapeEmitter emitter) {
			if (m_shapeEmitters == null) {
				m_shapeEmitters = new List<TCShapeEmitter>();
			}

			if (m_shapeEmitters.Contains(emitter)) {
				m_shapeEmitters.Remove(emitter);
			}
		}

		void InitializeProperties() {
			pes.radius.Init();
			Speed.Init();
			Energy.Init();
			Size.Init();
			Rotation.Init();
		}


		void UpdateLifetimeTexture() {
			m_doSizeOverLifetime = false;

			for (int i = 0; i < c_texDim; ++i) {
				float t = i / (c_texDim - 1.0f);

				Vector3 v = VelocityOverLifetime.Value(t);

				float r = v.x; //xspeed
				float g = v.y; //yspeed
				float b = v.z; //zspeed
				float a = _sizeOverLifetime.Evaluate(t); //size

				if (a > 0.0f) {
					m_doSizeOverLifetime = true;
				}

				Colors[i] = new Color(r, g, b, a);
			}

			m_lifetimeTexture.SetPixels(Colors);
			m_lifetimeTexture.Apply();
		}

		public void UpdateSizeOverLifetime() {
			UpdateLifetimeTexture();
		}

		void UpdateVelocityOverLifetime() {
			UpdateLifetimeTexture();
		}

		void SetShapeData(ParticleEmitterShape emitShape, Transform trans, ref Vector3 prevPos, ref Vector3 prevSpeed) {
			var emitter = m_emitSet[0];

			emitter.EmitOffset = (uint) ((Manager.Offset + Manager.NumParticles) % Manager.MaxParticles);

			if (m_toEmitList == null) {
				emitter.Shape = (uint)emitShape.shape;

				switch (emitShape.shape) {
					case EmitShapes.Box:
						emitter.CubeSize = emitShape.cubeSize * 0.5f;
						break;

					case EmitShapes.Cone:
						emitter.RadiusMin = emitShape.coneRadius;
						float tan = Mathf.Tan(emitShape.coneAngle * Mathf.Deg2Rad);
						emitter.RadiusMax = emitShape.coneRadius + tan * emitShape.coneHeight;
						emitter.ConePointUnder = new Vector3(0.0f, 0.0f,
							-Mathf.Tan((90.0f - emitShape.coneAngle) * Mathf.Deg2Rad) * emitShape.coneRadius);
						emitter.ConeHeight = emitShape.coneHeight;
						break;

					case EmitShapes.HemiSphere:
						emitter.RadiusMin = emitShape.radius.IsConstant
							? 0.0f
							: Mathf.Max(Mathf.Min(emitShape.radius.Min, emitShape.radius.Max), 0);
						emitter.RadiusMax = emitShape.radius.Max;
						break;

					case EmitShapes.Line:
						emitter.LineLength = emitShape.lineLength;
						emitter.RadiusMin = 0.0f;
						emitter.RadiusMax = emitShape.lineRadius;
						break;

					case EmitShapes.Mesh:
						pes.SetMeshData(ComputeShader, EmitKernel, "emitFaces");
						emitter.MeshVertLen = (uint) emitShape.meshCount;
						emitter.OnSurface = (uint)emitShape.OnSurface;
						break;

					case EmitShapes.Ring:
						emitter.RadiusMin = emitShape.ringRadius;
						emitter.RadiusMax = emitShape.ringOuterRadius;
						break;

					case EmitShapes.Sphere:
						emitter.RadiusMin = emitShape.radius.IsConstant
							? 0.0f
							: Mathf.Max(Mathf.Min(emitShape.radius.Min, emitShape.radius.Max), 0.0f);
						emitter.RadiusMax = emitShape.radius.Max;
						break;
				}
			} else {
				//Only exist GPU side, don't clutter UI
				emitter.Shape = (uint)EmitShapes.Mesh + 1;
				pes.SetListData(ComputeShader, EmitKernel, m_toEmitList);
				ComputeShader.SetVector("_UseVelSizeColor", m_emitUseVelSizeColor);

				m_toEmitList = null;
			}

			emitter.VelType = (uint) StartDirectionType;
			emitter.RandomAngle = Mathf.Cos(StartDirectionRandomAngle * Mathf.Deg2Rad);

			var localScale = trans.localScale;
			emitter.Scale = localScale;

			UpdateMatrix(emitShape, trans);

			emitter.Pos = prevPos;

			var pos = GetEmitPos(trans);
			Vector3 speed = pos - prevPos;
			emitter.Vel = speed;
			prevPos = pos;
			emitter.Accel = (speed - prevSpeed);
			prevSpeed = speed;

			m_emitSet[0] = emitter;
			m_emitBuffer.SetData(m_emitSet);
		}

		void UpdateMatrix(ParticleEmitterShape emitShape, Transform trans) {
			var localScale = trans.localScale;

			Profiler.BeginSample("Set matrices");
			Matrix4x4 id = Matrix4x4.identity;

			switch (Manager.SimulationSpace) {
				case Space.World:
					var rot = trans.rotation;
					if (m_lastRot != rot || m_lastScale != localScale) {
						m_emitMatrix = Matrix4x4.TRS(Vector3.zero, rot, localScale);
						m_emitRotationMatrix = Matrix4x4.TRS(Vector3.zero, rot, Vector3.one);
						m_lastRot = rot;
						m_lastScale = localScale;
					}

					TCHelper.SetMatrix4(ComputeShader, "emitterMatrix", m_emitMatrix);
					TCHelper.SetMatrix3(ComputeShader, "emitterRotationMatrix", m_emitRotationMatrix);

					break;

				case Space.Local:
					TCHelper.SetMatrix4(ComputeShader, "emitterMatrix", Matrix4x4.TRS(Vector3.zero, Quaternion.identity, localScale));
					TCHelper.SetMatrix3(ComputeShader, "emitterRotationMatrix", id);
					break;

				case Space.LocalWithScale:
					TCHelper.SetMatrix4(ComputeShader, "emitterMatrix", id);
					TCHelper.SetMatrix3(ComputeShader, "emitterRotationMatrix", id);
					break;

				case Space.Parent:
					if (trans.parent != null) {
						var rot2 = trans.localRotation;
						TCHelper.SetMatrix4(ComputeShader, "emitterMatrix", Matrix4x4.TRS(Vector3.zero, rot2, localScale));
						TCHelper.SetMatrix3(ComputeShader, "emitterRotationMatrix", Matrix4x4.TRS(Vector3.zero, rot2, Vector3.one));
					}
					break;
			}

			if (emitShape.startDirectionType == StartDirectionType.Vector) {
				TCHelper.SetMatrix3(ComputeShader, "emitterStartRotationMatrix",
					Matrix4x4.TRS(Vector3.zero, Quaternion.FromToRotation(Vector3.forward, StartDirectionVector), Vector3.one));
			}

			Profiler.EndSample();
		}

		public Vector3 GetEmitPos(Transform trans) {
			Vector3 pos = Vector3.zero;

			switch (Manager.SimulationSpace) {
				case Space.World:
					pos = trans.position;
					break;

				case Space.Local:
					pos = trans.position - Transform.position;
					break;

				case Space.Parent:
					pos = trans.localPosition;
					break;
			}

			return pos;
		}


		protected override void Bind() {
			ComputeShader.SetTexture(UpdateAllKernel, "lifetimeTexture", m_lifetimeTexture);
			ComputeShader.SetBuffer(EmitKernel, "emitter", m_emitBuffer);

			ComputeShader.SetVector("_LifeMinMax", new Vector4(Energy.Min, Energy.Max));

			float t = Manager.SystemTime / Manager.Duration;
			Energy.t = t;
			Size.t = t;
			Speed.t = t;
			Rotation.t = t;

			var emitter = m_emitSet[0];

			emitter.SizeMax = Size.Max;
			emitter.SizeMin = Size.Min;
			emitter.SpeedMax = Speed.Max;
			emitter.SpeedMin = Speed.Min;
			emitter.RotationMax = Rotation.Max * Mathf.Deg2Rad;
			emitter.RotationMin = Rotation.Min * Mathf.Deg2Rad;
			emitter.StartSpeed = m_velocity * InheritVelocity;
			emitter.MassVariance = ForceManager.MassVariance;
			emitter.Time = (uint)Random.Range(0, Manager.MaxParticlesBuffer);
			m_emitSet[0] = emitter;

			SetShapeData(pes, Transform, ref m_emitPrevPos, ref m_emitPrevSpeed);
		}

		public void Update() {
			if (Manager.ParticleTimeDelta > 0.0f) {
				m_velocity = (Transform.position - m_prevPosition) / Manager.ParticleTimeDelta;
				m_prevPosition = Transform.position;
			}
		}

		public void UpdateSpace() {
			m_emitPrevPos = GetEmitPos(Transform);
			m_emitPrevSpeed = Vector3.zero;
			m_prevPosition = Transform.position;
		}

		public void PlayEvent() {
			m_prevTime = -1.0f;
		}

		public void UpdateParticleBursts() {
			var realTime = Manager.RealTime;


			while (m_burstsDone.Count > 0) {
				var nextTime = m_burstsDone.Peek();

				if (realTime - nextTime.time > nextTime.life) {
					Manager.Offset += nextTime.amount;
					Manager.NumParticles -= nextTime.amount;
					m_burstsDone.Dequeue();
				}
				else {
					break;
				}
			}

			Manager.Offset %= Manager.MaxParticles;
		}

		public void UpdatePlayEvent() {
			Profiler.BeginSample("Update play event");



			if (emit) {
				if (m_emissionType == EmissionTypeEnum.PerSecond) {
					m_femit += Manager.ParticleTimeDelta * EmissionRate;
				}
				else {
					Vector3 pos = GetEmitPos(Transform);
					Vector3 delta = pos - m_emitPrevPos;
					m_femit += delta.magnitude * EmissionRate;
				}

				for (int i = 0; i < bursts.Count; i++) {
					var b = bursts[i];

					if (Manager.SystemTime >= b.time && m_prevTime < b.time) {
						m_femit += b.amount;
					}
				}

				int num = Mathf.FloorToInt(m_femit);
				m_femit -= num;

				Emit(num);
				m_prevTime = Manager.SystemTime;
			}

			if (m_shapeEmitters != null && DoEmit) {
				for (int i = 0; i < m_shapeEmitters.Count; i++) {
					var tcShapeEmitter = m_shapeEmitters[i];

					if (!tcShapeEmitter.Emit || !tcShapeEmitter.enabled) {
						continue;
					}

					int num = tcShapeEmitter.TickEmission(this, Manager.ParticleTimeDelta);
					if (num == 0) {
						continue;
					}

					//pdate shape
					SetShapeData(tcShapeEmitter.ShapeData, tcShapeEmitter.transform, ref tcShapeEmitter.PrevPos, ref tcShapeEmitter.PrevSpeed);

					//Set local data
					EmitAfterSet(num);
				}
			}

			Profiler.EndSample();
		}

		public void ClearParticles() {
			Manager.Clear();
		}

		public void ClearAllEmittedParticles() {
			//Manager mas been cleared, meaning buffer is completely free again
			if (!Supported) {
				if (Manager.shurikenFallback != null) {
					Manager.shurikenFallback.Clear();
				}

				return;
			}

			Manager.Offset = 0;
			m_burstsDone.Clear();


			//TODO: Only need to set offsets and such, optimise away more of these SetParticles
			//If set particles is only called in one place we can remove some other silly Set == Manager tracking BS
			SetParticles();

			Manager.SetPariclesToKernel(ComputeShader, ClearKernel);
			ComputeShader.Dispatch(ClearKernel, Mathf.CeilToInt(Manager.ParticleCount / GroupSize), 1, 1);

			Manager.NumParticles = 0;
		}

		public void Simulate(float time) {
			Manager.Simulate(time, false);
		}

		ParticleProto[] m_toEmitList;
		Vector4 m_emitUseVelSizeColor;

		public void Emit(List<Vector3> positions) {
			Emit(positions.Select(pos => new ParticleProto { Position = pos }).ToArray(), false, false, false);
		}

		public void Emit(Vector3[] positions) {
			Emit(positions.Select(pos => new ParticleProto { Position = pos}).ToArray(), false, false, false);
		}

		public void Emit(ParticleProto[] prototypes, bool useColor = true, bool useSize = true, bool useVelocity = true) {
			if (prototypes == null || prototypes.Length == 0) {
				return;
			}

			m_toEmitList = prototypes;
			m_emitUseVelSizeColor = new Vector4(useVelocity ? 1 : 0, useSize ? 1 : 0, useColor ? 1 : 0);

			Emit(prototypes.Length);
		}
		

		//Emits without doing any kind of buffer setting first
		public void Emit(int count) {
			if (count <= 0 && (m_shapeEmitters == null || m_shapeEmitters.Count == 0)) {
				return;
			}

			SetParticles();
			EmitAfterSet(count);
		}

		void EmitAfterSet(int count) {
			Profiler.BeginSample("Emit now");
			//If the particles actually die at some point, track them
			if (Energy.Max > 0 && !Manager.NoSimulation) {
				var b = new Burst {amount = count, time = Manager.RealTime, life = Energy.Max};
				m_burstsDone.Enqueue(b);
			}

			const int emitGroupSize = 32;

			Manager.NumParticles += count;
			int amount = Mathf.CeilToInt((float)count / emitGroupSize);
			ComputeShader.SetInt("numToGo", count);

			Manager.SetPariclesToKernel(ComputeShader, EmitKernel);
			ComputeShader.Dispatch(EmitKernel, amount, 1, 1);
			Profiler.EndSample();
		}

		public override void OnDestroy() {
			Release(ref m_emitBuffer);
			pes.ReleaseData();
			Object.DestroyImmediate(m_lifetimeTexture);
		}
	}
}