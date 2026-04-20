using UnityEngine;

/// <summary>
/// Drives progressive environmental dirtiness feedback based on RestaurantManager's
/// dirtiness level. Replaces the text-based DirtinessHud with visual and audio cues:
///   - Wall / table / floor material tinting via MaterialPropertyBlock
///   - Grime overlay sprites shown at VeryDirty+
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

    [Header("Grime Overlays (VeryDirty+)")]
    [Tooltip("Plane/quad GameObjects with dirty-looking sprites, disabled by default")]
    [SerializeField] private GameObject[] grimeOverlayObjects;

    [Header("Clutter Obstacles (Dirty+) — must have solid colliders")]
    [Tooltip("GameObjects with Box/CapsuleColliders that physically block the player. Activated at Dirty and above.")]
    [SerializeField] private GameObject[] dirtyClutterObjects;

    [Header("Clutter Obstacles (VeryDirty+) — must have solid colliders")]
    [Tooltip("Denser obstacle set added on top of Dirty clutter. Activated at VeryDirty and above.")]
    [SerializeField] private GameObject[] veryDirtyClutterObjects;

    [Header("Debris (Filthy only)")]
    [Tooltip("Small litter/debris GameObjects on the floor, disabled by default")]
    [SerializeField] private GameObject[] debrisObjects;

    [Header("Flies (Filthy only)")]
    [SerializeField] private ParticleSystem flyParticles;
    [SerializeField] private AudioSource flyAudioSource;
    [Tooltip("How fast fly audio volume fades in/out")]
    [SerializeField] private float audioFadeSpeed = 1.5f;
    [SerializeField] private float flyAudioMaxVolume = 0.6f;

    [Header("Refresh")]
    [SerializeField] private float refreshSeconds = 0.25f;

    // Tint t values per dirtiness level index
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

        SetObjects(dirtyClutterObjects,     false);
        SetObjects(veryDirtyClutterObjects, false);
        SetGrimeOverlays(false);
        SetDebris(false);
        SetFlyParticles(false);
    }

    private void Update()
    {
        if (restaurantManager == null)
        {
            restaurantManager = RestaurantManager.Instance;
            if (restaurantManager == null) return;
        }

        // Smoothly move tint toward target
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

        currentLevel = newLevel;

        bool dirtyOrWorse     = currentLevel >= RestaurantManager.DirtinessLevel.Dirty;
        bool veryDirtyOrWorse = currentLevel >= RestaurantManager.DirtinessLevel.VeryDirty;
        bool filthy            = currentLevel == RestaurantManager.DirtinessLevel.Filthy;

        // Physical clutter that blocks player movement
        SetObjects(dirtyClutterObjects,     dirtyOrWorse);
        SetObjects(veryDirtyClutterObjects, veryDirtyOrWorse);

        // Visual-only overlays and effects
        SetGrimeOverlays(veryDirtyOrWorse);
        SetDebris(filthy);
        SetFlyParticles(filthy);

        if (filthy && flyAudioSource != null && !flyAudioSource.isPlaying)
            flyAudioSource.Play();
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

    /// <summary>Generic helper for toggling any group of GameObjects.</summary>
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
}
