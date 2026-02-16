using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SpillManager : MonoBehaviour
{
    [Header("Cleaning")]
    [SerializeField] private KeyCode cleanKey = KeyCode.K;
    [SerializeField] private int pressesToClean = 3;

    [Header("Fade")]
    [SerializeField] private SpriteRenderer spriteRenderer; // assign, or auto-find
    [SerializeField] private bool destroyRoot = false; // true if this script is on a child trigger

    private bool playerInRange;
    private int pressesRemaining;
    private Collider col;

    private void Awake()
    {
        pressesRemaining = pressesToClean;

        col = GetComponent<Collider>();
        col.isTrigger = true;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        UpdateVisual(); // start at full alpha
        Debug.Log($"[Spill3D] Awake. Trigger={col.isTrigger} Pos={transform.position}");
    }

    private void Update()
    {
        if (!playerInRange) return;

        if (Input.GetKeyDown(cleanKey))
        {
            pressesRemaining--;
            UpdateVisual();

            Debug.Log($"[Spill3D] Clean progress {pressesToClean - pressesRemaining}/{pressesToClean}");

            if (pressesRemaining <= 0)
            {
                Debug.Log("[Spill3D] Clean complete -> Destroy");
                if (destroyRoot && transform.parent != null) Destroy(transform.parent.gameObject);
                else Destroy(gameObject);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[Spill3D] Enter with {other.name} tag={other.tag}");

        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            Debug.Log("[Spill3D] Player IN RANGE ✅");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log($"[Spill3D] Exit with {other.name}");

        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            Debug.Log("[Spill3D] Player OUT OF RANGE ❌");
        }
    }

    private void UpdateVisual()
    {
        if (spriteRenderer == null) return;

        float alpha = Mathf.Clamp01((float)pressesRemaining / pressesToClean);
        var c = spriteRenderer.color;
        c.a = alpha;
        spriteRenderer.color = c;
    }
}
