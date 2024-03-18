namespace SFS.World.Drag;

public struct Surface
{
	public HeatModuleBase owner;

	public Valid valid;

	public Line2 line;

	public Surface(HeatModuleBase owner, Valid valid, Line2 line)
	{
		this.owner = owner;
		this.valid = valid;
		this.line = line;
	}
}
