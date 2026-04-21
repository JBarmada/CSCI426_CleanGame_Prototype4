using UnityEngine;

public class RestaurantSpillTracker : MonoBehaviour
{
    [SerializeField] private RestaurantDayCycle dayCycle;

    public int SpillsCleaned => spillsCleaned;

    /// <summary>How many spill prefabs were spawned this in-game day (drives decorative trash).</summary>
    public int SpillsSpawnedThisDay => spillsSpawnedThisDay;

    private int spillsCleaned;
    private int spillsSpawnedThisDay;

    private void Awake()
    {
        if (dayCycle == null)
            dayCycle = GetComponent<RestaurantDayCycle>();
    }

    private void OnEnable()
    {
        if (dayCycle != null)
            dayCycle.DayStarted += HandleDayStarted;
    }

    private void OnDisable()
    {
        if (dayCycle != null)
            dayCycle.DayStarted -= HandleDayStarted;
    }

    public void AddSpillCleaned()
    {
        spillsCleaned++;
    }

    public void RegisterSpillSpawned()
    {
        spillsSpawnedThisDay++;
    }

    private void HandleDayStarted(int day)
    {
        spillsCleaned = 0;
        spillsSpawnedThisDay = 0;
    }
}
