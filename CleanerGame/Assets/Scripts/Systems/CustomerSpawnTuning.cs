using UnityEngine;

public class CustomerSpawnTuning : MonoBehaviour
{
    [Header("Spawn Base")]
    [SerializeField] private float baseSpawnIntervalSeconds = 6f;
    [SerializeField] private int maxActiveCustomers = 12;

    [Header("Day Phase Multipliers")]
    [SerializeField] private float morningMultiplier = 0.25f;
    [SerializeField] private float rushMultiplier = 1f;
    [SerializeField] private float afternoonMultiplier = 0.5f;
    [SerializeField] private float closingMultiplier = 0f;

    [Header("Reputation Caps")]
    [SerializeField] private int reputation0Cap = 6;
    [SerializeField] private int reputation1Cap = 8;
    [SerializeField] private int reputation2Cap = 9;
    [SerializeField] private int reputation3Cap = 12;

    [Header("Reputation Spawn Bonus (seconds)")]
    [SerializeField] private float reputation1SpawnBonus = 1f;
    [SerializeField] private float reputation2SpawnBonus = 2f;
    [SerializeField] private float reputation3SpawnBonus = 2f;

    [Header("Dirtiness Thresholds (spills)")]
    [SerializeField] private int lightDirtinessMinSpillCount = 1;
    [SerializeField] private int mediumDirtinessMinSpillCount = 4;
    [SerializeField] private int veryDirtyMinSpillCount = 6;
    [SerializeField] private int filthyMinSpillCount = 7;
    [SerializeField] private int filthySpillCountSpan = 5;

    [Header("Dirtiness Customer Caps")]
    [SerializeField] private int cleanMaxCustomersCap = 12;
    [SerializeField] private Vector2Int lightDirtinessCustomerRange = new Vector2Int(8, 10);
    [SerializeField] private Vector2Int mediumDirtinessCustomerRange = new Vector2Int(4, 6);
    [SerializeField] private Vector2Int filthyDirtinessCustomerRange = new Vector2Int(1, 2);

    [Header("Dirtiness Spawn Penalty (seconds)")]
    [SerializeField] private int dirtyPenaltySeconds = 1;
    [SerializeField] private int veryDirtyPenaltySeconds = 2;
    [SerializeField] private int filthyPenaltySeconds = 3;

    public float BaseSpawnIntervalSeconds => baseSpawnIntervalSeconds;
    public int MaxActiveCustomers => maxActiveCustomers;

    public float GetDayMultiplier(RestaurantDayCycle.DayPhase phase)
    {
        switch (phase)
        {
            case RestaurantDayCycle.DayPhase.Morning:
                return morningMultiplier;
            case RestaurantDayCycle.DayPhase.RushHour:
                return rushMultiplier;
            case RestaurantDayCycle.DayPhase.AfternoonSlowdown:
                return afternoonMultiplier;
            default:
                return closingMultiplier;
        }
    }

    public int GetReputationCap(int reputation)
    {
        switch (Mathf.Max(0, reputation))
        {
            case 0:
                return reputation0Cap;
            case 1:
                return reputation1Cap;
            case 2:
                return reputation2Cap;
            default:
                return reputation3Cap;
        }
    }

    public float GetReputationSpawnBonusSeconds(int reputation)
    {
        switch (Mathf.Max(0, reputation))
        {
            case 1:
                return reputation1SpawnBonus;
            case 2:
                return reputation2SpawnBonus;
            case 3:
                return reputation3SpawnBonus;
            default:
                return 0f;
        }
    }

    public int GetDirtinessSpawnPenaltySeconds(RestaurantManager.DirtinessLevel level)
    {
        switch (level)
        {
            case RestaurantManager.DirtinessLevel.Dirty:
                return dirtyPenaltySeconds;
            case RestaurantManager.DirtinessLevel.VeryDirty:
                return veryDirtyPenaltySeconds;
            case RestaurantManager.DirtinessLevel.Filthy:
                return filthyPenaltySeconds;
            default:
                return 0;
        }
    }

    public int LightDirtinessMinSpillCount => lightDirtinessMinSpillCount;
    public int MediumDirtinessMinSpillCount => mediumDirtinessMinSpillCount;
    public int VeryDirtyMinSpillCount => veryDirtyMinSpillCount;
    public int FilthyMinSpillCount => filthyMinSpillCount;
    public int FilthySpillCountSpan => filthySpillCountSpan;

    public int CleanMaxCustomersCap => cleanMaxCustomersCap;
    public Vector2Int LightDirtinessCustomerRange => lightDirtinessCustomerRange;
    public Vector2Int MediumDirtinessCustomerRange => mediumDirtinessCustomerRange;
    public Vector2Int FilthyDirtinessCustomerRange => filthyDirtinessCustomerRange;
}
