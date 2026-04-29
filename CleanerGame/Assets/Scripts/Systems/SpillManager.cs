using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SpillManager : MonoBehaviour
{
    // ── Static registry so ThirdPersonController can cheaply query all live spills ──
    private static readonly List<SpillManager> s_activeSpills = new List<SpillManager>();
    /// <summary>All SpillManager instances currently alive in the scene.</summary>
    public static IReadOnlyList<SpillManager> ActiveSpills => s_activeSpills;

    /// <summary>True while the player is in range, holding the sweep key, and the spill isn't cleaned.</summary>
    public bool IsBeingCleaned => playerInRange && !cleaned && Input.GetKey(sweepKey);

    public bool ContainsSlipPoint(Vector3 worldPosition)
    {
        if (cleaned || spriteRenderer == null)
            return false;

        Vector3 localPoint = spriteRenderer.transform.InverseTransformPoint(worldPosition);
        Vector2 halfSize = spriteRenderer.size * 0.5f * Mathf.Max(0.1f, slipVisualFootprintScale);
        if (halfSize.x <= 0f || halfSize.y <= 0f)
            return false;

        float normalizedX = localPoint.x / halfSize.x;
        float normalizedY = localPoint.y / halfSize.y;
        return (normalizedX * normalizedX) + (normalizedY * normalizedY) <= 1f;
    }

    [Header("Cleaning (Hold to Sweep)")]
    [SerializeField] private KeyCode sweepKey = KeyCode.Space;
    [SerializeField] private float sweepsPerSecond = 3f;   // 3 sweep motions / sec
    [SerializeField] private int sweepsToClean = 3;        // total motions needed (3 @ 3/sec = 1s)
    [SerializeField] private float cleaningRadius = 1.15f;

    [Header("Slip Trigger")]
    [Tooltip("How much of the visible sprite can trigger slipping. 1 = full sprite, lower values require the player to be more centered on the spill.")]
    [Range(0.1f, 1.25f)]
    [SerializeField] private float slipVisualFootprintScale = 0.8f;

    [Header("Coins")]
    [SerializeField] private int coinsPerClean = 1;
    [SerializeField] private CoinWallet coinWallet;
    [SerializeField] private RestaurantSpillTracker spillTracker;
    [SerializeField] private SpillComboSystem comboSystem;

    [Header("Day 2 Tuning")]
    [SerializeField] private bool useDay2Tuning = true;
    [Range(0.1f, 1f)]
    [SerializeField] private float day2SweepsToCleanMultiplier = 0.75f;
    [SerializeField] private RestaurantDayCycle dayCycle;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer spriteRenderer; // assign, or auto-find
    [SerializeField] private SpriteRenderer glowRenderer;   // child named "glow"
    [SerializeField] private bool destroyRoot = false;      // true if this script is on a child trigger

    [Header("Glow Pulse")]
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseMinAlpha = 0.3f;

    [Header("Particles")]
    [SerializeField] private ParticleSystem spillParticles;
    [SerializeField] private bool hideSpillParticles = true;
    [SerializeField] private bool enhanceDirtyParticles = true;
    [SerializeField] private bool suppressDirtyParticlesForWaterSpills = true;
    [SerializeField] private float dirtyParticleHeight = 0.45f;
    [SerializeField] private float dirtyParticleEmissionRate = 85f;
    [SerializeField] private float dirtyParticleSize = 0.24f;

    [Header("Completion Feedback")]
    [SerializeField] private GameObject cleanBurstPrefab;
    [SerializeField] private float cleanBurstLifetime = 1f;
    [SerializeField] private bool spawnCleanStarBurst = true;
    [SerializeField] private int cleanStarBurstCount = 22;
    [SerializeField] private float cleanStarBurstSize = 0.32f;
    [SerializeField] private AudioClip cleanCompleteClip;
    [Range(0f, 1f)]
    [SerializeField] private float cleanCompleteVolume = 0.8f;
    [SerializeField] private float cleanPunchScale = 1.35f;
    [SerializeField] private float cleanFinishSeconds = 0.12f;

    private bool playerInRange;
    private float sweepProgress; // counts "motions" continuously
    private Collider col;
    private bool cleaned;
    private bool particlesStopped;
    private Vector3 initialScale;
    private Coroutine cleanRoutine;
    private Transform playerTransform;
    private Transform cleaningAnchor;

    private static Mesh s_fourPointStarMesh;

    private void Awake()
    {
        s_activeSpills.Add(this);
        initialScale = transform.localScale;
        col = GetComponent<Collider>();
        col.isTrigger = true;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (glowRenderer == null)
        {
            var glowTransform = transform.Find("glow");
            if (glowTransform != null)
                glowRenderer = glowTransform.GetComponent<SpriteRenderer>();
        }

        if (spillParticles == null)
            spillParticles = GetComponentInChildren<ParticleSystem>();
        if (hideSpillParticles && spillParticles != null)
        {
            spillParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            spillParticles.gameObject.SetActive(false);
        }
        if (enhanceDirtyParticles && !hideSpillParticles && !ShouldSkipDirtyParticles())
            EnsureEnhancedDirtyParticles();

        if (dayCycle == null)
            dayCycle = FindFirstObjectByType<RestaurantDayCycle>();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            ResolveCleaningAnchor();
        }

        sweepProgress = 0f;
        UpdateVisual();
    }

    private void Update()
    {
        // Always update glow pulse even when player isn't cleaning
        UpdateGlowPulse();

        UpdatePlayerCleaningRange();

        if (!playerInRange || cleaned) return;

        if (Input.GetKey(sweepKey))
        {
            float mult = BroomPowerupSystem.Instance != null ? BroomPowerupSystem.Instance.CurrentMultiplier : 1f;
            sweepProgress += (sweepsPerSecond * mult) * Time.deltaTime;
            UpdateVisual();

            // Stop particles as soon as cleaning begins
            if (!particlesStopped && spillParticles != null)
            {
                if (!hideSpillParticles)
                    spillParticles.Stop();
                particlesStopped = true;
            }

            if (sweepProgress >= GetEffectiveSweepsToClean())
            {
                cleaned = true;
                AwardCoins();
                playerInRange = false;

                if (col != null)
                    col.enabled = false;

                Debug.Log($"[Spill] Cleaned with multiplier {mult:F2}x");
                cleanRoutine = StartCoroutine(PlayCleanCompletion());
            }
        }
    }

    private void UpdateGlowPulse()
    {
        if (glowRenderer == null || cleaned) return;

        float t = Mathf.Clamp01(sweepProgress / GetEffectiveSweepsToClean());

        if (t <= 0f)
        {
            // Idle: pulse alpha between pulseMinAlpha and 1
            float pulse = Mathf.PingPong(Time.time * pulseSpeed, 1f);
            float alpha = Mathf.Lerp(pulseMinAlpha, 1f, pulse);
            var c = glowRenderer.color;
            c.a = alpha;
            glowRenderer.color = c;
        }
        else
        {
            // Cleaning in progress: fade glow out with main sprite
            var c = glowRenderer.color;
            c.a = 1f - t;
            glowRenderer.color = c;
        }
    }

    private void UpdateVisual()
    {
        if (spriteRenderer == null) return;

        float t = Mathf.Clamp01(sweepProgress / GetEffectiveSweepsToClean());

        // Shrink the spill toward 50% of its original size as it gets cleaned
        transform.localScale = initialScale * Mathf.Lerp(1f, 0.5f, t);
    }

    private void UpdatePlayerCleaningRange()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                ResolveCleaningAnchor();
            }
        }

        if (playerTransform == null)
        {
            playerInRange = false;
            return;
        }

        if (cleaningAnchor == null)
            ResolveCleaningAnchor();

        Transform rangeSource = cleaningAnchor != null ? cleaningAnchor : playerTransform;
        Vector3 playerPlanar = new Vector3(rangeSource.position.x, 0f, rangeSource.position.z);
        Vector3 spillPlanar = new Vector3(transform.position.x, 0f, transform.position.z);
        playerInRange = Vector3.Distance(playerPlanar, spillPlanar) <= cleaningRadius;
    }

    private void ResolveCleaningAnchor()
    {
        if (ThirdPersonController.Instance != null && ThirdPersonController.Instance.broom != null)
        {
            cleaningAnchor = ThirdPersonController.Instance.broom;
            return;
        }

        cleaningAnchor = playerTransform;
    }

    private System.Collections.IEnumerator PlayCleanCompletion()
    {
        if (cleanBurstPrefab != null)
        {
            GameObject fx = Instantiate(cleanBurstPrefab, transform.position, transform.rotation);
            if (cleanBurstLifetime > 0f)
                Destroy(fx, cleanBurstLifetime);
        }

        if (spawnCleanStarBurst)
            SpawnCleanStarBurst();

        if (cleanCompleteClip != null)
            AudioSource.PlayClipAtPoint(cleanCompleteClip, transform.position, cleanCompleteVolume);

        float duration = Mathf.Max(0.01f, cleanFinishSeconds);
        Vector3 startScale = transform.localScale;
        Vector3 peakScale = startScale * Mathf.Max(1f, cleanPunchScale);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float scaleT = Mathf.Sin(t * Mathf.PI);
            transform.localScale = Vector3.Lerp(startScale, peakScale, scaleT);

            yield return null;
        }

        DestroySelf();
    }

    private void OnDestroy()
    {
        s_activeSpills.Remove(this);
    }

    private void DestroySelf()
    {
        if (destroyRoot && transform.parent != null)
            Destroy(transform.parent.gameObject);
        else
            Destroy(gameObject);
    }

    private void AwardCoins()
    {
        if (coinsPerClean <= 0) return;

        if (coinWallet == null)
            coinWallet = FindFirstObjectByType<CoinWallet>();

        if (coinWallet != null)
        {
            if (comboSystem == null)
                comboSystem = SpillComboSystem.Instance ?? FindFirstObjectByType<SpillComboSystem>();

            int awardedCoins = comboSystem != null
                ? comboSystem.RegisterSpillCleanAndGetCoins(coinsPerClean)
                : coinsPerClean;

            coinWallet.AddCoins(awardedCoins);
        }

        if (spillTracker == null)
            spillTracker = FindFirstObjectByType<RestaurantSpillTracker>();

        if (spillTracker != null)
            spillTracker.AddSpillCleaned();
    }

    private float GetEffectiveSweepsToClean()
    {
        float target = Mathf.Max(1f, sweepsToClean);
        if (useDay2Tuning && dayCycle != null && dayCycle.DayCount == 2)
            target = Mathf.Max(1f, target * day2SweepsToCleanMultiplier);

        return target;
    }

    private void EnsureEnhancedDirtyParticles()
    {
        if (spillParticles == null)
        {
            if (hideSpillParticles)
                return;

            GameObject particleObject = new GameObject("Runtime_DirtySpillParticles");
            particleObject.transform.SetParent(transform, false);
            particleObject.transform.localPosition = Vector3.up * dirtyParticleHeight;
            spillParticles = particleObject.AddComponent<ParticleSystem>();
        }
        else
        {
            if (hideSpillParticles)
            {
                spillParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                spillParticles.gameObject.SetActive(false);
                return;
            }

            Vector3 localPosition = spillParticles.transform.localPosition;
            localPosition.y = Mathf.Max(localPosition.y, dirtyParticleHeight);
            spillParticles.transform.localPosition = localPosition;
        }

        spillParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = spillParticles.main;
        main.playOnAwake = true;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(dirtyParticleSize * 0.6f, dirtyParticleSize * 1.4f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.28f, 0.18f, 0.08f, 0.42f),
            new Color(0.78f, 0.56f, 0.20f, 0.68f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 160;

        var emission = spillParticles.emission;
        emission.rateOverTime = Mathf.Max(0f, dirtyParticleEmissionRate);

        var shape = spillParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.2f;

        var velocity = spillParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.25f, 0.85f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

        var color = spillParticles.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(BuildDirtyParticleGradient());

        var renderer = spillParticles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        ConfigureParticleRenderer(renderer, new Color(0.75f, 0.52f, 0.18f, 0.82f));

        spillParticles.Play();
    }

    private void SpawnCleanStarBurst()
    {
        GameObject burstObject = new GameObject("Runtime_CleanStarBurst");
        burstObject.transform.position = transform.position + Vector3.up * 0.25f;
        burstObject.transform.rotation = Quaternion.identity;

        ParticleSystem burst = burstObject.AddComponent<ParticleSystem>();
        var main = burst.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.35f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(cleanStarBurstSize * 0.65f, cleanStarBurstSize * 1.25f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.75f, 1f, 1f, 0.95f),
            new Color(1f, 1f, 0.65f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(8, cleanStarBurstCount * 2);

        var emission = burst.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)Mathf.Clamp(cleanStarBurstCount, 1, 120))
        });

        var shape = burst.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;

        var color = burst.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(BuildCleanStarGradient());

        var size = burst.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1.25f, 1f, 0.15f));

        var rotation = burst.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-360f * Mathf.Deg2Rad, 360f * Mathf.Deg2Rad);

        var renderer = burst.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = GetFourPointStarMesh();
        ConfigureParticleRenderer(renderer, Color.white);

        burst.Play();
        Destroy(burstObject, Mathf.Max(0.5f, cleanBurstLifetime));
    }

    private static Gradient BuildDirtyParticleGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.55f, 0.32f, 0.10f), 0f),
                new GradientColorKey(new Color(0.95f, 0.72f, 0.28f), 0.5f),
                new GradientColorKey(new Color(0.30f, 0.20f, 0.10f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.56f, 0.18f),
                new GradientAlphaKey(0.46f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private bool ShouldSkipDirtyParticles()
    {
        if (!suppressDirtyParticlesForWaterSpills)
            return false;

        if (GetComponentInParent<WaterSpillManager>() != null)
            return true;

        string n = gameObject.name;
        return !string.IsNullOrEmpty(n) && n.ToLowerInvariant().Contains("water");
    }

    private static Gradient BuildCleanStarGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.65f, 1f, 1f), 0f),
                new GradientColorKey(Color.white, 0.45f),
                new GradientColorKey(new Color(1f, 0.9f, 0.35f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(1f, 0.42f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static void ConfigureParticleRenderer(ParticleSystemRenderer renderer, Color tint)
    {
        if (renderer == null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            return;

        var material = new Material(shader);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", tint);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", tint);
        else if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", tint);

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        renderer.material = material;
    }

    private static Mesh GetFourPointStarMesh()
    {
        if (s_fourPointStarMesh != null)
            return s_fourPointStarMesh;

        s_fourPointStarMesh = new Mesh { name = "Runtime_FourPointStarMesh" };
        s_fourPointStarMesh.vertices = new[]
        {
            new Vector3(0f, 1f, 0f),
            new Vector3(0.18f, 0.18f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0.18f, -0.18f, 0f),
            new Vector3(0f, -1f, 0f),
            new Vector3(-0.18f, -0.18f, 0f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(-0.18f, 0.18f, 0f),
            Vector3.zero
        };
        s_fourPointStarMesh.triangles = new[]
        {
            8, 0, 1,
            8, 1, 2,
            8, 2, 3,
            8, 3, 4,
            8, 4, 5,
            8, 5, 6,
            8, 6, 7,
            8, 7, 0
        };
        s_fourPointStarMesh.RecalculateBounds();
        return s_fourPointStarMesh;
    }
}
