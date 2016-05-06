using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class OpenClose : ScriptableObject
{
	[SerializeField] private List<bool> open;
	[SerializeField] private List<string> names;

	private int CheckIfOpen(string nameDecl)
	{
		if (!names.Contains(nameDecl)) {
			open.Add(false);
			names.Add(nameDecl);
		}

		return names.IndexOf(nameDecl);
	}

	public bool ToggleArea(string areaName, Color col)
	{
		int i = CheckIfOpen(areaName);

		Color oldCol = GUI.color;

		if (!open[i]) {
			col *= 0.8f;
		}

		GUI.color = col;

		var pos = GUILayoutUtility.GetRect(0.0f, 16.0f, "ShurikenModuleTitle");
		pos.x -= 15.0f;
		pos.width += 15.0f;

		open[i] = GUI.Toggle(pos, open[i], new GUIContent(areaName), "ShurikenModuleTitle");

		GUI.color = oldCol;


		if (open[i]) {
			GUILayout.BeginVertical(TCEditorStyles.GetBackgroundForColor(col), GUILayout.MinHeight(20.0f)); 
		}

		return open[i];
	}

	public void ToggleAreaEnd(string areaName)
	{
		int i = CheckIfOpen(areaName);

		if (!open[i]) {
			return;
		}


		GUILayout.Space(5.5f);
		GUILayout.EndVertical();
	}


	private void OnEnable()
	{
		if (open != null && names != null) return;

		open = new List<bool>();
		names = new List<string>();
	}
}