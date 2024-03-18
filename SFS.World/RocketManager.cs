using System;
using System.Collections.Generic;
using System.Linq;
using SFS.Builds;
using SFS.Parts;
using SFS.Parts.Modules;
using SFS.Stats;
using SFS.Tutorials;
using SFS.WorldBase;
using UnityEngine;

namespace SFS.World;

public class RocketManager : MonoBehaviour
{
	public Rocket rocketPrefab;

	private static Rocket prefab;

	public GameObject spawner;

	private void Awake()
	{
		prefab = rocketPrefab;
	}

	private void Start()
	{
	}

	public static void SpawnBlueprint(Blueprint blueprint)
	{
		WorldView.main.SetViewLocation(Base.planetLoader.spaceCenter.LaunchPadLocation);
		if (blueprint.rotation != 0f)
		{
			PartSave[] parts = blueprint.parts;
			foreach (PartSave obj in parts)
			{
				obj.orientation += new Orientation(1f, 1f, blueprint.rotation);
				obj.position *= new Orientation(1f, 1f, blueprint.rotation);
			}
		}
		OwnershipState[] ownershipState;
		Part[] array = PartsLoader.CreateParts(blueprint.parts, null, null, OnPartNotOwned.Delete, out ownershipState);
		Part[] array2 = array.Where((Part a) => a != null).ToArray();
		if (blueprint.rotation != 0f)
		{
			PartSave[] parts = blueprint.parts;
			foreach (PartSave obj2 in parts)
			{
				obj2.orientation += new Orientation(1f, 1f, 0f - blueprint.rotation);
				obj2.position *= new Orientation(1f, 1f, 0f - blueprint.rotation);
			}
		}
		Part_Utility.PositionParts(WorldView.ToLocalPosition(Base.planetLoader.spaceCenter.LaunchPadLocation.position), new Vector2(0.5f, 0f), round: true, useLaunchBounds: true, array2);
		new JointGroup(GenerateJoints(array2), array2.ToList()).RecreateGroups(out var newGroups);
		Rocket[] array3 = SpawnRockets(newGroups);
		Staging.CreateStages(blueprint.stages, array);
		Rocket rocket = array3.FirstOrDefault((Rocket a) => a.hasControl.Value);
		PlayerController.main.player.Value = ((rocket != null) ? rocket : ((array3.Length != 0) ? array3[0] : null));
	}

	public static List<PartJoint> GenerateJoints(Part[] parts)
	{
		(Line2[] surfaces, ConvexPolygon[] polygons)[] data = new(Line2[], ConvexPolygon[])[parts.Length];
		(bool valid, Rect rect)[] bounds = new(bool, Rect)[parts.Length];
		Vector2 min2 = Vector2.positiveInfinity;
		Vector2 max2 = Vector2.negativeInfinity;
		for (int i = 0; i < parts.Length; i++)
		{
			Part part = parts[i];
			data[i] = (surfaces: part.GetAttachmentSurfacesWorld(), polygons: part.GetBuildColliderPolygons(forAttach: true).Item1);
			Vector2 min = Vector2.positiveInfinity;
			Vector2 max = Vector2.negativeInfinity;
			data[i].surfaces.ForEach(delegate(Line2 line)
			{
				Part_Utility.ExpandToFitPoint(ref min, ref max, line.start, line.end);
			});
			data[i].polygons.ForEach(delegate(ConvexPolygon poly)
			{
				Part_Utility.ExpandToFitPoint(ref min, ref max, poly.points);
			});
			min -= Vector2.one * 0.2f;
			max += Vector2.one * 0.2f;
			bounds[i] = (valid: !double.IsPositiveInfinity(min.x), rect: new Rect(min, max - min));
			Part_Utility.ExpandToFitPoint(ref min2, ref max2, min, max);
		}
		min2 -= Vector2.one * 0.2f;
		max2 += Vector2.one * 0.2f;
		List<int> list = new List<int>();
		List<int>[,] array = new List<int>[8, 12];
		for (int j = 0; j < array.GetLength(0); j++)
		{
			for (int k = 0; k < array.GetLength(1); k++)
			{
				array[j, k] = new List<int>();
			}
		}
		Vector2 vector = max2 - min2;
		for (int l = 0; l < parts.Length; l++)
		{
			var (flag, rect) = bounds[l];
			if (!flag)
			{
				continue;
			}
			int num = Mathf.FloorToInt((rect.xMin - min2.x) / vector.x * (float)array.GetLength(0));
			int num2 = Mathf.FloorToInt((rect.xMax - min2.x) / vector.x * (float)array.GetLength(0));
			int num3 = Mathf.FloorToInt((rect.yMin - min2.y) / vector.y * (float)array.GetLength(1));
			int num4 = Mathf.FloorToInt((rect.yMax - min2.y) / vector.y * (float)array.GetLength(1));
			if ((num2 - num + 1) * (num4 - num3 + 1) > 4)
			{
				list.Add(l);
				continue;
			}
			for (int m = num; m <= num2; m++)
			{
				for (int n = num3; n <= num4; n++)
				{
					array[m, n].Add(l);
				}
			}
		}
		List<PartJoint> joints = new List<PartJoint>();
		HashSet<(int, int)> connections = new HashSet<(int, int)>();
		Create(list, list, same: true);
		for (int num5 = 0; num5 < array.GetLength(0); num5++)
		{
			for (int num6 = 0; num6 < array.GetLength(1); num6++)
			{
				List<int> list2 = array[num5, num6];
				Create(list2, list2, same: true);
				Create(list2, list, same: false);
			}
		}
		return joints;
		void Create(List<int> chunk_A, List<int> chunk_B, bool same)
		{
			for (int num7 = 0; num7 < chunk_A.Count; num7++)
			{
				int num8 = chunk_A[num7];
				Part part2 = parts[num8];
				Line2[] item = data[num8].surfaces;
				ConvexPolygon[] item2 = data[num8].polygons;
				for (int num9 = (same ? (num7 + 1) : 0); num9 < chunk_B.Count; num9++)
				{
					int num10 = chunk_B[num9];
					if (bounds[num8].valid && bounds[num10].valid && bounds[num8].rect.Overlaps(bounds[num10].rect))
					{
						Part part3 = parts[num10];
						if (part2.IsFront() == part3.IsFront())
						{
							int instanceID = part2.GetInstanceID();
							int instanceID2 = part3.GetInstanceID();
							(int, int) item3 = (Math.Min(instanceID, instanceID2), Math.Max(instanceID, instanceID2));
							if (!connections.Contains(item3))
							{
								Line2[] item4 = data[num10].surfaces;
								ConvexPolygon[] item5 = data[num10].polygons;
								if (SurfaceUtility.SurfacesConnect(item, item4, out var _, out var _) || Polygon.Intersect(item2, item5, -0.08f))
								{
									joints.Add(new PartJoint(part2, part3, part3.Position - part2.Position));
									connections.Add(item3);
								}
							}
						}
					}
				}
			}
		}
	}

	private static Rocket[] SpawnRockets(List<JointGroup> groups)
	{
		List<Rocket> list = new List<Rocket>();
		foreach (JointGroup group in groups)
		{
			Location location = GetSpawnLocation(group);
			Rocket rocket = CreateRocket(group, "", throttleOn: false, 0.5f, RCS: false, 0f, 0f, (Rocket a) => location, physicsMode: false);
			list.Add(rocket);
			rocket.stats.Load(-1);
		}
		return list.ToArray();
	}

	private static Location GetSpawnLocation(JointGroup group)
	{
		Vector2 zero = Vector2.zero;
		float num = 0f;
		foreach (Part part in group.parts)
		{
			zero += (Vector2)part.transform.TransformPoint(part.centerOfMass.Value) * part.mass.Value;
			num += part.mass.Value;
		}
		zero /= num;
		return new Location(Base.planetLoader.spaceCenter.LaunchPadLocation.planet, WorldView.ToGlobalPosition(zero));
	}

	public static void LoadRocket(RocketSave rocketSave, out bool hasNonOwnedParts)
	{
		hasNonOwnedParts = false;
		if (rocketSave.location.address.HasPlanet())
		{
			OwnershipState[] ownershipState;
			Part[] parts = PartsLoader.CreateParts(rocketSave.parts, null, null, OnPartNotOwned.UsePlaceholder, out ownershipState);
			hasNonOwnedParts = ownershipState.Any((OwnershipState a) => a != OwnershipState.OwnedAndUnlocked);
			Rocket rocket = CreateRocket(new JointGroup(rocketSave.joints.Select((JointSave a) => new PartJoint(parts[a.partIndex_A], parts[a.partIndex_B], parts[a.partIndex_B].Position - parts[a.partIndex_A].Position)).ToList(), parts.ToList()), rocketSave.rocketName, rocketSave.throttleOn, rocketSave.throttlePercent, rocketSave.RCS, rocketSave.rotation, rocketSave.angularVelocity, (Rocket a) => rocketSave.location.GetSaveLocation(WorldTime.main.worldTime), physicsMode: false);
			rocket.staging.Load(rocketSave.stages, rocket.partHolder.GetArray(), record: false);
			rocket.staging.editMode.Value = rocketSave.staging_EditMode;
			rocket.stats.Load(rocketSave.branch);
		}
	}

	public static Rocket CreateRocket_Child(JointGroup jointGroup, Rocket parentRocket, Vector2 offset)
	{
		Vector2 rootPosition = jointGroup.parts[0].transform.localPosition;
		Rocket rocket = CreateRocket(jointGroup, parentRocket.rocketName, parentRocket.throttle.throttleOn, parentRocket.throttle.throttlePercent, parentRocket.arrowkeys.rcs, parentRocket.partHolder.transform.eulerAngles.z, parentRocket.rb2d.angularVelocity, GetLocation, physicsMode: true);
		StatsRecorder.OnSplit(parentRocket.stats, rocket.stats);
		Staging.OnSplit(parentRocket, rocket);
		return rocket;
		Location GetLocation(Rocket childRocket)
		{
			Vector2 localPosition = parentRocket.partHolder.transform.TransformPoint(rootPosition + childRocket.rb2d.centerOfMass - offset);
			return new Location(parentRocket.location.planet, WorldView.ToGlobalPosition(localPosition), parentRocket.location.velocity);
		}
	}

	private static Rocket CreateRocket(JointGroup jointGroup, string rocketName, bool throttleOn, float throttlePercent, bool RCS, float rotation, float angularVelocity, Func<Rocket, Location> location, bool physicsMode)
	{
		Rocket rocket = UnityEngine.Object.Instantiate(prefab);
		rocket.rocketName = rocketName;
		rocket.throttle.throttleOn.Value = throttleOn;
		rocket.throttle.throttlePercent.Value = throttlePercent;
		rocket.arrowkeys.rcs.Value = RCS;
		rocket.SetJointGroup(jointGroup);
		rocket.rb2d.transform.eulerAngles = new Vector3(0f, 0f, rotation);
		rocket.physics.SetLocationAndState(location(rocket), physicsMode);
		rocket.rb2d.angularVelocity = angularVelocity;
		return rocket;
	}

	public static void MergeRockets(Rocket rocket_A, Part part_A, Rocket rocket_B, Part part_B, Vector2 anchor)
	{
		if (rocket_A == rocket_B)
		{
			return;
		}
		float num = rocket_A.rb2d.mass + rocket_B.rb2d.mass;
		Vector2 velocity = (rocket_A.rb2d.velocity * rocket_A.rb2d.mass + rocket_B.rb2d.velocity * rocket_B.rb2d.mass) / num;
		float angularVelocity = (rocket_A.rb2d.angularVelocity * rocket_A.rb2d.mass + rocket_B.rb2d.angularVelocity * rocket_B.rb2d.mass) / num;
		rocket_A.jointsGroup.parts.AddRange(rocket_B.jointsGroup.parts);
		foreach (PartJoint joint in rocket_B.jointsGroup.joints)
		{
			rocket_A.jointsGroup.AddJoint(joint);
		}
		rocket_A.jointsGroup.AddJoint(new PartJoint(part_A, part_B, anchor));
		rocket_A.SetParts(rocket_A.jointsGroup.parts.ToArray());
		StatsRecorder.OnMerge(rocket_A.stats, rocket_B.stats);
		Staging.OnMerge(rocket_A, rocket_B);
		if (rocket_A.rocketName == "" && rocket_B.rocketName != "")
		{
			rocket_A.rocketName = rocket_B.rocketName;
		}
		if ((bool)rocket_B.isPlayer)
		{
			PlayerController.main.player.Value = rocket_A;
		}
		rocket_A.rb2d.velocity = velocity;
		rocket_A.rb2d.angularVelocity = angularVelocity;
		DestroyRocket(rocket_B, DestructionReason.Intentional);
	}

	public static void DestroyRocket(Rocket rocket, DestructionReason reason)
	{
		if ((bool)rocket.isPlayer)
		{
			FailureMenu.main.OnPlayerDestroyed(rocket, reason);
		}
		UnityEngine.Object.Destroy(rocket.gameObject);
	}
}
