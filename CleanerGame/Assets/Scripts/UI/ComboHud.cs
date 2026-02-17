using TMPro;
using UnityEngine;

public class ComboHud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private SpillComboSystem comboSystem;

    [Header("Text")]
    [SerializeField] private string idleText = string.Empty;
    [SerializeField] private string comboFormat = "Combo x{0}";

    [Header("Refresh")]
    [SerializeField] private float refreshSeconds = 0.1f;

    private float refreshTimer;

    private void Awake()
    {
        if (comboText == null)
            comboText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        ResolveComboSystem();

        if (comboSystem != null)
            comboSystem.OnComboChanged += HandleComboChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (comboSystem != null)
            comboSystem.OnComboChanged -= HandleComboChanged;
    }

    private void Update()
    {
        refreshTimer += Time.deltaTime;
        if (refreshTimer < refreshSeconds) return;

        refreshTimer = 0f;
        Refresh();
    }

    private void HandleComboChanged()
    {
        Refresh();
    }

    private void ResolveComboSystem()
    {
        if (comboSystem != null) return;
        comboSystem = SpillComboSystem.Instance ?? FindFirstObjectByType<SpillComboSystem>();
    }

    private void Refresh()
    {
        if (comboText == null) return;

        if (comboSystem == null)
            ResolveComboSystem();

        if (comboSystem == null)
        {
            comboText.text = idleText;
            return;
        }

        if (comboSystem.ComboCount <= 0)
        {
            comboText.text = idleText;
            return;
        }

        string text = string.Format(comboFormat, comboSystem.ComboCount);
        comboText.text = text;
    }
}
