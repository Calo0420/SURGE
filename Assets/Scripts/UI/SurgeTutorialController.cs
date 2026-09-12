// ============================================================================
// SURGE — SurgeTutorialController
//
// Displays the 3-card "How to Play" tutorial overlay (Connect, Surge, Purge).
// Shows automatically on first match/launch (saved in PlayerPrefs).
// Auto-builds a cyber-arcade modal UI on the active Canvas if not assigned.
// Pauses match timer while reading, resumes seamlessly on "PLAY!".
// ============================================================================

using Surge.Runtime;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SurgeTutorialController : MonoBehaviour
{
    private const string PrefKeyTutorialSeen = "SURGE_TUTORIAL_SEEN";

    [Header("UI Hierarchy")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private Image cardDisplay;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Text pageIndicatorText;
    [SerializeField] private Text nextButtonText;

    [Header("Cards")]
    [SerializeField] private Sprite[] cards;

    [Header("Game Seam")]
    [SerializeField] private MatchDriver driver;

    private int _currentIndex = 0;
    private bool _pausedMatch;

    public bool IsOpen => tutorialPanel != null && tutorialPanel.activeSelf;

    private void Awake()
    {
        if (driver == null)
            driver = FindFirstObjectByType<MatchDriver>();

        LoadCardsIfEmpty();
        EnsureUI();
    }

    private void Start()
    {
        // First-time player check: auto-open if never seen before
        if (PlayerPrefs.GetInt(PrefKeyTutorialSeen, 0) == 0)
        {
            OpenTutorial();
        }
        else
        {
            if (tutorialPanel != null)
                tutorialPanel.SetActive(false);
        }
    }

    private void LoadCardsIfEmpty()
    {
        if (cards == null || cards.Length == 0)
        {
            Sprite c1 = Resources.Load<Sprite>("Tutorial/Tutorial_Card_01_Connect");
            Sprite c2 = Resources.Load<Sprite>("Tutorial/Tutorial_Card_02_Surge");
            Sprite c3 = Resources.Load<Sprite>("Tutorial/Tutorial_Card_03_Purge");

            if (c1 != null && c2 != null && c3 != null)
            {
                cards = new Sprite[] { c1, c2, c3 };
            }
        }
    }

    private void EnsureUI()
    {
        if (tutorialPanel != null && cardDisplay != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        Font standardFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        // Fullscreen Backdrop
        GameObject panelObj = new GameObject("TutorialModal_Runtime", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObj.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImg = panelObj.GetComponent<Image>();
        panelImg.color = new Color(0.01f, 0.02f, 0.06f, 0.94f);
        panelImg.raycastTarget = true;
        tutorialPanel = panelObj;

        // Container
        GameObject containerObj = new GameObject("Container", typeof(RectTransform));
        containerObj.transform.SetParent(panelObj.transform, false);
        RectTransform containerRect = containerObj.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector2(520, 840);

        // Header Title
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        titleObj.transform.SetParent(containerObj.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0, -10);
        titleRect.sizeDelta = new Vector2(400, 50);

        Text titleText = titleObj.GetComponent<Text>();
        titleText.font = standardFont;
        titleText.fontSize = 32;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(0.24f, 0.86f, 1f, 1f);
        titleText.text = "HOW TO PLAY";

        // Card Display Image
        GameObject cardObj = new GameObject("CardDisplay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cardObj.transform.SetParent(containerObj.transform, false);
        RectTransform cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(0, 10);
        cardRect.sizeDelta = new Vector2(460, 680);

        cardDisplay = cardObj.GetComponent<Image>();
        cardDisplay.preserveAspect = true;

        // Navigation Bar Container
        GameObject navObj = new GameObject("NavBar", typeof(RectTransform));
        navObj.transform.SetParent(containerObj.transform, false);
        RectTransform navRect = navObj.GetComponent<RectTransform>();
        navRect.anchorMin = new Vector2(0.5f, 0f);
        navRect.anchorMax = new Vector2(0.5f, 0f);
        navRect.pivot = new Vector2(0.5f, 0f);
        navRect.anchoredPosition = new Vector2(0, 15);
        navRect.sizeDelta = new Vector2(480, 60);

        // Prev Button
        prevButton = CreateNavButton(navObj, "BtnPrev", "< PREV", new Vector2(-150, 0), standardFont, new Color(0.1f, 0.2f, 0.35f, 0.9f), Color.white);
        prevButton.onClick.AddListener(OnPrevClicked);

        // Page Indicator
        GameObject indicatorObj = new GameObject("PageText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        indicatorObj.transform.SetParent(navObj.transform, false);
        RectTransform indRect = indicatorObj.GetComponent<RectTransform>();
        indRect.anchorMin = new Vector2(0.5f, 0.5f);
        indRect.anchorMax = new Vector2(0.5f, 0.5f);
        indRect.anchoredPosition = Vector2.zero;
        indRect.sizeDelta = new Vector2(100, 40);

        pageIndicatorText = indicatorObj.GetComponent<Text>();
        pageIndicatorText.font = standardFont;
        pageIndicatorText.fontSize = 24;
        pageIndicatorText.alignment = TextAnchor.MiddleCenter;
        pageIndicatorText.color = new Color(0.8f, 0.9f, 1f, 0.9f);
        pageIndicatorText.text = "1 / 3";

        // Next Button
        nextButton = CreateNavButton(navObj, "BtnNext", "NEXT >", new Vector2(150, 0), standardFont, new Color(0.12f, 0.45f, 0.7f, 0.95f), new Color(0.3f, 1f, 0.9f, 1f));
        nextButtonText = nextButton.GetComponentInChildren<Text>();
        nextButton.onClick.AddListener(OnNextClicked);

        // Close 'X' Button at top-right
        closeButton = CreateNavButton(containerObj, "BtnClose", "✕", new Vector2(210, 395), standardFont, new Color(0.2f, 0.05f, 0.1f, 0.8f), new Color(1f, 0.3f, 0.5f, 1f));
        closeButton.GetComponent<RectTransform>().sizeDelta = new Vector2(44, 44);
        closeButton.onClick.AddListener(CloseTutorial);

        tutorialPanel.SetActive(false);
    }

    private Button CreateNavButton(GameObject parent, string name, string label, Vector2 pos, Font font, Color btnColor, Color textColor)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent.transform, false);

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(130, 48);

        Image img = btnObj.GetComponent<Image>();
        img.color = btnColor;

        Button btn = btnObj.GetComponent<Button>();

        GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        Text txt = txtObj.GetComponent<Text>();
        txt.font = font;
        txt.fontSize = 20;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = textColor;
        txt.text = label;

        return btn;
    }

    public void OpenTutorial()
    {
        LoadCardsIfEmpty();
        EnsureUI();

        _currentIndex = 0;
        if (tutorialPanel != null)
            tutorialPanel.SetActive(true);

        UpdateCardView();

        // Pause match clock during tutorial reading
        if (driver != null && driver.MatchRunning)
        {
            _pausedMatch = true;
            Time.timeScale = 0f;
        }

        if (SurgeAudioManager.Instance != null)
            SurgeAudioManager.Instance.PlayNodeTick(0);
    }

    public void CloseTutorial()
    {
        PlayerPrefs.SetInt(PrefKeyTutorialSeen, 1);
        PlayerPrefs.Save();

        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);

        // Resume match clock
        if (_pausedMatch)
        {
            _pausedMatch = false;
            Time.timeScale = 1f;
        }

        if (SurgeAudioManager.Instance != null)
            SurgeAudioManager.Instance.PlayHeavyClear();
    }

    private void OnPrevClicked()
    {
        if (_currentIndex > 0)
        {
            _currentIndex--;
            UpdateCardView();
            if (SurgeAudioManager.Instance != null)
                SurgeAudioManager.Instance.PlayNodeTick(_currentIndex);
        }
    }

    private void OnNextClicked()
    {
        if (cards != null && _currentIndex < cards.Length - 1)
        {
            _currentIndex++;
            UpdateCardView();
            if (SurgeAudioManager.Instance != null)
                SurgeAudioManager.Instance.PlayNodeTick(_currentIndex);
        }
        else
        {
            CloseTutorial();
        }
    }

    private void UpdateCardView()
    {
        if (cards == null || cards.Length == 0) return;

        _currentIndex = Mathf.Clamp(_currentIndex, 0, cards.Length - 1);

        if (cardDisplay != null)
        {
            cardDisplay.sprite = cards[_currentIndex];
            cardDisplay.preserveAspect = true;
        }

        if (prevButton != null)
            prevButton.interactable = (_currentIndex > 0);

        if (pageIndicatorText != null)
        {
            pageIndicatorText.text = $"{_currentIndex + 1} / {cards.Length}";
        }

        if (nextButtonText != null)
        {
            nextButtonText.text = (_currentIndex == cards.Length - 1) ? "PLAY!" : "NEXT >";
        }
    }
}
