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
        private static readonly Dictionary<MethodBase, int> MethodInvocationCounts = new();
        private static readonly object MethodInvocationLock = new();
        private static int _lastSeed = int.MinValue;
        [ThreadStatic] private static Stack<MethodContext> _contextStack;

        internal readonly struct MethodContext
        {
            public MethodBase Method { get; }
            public int CallIndex { get; }
            public bool SeededUnityRandom { get; }

            public MethodContext(MethodBase method, int callIndex, bool seededUnityRandom)
            {
                Method = method;
                CallIndex = callIndex;
                SeededUnityRandom = seededUnityRandom;
            }
        }

        internal static bool TryGetCurrentContext(out MethodContext context)
        {
            var stack = _contextStack;
            if (stack != null && stack.Count > 0)
            {
                context = stack.Peek();
                return true;
            }

            context = default;
            return false;
        }

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
				("Assets.Scripts.Actors.Enemies.Enemy", new[]
				{
					"TryTeleport"
				}),
				("Assets.Scripts.Game.Combat.EnemySpecialAttacks.EnemyProjectileMud", new[]
				{
					"Init",
					"SpawnHitEffect"
				}),
				("Assets.Scripts.Game.Combat.EnemySpecialAttacks.Implementations.EnemySpecialAttackPrefabSingle", new[]
				{
					"Init",
					"SpawnHitEffect"
				}),
				("Assets.Scripts.Game.Combat.EnemySpecialAttacks.Implementations.EnemySpecialAttackFollowing", new[]
				{
					"SpawnHitEffect"
				}),
				("Assets.Scripts.Game.Combat.EnemySpecialAttacks.Implementations.EnemySpecialAttackFollowing+<DoAttack>d__5", new[]
				{
					"MoveNext"
				}),
				("Assets.Scripts.Game.Combat.ProjectileExplosion", new[]
				{
					"CheckZone"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.AbilitiesPassive.Implementations.PassiveAbilityFlex", new[]
				{
					"UseFlex"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.AbilitiesPassive.Implementations.PassiveAbilityZooma", new[]
				{
					"OnEnemyDamage"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.Rarity", new[]
				{
					"GetItemRarity",
					"GetEncounterOfferRarity",
					"GetShadyGuyRarity"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.GoldAndMoney.MoneyUtility", new[]
				{
					"SpawnSilver",
					"SpawnSilverNoTimerImpact"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.Interactables.DetectInteractables", new[]
				{
					"FixedUpdate"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.Upgrades.EncounterUtility", new[]
				{
					"GetRandomStatOffers",
					"GetRandomStatsBalanceShrine",
					"GetBalanceShrineOffers"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.Items.ItemImplementations.ItemBorgor", new[]
				{
					"OnEnemyDied"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.Items.ItemImplementations.ItemCactus", new[]
				{
					"OnTakeDamage"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.Items.ItemImplementations.ItemSuckyHoof", new[]
				{
					"Tick"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.Weapons.Attacks.WeaponAttack", new[]
				{
					"SpawnProjectile"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.Weapons.EnemyTargeting", new[]
				{
					"GetClosestEnemy",
					"GetEnemiesInRadius",
					"GetRandomEnemy",
					"GetSmartEnemy"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.Weapons.Firefield", new[]
				{
					"Set"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.Weapons.Projectiles.ProjectileBase", new[]
				{
					"CheckSpawnCollision",
					"HitOther",
					"StepMovement"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.Weapons.Projectiles.ProjectileDexecutioner", new[]
				{
					"CheckZone"
				}),
				("Assets.Scripts.Inventory__Items__Pickups.Weapons.WeaponUtility", new[]
				{
					"ConecastEnemyInVision",
					"ChainLightning",
					"GetEnemiesInRadius",
					"GetIndex0EnemyInRange",
					"GetRandomEnemyInRange",
					"GetRandomEnemyInRadius"
				}),
				("Assets.Scripts.MapGeneration.ProceduralTiles.MazeHeightGenerator", new[]
				{
					"GenerateHeight",
					"GenerateHeightHighlands",
					"GenerateHeightMountains"
				}),
				("Assets.Scripts.MapGeneration.ProceduralTiles.Maze", new[]
				{
					"Generate",
					"Shuffle"
				}),
				("Assets.Scripts.MapGeneration.RandomMapObject", new[]
				{
					"GetAmount"
				}),
				("ProceduralTileGeneration", new[]
				{
					"Generate",
					"FillEdges",
					"FillEdge",
					"FillEdgesIsland",
					"FillEdgesTrees",
					"FillEdgesWalls",
					"FillHoles",
					"FillHole",
					"FillWallSlopeToFlat",
					"FillWallSlopToSlop",
					"InstantiateFillTile",
					"InstantiateTile"
				}),
				("RandomObjectPlacer", new[]
				{
					"Generate",
					"GenerateInteractables",
					"RandomObjectSpawner"
				}),
				("SpawnInteractables", new[]
				{
					"SpawnChests",
					"SpawnOther",
					"SpawnRails",
					"SpawnShrines",
					"SpawnShit",
					"SetArea"
				}),
				("EffectManager", new[]
				{
					"OnMapGenerationComplete"
				}),
				("PlayerRenderer", new[]
				{
					"Update"
				}),
				("MapGenerationController+<GenerateMap>d__22", new[] { "MoveNext" }),
				("MapGenerationFinalBoss+<GenerateMap>d__14", new[] { "MoveNext" })
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
					const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
					try { allMethods = type.GetMethods(flags); }
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

                        if (!yielded.Add(method))
                            continue;

                        if (MultiplayerPlugin.VerboseJobRng)
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
                string methodName = __originalMethod?.Name ?? "<unknown>";
                bool isStatic = __originalMethod?.IsStatic ?? false;
                Type declaringType = __originalMethod?.DeclaringType;

                int callIndex = 0;
                if (__originalMethod != null)
                    callIndex = NextInvocationIndex(__originalMethod);

                object scopeTarget = __instance ?? (object?)declaringType;
                bool seededUnityRandom = false;
                if (__originalMethod != null)
                {
                    UnityRandomScope.Enter(__originalMethod, scopeTarget, enableLogging: false, captureState: false);
                    seededUnityRandom = true;
                }

                (_contextStack ??= new Stack<MethodContext>()).Push(new MethodContext(__originalMethod, callIndex, seededUnityRandom));

                if (__instance != null)
                {
                    string typeName = __instance.GetType()?.FullName ?? declaringType?.FullName ?? "<null>";
                    if (MultiplayerPlugin.VerboseJobRng)
                        MultiplayerPlugin.LogS.LogDebug($"[JOBRNG] Prefix {typeName}.{methodName}()");
                    Patch_ForceJobRNGs.ApplyToObject(__instance);
                    return;
                }

                if (isStatic && declaringType != null)
                {
                    string typeName = declaringType.FullName ?? "<null>";
                    if (MultiplayerPlugin.VerboseJobRng)
                        MultiplayerPlugin.LogS.LogDebug($"[JOBRNG] Prefix static {typeName}.{methodName}()");
                    Patch_ForceJobRNGs.ApplyToStatic(declaringType);
                    return;
                }

                string fallbackName = declaringType?.FullName ?? "<null>";
                if (MultiplayerPlugin.VerboseJobRng)
                    MultiplayerPlugin.LogS.LogDebug($"[JOBRNG] Prefix (no instance) {fallbackName}.{methodName}()");
            }
            catch (Exception e)
            {
                MultiplayerPlugin.LogS.LogError($"[JOBRNG] Error forcing job RNGs ({__originalMethod?.DeclaringType?.FullName}.{__originalMethod?.Name}): {e}");
            }
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            var stack = _contextStack;
            if (stack != null && stack.Count > 0)
            {
                var context = stack.Pop();
                if (context.SeededUnityRandom)
                    UnityRandomScope.Exit();
            }
        }

        private static int NextInvocationIndex(MethodBase method)
        {
            int activeSeed = Patch_ForceJobRNGs.GetActiveSeed();

            lock (MethodInvocationLock)
            {
                if (_lastSeed != activeSeed)
                {
                    MethodInvocationCounts.Clear();
                    _lastSeed = activeSeed;
                }

                if (!MethodInvocationCounts.TryGetValue(method, out int index))
                    index = 0;

                MethodInvocationCounts[method] = index + 1;
                return index;
            }
        }
    }
}
