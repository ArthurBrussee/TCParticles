// Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;

using System;
using System.Collections.Generic;
using System.IO;
using TC;
using System.Runtime.CompilerServices;

namespace Pcx {
	//TODO: Agressive inline
	class PclBinaryReader {
		byte[] m_bytes;
		public int Position;

		byte[] m_scratchRead = new byte[4];

		public PclBinaryReader(byte[] bytes) {
			m_bytes = bytes;
			Position = 0;
		}

		public float ReadSingleLittleEndian() {
			m_scratchRead[0] = m_bytes[Position++];
			m_scratchRead[1] = m_bytes[Position++];
			m_scratchRead[2] = m_bytes[Position++];
			m_scratchRead[3] = m_bytes[Position++];
			return BitConverter.ToSingle(m_scratchRead, 0);
		}

		public float ReadSingleBigEndian() {
			m_scratchRead[3] = m_bytes[Position++];
			m_scratchRead[2] = m_bytes[Position++];
			m_scratchRead[1] = m_bytes[Position++];
			m_scratchRead[0] = m_bytes[Position++];
			return BitConverter.ToSingle(m_scratchRead, 0);
		}

		public void AdvancePropertyAscii() {
			while (true) {
				char c = (char)m_bytes[Position++];

				if (c == ' ') {
					return;
				}
			}
		}

		public float ReadSingleAscii() {
			int charsRead = 0;

			while (true) {
				char c = (char)m_bytes[Position++];

				if (c == ' ') {
					return float.Parse(new string(m_readChars, 0, charsRead));
				}

				m_readChars[charsRead++] = c;
			}
		}


		char[] m_readChars = new char[512];

		public byte ReadByteAscii() {
			int charsRead = 0;

			while (true) {
				char c = (char)m_bytes[Position++];

				if (c == ' ') {
					return byte.Parse(new string(m_readChars, 0, charsRead));
				}

				m_readChars[charsRead++] = c;
			}
		}

		public byte ReadByte() {
			return m_bytes[Position++];
		}
	}

	[ScriptedImporter(1, "ply")]
	class PlyImporter : ScriptedImporter {
#pragma warning disable CS0649
		[Header("Point Cloud Data Settings")]
		public float Scale = 1;
		public Vector3 PivotOffset;

		[Header("Default Renderer")]
		public float DefaultPointSize = 0.02f;
#pragma warning restore CS0649

		public override void OnImportAsset(AssetImportContext context) {
			// ComputeBuffer container
			// Create a prefab with PointCloudRenderer.
			var gameObject = new GameObject();
			var data = ImportAsPointCloudData(context.assetPath);

			var system = gameObject.AddComponent<TCParticleSystem>();
			system.Emitter.Shape = EmitShapes.PointCloud;
			system.Emitter.PointCloud = data;
			system.Emitter.SetBursts(new BurstEmission[] { new BurstEmission { Time = 0, Amount = data.PointCount } });
			system.Emitter.EmissionRate = 0;
			system.Emitter.Lifetime = MinMaxRandom.Constant(-1.0f);
			system.Looping = false;
			system.MaxParticles = data.PointCount + 1000;
			system.Emitter.Size = MinMaxRandom.Constant(DefaultPointSize);
			system.Manager.NoSimulation = true;

			context.AddObjectToAsset("prefab", gameObject);
			if (data != null) {
				context.AddObjectToAsset("data", data);
			}
			context.SetMainObject(gameObject);
		}

		static Material GetDefaultMaterial() {
			return AssetDatabase.LoadAssetAtPath<Material>(
				"Assets/Pcx/Editor/Default Point.mat"
			);
		}

		enum DataProperty {
			Invalid,
			X, Y, Z,
			R, G, B, A,
			Data8, Data16, Data32,
			DataAscii
		}

		static int GetPropertySize(DataProperty p) {
			switch (p) {
				case DataProperty.X: return 4;
				case DataProperty.Y: return 4;
				case DataProperty.Z: return 4;
				case DataProperty.R: return 1;
				case DataProperty.G: return 1;
				case DataProperty.B: return 1;
				case DataProperty.A: return 1;
				case DataProperty.Data8: return 1;
				case DataProperty.Data16: return 2;
				case DataProperty.Data32: return 4;
			}
			return 0;
		}

		class DataHeader {
			public List<DataProperty> properties = new List<DataProperty>();
			public int vertexCount = -1;
			public PlyFormat Format;
		}

		class DataBody {
			public Vector3[] vertices;
			public Color32[] colors;

			public DataBody(int vertexCount) {
				vertices = new Vector3[vertexCount];
				colors = new Color32[vertexCount];
			}
		}

		PointCloudData ImportAsPointCloudData(string path) {
			var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			var header = ReadDataHeader(new StreamReader(stream));
			var body = ReadDataBody(header, stream);

			var data = ScriptableObject.CreateInstance<PointCloudData>();
			data.Initialize(body.vertices, body.colors, Scale, PivotOffset);

			data.name = Path.GetFileNameWithoutExtension(path);
			return data;
		}

		enum PlyFormat {
			BinaryLittleEndian,
			BinaryBigEndian,
			ASCII
		}

		DataHeader ReadDataHeader(StreamReader reader) {
			var data = new DataHeader();
			var readCount = 0;

			// Magic number line ("ply")
			var line = reader.ReadLine();
			readCount += line.Length + 1;

			if (line != "ply") {
				throw new ArgumentException("Magic number ('ply') mismatch.");
			}

			// Read header contents.
			while (true) {
				// Read a line and split it with white space.
				line = reader.ReadLine();
				readCount += line.Length + 1;
				if (line == "end_header") break;
				var col = line.Split(' ');

				if (col[0] == "comment") {
					continue;
				}

				// Element declaration (unskippable)
				if (col[0] == "element") {
					if (col[1] == "vertex") {
						data.vertexCount = Convert.ToInt32(col[2]);
					} else {
						// Don't read elements other than vertices.
						continue;
					}
				}

				if (col[0] == "format") {
					switch (col[1]) {
						case "binary_little_endian":
							data.Format = PlyFormat.BinaryLittleEndian;
							break;

						case "binary_big_endian":
							data.Format = PlyFormat.BinaryBigEndian;
							break;

						case "ascii":
							data.Format = PlyFormat.ASCII;
							break;

						default:
							throw new ArgumentException("Unrecognized ply format! " + line);
					}

					continue;
				}

				// Property declaration line
				if (col[0] == "property") {
					if (col[1] == "list") {
						continue;
					}

					var prop = DataProperty.Invalid;

					// Parse the property name entry.
					switch (col[2]) {
						case "x": prop = DataProperty.X; break;
						case "y": prop = DataProperty.Y; break;
						case "z": prop = DataProperty.Z; break;
						case "red": case "r": case "diffuse_red": prop = DataProperty.R; break;
						case "green": case "g": case "diffuse_green": prop = DataProperty.G; break;
						case "blue": case "b": case "diffuse_blue": prop = DataProperty.B; break;
						case "alpha": case "a": case "diffuse_alpha": prop = DataProperty.A; break;
					}

					// Check the property type.
					if (col[1] == "char" || col[1] == "uchar" || col[1] == "uint8") {
						if (prop == DataProperty.Invalid) {
							prop = DataProperty.Data8;
						} else if (GetPropertySize(prop) != 1)
							throw new ArgumentException("Invalid property type ('" + line + "').");
					} else if (col[1] == "short" || col[1] == "ushort") {
						if (prop == DataProperty.Invalid) {
							prop = DataProperty.Data16;
						} else if (GetPropertySize(prop) != 2)
							throw new ArgumentException("Invalid property type ('" + line + "').");
					} else if (col[1] == "int" || col[1] == "uint") {
						if (prop == DataProperty.Invalid) {
							prop = DataProperty.Data32;
						} else if (GetPropertySize(prop) != 4) {
							throw new ArgumentException("Invalid property type ('" + line + "').");
						}
					} else if (col[1] == "float" || col[1] == "float32") {
						if (prop == DataProperty.Invalid) {
							if (data.Format == PlyFormat.ASCII) {
								prop = DataProperty.DataAscii;
							} else {
								prop = DataProperty.Data32;
							}
						} else if (GetPropertySize(prop) != 4)
							throw new ArgumentException("Invalid property type ('" + line + "').");
					} else {
						throw new ArgumentException("Unsupported property type ('" + line + "').");
					}

					data.properties.Add(prop);
				}
			}

			// Rewind the stream back to the exact position of the reader.
			reader.BaseStream.Position = readCount;

			return data;
		}

		DataBody ReadDataBody(DataHeader header, FileStream stream) {

			byte[] arrfile = new byte[stream.Length - stream.Position];

			int remainder = arrfile.Length;
			int startIndex = 0;
			int read;

			do {
				read = stream.Read(arrfile, startIndex, remainder);
				startIndex += read;
				remainder -= read;
			} while (remainder > 0 && read > 0);

			var reader = new PclBinaryReader(arrfile);

			//var bytes = File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, StreamPclPath));
			//var binReader = new PclBinaryReader(bytes);

			var data = new DataBody(header.vertexCount);

			byte r = 255, g = 255, b = 255, a = 255;

			switch (header.Format) {
				case PlyFormat.BinaryLittleEndian:
					for (var i = 0; i < header.vertexCount; i++) {
						for (int j = 0; j < header.properties.Count; ++j) {
							switch (header.properties[j]) {
								case DataProperty.X: data.vertices[i].x = reader.ReadSingleLittleEndian(); break;
								case DataProperty.Y: data.vertices[i].y = reader.ReadSingleLittleEndian(); break;
								case DataProperty.Z: data.vertices[i].z = reader.ReadSingleLittleEndian(); break;

								case DataProperty.R: r = reader.ReadByte(); break;
								case DataProperty.G: g = reader.ReadByte(); break;
								case DataProperty.B: b = reader.ReadByte(); break;
								case DataProperty.A: a = reader.ReadByte(); break;

								case DataProperty.Data8: reader.Position += 1; break;
								case DataProperty.Data16: reader.Position += 2; break;
								case DataProperty.Data32: reader.Position += 4; break;
							}
						}

						data.colors[i].r = r;
						data.colors[i].g = g;
						data.colors[i].b = b;
						data.colors[i].a = a;
					}
					break;

				case PlyFormat.BinaryBigEndian:
					for (var i = 0; i < header.vertexCount; i++) {
						for (int j = 0; j < header.properties.Count; ++j) {
							switch (header.properties[j]) {
								case DataProperty.X: data.vertices[i].x = reader.ReadSingleBigEndian(); break;
								case DataProperty.Y: data.vertices[i].y = reader.ReadSingleBigEndian(); break;
								case DataProperty.Z: data.vertices[i].z = reader.ReadSingleBigEndian(); break;

								case DataProperty.R: r = reader.ReadByte(); break;
								case DataProperty.G: g = reader.ReadByte(); break;
								case DataProperty.B: b = reader.ReadByte(); break;
								case DataProperty.A: a = reader.ReadByte(); break;

								case DataProperty.Data8: reader.Position += 1; break;
								case DataProperty.Data16: reader.Position += 2; break;
								case DataProperty.Data32: reader.Position += 4; break;
							}
						}

						data.colors[i].r = r;
						data.colors[i].g = g;
						data.colors[i].b = b;
						data.colors[i].a = a;
					}
					break;

				case PlyFormat.ASCII:
					for (var i = 0; i < header.vertexCount; i++) {
						for (int j = 0; j < header.properties.Count; ++j) {
							switch (header.properties[j]) {
								case DataProperty.X: data.vertices[i].x = reader.ReadSingleAscii(); break;
								case DataProperty.Y: data.vertices[i].y = reader.ReadSingleAscii(); break;
								case DataProperty.Z: data.vertices[i].z = reader.ReadSingleAscii(); break;

								case DataProperty.R: r = reader.ReadByteAscii(); break;
								case DataProperty.G: g = reader.ReadByteAscii(); break;
								case DataProperty.B: b = reader.ReadByteAscii(); break;
								case DataProperty.A: a = reader.ReadByteAscii(); break;

								case DataProperty.DataAscii: reader.AdvancePropertyAscii(); break;
							}
						}

						data.colors[i].r = r;
						data.colors[i].g = g;
						data.colors[i].b = b;
						data.colors[i].a = a;
					}
					break;
			}

			return data;
		}
	}
}