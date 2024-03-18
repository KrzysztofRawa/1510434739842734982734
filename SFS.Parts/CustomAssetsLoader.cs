using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Cysharp.Threading.Tasks;
using ModLoader;
using ModLoader.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SFS.Builds;
using SFS.Input;
using SFS.IO;
using SFS.Parts.Modules;
using SFS.UI;
using UnityEngine;

namespace SFS.Parts;

public static class CustomAssetsLoader
{
	private class T2DConverter : JsonConverter<Texture2D>
	{
		public override Texture2D ReadJson(JsonReader reader, Type objectType, Texture2D existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.Value == null || string.IsNullOrWhiteSpace((string)reader.Value))
			{
				return null;
			}
			Texture2D texture2D = TextureUtility.FromFile(currentPack.CloneAndExtend("Textures").ExtendToFile((string)reader.Value));
			texture2D.wrapMode = TextureWrapMode.Clamp;
			return texture2D;
		}

		public override void WriteJson(JsonWriter writer, Texture2D value, JsonSerializer serializer)
		{
			string text = value.name + ".png";
			writer.WriteValue(text);
			FilePath filePath = currentPack.CloneAndExtend("Textures").CreateFolder().ExtendToFile(text);
			value.SaveToFile(filePath);
		}
	}

	private class ShadowTextureConverter : JsonConverter<ShadowTexture>
	{
		public override void WriteJson(JsonWriter writer, ShadowTexture value, JsonSerializer serializer)
		{
			writer.WriteValue(value.name);
		}

		public override ShadowTexture ReadJson(JsonReader reader, Type objectType, ShadowTexture existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			return shadowTexturesDictionary.GetValueOrDefault((string)reader.Value);
		}
	}

	private class SpriteConverter : JsonConverter<Sprite>
	{
		public override void WriteJson(JsonWriter writer, Sprite value, JsonSerializer serializer)
		{
			writer.WriteValue((object?)null);
		}

		public override Sprite ReadJson(JsonReader reader, Type objectType, Sprite existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			return null;
		}
	}

	private static bool loadedAssetPacks;

	private static readonly StringBuilder Report = new StringBuilder();

	public static bool finishedLoading;

	private static bool loadedTexturePacks;

	private static bool loadingShadowTextures;

	private static JsonSerializer serializer;

	private static JsonSerializer serializerForShadowTexture;

	private const string UpdateLegacyPackMessage = "Game detected lagacy formatted pack. Do you want to convert it to new format? If not, you will not be able to use this pack";

	private static bool? confirmationResult;

	private static FolderPath currentPack;

	private static Dictionary<string, ShadowTexture> shadowTexturesDictionary;

	public static FolderPath CustomAssetsFolder => Loader.ModsFolder.Extend("Custom Assets").CreateFolder();

	private static async UniTask LoadAssetPacks()
	{
		if (Application.isEditor || !DevSettings.FullVersion || loadedAssetPacks)
		{
			return;
		}
		loadedAssetPacks = true;
		Dictionary<string, ResourceType> resourceTypes = ResourcesLoader.GetFiles_Dictionary<ResourceType>("");
		Dictionary<string, PickCategory> pickCategories = ResourcesLoader.GetFiles_Dictionary<PickCategory>("");
		await UniTask.Yield();
		foreach (FilePath path in CustomAssetsFolder.Extend("Parts").CreateFolder().GetFilesInFolder(recursively: false))
		{
			try
			{
				AssetBundlePack bundlePack = await UniTask.RunOnThreadPool(delegate
				{
					try
					{
						return JsonConvert.DeserializeObject<AssetBundlePack>(path.ReadText());
					}
					catch (Exception exception2)
					{
						Debug.LogException(exception2);
					}
					return (AssetBundlePack)null;
				});
				if (bundlePack == null)
				{
					throw new Exception("Can't deserialize pack file");
				}
				if (bundlePack.Data == null)
				{
					throw new Exception("Pack doesn't support current runtime platform");
				}
				if (bundlePack.CodeAssembly != null)
				{
					await UniTask.RunOnThreadPool(delegate
					{
						try
						{
							Assembly.Load(bundlePack.CodeAssembly);
						}
						catch (Exception exception)
						{
							Debug.Log("Failed to load custom scripts for " + path.CleanFileName + " pack");
							Debug.LogException(exception);
						}
					});
				}
				UnityEngine.Object[] source = (await AssetBundle.LoadFromMemoryAsync(bundlePack.Data)).LoadAllAssets();
				if (!ModsSettings.main.settings.assetPacksActive.ContainsKey(path.FileName))
				{
					ModsSettings.main.settings.assetPacksActive.Add(path.FileName, value: true);
				}
				ModsListElement.ModData data;
				if (source.Any((UnityEngine.Object x) => x.GetType() == typeof(PackData)))
				{
					PackData packData = source.OfType<PackData>().First();
					ModsListElement.ModData modData = default(ModsListElement.ModData);
					modData.name = packData.DisplayName;
					modData.author = packData.Author;
					modData.description = packData.Description;
					modData.icon = (packData.ShowIcon ? packData.Icon : null);
					modData.type = ModsListElement.ModType.AssetsPack;
					modData.version = packData.Version;
					modData.saveName = path.FileName;
					data = modData;
				}
				else
				{
					ModsListElement.ModData modData = default(ModsListElement.ModData);
					modData.name = path.FileName;
					modData.author = "Unknown Author";
					modData.description = "No description";
					modData.icon = null;
					modData.type = ModsListElement.ModType.AssetsPack;
					modData.version = "1.0";
					modData.saveName = path.FileName;
					data = modData;
				}
				ModsMenu.AddElement(data);
				if (!ModsSettings.main.settings.assetPacksActive[path.FileName])
				{
					continue;
				}
				foreach (ResourceType item in source.OfType<ResourceType>())
				{
					if (!resourceTypes.ContainsKey(item.name))
					{
						resourceTypes.Add(item.name, item);
					}
				}
				foreach (PickCategory item2 in source.OfType<PickCategory>())
				{
					if (!pickCategories.ContainsKey(item2.name))
					{
						pickCategories.Add(item2.name, item2);
					}
				}
				foreach (ColorTexture item3 in source.OfType<ColorTexture>())
				{
					if (!Base.partsLoader.colorTextures.ContainsKey(item3.name))
					{
						Base.partsLoader.colorTextures.Add(item3.name, item3);
						continue;
					}
					ColorTexture colorTexture = Base.partsLoader.colorTextures[item3.name];
					item3.multiple = colorTexture.multiple;
					item3.colorTex = colorTexture.colorTex;
					item3.segments = colorTexture.segments;
				}
				foreach (ShapeTexture item4 in source.OfType<ShapeTexture>())
				{
					if (!Base.partsLoader.shapeTextures.ContainsKey(item4.name))
					{
						Base.partsLoader.shapeTextures.Add(item4.name, item4);
						continue;
					}
					ShapeTexture shapeTexture = Base.partsLoader.shapeTextures[item4.name];
					item4.multiple = shapeTexture.multiple;
					item4.shapeTex = shapeTexture.shapeTex;
					item4.segments = shapeTexture.segments;
					item4.shadowTex = shapeTexture.shadowTex;
				}
				foreach (GameObject item5 in source.OfType<GameObject>())
				{
					if (!item5.HasComponent<Part>(out var component))
					{
						continue;
					}
					Variants[] variants = component.variants;
					foreach (Variants variants2 in variants)
					{
						Variants.Variant[] variants3 = variants2.variants;
						for (int j = 0; j < variants3.Length; j++)
						{
							Variants.PickTag[] tags = variants3[j].tags;
							foreach (Variants.PickTag pickTag in tags)
							{
								pickTag.tag = pickCategories.GetValueOrDefault(pickTag.tag.name, pickTag.tag);
							}
						}
						variants2.tags = Array.Empty<Variants.PickTag>();
					}
					FlowModule[] componentsInChildren = item5.GetComponentsInChildren<FlowModule>();
					for (int i = 0; i < componentsInChildren.Length; i++)
					{
						FlowModule.Flow[] sources = componentsInChildren[i].sources;
						foreach (FlowModule.Flow flow in sources)
						{
							flow.resourceType = resourceTypes.GetValueOrDefault(flow.resourceType.name, flow.resourceType);
						}
					}
					ResourceModule[] componentsInChildren2 = item5.GetComponentsInChildren<ResourceModule>();
					foreach (ResourceModule resourceModule in componentsInChildren2)
					{
						resourceModule.resourceType = resourceTypes.GetValueOrDefault(resourceModule.resourceType.name, resourceModule.resourceType);
					}
					if (!Base.partsLoader.parts.ContainsKey(component.name))
					{
						Base.partsLoader.parts.Add(component.name, component);
					}
					for (int l = 0; l < component.variants.Length; l++)
					{
						for (int m = 0; m < component.variants[l].variants.Length; m++)
						{
							VariantRef variantRef = new VariantRef(component, l, m);
							if (!Base.partsLoader.partVariants.ContainsKey(variantRef.GetNameID()))
							{
								Base.partsLoader.partVariants.Add(variantRef.GetNameID(), variantRef);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Report.AppendLine("Failed to load asset pack: " + path);
				Report.AppendLine(ex.Message);
			}
		}
		GC.Collect();
	}

	public static async UniTask LoadAllCustomAssets()
	{
		await LoadAssetPacks();
		await LoadTexturePacks();
		if (Report.Length > 0)
		{
			Menu.read.ShowReport(Report, delegate
			{
			});
		}
		finishedLoading = true;
	}

	private static async UniTask LoadTexturePacks()
	{
		if (Application.isEditor || !DevSettings.FullVersion || loadedTexturePacks)
		{
			return;
		}
		loadedTexturePacks = true;
		FolderPath folderPath = CustomAssetsFolder.Extend("Texture Packs").CreateFolder();
		serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
		{
			MaxDepth = 10,
			MissingMemberHandling = MissingMemberHandling.Ignore,
			Converters = new List<JsonConverter>
			{
				new T2DConverter(),
				new ShadowTextureConverter(),
				new SpriteConverter()
			},
			Formatting = Formatting.Indented
		});
		serializerForShadowTexture = JsonSerializer.CreateDefault(new JsonSerializerSettings
		{
			MaxDepth = 10,
			MissingMemberHandling = MissingMemberHandling.Ignore,
			Converters = new List<JsonConverter>
			{
				new T2DConverter(),
				new SpriteConverter()
			},
			Formatting = Formatting.Indented
		});
		shadowTexturesDictionary = ResourcesLoader.GetFiles_Dictionary<ShadowTexture>("");
		CreateExampleTexturePack(folderPath);
		foreach (FolderPath item in folderPath.GetFoldersInFolder(recursively: false))
		{
			if (!(item.FolderName == "Example"))
			{
				await LoadTexturePack(item);
			}
		}
	}

	private static async UniTask<bool> UpdateLegacyFormat(FolderPath pack)
	{
		if (pack.CloneAndExtend("Textures").FolderExists())
		{
			return true;
		}
		if (!confirmationResult.HasValue)
		{
			confirmationResult = await MenuGenerator.OpenConfirmationAsync(CloseMode.Current, () => "Game detected lagacy formatted pack. Do you want to convert it to new format? If not, you will not be able to use this pack", () => "Update", () => "Cancel");
		}
		if (!confirmationResult.Value)
		{
			Debug.Log("Legacy pack update cancelled");
			return false;
		}
		Debug.Log("Updating legacy pack");
		FolderPath textures = pack.CloneAndExtend("Textures").CreateFolder();
		FolderPath folderPath = pack.CloneAndExtend("Color Textures");
		FolderPath folderPath2 = pack.CloneAndExtend("Shadow Textures");
		FolderPath folderPath3 = pack.CloneAndExtend("Shape Textures");
		if (folderPath.FolderExists())
		{
			foreach (FolderPath item in folderPath.GetFoldersInFolder(recursively: false))
			{
				MoveAllFiles(item, folderPath);
			}
		}
		if (folderPath2.FolderExists())
		{
			foreach (FolderPath item2 in folderPath2.GetFoldersInFolder(recursively: false))
			{
				MoveAllFiles(item2, folderPath2);
			}
		}
		if (folderPath3.FolderExists())
		{
			foreach (FolderPath item3 in folderPath3.GetFoldersInFolder(recursively: false))
			{
				MoveAllFiles(item3, folderPath3);
			}
		}
		if (!pack.ExtendToFile("pack_info.txt").FileExists())
		{
			return true;
		}
		JToken value = JObject.Parse(await pack.ExtendToFile("pack_info.txt").ReadTextAsync()).GetValue("Icon");
		if (value == null)
		{
			return true;
		}
		FilePath filePath = pack.ExtendToFile((string?)value);
		FilePath path = pack.CloneAndExtend("Textures").ExtendToFile((string?)value);
		filePath.Move(path);
		return true;
		void MoveAllFiles(FolderPath textureFolder, FolderPath to)
		{
			foreach (FilePath item4 in textureFolder.GetFilesInFolder(recursively: false))
			{
				if (item4.Extension == "txt" && item4.FileName == "config.txt")
				{
					string folderName = textureFolder.FolderName;
					item4.Move(to.ExtendToFile(folderName + ".txt"));
				}
				else
				{
					item4.Move(textures.ExtendToFile(item4.FileName));
				}
			}
			textureFolder.DeleteFolder();
		}
	}

	private static void CreateExampleTexturePack(FolderPath basePath)
	{
		FolderPath folderPath = basePath.CloneAndExtend("Example");
		if (folderPath.FolderExists())
		{
			if (folderPath.CloneAndExtend("Textures").FolderExists())
			{
				return;
			}
			folderPath.DeleteFolder();
		}
		folderPath.CreateFolder();
		FolderPath folderPath2 = folderPath.CloneAndExtend("Color Textures").CreateFolder();
		FolderPath folderPath3 = folderPath.CloneAndExtend("Shadow Textures").CreateFolder();
		FolderPath folderPath4 = folderPath.CloneAndExtend("Shape Textures").CreateFolder();
		currentPack = folderPath;
		foreach (KeyValuePair<string, ColorTexture> colorTexture in Base.partsLoader.colorTextures)
		{
			serializer.SerializeTo(folderPath2.ExtendToFile(colorTexture.Key + ".txt"), colorTexture.Value, typeof(ColorTexture));
		}
		foreach (KeyValuePair<string, ShapeTexture> shapeTexture in Base.partsLoader.shapeTextures)
		{
			serializer.SerializeTo(folderPath4.ExtendToFile(shapeTexture.Key + ".txt"), shapeTexture.Value, typeof(ShapeTexture));
		}
		foreach (KeyValuePair<string, ShadowTexture> item in shadowTexturesDictionary)
		{
			serializerForShadowTexture.SerializeTo(folderPath3.ExtendToFile(item.Key + ".txt"), item.Value, typeof(ShadowTexture));
		}
		serializer.SerializeTo(folderPath.ExtendToFile("pack_info.txt"), PackData.ExampleTexturePack(), typeof(PackData));
	}

	private static async UniTask LoadTexturePack(FolderPath packPath)
	{
		if (!(await UpdateLegacyFormat(packPath)))
		{
			return;
		}
		if (!ModsSettings.main.settings.texturePacksActive.ContainsKey(packPath.FolderName))
		{
			ModsSettings.main.settings.texturePacksActive.Add(packPath.FolderName, value: true);
		}
		currentPack = packPath;
		FilePath filePath = packPath.ExtendToFile("pack_info.txt");
		ModsListElement.ModData data;
		if (filePath.FileExists())
		{
			try
			{
				PackData packData = ScriptableObject.CreateInstance<PackData>();
				JObject.Parse(await filePath.ReadTextAsync()).Populate(packData, serializer);
				ModsListElement.ModData modData = default(ModsListElement.ModData);
				modData.author = packData.Author;
				modData.description = packData.Description;
				modData.icon = packData.Icon;
				modData.name = packData.DisplayName;
				modData.type = ModsListElement.ModType.TexturesPack;
				modData.version = packData.Version;
				modData.saveName = packPath.FolderName;
				data = modData;
			}
			catch (Exception exception)
			{
				Report.AppendLine("Failed to load texture pack info file: " + packPath.FolderName);
				Debug.LogException(exception);
				ModsListElement.ModData modData = default(ModsListElement.ModData);
				modData.name = packPath.FolderName;
				modData.author = "Unknown Author";
				modData.description = "No description";
				modData.icon = null;
				modData.type = ModsListElement.ModType.TexturesPack;
				modData.version = "1.0";
				modData.saveName = packPath.FolderName;
				data = modData;
			}
		}
		else
		{
			ModsListElement.ModData modData = default(ModsListElement.ModData);
			modData.name = packPath.FolderName;
			modData.author = "Unknown Author";
			modData.description = "No description";
			modData.icon = null;
			modData.type = ModsListElement.ModType.TexturesPack;
			modData.version = "1.0";
			modData.saveName = packPath.FolderName;
			data = modData;
		}
		ModsMenu.AddElement(data);
		if (!ModsSettings.main.settings.texturePacksActive[packPath.FolderName])
		{
			return;
		}
		FolderPath folderPath = packPath.CloneAndExtend("Color Textures").CreateFolder();
		FolderPath shadowTextures = packPath.CloneAndExtend("Shadow Textures").CreateFolder();
		FolderPath shapeTextures = packPath.CloneAndExtend("Shape Textures").CreateFolder();
		if (folderPath.FolderExists())
		{
			foreach (FilePath colorTextureFile3 in folderPath.GetFilesInFolder(recursively: false))
			{
				try
				{
					ColorTexture colorTexture = ScriptableObject.CreateInstance<ColorTexture>();
					JObject.Parse(await colorTextureFile3.ReadTextAsync()).Populate(colorTexture, serializer);
					Base.partsLoader.colorTextures.Add(colorTexture.name, colorTexture);
				}
				catch (Exception ex)
				{
					Report.AppendLine("Failed to load " + colorTextureFile3.CleanFileName + " texture in " + packPath.FolderName + " pack:");
					Report.AppendLine(ex.Message);
				}
			}
		}
		if (shadowTextures.FolderExists())
		{
			foreach (FilePath colorTextureFile3 in shadowTextures.GetFilesInFolder(recursively: false))
			{
				try
				{
					ShadowTexture shadowTexture = ScriptableObject.CreateInstance<ShadowTexture>();
					JObject.Parse(await colorTextureFile3.ReadTextAsync()).Populate(shadowTexture, serializerForShadowTexture);
					shadowTexturesDictionary.Add(shadowTexture.name, shadowTexture);
				}
				catch (Exception ex2)
				{
					Report.AppendLine("Failed to load " + colorTextureFile3.CleanFileName + " texture in " + packPath.FolderName + " pack:");
					Report.AppendLine(ex2.Message);
				}
			}
		}
		if (!shapeTextures.FolderExists())
		{
			return;
		}
		foreach (FilePath colorTextureFile3 in shapeTextures.GetFilesInFolder(recursively: false))
		{
			try
			{
				ShapeTexture shapeTexture = ScriptableObject.CreateInstance<ShapeTexture>();
				JObject.Parse(await colorTextureFile3.ReadTextAsync()).Populate(shapeTexture, serializer);
				Base.partsLoader.shapeTextures.Add(shapeTexture.name, shapeTexture);
			}
			catch (Exception ex3)
			{
				Report.AppendLine("Failed to load " + colorTextureFile3.CleanFileName + " texture in " + packPath.FolderName + " pack:");
				Report.AppendLine(ex3.Message);
			}
		}
	}
}
