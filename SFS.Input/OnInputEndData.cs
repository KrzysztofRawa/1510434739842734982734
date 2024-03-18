namespace SFS.Input;

public class OnInputEndData
{
	public InputType inputType;

	public TouchPosition position;

	public bool click;

	public bool LeftClick
	{
		get
		{
			if (click)
			{
				if (inputType != 0)
				{
					return inputType == InputType.MouseLeft;
				}
				return true;
			}
			return false;
		}
	}

	public OnInputEndData(InputType inputType, TouchPosition position, bool click)
	{
		this.inputType = inputType;
		this.position = position;
		this.click = click;
	}
}
