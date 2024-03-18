using System.Linq;
using UnityEngine;

namespace SFS.UI;

public class VerticalLayout : LayoutGroup
{
	protected override void SetSize_Internal(Vector2 size)
	{
		size = LayoutUtility.GetApplySize(GetRectSize(), size, applySelfSizeX, applySelfSizeY);
		SetRectSize(size);
		Vector2 usableSizeForChildren = GetUsableSizeForChildren(size, RectTransform.Axis.Vertical);
		if (spaceDistributionMode == SpaceDistributionMode.DistributeEvenly)
		{
			float y = usableSizeForChildren.y;
			NewElement[] activeElements = base.ActiveElements;
			for (int i = 0; i < activeElements.Length; i++)
			{
				activeElements[i].SetSize(new Vector2(usableSizeForChildren.x, y / (float)base.ActiveElements.Length));
			}
		}
		else if (spaceDistributionMode == SpaceDistributionMode.DistributeByUse)
		{
			Vector2 preferredCombinedSize;
			float[] array = (from d in GetSizeDistribution(out preferredCombinedSize)
				select d.y).ToArray();
			for (int j = 0; j < base.ActiveElements.Length; j++)
			{
				base.ActiveElements[j].SetSize(new Vector2(usableSizeForChildren.x, usableSizeForChildren.y * array[j]));
			}
			if (preferredCombinedSize.y < usableSizeForChildren.y)
			{
				float num = (usableSizeForChildren.y - preferredCombinedSize.y) / (float)base.ActiveElements.Length;
				for (int k = 0; k < base.ActiveElements.Length; k++)
				{
					base.ActiveElements[k].SetSize(new Vector2(usableSizeForChildren.x, preferredCombinedSize.y * array[k] + num));
				}
			}
			else
			{
				for (int l = 0; l < base.ActiveElements.Length; l++)
				{
					base.ActiveElements[l].SetSize(new Vector2(usableSizeForChildren.x, usableSizeForChildren.y * array[l]));
				}
			}
		}
		else if (spaceDistributionMode == SpaceDistributionMode.TakeAvailable)
		{
			float num2 = usableSizeForChildren.y;
			NewElement[] activeElements = base.ActiveElements;
			foreach (NewElement newElement in activeElements)
			{
				float num3 = Mathf.Min(num2, newElement.GetPreferredSize().y);
				num2 -= num3;
				newElement.SetSize(new Vector2(usableSizeForChildren.x, num3));
			}
		}
		if (layoutGroup != null)
		{
			layoutGroup.enabled = false;
			layoutGroup.enabled = true;
		}
	}

	protected override Vector2 GetPreferredSize_Internal()
	{
		float x = LayoutUtility.GetFilteredSize(GetRectSize(), base.ActiveElements.Select((NewElement e) => e.GetPreferredSize()), sizeMode).x + (float)base.Padding.horizontal;
		float y = (float)base.Padding.vertical + base.ActiveElements.Sum((NewElement e) => e.GetPreferredSize().y + base.Spacing) - base.Spacing;
		return new Vector2(x, y);
	}
}
