using UnityEngine;

public class RestaurantReputation : MonoBehaviour
{
    [Header("Reputation")]
    [SerializeField] private int maxReputation = 3;

    public int Reputation => reputation;

    private int reputation;

    public bool TryIncreaseReputation()
    {
        if (reputation >= maxReputation) return false;
        reputation++;
        return true;
    }

    public int GetCustomerCapForReputation()
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

    public float GetSpawnIntervalBonusSeconds()
    {
        if (reputation <= 0) return 0f;
        if (reputation == 1) return 1f;
        return 2f;
    }
}
