using UnityEngine;
using UnityEngine.UI;

public class ReputationHud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RestaurantReputation reputation;
    [SerializeField] private Image[] stars;

    [Header("Sprites")]
    [SerializeField] private Sprite emptyStar;
    [SerializeField] private Sprite fullStar;

    [Header("Refresh")]
    [SerializeField] private float refreshSeconds = 0.25f;

    private float refreshTimer;

    private void Awake()
    {
        if (reputation == null)
            reputation = FindFirstObjectByType<RestaurantReputation>();
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
        if (reputation == null || stars == null) return;

        int rep = Mathf.Max(0, reputation.Reputation);
        for (int i = 0; i < stars.Length; i++)
        {
            if (stars[i] == null) continue;
            stars[i].sprite = i < rep ? fullStar : emptyStar;
        }
    }
}
