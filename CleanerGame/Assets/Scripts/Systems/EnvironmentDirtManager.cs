using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives progressive environmental dirtiness feedback based on RestaurantManager's
/// dirtiness level. Replaces the text-based DirtinessHud with visual and audio cues:
///   - Wall / table / floor material tinting via MaterialPropertyBlock
///   - Clean: volumetric sparkle particles + optional manual decor
///   - Grime overlay sprites shown at VeryDirty+
///   - Spill-linked decorative trash: scripted WasteOvergrowth prefabs (left wall choreography → counter → right wall), matches live SpillManager count; no colliders
///   - Debris objects shown at Filthy
///   - Fly particle system and audio at Filthy
/// </summary>
public class EnvironmentDirtManager : MonoBehaviour
{
    public static EnvironmentDirtManager Instance { get; private set; }

    [Header("Restaurant Manager")]
    [SerializeField] private RestaurantManager restaurantManager;

    [Header("Surface Renderers")]
    [Tooltip("Assign all wall MeshRenderers here")]
    [SerializeField] private Renderer[] wallRenderers;
    [Tooltip("Assign all table MeshRenderers here")]
    [SerializeField] private Renderer[] tableRenderers;
    [Tooltip("Assign all floor MeshRenderers here")]
    [SerializeField] private Renderer[] floorRenderers;

    [Header("Dirt Tint")]
    [Tooltip("Color that walls/tables/floor tint toward at max dirtiness")]
    [SerializeField] private Color dirtyTintColor = new Color(0.42f, 0.29f, 0.17f);
    [Tooltip("How fast the tint transitions between dirtiness levels (units per second)")]
    [SerializeField] private float tintTransitionSpeed = 0.4f;

    [Header("Clean aesthetic (Clean tier only)")]
    [Tooltip("Extra sparkles / props shown only while the restaurant is Clean")]
    [SerializeField] private GameObject[] cleanAestheticObjects;
    [Header("Clean sparkle volume (local to Restaurant if found, else decor parent)")]
    [SerializeField] private bool useRoomSparkleParticles = true;
    [SerializeField] private Vector3 sparkleVolumeCenterLocal = new Vector3(0f, 1.15f, 0f);
    [SerializeField] private Vector3 sparkleVolumeHalfExtents = new Vector3(10f, 2.5f, 9f);
    [SerializeField] private float sparkleEmissionRate = 55f;
    [SerializeField] private float sparkleParticleSize = 0.09f;
    [SerializeField] private float sparkleLifetime = 1.8f;
    [Tooltip("Optional extra prefab instances (e.g. CFXR); scattered in the sparkle volume when count > 0")]
    [SerializeField] private GameObject cleanSparklePrefab;
    [SerializeField] private int cleanSparklePrefabCount;
    [SerializeField] private float cleanSparklePrefabScale = 0.22f;

    [Header("Grime Overlays (VeryDirty+)")]
    [Tooltip("Plane/quad GameObjects with dirty-looking sprites, disabled by default")]
    [SerializeField] private GameObject[] grimeOverlayObjects;

    [Header("Clutter Obstacles (Dirty+) — must have solid colliders")]
    [Tooltip("GameObjects with Box/CapsuleColliders that physically block the player. Activated at Dirty and above.")]
    [SerializeField] private GameObject[] dirtyClutterObjects;

    [Header("Clutter Obstacles (VeryDirty+) — must have solid colliders")]
    [Tooltip("Denser obstacle set added on top of Dirty clutter. Activated at VeryDirty and above.")]
    [SerializeField] private GameObject[] veryDirtyClutterObjects;

    [Header("Ambient litter (manual, non-runtime)")]
    [SerializeField] private GameObject[] ambientLitterDirtyObjects;
    [SerializeField] private GameObject[] ambientLitterVeryDirtyObjects;

    [Header("Spill-driven decorative trash (scripted path, no colliders)")]
    [Tooltip("When disabled, spill-linked trash bag/cart visuals are skipped but clean sparkles still work.")]
    [SerializeField] private bool enableSpillTrashDecor;
    [SerializeField] private bool spawnRuntimeDecorFromPrefabs = true;
    [Tooltip("Parent for sparkles etc.; defaults to grime_and_debris")]
    [SerializeField] private Transform decorSpawnParent;
    [SerializeField] private RestaurantSpillTracker spillTracker;
    [Tooltip("Prefab_TrashBag")]
    [SerializeField] private UnityEngine.Object spillDecorTrashBagPrefab;
    [Tooltip("Prefab_Trahbbag_Leaning")]
    [SerializeField] private UnityEngine.Object spillDecorTrashBagLeaningPrefab;
    [Tooltip("Trash bin for counter / right-wall variety")]
    [SerializeField] private UnityEngine.Object spillDecorTrashContainerPrefab;
    [Tooltip("Prefab_TrashCart (closed)")]
    [SerializeField] private UnityEngine.Object spillDecorTrashCartPrefab;
    [Tooltip("Prefab_TrashCart_Opened")]
    [SerializeField] private UnityEngine.Object spillDecorTrashCartOpenPrefab;
    [Tooltip("Prefab_TrashGroup_3 (replaces cart pair)")]
    [SerializeField] private UnityEngine.Object spillDecorTrashGroup3Prefab;
    [Tooltip("Prefab_TrashGroup_4 (replaces first two bags)")]
    [SerializeField] private UnityEngine.Object spillDecorTrashGroup4Prefab;
    [Tooltip("Prefab_TrashGroup_5 (replaces group4 / group4+bag)")]
    [SerializeField] private UnityEngine.Object spillDecorTrashGroup5Prefab;

    [Header("Debris (Filthy only)")]
    [SerializeField] private GameObject[] debrisObjects;

    [Header("Flies (Filthy only)")]
    [SerializeField] private ParticleSystem flyParticles;
    [SerializeField] private AudioSource flyAudioSource;
    [SerializeField] private float audioFadeSpeed = 1.5f;
    [SerializeField] private float flyAudioMaxVolume = 0.6f;

    [Header("Refresh")]
    [SerializeField] private float refreshSeconds = 0.25f;

    private static readonly float[] LevelTint =
    {
        0.00f, // Clean
        0.20f, // Dirty
        0.55f, // VeryDirty
        1.00f  // Filthy
    };

    private const string URP_BASE_COLOR = "_BaseColor";
    private const string LEGACY_COLOR   = "_Color";

    private float refreshTimer;
    private float currentTintT;
    private RestaurantManager.DirtinessLevel currentLevel;

    private Color[] wallBaseColors;
    private Color[] tableBaseColors;
    private Color[] floorBaseColors;

    private GameObject[] _spawnedCleanSparklePrefabs = System.Array.Empty<GameObject>();
    private Transform _spillTrashDecorRoot;
    /// <summary>Last observed <see cref="SpillManager.ActiveSpills"/> count (decor rebuild when it changes).</summary>
    private int _lastActiveSpillCount = int.MinValue;

    private const float ScriptedLeftWallX = -9.86f;
    /// <summary>Extra local X (negative = further left) for left-wall group/cart tier (spill 3+), not single bags at zBagMid/zBagLean.</summary>
    private const float ScriptedLeftWallGroupClusterExtraX = -0.28f;
    private const float ScriptedRightWallX = 9.86f;
    private const float ScriptedFloorY = 0.02f;
    private const float ScriptedCounterZ = 2.78f;
    private const int ScriptedLeftWallSpillCap = 8;
    private const int ScriptedCounterSlots = 6;
    private const int ScriptedRightWallSlots = 6;
    private static readonly Vector3 ScriptedYawLeft = new Vector3(0f, 90f, 0f);
    private static readonly Vector3 ScriptedYawRight = new Vector3(0f, -90f, 0f);
    private static readonly Vector3 ScriptedYawCounter = new Vector3(0f, 180f, 0f);

    private ParticleSystem _cleanRoomSparkles;
    private Transform _cleanFxRoot;

    private GameObject[] _combinedClean;
    private GameObject[] _combinedAmbientDirty;
    private GameObject[] _combinedAmbientVeryDirty;
    private GameObject[] _combinedDirtyClutter;
    private GameObject[] _combinedVeryDirtyClutter;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (restaurantManager == null)
            restaurantManager = RestaurantManager.Instance;

        CacheBaseColors(wallRenderers,  ref wallBaseColors);
        CacheBaseColors(tableRenderers, ref tableBaseColors);
        CacheBaseColors(floorRenderers, ref floorBaseColors);

        if (flyAudioSource != null)
        {
            flyAudioSource.volume = 0f;
            flyAudioSource.loop   = true;
        }

        if (spawnRuntimeDecorFromPrefabs)
            SpawnRuntimeDecor();

        RebuildCombinedArrays();

        SetObjects(_combinedDirtyClutter,     false);
        SetObjects(_combinedVeryDirtyClutter, false);
        SetGrimeOverlays(false);
        SetDebris(false);
        SetFlyParticles(false);
        SetObjects(_combinedClean, false);
        SetObjects(_combinedAmbientDirty, false);
        SetObjects(_combinedAmbientVeryDirty, false);
        SetCleanSparklesPlaying(false);

        if (spillTracker == null)
            spillTracker = FindFirstObjectByType<RestaurantSpillTracker>();

        if (enableSpillTrashDecor)
        {
            EnsureSpillTrashDecorRoot();
            RebuildSpillTrashDecor();
            _lastActiveSpillCount = CountActiveSpillsForDecor();
        }

        if (restaurantManager != null)
        {
            currentLevel = restaurantManager.GetDirtinessLevel();
            ApplyDecorationState(currentLevel, currentLevel);
        }
    }

    private void Update()
    {
        if (restaurantManager == null)
        {
            restaurantManager = RestaurantManager.Instance;
            if (restaurantManager == null) return;
        }

        float targetT = LevelTint[(int)currentLevel];
        currentTintT = Mathf.MoveTowards(currentTintT, targetT, tintTransitionSpeed * Time.deltaTime);

        ApplyTint(wallRenderers,  wallBaseColors);
        ApplyTint(tableRenderers, tableBaseColors);
        ApplyTint(floorRenderers, floorBaseColors);

        UpdateFlyAudio();

        if (spillTracker == null)
            spillTracker = FindFirstObjectByType<RestaurantSpillTracker>();

        if (enableSpillTrashDecor)
        {
            int activeSpills = CountActiveSpillsForDecor();
            if (activeSpills != _lastActiveSpillCount)
            {
                _lastActiveSpillCount = activeSpills;
                RebuildSpillTrashDecor();
            }
        }

        refreshTimer += Time.deltaTime;
        if (refreshTimer < refreshSeconds) return;
        refreshTimer = 0f;
        Refresh();
    }

    private void Refresh()
    {
        var newLevel = restaurantManager.GetDirtinessLevel();
        if (newLevel == currentLevel) return;

        var previousLevel = currentLevel;
        currentLevel = newLevel;
        ApplyDecorationState(currentLevel, previousLevel);
    }

    private void ApplyDecorationState(RestaurantManager.DirtinessLevel level, RestaurantManager.DirtinessLevel previousLevel)
    {
        bool isClean           = level == RestaurantManager.DirtinessLevel.Clean;
        bool dirtyOrWorse       = level >= RestaurantManager.DirtinessLevel.Dirty;
        bool veryDirtyOrWorse   = level >= RestaurantManager.DirtinessLevel.VeryDirty;
        bool filthy             = level == RestaurantManager.DirtinessLevel.Filthy;

        SetObjects(_combinedClean, isClean);
        SetCleanSparklesPlaying(isClean);

        SetObjects(_combinedAmbientDirty, dirtyOrWorse);
        SetObjects(_combinedAmbientVeryDirty, veryDirtyOrWorse);

        SetObjects(_combinedDirtyClutter, dirtyOrWorse);
        SetObjects(_combinedVeryDirtyClutter, veryDirtyOrWorse);

        SetGrimeOverlays(veryDirtyOrWorse);
        SetDebris(filthy);
        SetFlyParticles(filthy);

        if (filthy && flyAudioSource != null && !flyAudioSource.isPlaying)
            flyAudioSource.Play();
    }

    private void SpawnRuntimeDecor()
    {
        Transform parent = decorSpawnParent;
        if (parent == null)
        {
            var grime = GameObject.Find("grime_and_debris");
            parent = grime != null ? grime.transform : transform;
        }

        if (useRoomSparkleParticles)
        {
            Transform sparkleParent = parent;
            var restaurantGo = GameObject.Find("Restaurant");
            if (restaurantGo != null)
                sparkleParent = restaurantGo.transform;

            _cleanFxRoot = new GameObject("Runtime_CleanSparkles").transform;
            _cleanFxRoot.SetParent(sparkleParent, false);
            _cleanFxRoot.localPosition = sparkleVolumeCenterLocal;
            _cleanFxRoot.localRotation = Quaternion.identity;
            _cleanFxRoot.localScale = Vector3.one;

            var go = new GameObject("RoomSparklePS");
            go.transform.SetParent(_cleanFxRoot, false);
            go.transform.localPosition = Vector3.zero;

            _cleanRoomSparkles = go.AddComponent<ParticleSystem>();
            _cleanRoomSparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _cleanRoomSparkles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = sparkleLifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.015f, 0.09f);
            main.startSize = new ParticleSystem.MinMaxCurve(
                sparkleParticleSize * 0.65f,
                sparkleParticleSize * 1.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.55f),
                new Color(1f, 1f, 1f, 0.95f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 600;

            var emission = _cleanRoomSparkles.emission;
            emission.rateOverTime = sparkleEmissionRate;

            var shape = _cleanRoomSparkles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = sparkleVolumeHalfExtents * 2f;

            var col = _cleanRoomSparkles.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(BuildWhiteSparkleAlphaGradient());

            var sol = _cleanRoomSparkles.sizeOverLifetime;
            sol.enabled = true;
            var sizeCurve = AnimationCurve.EaseInOut(0f, 0.75f, 1f, 1.15f);
            sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var renderer = _cleanRoomSparkles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            ConfigureSparkleRendererForSoftGlow(renderer);

            _cleanRoomSparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (cleanSparklePrefab != null && cleanSparklePrefabCount > 0)
        {
            var prefabParent = new GameObject("Runtime_CleanSparklePrefabs").transform;
            prefabParent.SetParent(parent, false);

            var list = new List<GameObject>(cleanSparklePrefabCount);
            for (int i = 0; i < cleanSparklePrefabCount; i++)
            {
                var go = Instantiate(cleanSparklePrefab, prefabParent);
                go.name = $"Runtime_SparklePrefab_{i}";
                go.transform.localPosition = RandomInSparkleVolumeLocal(i, 17);
                go.transform.localRotation = Quaternion.Euler(0f, i * 41f, 0f);
                go.transform.localScale = Vector3.one * cleanSparklePrefabScale;
                go.SetActive(false);
                list.Add(go);
            }

            _spawnedCleanSparklePrefabs = list.ToArray();
        }

    }

    private void EnsureSpillTrashDecorRoot()
    {
        if (_spillTrashDecorRoot != null)
            return;

        Transform trashParent = decorSpawnParent;
        if (trashParent == null)
        {
            var grime = GameObject.Find("grime_and_debris");
            trashParent = grime != null ? grime.transform : transform;
        }

        var restaurantGo = GameObject.Find("Restaurant");
        if (restaurantGo != null)
            trashParent = restaurantGo.transform;

        var rootGo = new GameObject("Runtime_SpillTrashDecor");
        _spillTrashDecorRoot = rootGo.transform;
        _spillTrashDecorRoot.SetParent(trashParent, false);
        _spillTrashDecorRoot.localPosition = Vector3.zero;
        _spillTrashDecorRoot.localRotation = Quaternion.identity;
        _spillTrashDecorRoot.localScale = Vector3.one;
    }

    private readonly struct SpillDecorSpawn
    {
        public readonly Vector3 LocalPosition;
        public readonly Vector3 LocalEuler;
        public readonly float Scale;
        public readonly UnityEngine.Object Prefab;

        public SpillDecorSpawn(Vector3 localPosition, Vector3 localEuler, float scale, UnityEngine.Object prefab)
        {
            LocalPosition = localPosition;
            LocalEuler = localEuler;
            Scale = scale;
            Prefab = prefab;
        }
    }

    private void RebuildSpillTrashDecor()
    {
        if (!enableSpillTrashDecor || !spawnRuntimeDecorFromPrefabs || _spillTrashDecorRoot == null)
            return;

        for (int c = _spillTrashDecorRoot.childCount - 1; c >= 0; c--)
            Destroy(_spillTrashDecorRoot.GetChild(c).gameObject);

        if (spillTracker == null)
            spillTracker = FindFirstObjectByType<RestaurantSpillTracker>();

        int spillCount = CountActiveSpillsForDecor();
        if (spillCount <= 0)
            return;

        var spawns = new List<SpillDecorSpawn>(24);
        CollectScriptedSpillDecorSpawns(spillCount, spawns);

        for (int i = 0; i < spawns.Count; i++)
        {
            SpillDecorSpawn s = spawns[i];
            if (s.Prefab == null)
                continue;

            var dec = InstantiateSpillDecorPrefab(s.Prefab, _spillTrashDecorRoot);
            if (dec == null)
                continue;

            dec.name = $"SpillDecor_{dec.name}_{i}";
            ApplyDecorTransform(dec.transform, s.LocalPosition, s.LocalEuler, s.Scale);
            DisableGameplayColliders(dec);
        }
    }

    private static int CountActiveSpillsForDecor()
    {
        return SpillManager.ActiveSpills.Count;
    }

    private void CollectScriptedSpillDecorSpawns(int spillCount, List<SpillDecorSpawn> list)
    {
        list.Clear();
        int leftUsed = Mathf.Min(spillCount, ScriptedLeftWallSpillCap);
        AppendLeftWallChoreography(leftUsed, list);
        if (spillCount > ScriptedLeftWallSpillCap)
            AppendCounterThenRightWall(spillCount - ScriptedLeftWallSpillCap, list);
    }

    /// <summary>
    /// Spills 1–2: upright bag then leaning bag (moving +Z). 3: TrashGroup_4 only. 4: group4 + bag above.
    /// 5: TrashGroup_5 only. 6–7: group5 + closed cart + opened cart. 8+: group5 + TrashGroup_3 (replaces carts).
    /// Group/cart tier uses <see cref="ScriptedLeftWallGroupClusterExtraX"/> so clusters sit further left than lone bags.
    /// </summary>
    private void AppendLeftWallChoreography(int n, List<SpillDecorSpawn> list)
    {
        if (n <= 0)
            return;

        float xBags = ScriptedLeftWallX;
        float xGroups = ScriptedLeftWallX + ScriptedLeftWallGroupClusterExtraX;
        float y = ScriptedFloorY;
        Vector3 yL = ScriptedYawLeft;

        float zBagMid = 0.18f;
        float zBagLean = 1.42f;
        float zGroupAnchor = 0.88f;
        float zBagAboveGroup = 2.58f;
        float zCartA = 3.38f;
        float zCartB = 4.12f;
        float zGroup3 = 3.74f;

        var bag = spillDecorTrashBagPrefab;
        var lean = spillDecorTrashBagLeaningPrefab != null ? spillDecorTrashBagLeaningPrefab : bag;
        var g4 = spillDecorTrashGroup4Prefab;
        var g5 = spillDecorTrashGroup5Prefab;
        var cart = spillDecorTrashCartPrefab;
        var open = spillDecorTrashCartOpenPrefab != null ? spillDecorTrashCartOpenPrefab : cart;
        var g3 = spillDecorTrashGroup3Prefab;

        if (n == 1)
        {
            list.Add(new SpillDecorSpawn(new Vector3(xBags, y, zBagMid), yL, 1f, bag));
            return;
        }

        if (n == 2)
        {
            list.Add(new SpillDecorSpawn(new Vector3(xBags, y, zBagMid), yL, 1f, bag));
            list.Add(new SpillDecorSpawn(new Vector3(xBags, y, zBagLean), yL, 1f, lean));
            return;
        }

        if (n == 3)
        {
            list.Add(new SpillDecorSpawn(new Vector3(xGroups, y, zGroupAnchor), yL, 1f, g4));
            return;
        }

        if (n == 4)
        {
            list.Add(new SpillDecorSpawn(new Vector3(xGroups, y, zGroupAnchor), yL, 1f, g4));
            list.Add(new SpillDecorSpawn(new Vector3(xGroups, y, zBagAboveGroup), yL, 1f, bag));
            return;
        }

        if (n == 5)
        {
            list.Add(new SpillDecorSpawn(new Vector3(xGroups, y, zGroupAnchor), yL, 1f, g5));
            return;
        }

        if (n == 6)
        {
            list.Add(new SpillDecorSpawn(new Vector3(xGroups, y, zGroupAnchor), yL, 1f, g5));
            list.Add(new SpillDecorSpawn(new Vector3(xGroups, y, zCartA), yL, 1f, cart));
            return;
        }

        if (n == 7)
        {
            list.Add(new SpillDecorSpawn(new Vector3(xGroups, y, zGroupAnchor), yL, 1f, g5));
            list.Add(new SpillDecorSpawn(new Vector3(xGroups, y, zCartA), yL, 1f, cart));
            list.Add(new SpillDecorSpawn(new Vector3(xGroups, y, zCartB), yL, 1f, open));
            return;
        }

        list.Add(new SpillDecorSpawn(new Vector3(xGroups, y, zGroupAnchor), yL, 1f, g5));
        list.Add(new SpillDecorSpawn(new Vector3(xGroups, y, zGroup3), yL, 1f, g3));
    }

    /// <summary>Spill indices after the first 8: counter left→right, then right wall top→bottom. Uses trash group prefabs in a cycle.</summary>
    private void AppendCounterThenRightWall(int remainingSpills, List<SpillDecorSpawn> list)
    {
        if (remainingSpills <= 0)
            return;

        int pathIndex = 0;
        for (int i = 0; i < remainingSpills && i < ScriptedCounterSlots; i++, pathIndex++)
        {
            float t = ScriptedCounterSlots <= 1 ? 0f : i / (float)(ScriptedCounterSlots - 1);
            float xPos = Mathf.Lerp(-2.1f, 2.1f, t);
            list.Add(new SpillDecorSpawn(
                new Vector3(xPos, ScriptedFloorY, ScriptedCounterZ),
                ScriptedYawCounter,
                1f,
                PickCounterOrRightTrashGroupPrefab(pathIndex)));
        }

        if (remainingSpills <= ScriptedCounterSlots)
            return;

        int rightCount = remainingSpills - ScriptedCounterSlots;
        for (int r = 0; r < rightCount && r < ScriptedRightWallSlots; r++, pathIndex++)
        {
            float t = ScriptedRightWallSlots <= 1 ? 0f : r / (float)(ScriptedRightWallSlots - 1);
            float zPos = Mathf.Lerp(4.02f, -3.72f, t);
            list.Add(new SpillDecorSpawn(
                new Vector3(ScriptedRightWallX, ScriptedFloorY, zPos),
                ScriptedYawRight,
                1f,
                PickCounterOrRightTrashGroupPrefab(pathIndex)));
        }
    }

    /// <summary>Cycles TrashGroup_4 → TrashGroup_5 → TrashGroup_3 so counter/right-wall decor stays grouped like the left wall.</summary>
    private UnityEngine.Object PickCounterOrRightTrashGroupPrefab(int pathIndex)
    {
        int phase = pathIndex % 3;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            int pick = (phase + attempt) % 3;
            UnityEngine.Object chosen = pick switch
            {
                0 => spillDecorTrashGroup4Prefab,
                1 => spillDecorTrashGroup5Prefab,
                _ => spillDecorTrashGroup3Prefab
            };
            if (chosen != null)
                return chosen;
        }

        if (spillDecorTrashBagPrefab != null)
            return spillDecorTrashBagPrefab;
        return spillDecorTrashContainerPrefab;
    }

    /// <summary>
    /// Prefab references must be a scene/prefab <see cref="GameObject"/> root; if the inspector
    /// stored a <see cref="Component"/>, we use its gameObject so Instantiate never hits InvalidCastException.
    /// </summary>
    private static GameObject ResolveSpillDecorPrefabRoot(UnityEngine.Object asset)
    {
        if (asset == null)
            return null;
        if (asset is GameObject go)
            return go;
        if (asset is Component c)
            return c.gameObject;
        Debug.LogWarning($"[EnvironmentDirtManager] Spill decor expects a GameObject prefab root; got {asset.GetType().Name} ({asset.name}). Reassign in the inspector.");
        return null;
    }

    private static GameObject InstantiateSpillDecorPrefab(UnityEngine.Object prefabAsset, Transform parent)
    {
        var root = ResolveSpillDecorPrefabRoot(prefabAsset);
        if (root == null)
            return null;
        return UnityEngine.Object.Instantiate(root, parent, false);
    }

    private static void ApplyDecorTransform(Transform t, Vector3 localPosition, Vector3 localEuler, float scale)
    {
        t.localPosition = localPosition;
        t.localEulerAngles = localEuler;
        float s = Mathf.Max(0.01f, scale);
        t.localScale = Vector3.one * s;
    }

    private static void DisableGameplayColliders(GameObject root)
    {
        if (root == null)
            return;
        var cols = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                cols[i].enabled = false;
        }
    }

    private static float Frac(float x)
    {
        return x - Mathf.Floor(x);
    }

    private Vector3 RandomInSparkleVolumeLocal(int i, int salt)
    {
        float u = Frac(0.161803f * i + 0.271828f * salt);
        float v = Frac(0.314159f * i + 0.123456f * salt);
        float w = Frac(0.618034f * i + 0.707107f * salt);
        var min = sparkleVolumeCenterLocal - sparkleVolumeHalfExtents;
        var max = sparkleVolumeCenterLocal + sparkleVolumeHalfExtents;
        return new Vector3(
            Mathf.Lerp(min.x, max.x, u),
            Mathf.Lerp(min.y, max.y, v),
            Mathf.Lerp(min.z, max.z, w));
    }

    private void SetCleanSparklesPlaying(bool playing)
    {
        if (_cleanRoomSparkles != null)
        {
            if (playing)
            {
                if (!_cleanRoomSparkles.isPlaying)
                    _cleanRoomSparkles.Play();
            }
            else
            {
                _cleanRoomSparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private void RebuildCombinedArrays()
    {
        var cleanExtras = new List<GameObject>();
        if (_cleanFxRoot != null)
            cleanExtras.Add(_cleanFxRoot.gameObject);
        if (cleanAestheticObjects != null)
        {
            for (int i = 0; i < cleanAestheticObjects.Length; i++)
            {
                if (cleanAestheticObjects[i] != null)
                    cleanExtras.Add(cleanAestheticObjects[i]);
            }
        }
        if (_spawnedCleanSparklePrefabs.Length > 0)
            cleanExtras.AddRange(_spawnedCleanSparklePrefabs);

        _combinedClean = cleanExtras.Count > 0 ? cleanExtras.ToArray() : System.Array.Empty<GameObject>();
        _combinedAmbientDirty = ambientLitterDirtyObjects ?? System.Array.Empty<GameObject>();
        _combinedAmbientVeryDirty = ambientLitterVeryDirtyObjects ?? System.Array.Empty<GameObject>();
        _combinedDirtyClutter = dirtyClutterObjects ?? System.Array.Empty<GameObject>();
        _combinedVeryDirtyClutter = veryDirtyClutterObjects ?? System.Array.Empty<GameObject>();
    }

    private void UpdateFlyAudio()
    {
        if (flyAudioSource == null) return;

        bool filthy = currentLevel == RestaurantManager.DirtinessLevel.Filthy;
        float target = filthy ? flyAudioMaxVolume : 0f;
        flyAudioSource.volume = Mathf.MoveTowards(flyAudioSource.volume, target, audioFadeSpeed * Time.deltaTime);

        if (!filthy && flyAudioSource.isPlaying && flyAudioSource.volume <= 0f)
            flyAudioSource.Stop();
    }

    private void CacheBaseColors(Renderer[] renderers, ref Color[] cache)
    {
        if (renderers == null) { cache = new Color[0]; return; }
        cache = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            var mat = renderers[i].sharedMaterial;
            if (mat == null) { cache[i] = Color.white; continue; }

            if (mat.HasProperty(URP_BASE_COLOR))
                cache[i] = mat.GetColor(URP_BASE_COLOR);
            else if (mat.HasProperty(LEGACY_COLOR))
                cache[i] = mat.GetColor(LEGACY_COLOR);
            else
                cache[i] = Color.white;
        }
    }

    private void ApplyTint(Renderer[] renderers, Color[] baseColors)
    {
        if (renderers == null || baseColors == null) return;
        var block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            var mat = renderers[i].sharedMaterial;
            renderers[i].GetPropertyBlock(block);
            Color tinted = Color.Lerp(baseColors[i], dirtyTintColor, currentTintT);
            if (mat != null && mat.HasProperty(URP_BASE_COLOR))
                block.SetColor(URP_BASE_COLOR, tinted);
            else
                block.SetColor(LEGACY_COLOR, tinted);
            renderers[i].SetPropertyBlock(block);
        }
    }

    private void SetGrimeOverlays(bool active)
    {
        if (grimeOverlayObjects == null) return;
        foreach (var obj in grimeOverlayObjects)
            if (obj != null) obj.SetActive(active);
    }

    private void SetDebris(bool active)
    {
        if (debrisObjects == null) return;
        foreach (var obj in debrisObjects)
            if (obj != null) obj.SetActive(active);
    }

    private void SetObjects(GameObject[] objects, bool active)
    {
        if (objects == null) return;
        foreach (var obj in objects)
            if (obj != null) obj.SetActive(active);
    }

    private void SetFlyParticles(bool active)
    {
        if (flyParticles == null) return;
        if (active && !flyParticles.isPlaying)  flyParticles.Play();
        else if (!active && flyParticles.isPlaying) flyParticles.Stop();
    }

    private static Gradient BuildWhiteSparkleAlphaGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(0.88f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        return g;
    }

    private static void ConfigureSparkleRendererForSoftGlow(ParticleSystemRenderer renderer)
    {
        if (renderer == null)
            return;

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            return;

        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", Color.white);
        else if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", Color.white);
        else if (mat.HasProperty("_TintColor"))
            mat.SetColor("_TintColor", Color.white);

        renderer.material = mat;
    }
}
