using UnityEngine;

public class RestaurantManager : MonoBehaviour
{
    public static RestaurantManager Instance;

    public int Dirtiness { get; private set; }
    public int Popularity { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    public void AddDirt(int amount)
    {
        Dirtiness += amount;

        if (Dirtiness > 50)
            DecreasePopularity();
    }

    void DecreasePopularity()
    {
        Popularity--;
    }
}
