using UnityEngine;

namespace SFS.Career;

public class AllowPerMode : MonoBehaviour
{
	public bool careerOnly;

	public bool sandboxOnly;

	public bool Allowed
	{
		get
		{
			if (careerOnly && !Base.worldBase.IsCareer)
			{
				return false;
			}
			if (sandboxOnly && Base.worldBase.settings.mode.mode != 0)
			{
				return false;
			}
			return true;
		}
	}
}
