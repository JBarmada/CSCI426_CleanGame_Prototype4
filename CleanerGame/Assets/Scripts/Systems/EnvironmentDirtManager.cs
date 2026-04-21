using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives progressive environmental dirtiness feedback based on RestaurantManager's
/// dirtiness level. Replaces the text-based DirtinessHud with visual and audio cues:
///   - Wall / table / floor material tinting via MaterialPropertyBlock
///   - Clean: volumetric sparkle particles + optional manual decor
///   - Grime overlay sprites shown at VeryDirty+
///   - Runtime trash walls (3 per filth tier by default) with solid colliders, random layout on tier changes
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

    [Header("Runtime trash pool (WasteOvergrowth)")]
    [SerializeField] private bool spawnRuntimeDecorFromPrefabs = true;
    [Tooltip("Parent for spawned props; defaults to GameObject named grime_and_debris")]
    [SerializeField] private Transform decorSpawnParent;
    [SerializeField] private GameObject runtimeTrashPrefabPrimary;
    [SerializeField] private GameObject runtimeTrashPrefabAlternate;
    [Tooltip("If off, only the primary prefab is used (avoids invisible or broken alternate assets).")]
    [SerializeField] private bool useAlternateTrashPrefab;
    [Tooltip("Trash prefab instances spawned (auto at least max of tier counts)")]
    [SerializeField] private int runtimeTrashPoolSize = 3;
    [SerializeField] private Vector3 trashScatterMinLocal = new Vector3(-8.2f, 0.02f, -4.8f);
    [SerializeField] private Vector3 trashScatterMaxLocal = new Vector3(5.8f, 0.02f, 5.2f);
    [Tooltip("Minimum distance on XZ between trash roots so piles do not stack")]
    [SerializeField] private float trashMinSeparationXZ = 2.1f;
    [SerializeField] private float trashUniformScale = 1.05f;
    [Tooltip("Multiplier on renderer bounds before clamping to mins (lower = tighter to mesh)")]
    [SerializeField] private float trashColliderPadding = 0.96f;
    [Tooltip("Minimum world size only when mesh bounds are tiny (keeps CharacterController from stepping over)")]
    [SerializeField] private float trashWallColliderMinHeightWorld = 1.12f;
    [SerializeField] private float trashWallColliderMinXZWorld = 1.35f;
    [SerializeField] private int trashActiveWhenDirty = 3;
    [SerializeField] private int trashActiveWhenVeryDirty = 3;
    [SerializeField] private int trashActiveWhenFilthy = 3;

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
    private GameObject[] _runtimeTrashPool = System.Array.Empty<GameObject>();
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
        SetRuntimeTrashActiveCount(0);
        SetCleanSparklesPlaying(false);

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

        int trashCount = 0;
        if (dirtyOrWorse)
        {
            int cap = filthy
                ? Mathf.Min(trashActiveWhenFilthy, _runtimeTrashPool.Length)
                : veryDirtyOrWorse
                    ? Mathf.Min(trashActiveWhenVeryDirty, _runtimeTrashPool.Length)
                    : Mathf.Min(trashActiveWhenDirty, _runtimeTrashPool.Length);
            trashCount = cap;
        }

        // New layout whenever filth tier changes while trash is in use (e.g. Clean→Dirty, Dirty→VeryDirty).
        if (dirtyOrWorse && previousLevel != level && _runtimeTrashPool.Length > 0)
            RepositionRuntimeTrash();

        SetRuntimeTrashActiveCount(trashCount);

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

        if (runtimeTrashPrefabPrimary != null || runtimeTrashPrefabAlternate != null)
        {
            int tierMax = Mathf.Max(1, Mathf.Max(trashActiveWhenDirty, Mathf.Max(trashActiveWhenVeryDirty, trashActiveWhenFilthy)));
            int poolSize = Mathf.Max(runtimeTrashPoolSize, tierMax);

            Transform trashParent = parent;
            var restaurantGo = GameObject.Find("Restaurant");
            if (restaurantGo != null)
                trashParent = restaurantGo.transform;

            var trashRoot = new GameObject("Runtime_TrashPool").transform;
            trashRoot.SetParent(trashParent, false);
            trashRoot.localPosition = Vector3.zero;
            trashRoot.localRotation = Quaternion.identity;
            trashRoot.localScale = Vector3.one;

            var placed = new List<Vector3>(poolSize);
            var pool = new List<GameObject>(poolSize);
            for (int i = 0; i < poolSize; i++)
            {
                var prefab = PickTrashPrefab(i);
                if (prefab == null)
                    continue;

                var go = Instantiate(prefab, trashRoot);
                go.name = $"{prefab.name}_Pool_{i}";
                go.transform.localPosition = PickRandomTrashLocal(placed);
                go.transform.localRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                go.transform.localScale = Vector3.one * trashUniformScale;
                InstallTrashWallCollider(go);
                go.SetActive(false);
                pool.Add(go);
                placed.Add(go.transform.localPosition);
            }

            _runtimeTrashPool = pool.ToArray();
        }
    }

    private GameObject PickTrashPrefab(int i)
    {
        if (useAlternateTrashPrefab && runtimeTrashPrefabAlternate != null && runtimeTrashPrefabPrimary != null)
            return (i % 2 == 0) ? runtimeTrashPrefabPrimary : runtimeTrashPrefabAlternate;
        return runtimeTrashPrefabPrimary != null ? runtimeTrashPrefabPrimary : runtimeTrashPrefabAlternate;
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

    private Vector3 PickRandomTrashLocal(List<Vector3> placed)
    {
        return PickRandomTrashLocal(placed, null);
    }

    private Vector3 PickRandomTrashLocal(List<Vector3> placed, System.Random rng)
    {
        const int maxAttempts = 64;
        float minX = Mathf.Min(trashScatterMinLocal.x, trashScatterMaxLocal.x);
        float maxX = Mathf.Max(trashScatterMinLocal.x, trashScatterMaxLocal.x);
        float minZ = Mathf.Min(trashScatterMinLocal.z, trashScatterMaxLocal.z);
        float maxZ = Mathf.Max(trashScatterMinLocal.z, trashScatterMaxLocal.z);
        float minY = Mathf.Min(trashScatterMinLocal.y, trashScatterMaxLocal.y);
        float maxY = Mathf.Max(trashScatterMinLocal.y, trashScatterMaxLocal.y);
        float sep = Mathf.Max(0.25f, trashMinSeparationXZ);
        float sepSq = sep * sep;

        float R(float a, float b)
        {
            if (rng != null)
                return a + (float)rng.NextDouble() * (b - a);
            return UnityEngine.Random.Range(a, b);
        }

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var p = new Vector3(R(minX, maxX), R(minY, maxY), R(minZ, maxZ));

            bool clear = true;
            for (int i = 0; i < placed.Count; i++)
            {
                float dx = p.x - placed[i].x;
                float dz = p.z - placed[i].z;
                if (dx * dx + dz * dz < sepSq)
                {
                    clear = false;
                    break;
                }
            }

            if (clear)
                return p;
        }

        return new Vector3(R(minX, maxX), minY, R(minZ, maxZ));
    }

    private void RepositionRuntimeTrash()
    {
        if (_runtimeTrashPool == null || _runtimeTrashPool.Length == 0)
            return;

        var rng = new System.Random(unchecked((int)(DateTime.UtcNow.Ticks ^ (GetInstanceID() * 397) ^ (_runtimeTrashPool.Length << 5))));

        var placed = new List<Vector3>(_runtimeTrashPool.Length);
        for (int i = 0; i < _runtimeTrashPool.Length; i++)
        {
            if (_runtimeTrashPool[i] == null)
                continue;

            Transform tr = _runtimeTrashPool[i].transform;
            tr.localPosition = PickRandomTrashLocal(placed, rng);
            tr.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            placed.Add(tr.localPosition);
        }
    }

    /// <summary>
    /// One solid box on the root so the player cannot step over low mesh colliders on the prefab.
    /// Disables all existing colliders on the instance (they are often too small for CharacterController).
    /// </summary>
    private void InstallTrashWallCollider(GameObject root)
    {
        var colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        var rootBoxes = root.GetComponents<BoxCollider>();
        for (int i = 0; i < rootBoxes.Length; i++)
        {
            if (rootBoxes[i] != null)
                Destroy(rootBoxes[i]);
        }

        var renderers = root.GetComponentsInChildren<Renderer>();
        Bounds wb;
        if (renderers.Length == 0)
        {
            wb = new Bounds(root.transform.position,
                new Vector3(trashWallColliderMinXZWorld, trashWallColliderMinHeightWorld, trashWallColliderMinXZWorld));
        }
        else
        {
            wb = renderers[0].bounds;
            for (int r = 1; r < renderers.Length; r++)
            {
                if (renderers[r] != null && renderers[r].enabled)
                    wb.Encapsulate(renderers[r].bounds);
            }
        }

        Vector3 minWorld = new Vector3(trashWallColliderMinXZWorld, trashWallColliderMinHeightWorld, trashWallColliderMinXZWorld);
        Vector3 worldSize = Vector3.Max(wb.size * trashColliderPadding, minWorld);
        Vector3 cWorld = wb.center;

        var box = root.AddComponent<BoxCollider>();
        box.isTrigger = false;
        Transform t = root.transform;
        box.center = t.InverseTransformPoint(cWorld);
        Vector3 lossy = t.lossyScale;
        lossy.x = Mathf.Max(1e-4f, Mathf.Abs(lossy.x));
        lossy.y = Mathf.Max(1e-4f, Mathf.Abs(lossy.y));
        lossy.z = Mathf.Max(1e-4f, Mathf.Abs(lossy.z));
        box.size = new Vector3(
            worldSize.x / lossy.x,
            worldSize.y / lossy.y,
            worldSize.z / lossy.z);
    }

    private void SetRuntimeTrashActiveCount(int count)
    {
        for (int i = 0; i < _runtimeTrashPool.Length; i++)
        {
            if (_runtimeTrashPool[i] != null)
                _runtimeTrashPool[i].SetActive(i < count);
        }
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
