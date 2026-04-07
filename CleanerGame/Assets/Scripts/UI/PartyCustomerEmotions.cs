using UnityEngine;

public class PartyCustomerEmotions : MonoBehaviour
{
    public MeshRenderer quadRenderer;

    public Material idleMat;
    public Material abouttoleaveMat;
    public Material cryingMat;

    private bool hasShownAboutToLeave = false;

    private void Awake()
    {
        SetIdle();
    }

    public void SetIdle()
    {
        if (quadRenderer != null && idleMat != null)
            quadRenderer.material = idleMat;
    }

    public void SetAbouttoleave()
    {
        if (quadRenderer != null && abouttoleaveMat != null)
            quadRenderer.material = abouttoleaveMat;
    }

    public void SetCrying()
    {
        if (quadRenderer != null && cryingMat != null)
            quadRenderer.material = cryingMat;
    }

    // Call this when the customer starts sitting down
    public void BeginSitting()
    {
        hasShownAboutToLeave = false;
        SetIdle();
    }

    // Call this every frame while sitting
    public void UpdateSittingTimer(float timeRemaining)
    {
        if (!hasShownAboutToLeave && timeRemaining <= 3f)
        {
            hasShownAboutToLeave = true;
            SetAbouttoleave();
        }
    }

    // Call this once when the customer gets up / starts leaving
    public void BeginLeaving()
    {
        SetCrying();
    }
}