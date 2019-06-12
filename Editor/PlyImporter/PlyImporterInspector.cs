using UnityEditor;
using UnityEditor.Experimental.AssetImporters;

namespace Pcx {
	[CustomEditor(typeof(PlyImporter))]
	class PlyImporterInspector : ScriptedImporterEditor {
		protected override bool useAssetDrawPreview => false;
	}
}