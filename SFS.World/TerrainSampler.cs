using System;
using System.Collections.Generic;
using System.Globalization;
using SFS.World.PlanetModules;
using SFS.WorldBase;

namespace SFS.World;

public static class TerrainSampler
{
	public class TerrainSample
	{
		public readonly double[] angles;

		public readonly double surfaceArea;

		public readonly Dictionary<string, double[]> userVariables = new Dictionary<string, double[]>();

		public double[] output;

		public TerrainSample(double[] angles, double surfaceArea)
		{
			this.angles = angles;
			this.surfaceArea = surfaceArea;
			output = new double[angles.Length];
		}

		public double[] GetUserVariable(string name)
		{
			if (!userVariables.ContainsKey(name))
			{
				userVariables[name] = new double[output.Length];
			}
			return userVariables[name];
		}
	}

	public class Executor
	{
		public delegate void SampleCommand(TerrainSample sampler);

		public List<SampleCommand> commands = new List<SampleCommand>();

		public double[] Calculate(double[] angles01, double surfaceArea)
		{
			TerrainSample terrainSample = new TerrainSample(angles01, surfaceArea);
			foreach (SampleCommand command in commands)
			{
				command(terrainSample);
			}
			if (terrainSample.userVariables.TryGetValue("OUTPUT", out var value))
			{
				return value;
			}
			return new double[angles01.Length];
		}
	}

	public static class Compiler
	{
		private static Executor.SampleCommand Assign(string variableName, Executor.SampleCommand command)
		{
			return delegate(TerrainSample sampler)
			{
				sampler.output = sampler.GetUserVariable(variableName);
				command(sampler);
			};
		}

		private static Executor.SampleCommand ApplyCurve(object[] arguments, I_MsgLogger log)
		{
			HeightMap curve = Base.planetLoader.GetHeightMap(arguments[0] as string, log);
			return delegate(TerrainSample sampler)
			{
				for (int i = 0; i < sampler.output.Length; i++)
				{
					sampler.output[i] = curve.EvaluateClamped((float)sampler.output[i]);
				}
			};
		}

		private static Executor.SampleCommand AddHeightMap(object[] arguments, I_MsgLogger log)
		{
			HeightMap heightMap = Base.planetLoader.GetHeightMap(arguments[0] as string, log);
			double width = (double)arguments[1];
			double height = (double)arguments[2];
			HeightMap curve = null;
			if (arguments.Length > 3 && arguments[3] as string != "null")
			{
				curve = Base.planetLoader.GetHeightMap(arguments[3] as string, log);
			}
			return delegate(TerrainSample sampler)
			{
				double[] array = ((arguments.Length > 4) ? sampler.userVariables[arguments[4] as string] : null);
				double num = sampler.surfaceArea / width;
				if (curve != null)
				{
					if (array != null)
					{
						for (int i = 0; i < sampler.output.Length; i++)
						{
							sampler.output[i] += curve.EvaluateDoubleOut(heightMap.EvaluateDoubleOut(sampler.angles[i] * num)) * height * array[i];
						}
					}
					else
					{
						for (int j = 0; j < sampler.output.Length; j++)
						{
							sampler.output[j] += curve.EvaluateDoubleOut(heightMap.EvaluateDoubleOut(sampler.angles[j] * num)) * height;
						}
					}
				}
				else if (array != null)
				{
					for (int k = 0; k < sampler.output.Length; k++)
					{
						sampler.output[k] += heightMap.EvaluateDoubleOut(sampler.angles[k] * num) * height * array[k];
					}
				}
				else
				{
					for (int l = 0; l < sampler.output.Length; l++)
					{
						sampler.output[l] += heightMap.EvaluateDoubleOut(sampler.angles[l] * num) * height;
					}
				}
			};
		}

		private static Executor.SampleCommand Add(object[] arguments)
		{
			double value = (double)arguments[0];
			return delegate(TerrainSample sampler)
			{
				for (int i = 0; i < sampler.output.Length; i++)
				{
					sampler.output[i] += value;
				}
			};
		}

		private static Executor.SampleCommand Multiply(object[] arguments)
		{
			double value = (double)arguments[0];
			return delegate(TerrainSample sampler)
			{
				for (int i = 0; i < sampler.output.Length; i++)
				{
					sampler.output[i] *= value;
				}
			};
		}

		private static Executor.SampleCommand ClampMinMax(object[] arguments)
		{
			double min = (double)arguments[0];
			double max = (double)arguments[1];
			return delegate(TerrainSample sampler)
			{
				for (int i = 0; i < sampler.output.Length; i++)
				{
					sampler.output[i] = Math.Min(Math.Max(sampler.output[i], min), max);
				}
			};
		}

		public static Executor Compile(string[] formula, I_MsgLogger log)
		{
			Executor executor = new Executor();
			for (int i = 0; i < formula.Length; i++)
			{
				string text = formula[i];
				string text2 = null;
				bool flag = false;
				bool flag2 = false;
				bool flag3 = false;
				string text3 = "";
				string text4 = null;
				bool flag4 = false;
				string variableName = null;
				bool flag5 = false;
				string fname2 = "";
				bool flag6 = false;
				List<object> list = new List<object>();
				if (text.StartsWith("//"))
				{
					continue;
				}
				for (int j = 0; j < text.Length; j++)
				{
					char c = text[j];
					if (c == '(')
					{
						if (!flag4)
						{
							throw new Exception($"[{i + 1}:{j + 1}]: Expected name!");
						}
						if (flag6)
						{
							throw new Exception($"[{i + 1}:{j + 1}]: Close open function first!");
						}
						flag4 = false;
						flag6 = true;
						fname2 = text4;
						continue;
					}
					if (flag6)
					{
						switch (c)
						{
						case ',':
							if (flag2)
							{
								list.Add(double.Parse(text3.Trim(), CultureInfo.InvariantCulture));
								text3 = null;
								flag2 = false;
								continue;
							}
							if (flag4)
							{
								list.Add(text4.Trim());
								text4 = null;
								flag4 = false;
								continue;
							}
							if (flag)
							{
								throw new Exception("[{l + 1}:{x + 1}]: Close string first!");
							}
							if (text2 != null)
							{
								list.Add(text2);
								text2 = null;
								flag4 = false;
							}
							continue;
						case ')':
							if (flag2)
							{
								list.Add(double.Parse(text3.Trim(), CultureInfo.InvariantCulture));
								flag2 = false;
							}
							else if (flag4)
							{
								list.Add(text4.Trim());
								flag4 = false;
							}
							else
							{
								if (flag)
								{
									throw new Exception("[{l + 1}:{x + 1}]: Close string first!");
								}
								if (text2 != null)
								{
									list.Add(text2);
									text2 = null;
								}
							}
							flag6 = false;
							if (flag5)
							{
								executor.commands.Add(Assign(variableName, GetFunctionCall(fname2, list.ToArray())));
								flag5 = false;
								variableName = null;
							}
							else
							{
								executor.commands.Add(GetFunctionCall(fname2, list.ToArray()));
							}
							continue;
						}
					}
					if ((c == '=' && flag4) || text2 != null)
					{
						if (flag5)
						{
							throw new Exception("Cannot assign again!");
						}
						if (flag4)
						{
							variableName = text4.Trim();
							text4 = null;
							flag4 = false;
						}
						else if (text2 != null)
						{
							variableName = text2;
							text2 = null;
						}
						flag5 = true;
					}
					else if (flag)
					{
						if (c == '"')
						{
							flag = false;
						}
						else
						{
							text2 += c;
						}
					}
					else if (flag4)
					{
						text4 += c;
					}
					else if (flag2)
					{
						if (c == '.')
						{
							if (flag3)
							{
								throw new Exception($"[{i + 1}:{j + 1}]: Already found dot in number");
							}
							flag3 = true;
						}
						text3 += c;
					}
					else if (c == '"')
					{
						flag = !flag;
						if (flag)
						{
							text2 = "";
						}
					}
					else if (char.IsNumber(c))
					{
						flag2 = true;
						flag3 = false;
						text3 = c.ToString();
					}
					else if (char.IsLetter(c))
					{
						flag4 = true;
						text4 = c.ToString();
					}
				}
			}
			return executor;
			Executor.SampleCommand GetFunctionCall(string fname, object[] arguments)
			{
				return fname switch
				{
					"ApplyCurve" => ApplyCurve(arguments, log), 
					"AddHeightMap" => AddHeightMap(arguments, log), 
					"Add" => Add(arguments), 
					"Multiply" => Multiply(arguments), 
					"ClampMinMax" => ClampMinMax(arguments), 
					_ => throw new Exception("Function " + fname + " not found!"), 
				};
			}
		}
	}

	public static double[] GetTerrainSamples(PlanetData planet, double[] angles01, double startAngle_Radians, double endAngle_Radians)
	{
		double[] array = planet.terrain.terrainSampler.Calculate(angles01, planet.basics.radius * (Math.PI * 2.0));
		TerrainModule.FlatZone[] flatZones = planet.terrain.flatZones;
		foreach (TerrainModule.FlatZone flatZone in flatZones)
		{
			double num = (flatZone.width + flatZone.transition) / planet.basics.radius / 2.0;
			double num2 = flatZone.angle - num;
			double num3 = flatZone.angle + num;
			if (!(num2 > endAngle_Radians) && !(num3 < startAngle_Radians))
			{
				double num4 = flatZone.width / planet.basics.radius / 2.0;
				double b = flatZone.angle - num4;
				double b2 = flatZone.angle + num4;
				for (int j = 0; j < angles01.Length; j++)
				{
					double value = angles01[j] * (Math.PI * 2.0);
					double value2 = Math.Min(Math_Utility.InverseLerp(num2, b, value), Math_Utility.InverseLerp(num3, b2, value));
					array[j] = Math_Utility.Lerp(array[j], flatZone.height, Math_Utility.Clamp01(value2));
				}
			}
		}
		return array;
	}

	public static double[] GetTextureSamples(PlanetData planet, double[] angles01)
	{
		return planet.terrain.textureSampler.Calculate(angles01, planet.basics.radius * (Math.PI * 2.0));
	}
}
