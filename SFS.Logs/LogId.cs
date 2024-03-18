using System;

namespace SFS.Logs;

[Serializable]
public struct LogId
{
	public LogType type;

	public int value;

	public string planet;

	public LogId(LogType type, int value, string planet)
	{
		this.type = type;
		this.value = value;
		this.planet = planet;
	}
}
