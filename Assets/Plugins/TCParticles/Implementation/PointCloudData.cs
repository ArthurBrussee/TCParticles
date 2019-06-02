using UnityEngine;

namespace TC {
	// A container class optimized for compute buffer
	public sealed class PointCloudData : ScriptableObject {
		//TODO: Can we store this in a big binary blob and LZ4 that?

		// Number of points
		public int PointCount => _positions.Length;

		public Vector3[] Vertices => _positions;
		public Vector3[] Normals => _normals;
		public Color32[] Colors => _colors;

		[SerializeField] Vector3[] _positions;
		[SerializeField] Vector3[] _normals;
		[SerializeField] Color32[] _colors;

		//TODO: Serialize this array instead? How big is this really?
		public ParticleProto[] GetPrototypes(int count) {
			var ret = new ParticleProto[count];

			for (int i = 0; i < count; ++i) {
				ret[i].Position = _positions[i];
				ret[i].Color = _colors[i];
			}

			return ret;
		}

		public void Initialize(Vector3[] positions, Vector3[] normals, Color32[] colors, float scale, Vector3 pivot, Vector3 normalRotate) {
			_positions = positions;
			_normals = normals;

			if (!Mathf.Approximately(scale, 1.0f)) {
				for (int i = 0; i < _positions.Length; ++i) {
					_positions[i] *= scale;
				}
			}

			if (!Mathf.Approximately(pivot.x, 0.0f) || !Mathf.Approximately(pivot.y, 0.0f) || !Mathf.Approximately(pivot.z, 0.0f)) {
				for (int i = 0; i < _positions.Length; ++i) {
					_positions[i].x += pivot.x;
					_positions[i].y += pivot.y;
					_positions[i].z += pivot.z;
				}
			}

			if (_normals != null) {
				Quaternion normalRotation = Quaternion.Euler(normalRotate);

				// Rotate and normalize normals
				for (int i = 0; i < _normals.Length; ++i) {
					_normals[i] = normalRotation * _normals[i].normalized;
				}
			}

			_colors = colors;
		}
	}
}