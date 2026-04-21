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

    [Header("Screen tint by tier (sickly green, darkens with filth)")]
    [SerializeField] private Color screenTintColor = new Color(0.18f, 0.38f, 0.16f, 1f);
    [SerializeField] private float screenTintAlphaClean;
    [SerializeField] private float screenTintAlphaDirty = 0.12f;
    [SerializeField] private float screenTintAlphaVeryDirty = 0.26f;
    [SerializeField] private float screenTintAlphaFilthy = 0.48f;
    [SerializeField] private float screenTintFadeSpeed = 3f;

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

    private void Refresh()
    {
        if (statusText == null) return;
        if (restaurantManager == null)
            restaurantManager = RestaurantManager.Instance;
        if (restaurantManager == null) return;

        var level = restaurantManager.GetDirtinessLevel();
        switch (level)
        {
            case RestaurantManager.DirtinessLevel.Clean:
                statusText.text = cleanText;
                statusText.color = cleanColor;
                screenTintTargetAlpha = screenTintAlphaClean;
                break;
            case RestaurantManager.DirtinessLevel.Dirty:
                statusText.text = dirtyText;
                statusText.color = dirtyColor;
                screenTintTargetAlpha = screenTintAlphaDirty;
                break;
            case RestaurantManager.DirtinessLevel.VeryDirty:
                statusText.text = veryDirtyText;
                statusText.color = veryDirtyColor;
                screenTintTargetAlpha = screenTintAlphaVeryDirty;
                break;
            default:
                statusText.text = filthyText;
                statusText.color = filthyColor;
                screenTintTargetAlpha = screenTintAlphaFilthy;
                break;
        }
    }

    private void EnsureScreenFilthTint()
    {
        if (!enableScreenFilthTint || screenFilthTint != null)
            return;

        Canvas leafCanvas = GetComponentInParent<Canvas>();
        if (leafCanvas == null)
            return;

        // Nest under the root canvas so anchors stretch the full viewport (not just a HUD sub-panel).
        Canvas rootCanvas = leafCanvas.rootCanvas != null ? leafCanvas.rootCanvas : leafCanvas;
        Transform rootTransform = rootCanvas.transform;

        var go = new GameObject("ScreenFilthTint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = rootCanvas.gameObject.layer;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(rootTransform, false);
        rt.SetAsFirstSibling();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.anchoredPosition3D = Vector3.zero;

        var img = go.GetComponent<Image>();
        img.sprite = null;
        img.type = Image.Type.Simple;
        img.color = new Color(screenTintColor.r, screenTintColor.g, screenTintColor.b, 0f);
        img.raycastTarget = false;

        screenFilthTint = img;
        screenTintCurrentAlpha = 0f;
        screenTintTargetAlpha = screenTintAlphaClean;
    }
}
