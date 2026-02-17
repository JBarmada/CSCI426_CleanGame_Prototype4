using TMPro;
using UnityEngine;

public class DirtinessHud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private RestaurantManager restaurantManager;

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

    [Header("Refresh")]
    [SerializeField] private float refreshSeconds = 0.25f;

    private float refreshTimer;

    private void Awake()
    {
        if (statusText == null)
            statusText = GetComponent<TMP_Text>();

        if (restaurantManager == null)
            restaurantManager = RestaurantManager.Instance;
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
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
                break;
            case RestaurantManager.DirtinessLevel.Dirty:
                statusText.text = dirtyText;
                statusText.color = dirtyColor;
                break;
            case RestaurantManager.DirtinessLevel.VeryDirty:
                statusText.text = veryDirtyText;
                statusText.color = veryDirtyColor;
                break;
            default:
                statusText.text = filthyText;
                statusText.color = filthyColor;
                break;
        }
    }
}
