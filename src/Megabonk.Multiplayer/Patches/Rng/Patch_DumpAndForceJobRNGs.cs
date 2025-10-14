// File: Patch_DumpAndForceJobRNGs.cs
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Megabonk.Multiplayer
{
    [HarmonyPatch]
    public static class Patch_DumpAndForceJobRNGs
    {
        [HarmonyTargetMethods]
		public static IEnumerable<MethodBase> TargetMethods()
		{
			string[] mapFragments =
			{
				"ProceduralTileGeneration",
				"GridPathGenerator",
				"Noise",
				"TextureGenerator",
				"MeshGenerator",
				"MapGenerator",
				"MapDisplay"
			};

			var mapMethodNames = new HashSet<string>(new[]
			{
				"Generate", "Schedule", "Create", "CreateJobs",
				"Run", "Start", "Execute", "GenerateMap"
			}, StringComparer.Ordinal);

			string[] spawnPrefixes =
			{
				"Assets.Scripts.Game.Spawning.",
				"Assets.Scripts.Actors.Enemies."
			};

			var explicitSpawnTypes = new HashSet<string>(new[]
			{
				"Assets.Scripts.Managers.EnemyManager",
				"EnemySpawner",
				"SpawnPlayerPortal",
				"Assets.Scripts.Game.Spawning.New.BaseSummoner",
				"Assets.Scripts.Game.Spawning.New.SummonerController",
				"Assets.Scripts.Game.Spawning.New.Summoners.BossSummoner",
				"Assets.Scripts.Game.Spawning.New.Summoners.ChallengeSummoner",
				"Assets.Scripts.Game.Spawning.New.Summoners.SpecialSkeletonSummoner",
				"Assets.Scripts.Game.Spawning.New.Summoners.StageSummoner",
				"Assets.Scripts.Game.Spawning.New.Summoners.SwarmSummoner",
				"Assets.Scripts.Inventory__Items__Pickups.PickupManager",
				"Assets.Scripts.Inventory__Items__Pickups.PickupOrb",
				"Assets.Scripts.Inventory__Items__Pickups.Pickup"
			}, StringComparer.Ordinal);

			var spawnMethodNames = new HashSet<string>(new[]
			{
				"Awake", "Start", "OnEnable",
				"Setup", "Configure", "Compose", "Populate", "Build",
				"Spawn", "SpawnNext", "SpawnEnemy", "SpawnBoss", "SpawnStageBoss",
				"SpawnPickup", "StartEvent", "EventSwarm", "EventMiniboss",
				"TrySpawn", "TrySummon",
				"RefreshCardWeights", "AddCredits",
				"Execute", "Run"
			}, StringComparer.Ordinal);

			var excludedNameFragments = new[]
			{
				"GenerateCards",
				"GenerateCard",
				"GenerateSpawnCards",
				"GenerateEnemyCards"
			};

			var typeSpecificMethods = new (string fragment, string[] methods)[]
			{
				("Assets.Scripts.Game.Spawning.SpawnPositions", new[]
				{
					"GetEnemySpawnPositionAroundPoint",
					"GetEnemySpawnPositionBiased",
					"GetEnemySpawnPositionTest",
					"GetObjectSpawnPosition",
					"SampleBiasedDirection",
					"GetEnemySpawnPosition" // calls biased helper internally; ensure wrapper covered
				}),
				("Assets.Scripts.Game.MapGeneration.MapEvents.MapEvents", new[]
				{
					"Init",
					"Tick"
				}),
				("Assets.Scripts.Game.MapGeneration.MapEvents.MapEventsDesert", new[]
				{
					"Init",
					"StartStorm",
					"TickStorms"
				}),
				("Assets.Scripts.Game.Spawning.New.SummonerController", new[]
				{
					"EventMiniboss",
					"SpawnStageBoss"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.Chests.InteractableChest", new[]
				{
					"Start"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.InventoryUtility", new[]
				{
					"GetRandomItemsShadyGuy"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.Rarity", new[]
				{
					"GetItemRarity",
					"GetEncounterOfferRarity",
					"GetShadyGuyRarity"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.Upgrades.EncounterUtility", new[]
				{
					"GetRandomStatOffers",
					"GetRandomStatsBalanceShrine",
					"GetBalanceShrineOffers"
				})
			};

			var yielded = new HashSet<MethodBase>();

			// only patch inside your main game assembly
			var gameAssemblies = AppDomain.CurrentDomain.GetAssemblies()
				.Where(a =>
				{
					var n = a.GetName().Name ?? "";
					return n.Equals("Assembly-CSharp", StringComparison.OrdinalIgnoreCase)
						|| n.Contains("Megabonk", StringComparison.OrdinalIgnoreCase)
						|| n.Contains("Ved", StringComparison.OrdinalIgnoreCase);
				});

			foreach (var asm in gameAssemblies)
			{
				Type[] allTypes;
				try { allTypes = asm.GetTypes(); }
				catch (ReflectionTypeLoadException ex) { allTypes = ex.Types.Where(x => x != null).ToArray(); }

				foreach (var type in allTypes)
				{
					if (type == null || string.IsNullOrEmpty(type.FullName))
						continue;
					string fullName = type.FullName;

					bool matchesMap = mapFragments.Any(f => fullName.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0);
					bool matchesSpawn =
						spawnPrefixes.Any(prefix => fullName.StartsWith(prefix, StringComparison.Ordinal)) ||
						explicitSpawnTypes.Contains(fullName);
					bool matchesSpecial = typeSpecificMethods.Any(entry =>
						fullName.IndexOf(entry.fragment, StringComparison.OrdinalIgnoreCase) >= 0);

					if (fullName.IndexOf(".Inventory__Items__Pickups.Items.", StringComparison.OrdinalIgnoreCase) >= 0 ||
					    fullName.IndexOf("ItemImplementations", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						// Skip individual item scripts; they run per item init and don't drive world spawning.
						continue;
					}

					if (!matchesMap && !matchesSpawn && !matchesSpecial)
						continue;
					if (type.ContainsGenericParameters)
						continue;

					MethodInfo[] allMethods;
					try { allMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
					catch { continue; }

					var methodSet = new HashSet<string>(StringComparer.Ordinal);
					if (matchesMap)
						methodSet.UnionWith(mapMethodNames);
					if (matchesSpawn)
						methodSet.UnionWith(spawnMethodNames);
					foreach (var entry in typeSpecificMethods)
					{
						if (fullName.IndexOf(entry.fragment, StringComparison.OrdinalIgnoreCase) >= 0)
						{
							methodSet.UnionWith(entry.methods);
						}
					}
					if (methodSet.Count == 0)
						continue;

					foreach (var method in allMethods)
					{
						if (method == null || method.IsSpecialName)
							continue;

						if (excludedNameFragments.Any(ex => method.Name.IndexOf(ex, StringComparison.OrdinalIgnoreCase) >= 0))
							continue;

						if (!methodSet.Contains(method.Name))
							continue;
						if (method.ContainsGenericParameters)
							continue;

						if (yielded.Add(method))
							MultiplayerPlugin.LogS.LogDebug($"[JOBRNG] Hooking {fullName}.{method.Name}()");

						yield return method;
					}
				}
			}
		}


        [HarmonyPrefix]
        public static void Prefix(object __instance, MethodBase __originalMethod)
        {
            try
            {
                string typeName = __instance?.GetType()?.FullName ?? "<null>";
                string methodName = __originalMethod?.Name ?? "<unknown>";
                MultiplayerPlugin.LogS.LogDebug($"[JOBRNG] Prefix {typeName}.{methodName}()");

                Patch_ForceJobRNGs.ApplyToObject(__instance);
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogError($"[JOBRNG] Error forcing job RNGs ({__originalMethod?.DeclaringType?.FullName}.{__originalMethod?.Name}): {e}");
            }
        }
    }
}
