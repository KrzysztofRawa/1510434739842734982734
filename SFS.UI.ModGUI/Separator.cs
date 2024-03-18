using UnityEngine;

namespace SFS.UI.ModGUI;

public class Separator : GUIElement
{
	public override void Init(GameObject self, Transform parent)
	{
		gameObject = self;
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		rectTransform = gameObject.Rect();
	}
}
