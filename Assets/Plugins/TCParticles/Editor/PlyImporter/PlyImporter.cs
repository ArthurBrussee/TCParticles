// Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using TC;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;

namespace Pcx {
	internal class PclBinaryReader {
		byte[] m_bytes;
		public int Position;

		byte[] m_scratchRead = new byte[4];

		public PclBinaryReader(byte[] bytes) {
			m_bytes = bytes;
			Position = 0;
		}

		public string ReadLine() {
			int charsRead = 0;

			while (true) {
				char c = (char) m_bytes[Position++];

				if (c == '\n') {
					return new string(m_readChars, 0, charsRead);
				}

				if (c != '\r') {
					m_readChars[charsRead++] = c;
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float ReadSingleLittleEndian() {
			m_scratchRead[0] = m_bytes[Position++];
			m_scratchRead[1] = m_bytes[Position++];
			m_scratchRead[2] = m_bytes[Position++];
			m_scratchRead[3] = m_bytes[Position++];
			return BitConverter.ToSingle(m_scratchRead, 0);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float ReadSingleBigEndian() {
			m_scratchRead[3] = m_bytes[Position++];
			m_scratchRead[2] = m_bytes[Position++];
			m_scratchRead[1] = m_bytes[Position++];
			m_scratchRead[0] = m_bytes[Position++];
			return BitConverter.ToSingle(m_scratchRead, 0);
		}

		public void AdvancePropertyAscii() {
			while (true) {
				char c = (char) m_bytes[Position++];

				if (c == ' ') {
					return;
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float ReadSingleAscii() {
			int charsRead = 0;

			while (true) {
				char c = (char) m_bytes[Position++];

				if (c == ' ') {
					return float.Parse(new string(m_readChars, 0, charsRead));
				}

				m_readChars[charsRead++] = c;
			}
		}

		char[] m_readChars = new char[512];

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte ReadByteAscii() {
			int charsRead = 0;

			while (true) {
				char c = (char) m_bytes[Position++];

				if (c == ' ') {
					return byte.Parse(new string(m_readChars, 0, charsRead));
				}

				m_readChars[charsRead++] = c;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte ReadByte() {
			return m_bytes[Position++];
		}
	}

	[ScriptedImporter(1, "ply")]
	internal class PlyImporter : ScriptedImporter {
		[Header("Point Cloud Data Settings")]
		public float Scale = 1;
#pragma warning disable 649
		public Vector3 PivotOffset;
		public Vector3 NormalRotation;
#pragma warning restore 649

		[Header("Default Renderer")]
		public float DefaultPointSize = 0.1f;

		public override void OnImportAsset(AssetImportContext context) {
			// ComputeBuffer container
			// Create a prefab with PointCloudRenderer.
			var gameObject = new GameObject();
			var data = ImportAsPointCloudData(context.assetPath);

			var system = gameObject.AddComponent<TCParticleSystem>();
			system.Emitter.Shape = EmitShapes.PointCloud;
			system.Emitter.PointCloud = data;
			system.Emitter.SetBursts(new[] {new BurstEmission {Time = 0, Amount = data.PointCount}});
			system.Emitter.EmissionRate = 0;
			system.Emitter.Lifetime = MinMaxRandom.Constant(-1.0f);
			system.Looping = false;
			system.MaxParticles = data.PointCount + 1000;
			system.Emitter.Size = MinMaxRandom.Constant(DefaultPointSize);
			system.Manager.NoSimulation = true;

			if (data.Normals != null) {
				system.ParticleRenderer.pointCloudNormals = true;
				system.ParticleRenderer.RenderMode = GeometryRenderMode.Mesh;

				var quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
				system.ParticleRenderer.Mesh = quadGo.GetComponent<MeshFilter>().sharedMesh;
				DestroyImmediate(quadGo);
			}

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
			X,
			Y,
			Z,
			NX,
			NY,
			NZ,
			R,
			G,
			B,
			A,
			Data8,
			Data16,
			Data32,
			DataAscii
		}

		static int GetPropertySize(DataProperty p) {
			switch (p) {
				case DataProperty.X: return 4;
				case DataProperty.Y: return 4;
				case DataProperty.Z: return 4;

				case DataProperty.NX: return 4;
				case DataProperty.NY: return 4;
				case DataProperty.NZ: return 4;

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
			public List<DataProperty> Properties = new List<DataProperty>();
			public int VertexCount = -1;
			public PlyFormat Format;
		}

		class DataBody {
			public Vector3[] Vertices;
			public Vector3[] Normals;
			public Color32[] Colors;

			public DataBody(int vertexCount) {
				Vertices = new Vector3[vertexCount];
				Normals = new Vector3[vertexCount];
				Colors = new Color32[vertexCount];
			}
		}

		byte[] ReadFully(Stream input) {
			byte[] buffer = new byte[16 * 1024];
			using (MemoryStream ms = new MemoryStream()) {
				int read;
				while ((read = input.Read(buffer, 0, buffer.Length)) > 0) {
					ms.Write(buffer, 0, read);
				}

				return ms.ToArray();
			}
		}

		PointCloudData ImportAsPointCloudData(string path) {
			var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			var reader = new PclBinaryReader(ReadFully(stream));

			var header = ReadDataHeader(reader);
			var body = ReadDataBody(header, reader);

			var data = ScriptableObject.CreateInstance<PointCloudData>();
			data.Initialize(body.Vertices, body.Normals, body.Colors, Scale, PivotOffset, NormalRotation);
			data.name = Path.GetFileNameWithoutExtension(path);

			return data;
		}

		enum PlyFormat {
			BinaryLittleEndian,
			BinaryBigEndian,
			Ascii
		}

		DataHeader ReadDataHeader(PclBinaryReader reader) {
			var data = new DataHeader();
			// Magic number line ("ply")
			string line = reader.ReadLine();

			if (line != "ply") {
				throw new ArgumentException("Magic number ('ply') mismatch.");
			}

			// Read header contents.
			while (true) {
				// Read a line and split it with white space.
				line = reader.ReadLine();
				if (line == "end_header") {
					break;
				}

				var col = line.Split(' ');
				switch (col[0]) {
					case "comment":
						continue;
					// Element declaration (unskippable)
					case "element" when col[1] == "vertex":
						data.VertexCount = Convert.ToInt32(col[2]);
						break;
					case "element":
						// Don't read elements other than vertices.
						continue;
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
							data.Format = PlyFormat.Ascii;
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
						case "x":
							prop = DataProperty.X;
							break;
						case "y":
							prop = DataProperty.Y;
							break;
						case "z":
							prop = DataProperty.Z;
							break;

						case "nx":
							prop = DataProperty.NX;
							break;
						case "ny":
							prop = DataProperty.NY;
							break;
						case "nz":
							prop = DataProperty.NZ;
							break;

						case "red":
						case "r":
						case "diffuse_red":
							prop = DataProperty.R;
							break;
						case "green":
						case "g":
						case "diffuse_green":
							prop = DataProperty.G;
							break;
						case "blue":
						case "b":
						case "diffuse_blue":
							prop = DataProperty.B;
							break;
						case "alpha":
						case "a":
						case "diffuse_alpha":
							prop = DataProperty.A;
							break;
					}

					switch (col[1]) {
						// Check the property type.
						case "char":
						case "uchar":
						case "uint8": {
							if (prop == DataProperty.Invalid) {
								prop = DataProperty.Data8;
							} else if (GetPropertySize(prop) != 1) {
								throw new ArgumentException("Invalid property type ('" + line + "').");
							}

							break;
						}

						case "short":
						case "ushort": {
							if (prop == DataProperty.Invalid) {
								prop = DataProperty.Data16;
							} else if (GetPropertySize(prop) != 2) {
								throw new ArgumentException("Invalid property type ('" + line + "').");
							}

							break;
						}

						case "int":
						case "uint": {
							if (prop == DataProperty.Invalid) {
								prop = DataProperty.Data32;
							} else if (GetPropertySize(prop) != 4) {
								throw new ArgumentException("Invalid property type ('" + line + "').");
							}

							break;
						}

						case "float":
						case "float32": {
							if (prop == DataProperty.Invalid) {
								if (data.Format == PlyFormat.Ascii) {
									prop = DataProperty.DataAscii;
								} else {
									prop = DataProperty.Data32;
								}
							} else if (GetPropertySize(prop) != 4) {
								throw new ArgumentException("Invalid property type ('" + line + "').");
							}

							break;
						}

						default:
							throw new ArgumentException("Unsupported property type ('" + line + "').");
					}

					data.Properties.Add(prop);
				}
			}

			// Rewind the stream back to the exact position of the reader.
			return data;
		}

		DataBody ReadDataBody(DataHeader header, PclBinaryReader reader) {
			var data = new DataBody(header.VertexCount);

			byte r = 255, g = 255, b = 255, a = 255;

			switch (header.Format) {
				case PlyFormat.BinaryLittleEndian:
					for (var i = 0; i < header.VertexCount; i++) {
						for (int j = 0; j < header.Properties.Count; ++j) {
							switch (header.Properties[j]) {
								case DataProperty.X:
									data.Vertices[i].x = reader.ReadSingleLittleEndian();
									break;
								case DataProperty.Y:
									data.Vertices[i].y = reader.ReadSingleLittleEndian();
									break;
								case DataProperty.Z:
									data.Vertices[i].z = reader.ReadSingleLittleEndian();
									break;

								case DataProperty.NX:
									data.Normals[i].x = reader.ReadSingleLittleEndian();
									break;
								case DataProperty.NY:
									data.Normals[i].y = reader.ReadSingleLittleEndian();
									break;
								case DataProperty.NZ:
									data.Normals[i].z = reader.ReadSingleLittleEndian();
									break;

								case DataProperty.R:
									r = reader.ReadByte();
									break;
								case DataProperty.G:
									g = reader.ReadByte();
									break;
								case DataProperty.B:
									b = reader.ReadByte();
									break;
								case DataProperty.A:
									a = reader.ReadByte();
									break;

								case DataProperty.Data8:
									reader.Position += 1;
									break;
								case DataProperty.Data16:
									reader.Position += 2;
									break;
								case DataProperty.Data32:
									reader.Position += 4;
									break;
							}
						}

						data.Colors[i].r = r;
						data.Colors[i].g = g;
						data.Colors[i].b = b;
						data.Colors[i].a = a;
					}

					break;

				case PlyFormat.BinaryBigEndian:
					for (var i = 0; i < header.VertexCount; i++) {
						for (int j = 0; j < header.Properties.Count; ++j) {
							switch (header.Properties[j]) {
								case DataProperty.X:
									data.Vertices[i].x = reader.ReadSingleBigEndian();
									break;
								case DataProperty.Y:
									data.Vertices[i].y = reader.ReadSingleBigEndian();
									break;
								case DataProperty.Z:
									data.Vertices[i].z = reader.ReadSingleBigEndian();
									break;

								case DataProperty.NX:
									data.Normals[i].x = reader.ReadSingleBigEndian();
									break;
								case DataProperty.NY:
									data.Normals[i].y = reader.ReadSingleBigEndian();
									break;
								case DataProperty.NZ:
									data.Normals[i].z = reader.ReadSingleBigEndian();
									break;

								case DataProperty.R:
									r = reader.ReadByte();
									break;
								case DataProperty.G:
									g = reader.ReadByte();
									break;
								case DataProperty.B:
									b = reader.ReadByte();
									break;
								case DataProperty.A:
									a = reader.ReadByte();
									break;

								case DataProperty.Data8:
									reader.Position += 1;
									break;
								case DataProperty.Data16:
									reader.Position += 2;
									break;
								case DataProperty.Data32:
									reader.Position += 4;
									break;
							}
						}

						data.Colors[i].r = r;
						data.Colors[i].g = g;
						data.Colors[i].b = b;
						data.Colors[i].a = a;
					}

					break;

				case PlyFormat.Ascii:
					for (var i = 0; i < header.VertexCount; i++) {
						for (int j = 0; j < header.Properties.Count; ++j) {
							switch (header.Properties[j]) {
								case DataProperty.X:
									data.Vertices[i].x = reader.ReadSingleAscii();
									break;
								case DataProperty.Y:
									data.Vertices[i].y = reader.ReadSingleAscii();
									break;
								case DataProperty.Z:
									data.Vertices[i].z = reader.ReadSingleAscii();
									break;

								case DataProperty.NX:
									data.Normals[i].x = reader.ReadSingleAscii();
									break;
								case DataProperty.NY:
									data.Normals[i].y = reader.ReadSingleAscii();
									break;
								case DataProperty.NZ:
									data.Normals[i].z = reader.ReadSingleAscii();
									break;

								case DataProperty.R:
									r = reader.ReadByteAscii();
									break;
								case DataProperty.G:
									g = reader.ReadByteAscii();
									break;
								case DataProperty.B:
									b = reader.ReadByteAscii();
									break;
								case DataProperty.A:
									a = reader.ReadByteAscii();
									break;

								case DataProperty.DataAscii:
									reader.AdvancePropertyAscii();
									break;
							}
						}

						data.Colors[i].r = r;
						data.Colors[i].g = g;
						data.Colors[i].b = b;
						data.Colors[i].a = a;
					}

					break;
			}

			return data;
		}
	}
}