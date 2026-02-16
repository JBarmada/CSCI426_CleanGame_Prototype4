using UnityEngine;

public class RestaurantManager : MonoBehaviour
{
    public static RestaurantManager Instance;

    public int Dirtiness { get; private set; }
    public int Popularity { get; private set; }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
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
}
