using UnityEngine;

public class RestaurantManager : MonoBehaviour
{
    public static RestaurantManager Instance;

    public int Dirtiness { get; private set; }
    public int Popularity { get; private set; }

    [Header("Dirtiness Tracking")]
    [SerializeField] private float dirtinessRefreshSeconds = 1f;

    [Header("Customer Caps")]
    [SerializeField] private int cleanMaxCustomers = 12;
    [SerializeField] private int someDirtinessMinSpills = 1;
    [SerializeField] private int mediumDirtinessMinSpills = 4;
    [SerializeField] private int tooMuchDirtinessMinSpills = 7;
    [SerializeField] private int tooMuchDirtinessSpan = 5;
    [SerializeField] private Vector2Int someDirtinessCustomers = new Vector2Int(8, 10);
    [SerializeField] private Vector2Int mediumDirtinessCustomers = new Vector2Int(4, 6);
    [SerializeField] private Vector2Int tooMuchDirtinessCustomers = new Vector2Int(1, 2);

    private float dirtinessTimer;
    private int cachedMaxCustomers;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        RefreshDirtiness();
    }

    private void Update()
    {
        dirtinessTimer += Time.deltaTime;
        if (dirtinessTimer < dirtinessRefreshSeconds) return;

        dirtinessTimer = 0f;
        RefreshDirtiness();
    }

    public void AddDirt(int amount)
    {
        Dirtiness += amount;
        Dirtiness = Mathf.Max(Dirtiness, 0);

        if (Dirtiness > 50)
            DecreasePopularity();
    }

    public void CleanDirt(int amount)
    {
        Dirtiness -= amount;
        Dirtiness = Mathf.Max(Dirtiness, 0);
    }

    void DecreasePopularity()
    {
        Popularity--;
    }

    public int GetMaxCustomersForDirtiness()
    {
        return cachedMaxCustomers;
    }

    private void RefreshDirtiness()
    {
        Dirtiness = FindObjectsByType<SpillManager>(FindObjectsSortMode.None).Length;
        Dirtiness = Mathf.Max(Dirtiness, 0);
        cachedMaxCustomers = CalculateMaxCustomers(Dirtiness);
    }

    private int CalculateMaxCustomers(int dirtiness)
    {
        if (dirtiness <= 0) return cleanMaxCustomers;

        if (dirtiness < mediumDirtinessMinSpills)
            return EvaluateRange(someDirtinessCustomers, dirtiness, someDirtinessMinSpills, mediumDirtinessMinSpills - 1);

        if (dirtiness < tooMuchDirtinessMinSpills)
            return EvaluateRange(mediumDirtinessCustomers, dirtiness, mediumDirtinessMinSpills, tooMuchDirtinessMinSpills - 1);

        int tooMuchMax = tooMuchDirtinessMinSpills + Mathf.Max(1, tooMuchDirtinessSpan);
        return EvaluateRange(tooMuchDirtinessCustomers, dirtiness, tooMuchDirtinessMinSpills, tooMuchMax);
    }

    private int EvaluateRange(Vector2Int range, int value, int min, int max)
    {
        int minValue = Mathf.Min(range.x, range.y);
        int maxValue = Mathf.Max(range.x, range.y);

        if (max <= min)
            return maxValue;

        float t = Mathf.InverseLerp(min, max, value);
        return Mathf.RoundToInt(Mathf.Lerp(minValue, maxValue, t));
    }
}
