using UnityEngine;

namespace TC {
	// A container class optimized for compute buffer
	public sealed class PointCloudData : ScriptableObject {
		//TODO: Can we store this in a big binary blob and LZ4 that?

		// Number of points
		public int PointCount {
			get { return _positions.Length; }
		}

		[SerializeField] Vector3[] _positions;
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

		public void Initialize(Vector3[] positions, Color32[] colors, float scale, Vector3 pivot) {
			_positions = positions;

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

			_colors = colors;
		}
	}
}