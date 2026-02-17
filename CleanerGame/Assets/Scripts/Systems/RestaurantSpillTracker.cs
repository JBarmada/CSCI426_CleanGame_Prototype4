using UnityEngine;

public class RestaurantSpillTracker : MonoBehaviour
{
    [SerializeField] private RestaurantDayCycle dayCycle;

    public int SpillsCleaned => spillsCleaned;

    private int spillsCleaned;

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

    private void HandleDayStarted(int day)
    {
        spillsCleaned = 0;
    }
}
