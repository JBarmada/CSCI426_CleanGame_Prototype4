using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SneakerPowerupButton : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private CoinWallet wallet;
    [SerializeField] private SneakerPowerupSystem sneakerSystem;
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image icon;
    [SerializeField] private RectTransform powerupRoot;
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text stateText;

    [Header("Cost")]
    [SerializeField] private int sneakerCost = 9;
    [SerializeField] private KeyCode shortcutKey = KeyCode.N;

    [Header("Visuals")]
    [SerializeField] private Color readyBackground = new Color(0.26f, 0.58f, 0.42f, 1f);
    [SerializeField] private Color ownedBackground = new Color(0.79f, 0.58f, 0.18f, 1f);
    [SerializeField] private Color readyIcon = Color.white;
    [SerializeField] private Color lockedIcon = new Color(0.68f, 0.71f, 0.74f, 1f);
    [SerializeField] private Color readyText = Color.white;
    [SerializeField] private Color lockedText = new Color(0.82f, 0.84f, 0.88f, 1f);
    [SerializeField] private Color ownedText = new Color(1f, 0.95f, 0.72f, 1f);
    [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.38f;

    [Header("Labels")]
    [SerializeField] private string coinSuffix = " coins";
    [SerializeField] private string readyStateLabel = "READY";
    [SerializeField] private string needCoinsStateLabel = "NEED COINS";
    [SerializeField] private string maxedStateLabel = "MAXED";
    [SerializeField] private string ownedStateLabel = "OWNED";
    [SerializeField] private string ownedMultiplierFormat = "x{0:F2}";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip availableClip;
    [Range(0f, 1f)]
    [SerializeField] private float availableVolume = 1f;
    [SerializeField] private AudioClip purchasedClip;
    [Range(0f, 1f)]
    [SerializeField] private float purchasedVolume = 1f;

    private bool wasAvailableLastFrame;

    private void Awake()
    {
        shortcutKey = KeyCode.N;

        if (powerupRoot == null)
            powerupRoot = transform.parent as RectTransform;

        if (button == null)
            button = GetComponent<Button>();

        if (backgroundImage == null && button != null)
            backgroundImage = button.targetGraphic as Image;

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (icon == null)
            icon = FindIconImage();

        if (wallet == null)
            wallet = CoinWallet.Instance != null ? CoinWallet.Instance : FindFirstObjectByType<CoinWallet>();

        if (sneakerSystem == null)
            sneakerSystem = SneakerPowerupSystem.Instance != null ? SneakerPowerupSystem.Instance : FindFirstObjectByType<SneakerPowerupSystem>();

        if (audioSource == null && availableClip != null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
        }

        if (button != null)
        {
            button.onClick.RemoveListener(TryActivatePowerup);
            button.onClick.AddListener(TryActivatePowerup);
            button.transition = Selectable.Transition.ColorTint;
        }

        if (wallet != null)
            wallet.CoinsChanged += HandleCoinsChanged;

        if (sneakerSystem != null)
            sneakerSystem.OnChanged += Refresh;

        PowerupHudLayout.MoveBelowCoinHud(powerupRoot);
        Refresh();
    }

    private void OnDestroy()
    {
        if (wallet != null)
            wallet.CoinsChanged -= HandleCoinsChanged;

        if (sneakerSystem != null)
            sneakerSystem.OnChanged -= Refresh;

        if (button != null)
            button.onClick.RemoveListener(TryActivatePowerup);
    }

    private void HandleCoinsChanged(int _)
    {
        Refresh();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (Input.GetKeyDown(shortcutKey))
            TryActivatePowerup();
    }

    private void Refresh()
    {
        if (button == null || icon == null || wallet == null || sneakerSystem == null)
            return;

        bool canAfford = wallet.Coins >= sneakerCost;
        bool hasUses = sneakerSystem.CanUseToday();
        bool isOwned = sneakerSystem.IsOwned;
        bool isAvailableNow = canAfford && hasUses;

        if (isAvailableNow && !wasAvailableLastFrame)
            PlayAvailableSound();

        wasAvailableLastFrame = isAvailableNow;

        button.interactable = isAvailableNow;

        if (costText != null)
            costText.text = sneakerCost + coinSuffix;

        if (stateText != null)
        {
            stateText.text = isOwned
                ? ownedStateLabel + " " + string.Format(ownedMultiplierFormat, sneakerSystem.CurrentMultiplier)
                : (isAvailableNow ? readyStateLabel : (hasUses ? needCoinsStateLabel : maxedStateLabel));
            stateText.color = isOwned ? ownedText : (isAvailableNow ? readyText : lockedText);
        }

        bool isInactive = !isAvailableNow && !isOwned;
        Color backgroundTint = isOwned ? ownedBackground : readyBackground;
        backgroundTint.a = 1f;
        Color iconTint = isAvailableNow || isOwned ? readyIcon : lockedIcon;
        iconTint.a = isInactive ? inactiveAlpha : 1f;
        icon.color = iconTint;

        if (backgroundImage != null)
            backgroundImage.color = backgroundTint;

        ApplyColorBlock(backgroundTint);
    }

    private void PlayAvailableSound()
    {
        if (availableClip == null)
            return;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
        }

        audioSource.PlayOneShot(availableClip, availableVolume);
    }

    private void ApplyColorBlock(Color tint)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = tint;
        colors.highlightedColor = Color.Lerp(tint, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(tint, Color.black, 0.16f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(tint.r, tint.g, tint.b, 1f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private void TryActivatePowerup()
    {
        if (wallet == null || sneakerSystem == null)
            return;

        if (!sneakerSystem.CanUseToday())
            return;

        if (!wallet.TrySpend(sneakerCost))
            return;

        if (!sneakerSystem.TryConsumeUse())
        {
            wallet.AddCoins(sneakerCost);
            return;
        }

        PlayPurchasedSound();
        Debug.Log($"[SneakerPowerupButton] Purchased. Uses={sneakerSystem.UsesToday}, Mult={sneakerSystem.CurrentMultiplier:F2}x");
        Refresh();
    }

    private void PlayPurchasedSound()
    {
        if (purchasedClip == null)
            return;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
        }

        audioSource.PlayOneShot(purchasedClip, purchasedVolume);
    }

    private Image FindIconImage()
    {
        if (button == null)
            return null;

        Image[] images = button.GetComponentsInChildren<Image>(true);
        for (int index = 0; index < images.Length; index++)
        {
            Image candidate = images[index];
            if (candidate == null)
                continue;

            if (candidate == backgroundImage)
                continue;

            if (button.targetGraphic != null && candidate == button.targetGraphic)
                continue;

            return candidate;
        }

        return null;
    }

}