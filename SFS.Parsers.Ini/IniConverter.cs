using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SFS.Parsers.Ini;

public class IniConverter
{
	private class StringReader
	{
		public readonly string input;

		public int pos;

		public StringReader(string input)
		{
			this.input = input;
		}

		public bool IsAtEnd()
		{
			return pos >= input.Length;
		}

		public char Read()
		{
			return input[pos++];
		}

		public string Read(int length)
		{
			length = Math.Min(input.Length - pos, length);
			string result = input.Substring(pos, length);
			pos += length;
			return result;
		}

		public char Peek()
		{
			return input[pos];
		}

		public string Peek(int length)
		{
			return input.Substring(pos, Math.Min(input.Length - pos, length));
		}

		public StringReader Split()
		{
			return new StringReader(input)
			{
				pos = pos
			};
		}

		public void Merge(StringReader reader)
		{
			pos = reader.pos;
		}
	}

	public IniDataEnv data = new IniDataEnv();

	public IniConverter()
	{
		data = new IniDataEnv();
	}

	public IniConverter(string iniText)
	{
		LoadIni(iniText);
	}

	public IniDataSection GetSection(string section)
	{
		if (!data.sections.ContainsKey(section))
		{
			data.sections[section] = new IniDataSection(section);
		}
		return data.sections[section];
	}

	public void LoadIni(string iniText)
	{
		string[] array = iniText.Split(new string[2] { "\n", "\r\n" }, StringSplitOptions.None);
		IniDataSection iniDataSection = data.Global;
		int num = 0;
		StringBuilder stringBuilder = new StringBuilder();
		string[] array2 = array;
		foreach (string obj in array2)
		{
			string input = obj.Trim();
			if (string.IsNullOrWhiteSpace(obj))
			{
				num++;
				continue;
			}
			StringReader reader2 = new StringReader(input);
			string sectionName2;
			if (ReadComment(reader2, out var comment2))
			{
				stringBuilder.AppendLine(comment2);
			}
			else if (ReadSection(reader2, out sectionName2))
			{
				iniDataSection = data.GetSection(sectionName2);
				iniDataSection.whitespacesBefore = num;
				num = 0;
				if (stringBuilder.Length > 0)
				{
					iniDataSection.comment = stringBuilder.ToString();
				}
				stringBuilder.Clear();
			}
			else
			{
				if (!ReadKey(reader2, out var keyName2))
				{
					continue;
				}
				ReadValue(reader2, out var value2, out var aftComment2);
				if (iniDataSection.data.ContainsKey(keyName2))
				{
					IniDataEnv.Value value3 = iniDataSection[keyName2];
					value3.value = value3.value + "\n" + value2;
					continue;
				}
				IniDataEnv.Value value4 = new IniDataEnv.Value(value2);
				value4.aftLineComment = aftComment2;
				value4.whitespacesBefore = num;
				num = 0;
				if (stringBuilder.Length > 0)
				{
					value4.preLineComment = stringBuilder.ToString();
				}
				stringBuilder.Clear();
				iniDataSection[keyName2] = value4;
			}
		}
		static bool ReadComment(StringReader reader, out string comment)
		{
			string[] obj2 = new string[3] { ";", "//", "#" };
			StringBuilder stringBuilder5 = new StringBuilder();
			string[] array3 = obj2;
			foreach (string text in array3)
			{
				if (reader.Peek(text.Length) == text)
				{
					reader.Read(text.Length);
					while (!reader.IsAtEnd())
					{
						stringBuilder5.Append(reader.Read());
					}
					comment = stringBuilder5.ToString();
					return true;
				}
			}
			comment = null;
			return false;
		}
		static bool ReadKey(StringReader reader, out string keyName)
		{
			StringReader stringReader = reader.Split();
			StringBuilder stringBuilder3 = new StringBuilder();
			while (!stringReader.IsAtEnd())
			{
				if (stringReader.Peek() == '=')
				{
					stringReader.Read();
					reader.Merge(stringReader);
					keyName = stringBuilder3.ToString();
					return true;
				}
				stringBuilder3.Append(stringReader.Read());
			}
			keyName = null;
			return false;
		}
		static bool ReadSection(StringReader reader, out string sectionName)
		{
			StringBuilder stringBuilder4 = new StringBuilder();
			if (reader.Peek() == '[')
			{
				reader.Read();
				while (reader.Peek() != ']')
				{
					stringBuilder4.Append(reader.Read());
				}
				sectionName = stringBuilder4.ToString();
				return true;
			}
			sectionName = null;
			return false;
		}
		static bool ReadValue(StringReader reader, out string value, out string aftComment)
		{
			aftComment = null;
			StringBuilder stringBuilder2 = new StringBuilder();
			while (!reader.IsAtEnd() && !ReadComment(reader, out aftComment))
			{
				stringBuilder2.Append(reader.Read());
			}
			value = stringBuilder2.ToString();
			return value.Length > 0;
		}
	}

	public string Serialize()
	{
		StringBuilder iniTextBuilder = new StringBuilder();
		int whitelines = 0;
		data.sections.ForEach(delegate(KeyValuePair<string, IniDataSection> section)
		{
			AppendSection(section.Value);
		});
		return iniTextBuilder.ToString();
		void Append(string txt, bool clearWhitelines)
		{
			whitelines = ((!clearWhitelines) ? whitelines : 0);
			iniTextBuilder.Append(txt);
		}
		void AppendAftComment(string aftComment)
		{
			if (aftComment != null)
			{
				AppendComment(aftComment, canUseWhiteline: false);
			}
			else
			{
				AppendLine("", clearWhitelines: true);
			}
		}
		void AppendComment(string comment, bool canUseWhiteline)
		{
			if (comment != null)
			{
				if (whitelines == 0 && canUseWhiteline)
				{
					EnsureWhitelines(1);
				}
				string[] array2 = comment.Split(new string[2] { "\n", "\r\n" }, StringSplitOptions.None);
				foreach (string text in array2)
				{
					AppendLine("# " + text, clearWhitelines: false);
				}
			}
		}
		void AppendDataLine(string keyName, IniDataEnv.Value value, bool canUseInitialWhiteline)
		{
			if (value.whitespacesBefore != 0 && canUseInitialWhiteline)
			{
				EnsureWhitelines(value.whitespacesBefore);
			}
			AppendComment(value.preLineComment, canUseInitialWhiteline);
			if (value.value.Contains("\n"))
			{
				EnsureWhitelines(1);
				string[] array = value.value.Split('\n');
				for (int i = 0; i < array.Length; i++)
				{
					AppendDataValue(array[i] + ((i < array.Length - 1) ? "\n" : ""));
				}
				AppendAftComment(value.aftLineComment);
				AppendWhiteLine();
			}
			else
			{
				AppendDataValue(value.value);
				AppendAftComment(value.aftLineComment);
			}
		}
		void AppendDataValue(string val)
		{
			Append("    " + P_1.keyName + "=" + val, clearWhitelines: true);
		}
		void AppendLine(string txt, bool clearWhitelines)
		{
			Append(txt + "\n", clearWhitelines);
		}
		void AppendSection(IniDataSection section)
		{
			AppendComment(section.comment, canUseWhiteline: true);
			if (iniTextBuilder.Length > 0)
			{
				EnsureWhitelines(1);
			}
			AppendLine("[" + section.name + "]", clearWhitelines: true);
			KeyValuePair<string, IniDataEnv.Value>[] array3 = section.data.ToArray();
			for (int l = 0; l < section.data.Count; l++)
			{
				KeyValuePair<string, IniDataEnv.Value> keyValuePair = array3[l];
				AppendDataLine(keyValuePair.Key, keyValuePair.Value, l > 0);
			}
		}
		void AppendWhiteLine()
		{
			whitelines++;
			iniTextBuilder.AppendLine();
		}
		void EnsureWhitelines(int amount)
		{
			int num = Math.Max(0, amount - whitelines);
			for (int j = 0; j < num; j++)
			{
				AppendWhiteLine();
			}
		}
	}

	public string[] GetSectionNames()
	{
		return new List<IniDataSection>(data.sections.Values).ConvertAll((IniDataSection data) => data.name).ToArray();
	}
}
