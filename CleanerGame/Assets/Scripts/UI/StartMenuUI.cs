using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StartMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button startButton;
    [SerializeField] private Button tutorialButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private GameFlowManager gameFlow;

    private void Awake()
    {
        if (root == null)
            root = gameObject;
        if (canvasGroup == null && root != null)
            canvasGroup = root.GetComponent<CanvasGroup>();
        if (gameFlow == null)
            gameFlow = GameFlowManager.Instance;

        EnsureTutorialButton();

        if (startButton != null)
            startButton.onClick.AddListener(OnStartPressed);
        if (tutorialButton != null)
            tutorialButton.onClick.AddListener(OnTutorialPressed);
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitPressed);

        if (gameFlow != null)
            gameFlow.PauseGame();
        else
            Time.timeScale = 0f;
    }

    private void EnsureTutorialButton()
    {
        if (tutorialButton != null || startButton == null)
            return;

        RectTransform startRt = startButton.GetComponent<RectTransform>();
        if (startRt == null)
            return;

        GameObject clone = Instantiate(startButton.gameObject, startRt.parent);
        clone.name = "TutorialButton";
        var cloneRt = clone.GetComponent<RectTransform>();
        if (cloneRt != null)
        {
            Vector2 p = cloneRt.anchoredPosition;
            p.y = (cloneRt.anchoredPosition.y + GetSiblingQuitAnchoredY(startRt)) * 0.5f;
            cloneRt.anchoredPosition = p;
        }

        var tmp = clone.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
            tmp.text = "TUTORIAL";

        tutorialButton = clone.GetComponent<Button>();
    }

    private float GetSiblingQuitAnchoredY(RectTransform startRt)
    {
        Transform parent = startRt.parent;
        if (parent == null)
            return 0f;

        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i) as RectTransform;
            if (child == null || child == startRt) continue;
            if (!child.name.ToLowerInvariant().Contains("quit"))
                continue;
            return child.anchoredPosition.y;
        }

        return 0f;
    }

    private void OnEnable()
    {
        ShowRoot();
        if (gameFlow != null)
            gameFlow.PauseGame();
        else
            Time.timeScale = 0f;
    }

    private void OnStartPressed()
    {
        TutorialMode.End();
        HideRoot();
        if (gameFlow != null)
            gameFlow.ResumeGame();
        else
            Time.timeScale = 1f;
    }

    private void OnTutorialPressed()
    {
        TutorialMode.Begin();
        ActivateAllInteractiveTutorials();

        HideRoot();
        if (gameFlow != null)
            gameFlow.ResumeGame();
        else
            Time.timeScale = 1f;
    }

    private void OnQuitPressed()
    {
        if (gameFlow != null)
            gameFlow.QuitGame();
    }

    private void ActivateAllInteractiveTutorials()
    {
        var seen = new System.Collections.Generic.HashSet<InteractiveTutorial>();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            foreach (InteractiveTutorial t in canvas.GetComponentsInChildren<InteractiveTutorial>(true))
            {
                if (t == null) continue;
                seen.Add(t);
                t.gameObject.SetActive(true);
            }
        }

        foreach (InteractiveTutorial t in FindObjectsByType<InteractiveTutorial>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || seen.Contains(t)) continue;
            t.gameObject.SetActive(true);
        }
    }

    private void ShowRoot()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            return;
        }

        if (root != null && root != gameObject)
            root.SetActive(true);
    }

    private void HideRoot()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            return;
        }

        if (root != null && root != gameObject)
            root.SetActive(false);
    }
}
