using System.Collections;
using UnityEngine;

public class RegularCustomerEmotions : MonoBehaviour
{
    public MeshRenderer quadRenderer;

    public Material whistlingMat;
    public Material spillMat;

    private Coroutine spillCoroutine;

    private void Awake()
    {
        SetWhistling();
    }

    public void SetWhistling()
    {
        if (quadRenderer != null && whistlingMat != null)
            quadRenderer.material = whistlingMat;
    }

    public void ShowSpillForThreeSeconds()
    {
        if (spillCoroutine != null)
            StopCoroutine(spillCoroutine);

        spillCoroutine = StartCoroutine(SpillRoutine());
    }

    private IEnumerator SpillRoutine()
    {
        if (quadRenderer != null && spillMat != null)
            quadRenderer.material = spillMat;

        yield return new WaitForSeconds(3f);

        SetWhistling();
        spillCoroutine = null;
    }
}