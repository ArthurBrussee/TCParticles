using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TC;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

//Pcl can be in big endian format
class PclBinaryReader {
	byte[] m_bytes;
	int m_position;

	byte[] m_scratchRead = new byte[4];

	public PclBinaryReader(byte[] bytes) {
		m_bytes = bytes;
		m_position = 0;
	}

	public byte ReadByte() {
		return m_bytes[m_position++];
	}

	public float ReadSingle() {
		m_scratchRead[3] = ReadByte();
		m_scratchRead[2] = ReadByte();
		m_scratchRead[1] = ReadByte();
		m_scratchRead[0] = ReadByte();
		return BitConverter.ToSingle(m_scratchRead, 0);
	}

	char[] m_readChars = new char[512];

	public byte ReadAsciiByte() {
		int charsRead = 0;

		while (true) {
			char c = ReadChar();

			if (c == ' ') {
				return byte.Parse(new string(m_readChars, 0, charsRead));
			}
			m_readChars[charsRead++] = c;
		}
	}

	public char ReadChar() {
		return (char)ReadByte();
	}

	public float ReadAsciiFloat() {
		int charsRead = 0;

		while (true) {
			char c = ReadChar();

			if (c == ' ') {
				return float.Parse(new string(m_readChars, 0, charsRead));
			}
			m_readChars[charsRead++] = c;
		}
	}
}

//NOTE: You have to rename your PCL files to .txt so untity picks them up!
public class PointCloudSpawner : MonoBehaviour {
	public string StreamPclPath;

	public float Scale = 0.01f;
	public bool UseRainbow;

	enum PropType {
		Float,
		Byte
	}

	void Awake() {
		SpawnPointCloud();
	}

	void SpawnPointCloud() {
		Stopwatch sw = new Stopwatch();

		sw.Start();

		Profiler.BeginSample("Setup");
		var bytes = File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, StreamPclPath));

		var binReader = new PclBinaryReader(bytes);

		StringBuilder curStr = new StringBuilder();
		int pointCount = 0;

		List<PropType> m_types = new List<PropType>();

		bool inVertElement = false;
		bool binary = false;
		Profiler.EndSample();

		Profiler.BeginSample("Read headers");

		while (true) {
			char cur = binReader.ReadChar();

			if (cur == '\n') {
				string line = curStr.ToString();

				if (line == "end_header") {
					break;
				}

				var lines = line.Split(' ');

				if (lines[0] == "format") {
					binary = lines[1] == "binary_big_endian";
				}
				else if (lines[0] == "element") {
					if (lines[1] == "vertex") {
						pointCount = int.Parse(lines[2]);
						inVertElement = true;
					}
					else {
						inVertElement = false;
					}
				}
				else if (inVertElement && lines[0] == "property" && lines[1] != "list") {
					if (lines[1] == "float" || lines[1] == "float32") {
						m_types.Add(PropType.Float);
					}

					if (lines[1] == "uchar" || lines[1] == "uint8") {
						m_types.Add(PropType.Byte);
					}
				}

				curStr.Length = 0;
				curStr.Capacity = 0;
			}
			else {
				curStr.Append(cur);
			}
		}
		Profiler.EndSample();

		Profiler.BeginSample("Parse points");
		var points = new ParticleProto[pointCount];

		bool hasColor = m_types.Count(c => c == PropType.Byte) >= 3;
		int typeCount = m_types.Count;

		float[] parseFloats = new float[typeCount];
		byte[] parseBytes = new byte[typeCount];

		int floatsParsed = 0;
		int bytesParsed = 0;

		for (int i = 0; i < pointCount; ++i) {
			floatsParsed = 0;
			bytesParsed = 0;

			for (var prop = 0; prop < typeCount; prop++) {
				var type = m_types[prop];

				if (type == PropType.Float) {
					float val = binary ? binReader.ReadSingle() : binReader.ReadAsciiFloat();
					parseFloats[floatsParsed++] = val;
				}
				else {
					byte val = binary ? binReader.ReadByte() : binReader.ReadAsciiByte();
					parseBytes[bytesParsed++] = val;
				}
			}

			float x = parseFloats[0] * Scale;
			float y = parseFloats[1] * Scale;
			float z = parseFloats[2] * Scale;

			if (hasColor && !UseRainbow) {
				byte r = parseBytes[0];
				byte g = parseBytes[1];
				byte b = parseBytes[2];

				points[i].Color.r = r / 255.0f;
				points[i].Color.g = g / 255.0f;
				points[i].Color.b = b / 255.0f;
				points[i].Color.a = 1.0f;
			}
			else if (UseRainbow) {
				//No color -> Rainbow color!
				points[i].Color = Color.HSVToRGB(Mathf.Repeat(x + y + z, 1.0f), 1, 1);
			}

			points[i].Position.x = x;
			points[i].Position.y = y;
			points[i].Position.z = z;

			points[i].Size = 1.0f;
		}
		Profiler.EndSample();

		Debug.Log("Parsed point cloud " + StreamPclPath + " with " + pointCount + " points in " + sw.ElapsedMilliseconds +
		          "ms (" + (binary ? "binary" : "test)"));
		GetComponent<TCParticleSystem>().Emit(points);
	}
}