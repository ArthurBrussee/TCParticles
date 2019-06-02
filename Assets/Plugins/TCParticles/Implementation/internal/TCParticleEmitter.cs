using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TC.Internal;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace TC.Internal {
	[SuppressMessage("ReSharper", "NotAccessedField.Global")]
	public struct ParticleEmitterData {
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
		public uint Time;
		public Vector3 Scale;
		public uint OnSurface;
	}
}

namespace TC {
	///<summary>
	/// Class that handles emitting of particles, streaming, and emission properties.
	/// </summary>
	[Serializable]
	public class ParticleEmitter : ParticleComponent {
		[SerializeField] ParticleEmitterShape pes = new ParticleEmitterShape();

		/// <summary>
		/// The current shape of the emitter - read only
		/// </summary>
		public EmitShapes Shape {
			get => pes.shape;
			set => pes.shape = value;
		}

		/// <summary>
		/// Radius of the emitting sphere used when <see cref="Shape"/> is <see cref="EmitShapes.HemiSphere" />
		/// </summary>
		public MinMax Radius => pes.radius;

		/// <summary>
		/// Cube size of the emitting cube when <see cref="Shape"/> is <see cref="EmitShapes.Box" />
		/// </summary>
		public Vector3 CubeSize {
			get => pes.cubeSize;
			set => pes.cubeSize = value;
		}

		/// <summary>
		/// The mesh shape of the emitter
		/// </summary>
		public Mesh EmitMesh {
			get => pes.emitMesh;
			set => pes.emitMesh = value;
		}

		/// <summary>
		/// Updates cached GPU data for the given mesh
		/// </summary>
		public void UpdateCacheForEmitMesh() {
			TCParticleGlobalManager.UpdateCacheForEmitMesh(EmitMesh, pes.uvChannel);
		}

		/// <summary>
		/// Releases cached GPU data for the given mesh
		/// </summary>
		public void ReleaseCacheForEmitMesh() {
			TCParticleGlobalManager.ReleaseCacheForEmitMesh(EmitMesh, pes.uvChannel);
		}

		/// <summary>
		/// The mesh shape of the emitter
		/// </summary>
		public Texture MeshTexture {
			get => pes.texture;
			set => pes.texture = value;
		}

		/// <summary>
		/// Angle of the cone when <see cref="Shape"/> is <see cref="EmitShapes.Cone" />
		/// </summary>
		public float ConeAngle {
			get => pes.coneAngle;
			set => pes.coneAngle = value;
		}

		/// <summary>
		/// Height of the cone when <see cref="Shape"/> is <see cref="EmitShapes.Cone" />
		/// </summary>
		public float ConeHeight {
			get => pes.coneHeight;
			set => pes.coneHeight = value;
		}

		/// <summary>
		/// Radius of the cone when <see cref="Shape"/> is <see cref="EmitShapes.Cone" />
		/// </summary>
		public float ConeRadius {
			get => pes.coneRadius;
			set => pes.coneRadius = value;
		}

		/// <summary>
		/// Outer radius of the ring when <see cref="Shape"/> is <see cref="EmitShapes.Ring" />
		/// </summary>
		public float RingOuterRadius {
			get => pes.ringOuterRadius;
			set => pes.ringOuterRadius = value;
		}

		/// <summary>
		/// Radius of the ring when <see cref="Shape"/> is <see cref="EmitShapes.Ring" />
		/// </summary>
		public float RingRadius {
			get => pes.ringRadius;
			set => pes.ringRadius = value;
		}

		/// <summary>
		/// The length of the line particles emit from when <see cref="Shape"/> is <see cref="EmitShapes.Line" />
		/// </summary>
		public float LineLength {
			get => pes.lineLength;
			set => pes.lineLength = value;
		}

		[SerializeField] MinMaxRandom _speed = MinMaxRandom.Constant(3.0f);

		/// <summary>
		/// Start speed of a particle in the particle's start direction
		/// </summary>
		public MinMaxRandom Speed => _speed;

		[SerializeField] MinMaxRandom _energy = MinMaxRandom.Constant(3.0f);

		/// <summary>
		/// Time a particle can be alive. Measured in seconds
		/// </summary>
		[Obsolete("Obsolete: Use Lifetime instead.")]
		public MinMaxRandom Energy => _energy;

		/// <summary>
		/// Time a particle can be alive. Measured in seconds
		/// </summary>
		public MinMaxRandom Lifetime {
			get => _energy;
			set => _energy = value;
		}

		[SerializeField] MinMaxRandom _size = MinMaxRandom.Constant(0.5f);

		/// <summary>
		/// Starting size of the particle
		/// </summary>
		public MinMaxRandom Size {
			get => _size;
			set => _size = value;
		}

		[SerializeField] MinMaxRandom _rotation = MinMaxRandom.Constant(0.0f);

		/// <summary>
		/// Start rotation of a particle
		/// </summary>
		public MinMaxRandom Rotation => _rotation;

		[SerializeField] float _angularVelocity;

		/// <summary>
		/// Angular velocity of a particle (degrees per second)
		/// </summary>
		public float AngularVelocity {
			get => _angularVelocity;
			set => _angularVelocity = value;
		}

		[SerializeField] Vector3Curve _constantForce = Vector3Curve.Zero();

		/// <summary>
		/// Constant force on the particle system. The force is in world space
		/// </summary>
		public Vector3Curve ConstantForce {
			get => _constantForce;
			set => _constantForce = value;
		}

		[SerializeField] float _emissionRate = 100.0f;

		/// <summary>
		/// Number of particles emitted per seond or per unit depending on the <see cref="EmissionType"/>
		/// </summary>
		public float EmissionRate {
			get => _emissionRate;
			set => _emissionRate = value;
		}

		/// <summary>
		/// Method of emission, per second or per time
		/// </summary>
		public enum EmissionMethod {
			/// <summary>
			/// Emit set number of particles per second
			/// </summary>
			PerSecond,

			/// <summary>
			/// Emit set number of particles per unit
			/// </summary>
			PerUnit
		}

		[SerializeField] EmissionMethod m_emissionType;

		/// <summary>
		/// Method of emission (per second or per unit)
		/// </summary>
		public EmissionMethod EmissionType {
			get => m_emissionType;
			set => m_emissionType = value;
		}

		[SerializeField] AnimationCurve _sizeOverLifetime = AnimationCurve.Linear(0.0f, 1.0f, 1.0f, 1.0f);

		/// <summary>
		/// Size over lifetime curve. Call UpdateSizeOverLifetime when changed. Clamped between [0-1]
		/// </summary>
		public AnimationCurve SizeOverLifetime {
			get => _sizeOverLifetime;
			set {
				_sizeOverLifetime = value;
				UpdateSizeOverLifetime();
			}
		}

		/// <summary>
		/// The first value of size over lifetime
		/// </summary>
		public float StartSizeMultiplier {
			get => _sizeOverLifetime.Evaluate(0.0f);

			set {
				_sizeOverLifetime.MoveKey(0, new Keyframe(0.0f, value));
				UpdateSizeOverLifetime();
			}
		}

		[SerializeField]
		Vector3Curve _velocityOverLifetime = Vector3Curve.Zero();

		/// <summary>
		/// Velocity over lifetime. Call <see cref="UpdateVelocityOverLifetime"/> when changed. 
		/// </summary>
		public Vector3Curve VelocityOverLifetime {
			get => _velocityOverLifetime;
			set {
				_velocityOverLifetime = value;
				UpdateVelocityOverLifetime();
			}
		}

		Texture2D m_lifetimeTexture;

		[SerializeField] bool emit = true;

		/// <summary>
		/// Should particles be emitted currently?
		/// </summary>
		public bool DoEmit {
			get => emit;
			set => emit = value;
		}

		/// <summary>
		/// The colour of the particles when they are emitted
		/// </summary>
		public Color StartColor => Renderer.ColorOverLifetime.Evaluate(0.0f);

		[SerializeField, Range(0.0f, 1.0f)] float _inheritVelocity;

		/// <summary>
		/// How much of the emitter's velocity should be inherited
		/// </summary>
		public float InheritVelocity {
			get => _inheritVelocity;
			set => _inheritVelocity = value;
		}

		/// <summary>
		/// How should the particle choose direction it should start in 
		/// </summary>
		public StartDirection StartDirectionType {
			get => pes.startDirectionType;
			set => pes.startDirectionType = value;
		}

		/// <summary>
		/// starting direciton of particles when <see cref="StartDirectionType"/> is <see cref="StartDirection.Vector"/>
		/// </summary>
		public Vector3 StartDirectionVector {
			get => pes.startDirectionVector;
			set => pes.startDirectionVector = value;
		}

		///<summay>
		/// Random angle of the starting direction (applies to <see cref="StartDirection.Vector"/> and <see cref="StartDirection.Normal"/>)
		/// </summay>
		public float StartDirectionRandomAngle {
			get => pes.startDirectionRandomAngle;
			set => pes.startDirectionRandomAngle = value;
		}

		public EmitShapes EmitShape {
			get => pes.shape;
			set => pes.shape = value;
		}

		/// <summary>
		/// Holds all data about the curret emission shape
		/// </summary>
		public ParticleEmitterShape ShapeData => pes;

		/// <summary>
		/// The current baked texture used for size over lifetime
		/// RGB = velocity. Alpha = size
		/// </summary>
		public Texture2D LifetimeTexture => m_lifetimeTexture;

		bool m_doSizeOverLifetime;

		/// <summary>
		/// Is size over lifetime used
		/// </summary>
		public bool DoSizeOverLifetime => m_doSizeOverLifetime;

		/// <summary>
		/// Current (approximate) particle count
		/// </summary>
		/// <remarks>
		/// This is only approximate as it is not known exactly when particles die off. All particles are assumed to live the maximum set lifetime
		/// </remarks>
		public int ParticleCount { get; private set; }

		/// <summary>
		/// Current offset in ring buffer. 
		/// </summary>
		/// <remarks>
		/// Only to be used in advanced use cases for the extension API.
		/// Usually you should use the <see cref="ParticleManager.DispatchExtensionKernel"/> directly
		/// </remarks>
		public int Offset { get; private set; }

		[SerializeField] TCShapeEmitTag m_emitTag;
		[SerializeField] List<Burst> bursts = new List<Burst>();

		public void SetBursts(BurstEmission[] setBursts) {
			bursts = setBursts.Select(b => new Burst {time = b.Time, amount = b.Amount}).ToList();
		}

		/// <summary>
		/// The emission tag used to link this emitter to the right shape emitters in the scene
		/// </summary>
		public TCShapeEmitTag Tag {
			get => m_emitTag;
			set => m_emitTag = value;
		}

		public PointCloudData PointCloud {
			get => pes.pointCloud;
			set => pes.pointCloud = value;
		}

		//Bursts sequences
		[Serializable]
		public class Burst {
			public int amount;
			public float time;
			public float life;
		}

		readonly Queue<Burst> m_burstsDone = new Queue<Burst>(100);

		struct BindSettings {
			public int Offset;
			public int Count;
		}

		BindSettings m_currentEmitBind;

		ParticleEmitterData[] m_emitSet;

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
		ComputeBuffer m_dummyBuffer;

		float m_femit;

		const int c_texDim = 128;
		static readonly Color[] Colors = new Color[c_texDim];

		[EditorBrowsable(EditorBrowsableState.Never)]
		protected override void Initialize() {
			UpdateLifetimeTexture();

			m_emitBuffer = new ComputeBuffer(1, SizeOf<ParticleEmitterData>());
			m_emitSet = new ParticleEmitterData[1];
			m_emitPrevPos = GetEmitPos(SystemComp, SystemComp.transform);
			m_emitPrevSpeed = Vector3.zero;
			m_dummyBuffer = new ComputeBuffer(1, 12);
		}

		internal override void OnEnable() {
			m_emitPrevPos = GetEmitPos(SystemComp, SystemComp.transform);
			m_emitPrevSpeed = Vector3.zero;
		}

		void UpdateLifetimeTexture() {
			if (m_lifetimeTexture == null) {
				m_lifetimeTexture = new Texture2D(c_texDim, 1, TextureFormat.RGBAHalf, false, true) {
					wrapMode = TextureWrapMode.Clamp,
					anisoLevel = 0
				};
			}

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

		/// <summary>
		/// Update size over lifetime, get's called automatically in most cases but might be neccesary for custom scripting
		/// </summary>
		public void UpdateSizeOverLifetime() {
			UpdateLifetimeTexture();
		}

		void UpdateVelocityOverLifetime() {
			UpdateLifetimeTexture();
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

					ComputeShader.SetMatrix(SID._EmitterMatrix, m_emitMatrix);
					ComputeShader.SetMatrix(SID._EmitterRotationMatrix, m_emitRotationMatrix);

					break;

				case Space.Local:
					ComputeShader.SetMatrix(SID._EmitterMatrix, Matrix4x4.TRS(Vector3.zero, Quaternion.identity, localScale));
					ComputeShader.SetMatrix(SID._EmitterRotationMatrix, id);
					break;

				case Space.LocalWithScale:
					Matrix4x4 mat = id;

					if (emitShape != pes) {
						mat = SystemComp.transform.worldToLocalMatrix * Matrix4x4.TRS(trans.position, Quaternion.identity, Vector3.one) * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, localScale);
					}

					ComputeShader.SetMatrix(SID._EmitterMatrix, mat);
					ComputeShader.SetMatrix(SID._EmitterRotationMatrix, id);
					break;

				case Space.Parent:
					if (trans.parent != null) {
						var rot2 = trans.localRotation;
						ComputeShader.SetMatrix(SID._EmitterMatrix, Matrix4x4.TRS(Vector3.zero, rot2, localScale));
						ComputeShader.SetMatrix(SID._EmitterRotationMatrix, Matrix4x4.TRS(Vector3.zero, rot2, Vector3.one));
					}

					break;
			}

			if (emitShape.startDirectionType == StartDirection.Vector) {
				ComputeShader.SetMatrix(SID._EmitterStartRotationMatrix,
					Matrix4x4.TRS(Vector3.zero, Quaternion.FromToRotation(Vector3.forward, emitShape.startDirectionVector), Vector3.one));
			}

			Profiler.EndSample();
		}

		internal static Vector3 GetEmitPos(TCParticleSystem system, Transform trans) {
			Vector3 pos = Vector3.zero;

			switch (system.Manager.SimulationSpace) {
				case Space.World:
					pos = trans.position;
					break;

				case Space.Local:
					pos = trans.position - system.transform.position;
					break;

				case Space.Parent:
					pos = trans.localPosition;
					break;
			}

			return pos;
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		public delegate void OnParticleEvent(ComputeShader shader, int kern);

		/// <summary>
		/// Callback when emission binds it's variables, can be used to bind custom emission variables at that time
		/// </summary>
		public event OnParticleEvent OnEmissionBind;

		[EditorBrowsable(EditorBrowsableState.Never)]
		protected override void Bind() {
			ComputeShader.SetTexture(UpdateAllKernel, SID._LifetimeTexture, m_lifetimeTexture);
			ComputeShader.SetVector(SID._LifeMinMax, new Vector4(Lifetime.Min, Lifetime.Max));

			float t = Manager.SystemTime / Manager.Duration;
			Lifetime.t = t;
			Size.t = t;
			Speed.t = t;
			Rotation.t = t;
		}

		internal void Update() {
			if (Manager.ParticleTimeDelta > 0.0f) {
				var position = SystemComp.transform.position;
				m_velocity = (position - m_prevPosition) / Manager.ParticleTimeDelta;
				m_prevPosition = position;
			}
		}

		internal void UpdateSpace() {
			m_emitPrevPos = GetEmitPos(SystemComp, SystemComp.transform);
			m_emitPrevSpeed = Vector3.zero;
			m_prevPosition = SystemComp.transform.position;
		}

		internal void UpdateForDispatch() {
			var realTime = Manager.RealTime;

			while (m_burstsDone.Count > 0) {
				var nextTime = m_burstsDone.Peek();

				if (realTime - nextTime.time > nextTime.life) {
					Offset += nextTime.amount;
					ParticleCount -= nextTime.amount;
					m_burstsDone.Dequeue();
				} else {
					break;
				}
			}

			Offset %= Manager.MaxParticles;
		}

		internal void UpdatePlayEvent(float prevSystemTime) {
			Profiler.BeginSample("Update play event");

			if (emit) {
				if (m_emissionType == EmissionMethod.PerSecond) {
					m_femit += Manager.ParticleTimeDelta * EmissionRate;
				} else {
					Vector3 pos = GetEmitPos(SystemComp, SystemComp.transform);
					Vector3 delta = pos - m_emitPrevPos;
					m_femit += delta.magnitude * EmissionRate;
				}

				for (int i = 0; i < bursts.Count; i++) {
					var b = bursts[i];

					if (Manager.SystemTime > b.time && prevSystemTime <= b.time) {
						m_femit += b.amount;
					}
				}

				int num = Mathf.FloorToInt(m_femit);
				m_femit -= num;
				Emit(num);
			}

			if (DoEmit && m_emitTag != null) {
				for (int i = 0; i < Tracker<TCShapeEmitter>.Count; i++) {
					var emitter = Tracker<TCShapeEmitter>.All[i];

					if (!emitter.Emit) {
						continue;
					}

					if (!emitter.LinksToTag(m_emitTag)) {
						continue;
					}

					int count = emitter.TickEmission(SystemComp);

					if (count <= 0) {
						continue;
					}

					//Set local data
					EmitSetInternal(count, emitter.ShapeData, emitter.transform, ref emitter.PrevPos, ref emitter.PrevSpeed);
				}
			}

			Profiler.EndSample();
		}

		/// <summary>
		/// Clears all particles in the current particle system
		/// </summary>
		public void ClearAllEmittedParticles() {
			//Manager mas been cleared, meaning buffer is completely free again
			Offset = 0;
			m_burstsDone.Clear();

			//TODO: Only need to set offsets and such, optimise away more of these SetParticles
			//If set particles is only called in one place we can remove some other silly Set == Manager tracking BS
			Manager.BindPariclesToKernel(ComputeShader, ClearKernel);

			if (Manager.DispatchCount > 0) {
				ComputeShader.Dispatch(ClearKernel, Manager.DispatchCount, 1, 1);
			}

			ParticleCount = 0;
		}

		/// <summary>
		/// Emit a given amount of particles with some initial starting positions
		/// </summary>
		/// <param name="positions">Starting positions of particles</param>
		public void Emit(Vector3[] positions) {
			Emit(positions.Select(pos => new ParticleProto {Position = pos}).ToArray(), false, false, false);
		}

		/// <summary>
		/// Emit a given amount of particles with some initial settings
		/// </summary>
		/// <param name="prototypes">List of particle prototypes</param>
		/// <param name="useColor">Whether the color of the prototypes should be applied</param>
		/// <param name="useSize">Whether the size of the prototypes should be applied</param>
		/// <param name="useVelocity">Whether the velocity of the prototypes should be applied</param>
		/// <param name="usePosition">Wether the position of the prototypes should be applied</param>
		public void Emit(ParticleProto[] prototypes, bool useColor = true, bool useSize = true, bool useVelocity = true, bool usePosition = true) {
			Emit(prototypes, prototypes.Length, useColor, useSize, useVelocity, usePosition);
		}

		/// <summary>
		/// Emit a given amount of particles with some initial settings
		/// </summary>
		/// <param name="prototypes">List of particle prototypes</param>
		/// <param name="count">Number of particles to emit taken from the prototype array</param>
		/// <param name="useColor">Whether the color of the prototypes should be used</param>
		/// <param name="useSize">Whether the size of the prototypes should be used</param>
		/// <param name="useVelocity">Whether the velocity of the prototypes should be used</param>
		public void Emit(ParticleProto[] prototypes, int count, bool useColor = true, bool useSize = true, bool useVelocity = true, bool usePosition = true) {
			if (prototypes == null || prototypes.Length == 0) {
				return;
			}

			pes.SetPrototypeEmission(prototypes, count, useColor, useSize, useVelocity, usePosition);
			Emit(count);
		}

		/// <summary>
		/// Emit a given amount of particles using the current setting of the particle emitter
		/// </summary>
		/// <param name="count">Number of particles to emit</param>
		public void Emit(int count) {
			if (count <= 0) {
				return;
			}

			BindParticles();

			EmitSetInternal(count, pes, SystemComp.transform, ref m_emitPrevPos, ref m_emitPrevSpeed);
		}

		void EmitSetInternal(int count, ParticleEmitterShape emitShape, Transform trans, ref Vector3 prevPos, ref Vector3 prevSpeed) {
			Profiler.BeginSample("Emit bind");

			ComputeShader.SetBuffer(EmitKernel, SID._Emitter, m_emitBuffer);

			var emitter = m_emitSet[0];
			emitter.SizeMax = Size.Max;
			emitter.SizeMin = Size.Min;
			emitter.SpeedMax = Speed.Max;
			emitter.SpeedMin = Speed.Min;
			emitter.RotationMax = Rotation.Max * Mathf.Deg2Rad;
			emitter.RotationMin = Rotation.Min * Mathf.Deg2Rad;
			emitter.StartSpeed = m_velocity * InheritVelocity;
			emitter.Time = (uint) Random.Range(0, Manager.MaxParticles);
			emitter.Shape = (uint) emitShape.shape;

			//Bind default
			ComputeShader.SetTexture(EmitKernel, "_MeshTexture", Texture2D.whiteTexture);
			ComputeShader.SetBuffer(EmitKernel, "emitFaces", m_dummyBuffer);

			switch (emitShape.shape) {
				case EmitShapes.Sphere:
					emitter.RadiusMin = emitShape.radius.IsConstant
						? 0.0f
						: Mathf.Max(Mathf.Min(emitShape.radius.Min, emitShape.radius.Max), 0.0f);
					emitter.RadiusMax = emitShape.radius.Max;
					break;

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

				case EmitShapes.Ring:
					emitter.RadiusMin = emitShape.ringRadius;
					emitter.RadiusMax = emitShape.ringOuterRadius;
					break;

				case EmitShapes.Mesh:
					emitShape.SetMeshData(ComputeShader, EmitKernel, ref emitter);
					break;

				case EmitShapes.PointCloud:
					emitShape.SetPointCloudData(ComputeShader, EmitKernel, count, ref emitter);
					break;
			}

			pes.UpdateListData(ComputeShader, EmitKernel);
			emitter.VelType = (uint) emitShape.startDirectionType;
			emitter.RandomAngle = Mathf.Cos(emitShape.startDirectionRandomAngle * Mathf.Deg2Rad);

			var localScale = trans.localScale;
			emitter.Scale = localScale;
			UpdateMatrix(emitShape, trans);

			emitter.Pos = prevPos;

			var pos = GetEmitPos(SystemComp, trans);

			Vector3 speed = pos - prevPos;
			emitter.Vel = speed;
			prevPos = pos;
			emitter.Accel = speed - prevSpeed;
			prevSpeed = speed;

			m_emitSet[0] = emitter;
			m_emitBuffer.SetData(m_emitSet);

			if (OnEmissionBind != null) {
				OnEmissionBind(ComputeShader, EmitKernel);
			}

			Profiler.EndSample();

			Profiler.BeginSample("Emit now");
			//If the particles actually die at some point, track them
			if (Lifetime.Max > 0 && !Manager.NoSimulation) {
				var b = new Burst {amount = count, time = Manager.RealTime, life = Lifetime.Max};
				m_burstsDone.Enqueue(b);
			}

			int emitOffset = (Offset + ParticleCount) % Manager.MaxParticles;

			m_currentEmitBind.Offset = emitOffset;
			m_currentEmitBind.Count = count;

			DispatchEmitExtensionKernel(ComputeShader, EmitKernel);
			ParticleCount += count;

			if (OnEmissionCallback != null) {
				OnEmissionCallback(count);
			}

			Profiler.EndSample();
		}

		public delegate void EmissionCallbackCB(int emittedCount);

		public EmissionCallbackCB OnEmissionCallback;

		/// <summary>
		/// Launch a compute shader kernel with a thread for each newly emitted particle. Should be used in <see cref="OnEmissionCallback"/>
		/// </summary>
		/// <param name="cs">The compute shader to dispatch</param>
		/// <param name="kernel">the kernel to dispatch in the compute shader</param>
		/// <remarks>
		/// The extension compute shader must adhere to certain guidelines:
		/// 
		/// 1. Have a groupsize of (TC_GROUP_SIZE, 1, 1)
		/// 2. Include TCFramework.cginc
		/// 3. To get a particle use particles[GetID(dispatchThreadID.x)]
		/// 4. Use the "_DeltTime" variable for calculations involving delta time
		/// </remarks>
		public void DispatchEmitExtensionKernel(ComputeShader cs, int kernel) {
			uint x, y, z;
			cs.GetKernelThreadGroupSizes(kernel, out x, out y, out z);

			int dispatch = Mathf.CeilToInt(m_currentEmitBind.Count / (float) x);

			Manager.BindPariclesToKernel(cs, EmitKernel);
			cs.SetInt(SID._BufferOffset, m_currentEmitBind.Offset);
			cs.SetInt(SID._ParticleEmitCount, m_currentEmitBind.Count);

			cs.Dispatch(kernel, dispatch, 1, 1);
		}

		internal virtual void OnDestroy() {
			Release(ref m_emitBuffer);

			if (m_dummyBuffer != null) {
				m_dummyBuffer.Dispose();
			}

			pes.ReleaseData();
			Object.DestroyImmediate(m_lifetimeTexture);
		}
	}
}