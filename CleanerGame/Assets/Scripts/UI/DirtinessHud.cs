using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DirtinessHud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private RestaurantManager restaurantManager;

    [Header("Screen filth tint")]
    [SerializeField] private bool enableScreenFilthTint = true;
    [Tooltip("Optional full-screen tint; if unset, a non-raycast Image is created on the parent Canvas")]
    [SerializeField] private Image screenFilthTint;
    [SerializeField] private bool forceRuntimeScreenTint = true;

    [Header("Text")]
    [SerializeField] private string cleanText = "Clean";
    [SerializeField] private string dirtyText = "Dirty";
    [SerializeField] private string veryDirtyText = "Very Dirty";
    [SerializeField] private string filthyText = "Filthy";

    [Header("Colors")]
    [SerializeField] private Color cleanColor = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color dirtyColor = new Color(0.45f, 0.25f, 0.1f);
    [SerializeField] private Color veryDirtyColor = new Color(0.7f, 0.25f, 0.1f);
    [SerializeField] private Color filthyColor = new Color(0.9f, 0.1f, 0.1f);

    [Header("Screen tint by tier (clear when clean, greener with filth)")]
    [SerializeField] private Color screenTintColor = new Color(0.06f, 0.62f, 0.08f, 1f);
    [SerializeField] private float screenTintAlphaClean;
    [SerializeField] private float screenTintAlphaDirty = 0.18f;
    [SerializeField] private float screenTintAlphaVeryDirty = 0.35f;
    [SerializeField] private float screenTintAlphaFilthy = 0.55f;
    [SerializeField] private float screenTintFadeSpeed = 3f;
    [SerializeField] private int screenTintSortingOrder = 1000;

    [Header("Screen tint by spill count")]
    [SerializeField] private bool useSpillCountForScreenTint = true;
    [SerializeField] private int spillsPerGreenStep = 2;
    [SerializeField] private float screenTintAlphaPerSpillStep = 0.12f;
    [SerializeField] private float screenTintMaxAlphaFromSpills = 0.6f;
    [SerializeField] private bool drawScreenTintWithOnGUI = true;

    [Header("Refresh")]
    [SerializeField] private float refreshSeconds = 0.25f;

    [Header("Layering")]
    [SerializeField] private int sortingOrder = 300;

    private float refreshTimer;
    private float screenTintTargetAlpha;
    private float screenTintCurrentAlpha;

    private void Awake()
    {
        if (statusText == null)
            statusText = GetComponent<TMP_Text>();

        if (restaurantManager == null)
            restaurantManager = RestaurantManager.Instance;

        UISortingUtility.EnsureSorting(gameObject, sortingOrder);
        EnsureScreenFilthTint();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        if (useSpillCountForScreenTint)
            UpdateScreenTintFromSpillCount();

        if (screenFilthTint != null)
        {
            screenTintCurrentAlpha = Mathf.MoveTowards(
                screenTintCurrentAlpha,
                screenTintTargetAlpha,
                screenTintFadeSpeed * Time.deltaTime);
            var c = screenTintColor;
            c.a = screenTintCurrentAlpha * screenTintColor.a;
            screenFilthTint.color = c;
        }

        refreshTimer += Time.deltaTime;
        if (refreshTimer < refreshSeconds) return;

        refreshTimer = 0f;
        Refresh();
    }

    private void OnGUI()
    {
        if (!drawScreenTintWithOnGUI || !enableScreenFilthTint || screenTintCurrentAlpha <= 0.001f)
            return;

        Color previousColor = GUI.color;
        Color tint = screenTintColor;
        tint.a = screenTintCurrentAlpha * screenTintColor.a;
        GUI.color = tint;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private void UpdateScreenTintFromSpillCount()
    {
        int spillsPerStep = Mathf.Max(1, spillsPerGreenStep);
        int activeSpills = CountActiveSpills();
        int greenSteps = activeSpills / spillsPerStep;
        screenTintTargetAlpha = Mathf.Clamp(
            greenSteps * Mathf.Max(0f, screenTintAlphaPerSpillStep),
            screenTintAlphaClean,
            Mathf.Max(screenTintAlphaClean, screenTintMaxAlphaFromSpills));
    }

    private int CountActiveSpills()
    {
        int activeSpills = SpillManager.ActiveSpills == null ? 0 : SpillManager.ActiveSpills.Count;
        if (activeSpills > 0)
            return activeSpills;

        return FindObjectsByType<SpillManager>(FindObjectsSortMode.None).Length;
    }

    private void Refresh()
    {
        if (restaurantManager == null)
            restaurantManager = RestaurantManager.Instance;
        if (restaurantManager == null) return;

        var level = restaurantManager.GetDirtinessLevel();
        switch (level)
        {
            case RestaurantManager.DirtinessLevel.Clean:
                SetStatusText(cleanText, cleanColor);
                SetTierScreenTintTarget(screenTintAlphaClean);
                break;
            case RestaurantManager.DirtinessLevel.Dirty:
                SetStatusText(dirtyText, dirtyColor);
                SetTierScreenTintTarget(screenTintAlphaDirty);
                break;
            case RestaurantManager.DirtinessLevel.VeryDirty:
                SetStatusText(veryDirtyText, veryDirtyColor);
                SetTierScreenTintTarget(screenTintAlphaVeryDirty);
                break;
            default:
                SetStatusText(filthyText, filthyColor);
                SetTierScreenTintTarget(screenTintAlphaFilthy);
                break;
        }
    }

    private void SetTierScreenTintTarget(float targetAlpha)
    {
        if (!useSpillCountForScreenTint)
            screenTintTargetAlpha = targetAlpha;
    }

    private void SetStatusText(string text, Color color)
    {
        if (statusText == null) return;

        statusText.text = text;
        statusText.color = color;
    }

    private void EnsureScreenFilthTint()
    {
        if (!enableScreenFilthTint)
            return;

        if (screenFilthTint != null && !forceRuntimeScreenTint)
            return;

        var go = new GameObject("ScreenFilthTintCanvas", typeof(RectTransform), typeof(Canvas));
        go.layer = gameObject.layer;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = screenTintSortingOrder;

        var rt = go.GetComponent<RectTransform>();
        rt.localScale = Vector3.one;
        rt.anchoredPosition3D = Vector3.zero;

        var imageGo = new GameObject("ScreenFilthTint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageGo.layer = gameObject.layer;
        imageGo.transform.SetParent(go.transform, false);

        var imageRt = imageGo.GetComponent<RectTransform>();
        imageRt.anchorMin = Vector2.zero;
        imageRt.anchorMax = Vector2.one;
        imageRt.offsetMin = Vector2.zero;
        imageRt.offsetMax = Vector2.zero;
        imageRt.pivot = new Vector2(0.5f, 0.5f);
        imageRt.localScale = Vector3.one;
        imageRt.anchoredPosition3D = Vector3.zero;

        var img = imageGo.GetComponent<Image>();
        img.sprite = null;
        img.type = Image.Type.Simple;
        img.color = new Color(screenTintColor.r, screenTintColor.g, screenTintColor.b, 0f);
        img.raycastTarget = false;

        screenFilthTint = img;
        screenTintCurrentAlpha = 0f;
        screenTintTargetAlpha = screenTintAlphaClean;
    }
}
