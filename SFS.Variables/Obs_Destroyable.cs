using System;

namespace SFS.Variables;

[Serializable]
public abstract class Obs_Destroyable<T> : Obs<T> where T : I_ObservableMonoBehaviour
{
	public new T Value
	{
		get
		{
			return base.Value;
		}
		set
		{
			T val = base.Value;
			base.Value = value;
			if (val != null)
			{
				val.OnDestroy = (Action)Delegate.Remove(val.OnDestroy, new Action(OnDestroy));
			}
			if (value != null)
			{
				value.OnDestroy = (Action)Delegate.Combine(value.OnDestroy, new Action(OnDestroy));
			}
		}
	}

	private void OnDestroy()
	{
		Value = default(T);
	}
}
