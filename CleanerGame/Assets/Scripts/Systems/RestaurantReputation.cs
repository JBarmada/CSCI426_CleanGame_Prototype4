using UnityEngine;

public class RestaurantReputation : MonoBehaviour
{
    [Header("Reputation")]
    [SerializeField] private int maxReputation = 3;
    [SerializeField] private CustomerSpawnTuning spawnTuning;

    public int Reputation => reputation;

    private int reputation;

    private void Awake()
    {
        if (spawnTuning == null)
            spawnTuning = FindFirstObjectByType<CustomerSpawnTuning>();
    }

    public bool TryIncreaseReputation()
    {
        if (reputation >= maxReputation) return false;
        reputation++;
        return true;
    }

    public void DebugSetReputation(int value)
    {
        reputation = Mathf.Clamp(value, 0, maxReputation);
    }

    public int GetCustomerCapForReputation()
    {
        if (spawnTuning == null)
        {
            switch (reputation)
            {
                case 0:
                    return 6;
                case 1:
                    return 8;
                case 2:
                    return 9;
                default:
                    return 12;
            }
        }

        return spawnTuning.GetReputationCap(reputation);
    }

    public float GetSpawnIntervalBonusSeconds()
    {
        if (spawnTuning == null)
        {
            if (reputation <= 0) return 0f;
            if (reputation == 1) return 1f;
            return 2f;
        }

        return spawnTuning.GetReputationSpawnBonusSeconds(reputation);
    }
}
