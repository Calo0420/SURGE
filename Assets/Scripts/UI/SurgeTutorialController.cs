// ============================================================================
// SURGE — SurgeTutorialController
//
// Displays the 3-card "How to Play" tutorial overlay (Connect, Surge, Purge).
// Shows automatically on first standalone launch (saved in PlayerPrefs).
// Auto-builds a cyber-arcade modal UI on the active Canvas if not assigned.
// Ends in a self-contained practice simulation, then resumes the local match.
// Tournament matches never expose this pauseable overlay.
// ============================================================================

using Surge.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using TMPro;
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
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private GameObject instructionBackdrop;
    [SerializeField] private GameObject practicePanel;
    [SerializeField] private TMP_Text practicePrompt;
    [SerializeField] private Button practiceReadyButton;

    [Header("Cards")]
    [SerializeField] private Sprite[] cards;

    [Header("Game Seam")]
    [SerializeField] private MatchDriver driver;

    private int _currentIndex = 0;
    private bool _pausedMatch;
    private int _practiceStep = -1;
    private int _practiceProgress;
    private bool _practiceDragging;
    private readonly RectTransform[] _practiceNodes = new RectTransform[4];
    private readonly Image[] _practiceNodeImages = new Image[4];
    private readonly Image[] _practiceLinks = new Image[3];
    private Sprite _practiceNodeSprite;

    private static readonly string[] CardInstructions =
    {
        "DRAG THROUGH 3+ MATCHING CAPACITORS\nUP, DOWN, LEFT, OR RIGHT — NEVER DIAGONALLY",
        "FILL THE METER, THEN TAP READY WITHIN 8 SECONDS\nSURGE LASTS 5 SECONDS AND 2-NODE LINKS SCORE",
        "DURING SURGE, CONNECT 4+ MATCHING CAPACITORS\nEVERY REMAINING CAPACITOR OF THAT COLOR IS PURGED"
    };

    public bool IsOpen => tutorialPanel != null && tutorialPanel.activeSelf;

    private void Awake()
    {
        if (driver == null)
            driver = FindAnyObjectByType<MatchDriver>();

        EnsureEventSystem();
        LoadCardsIfEmpty();
        EnsureUI();
    }

    private void Start()
    {
        // First-time player check: auto-open if never seen before
        if (PlayerPrefs.GetInt(PrefKeyTutorialSeen, 0) == 0 &&
            !SkillzCrossPlatform.IsMatchInProgress())
        {
            OpenTutorial();
        }
        else
        {
            if (tutorialPanel != null)
                tutorialPanel.SetActive(false);
        }
    }

    private void Update()
    {
        // Press 'H' at any time to toggle the How to Play tutorial cards
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.hKey.wasPressedThisFrame)
        {
            if (IsOpen) CloseTutorial();
            else OpenTutorial();
            return;
        }

        if (!IsOpen) return;

        var pointer = Pointer.current;
        if (_practiceStep >= 0)
        {
            UpdatePractice(pointer);
            return;
        }

        // Keyboard & gamepad navigation fallback
        if (keyboard != null)
        {
            if (keyboard.spaceKey.wasPressedThisFrame ||
                keyboard.enterKey.wasPressedThisFrame ||
                keyboard.numpadEnterKey.wasPressedThisFrame ||
                keyboard.rightArrowKey.wasPressedThisFrame ||
                keyboard.dKey.wasPressedThisFrame)
            {
                OnNextClicked();
            }
            else if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
            {
                OnPrevClicked();
            }
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseTutorial();
            }
        }
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var uiModule = esObj.GetComponent<InputSystemUIInputModule>();
            if (uiModule != null)
            {
                uiModule.AssignDefaultActions();
            }
            DontDestroyOnLoad(esObj);
        }
    }

    private void LoadCardsIfEmpty()
    {
        if (cards == null || cards.Length == 0)
        {
            var loaded = Resources.LoadAll<Sprite>("Tutorial");
            if (loaded != null && loaded.Length > 0)
            {
                System.Array.Sort(loaded, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
                cards = loaded;
            }
        }
    }

    private void EnsureUI()
    {
        EnsureEventSystem();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        // If runtime modal already exists under canvas, bind or refresh
        Transform existingPanel = canvas.transform.Find("TutorialModal_Runtime");
        if (existingPanel != null && tutorialPanel == null)
        {
            Destroy(existingPanel.gameObject);
        }

        if (tutorialPanel != null && cardDisplay != null) return;

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
        panelImg.color = new Color(0.01f, 0.02f, 0.06f, 1f);
        panelImg.raycastTarget = true;
        tutorialPanel = panelObj;

        // Container
        GameObject containerObj = new GameObject("Container", typeof(RectTransform));
        containerObj.transform.SetParent(panelObj.transform, false);
        RectTransform containerRect = containerObj.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector2(660, 900);

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
        titleText.raycastTarget = false;

        // Card Display Image (clickable to advance!)
        GameObject cardObj = new GameObject("CardDisplay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        cardObj.transform.SetParent(containerObj.transform, false);
        RectTransform cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(0, 5);
        cardRect.sizeDelta = new Vector2(600, 760);

        cardDisplay = cardObj.GetComponent<Image>();
        cardDisplay.preserveAspect = true;
        cardDisplay.raycastTarget = true;

        Button cardBtn = cardObj.GetComponent<Button>();
        cardBtn.transition = Selectable.Transition.None;
        cardBtn.onClick.AddListener(OnNextClicked);

        TMP_FontAsset tmpFont = FindAnyObjectByType<TMP_Text>()?.font;
        GameObject instructionBackdropObj = new GameObject(
            "InstructionBackdrop", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        instructionBackdrop = instructionBackdropObj;
        instructionBackdropObj.transform.SetParent(containerObj.transform, false);
        RectTransform backdropRect = instructionBackdropObj.GetComponent<RectTransform>();
        backdropRect.anchorMin = new Vector2(0.5f, 0.5f);
        backdropRect.anchorMax = new Vector2(0.5f, 0.5f);
        backdropRect.pivot = new Vector2(0.5f, 0.5f);
        backdropRect.anchoredPosition = new Vector2(0f, -290f);
        backdropRect.sizeDelta = new Vector2(560f, 104f);
        Image backdropImage = instructionBackdropObj.GetComponent<Image>();
        backdropImage.color = new Color(0.005f, 0.025f, 0.055f, 0.84f);
        backdropImage.raycastTarget = false;

        GameObject instructionObj = new GameObject(
            "InstructionText", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        instructionObj.transform.SetParent(containerObj.transform, false);
        RectTransform instructionRect = instructionObj.GetComponent<RectTransform>();
        instructionRect.anchorMin = new Vector2(0.5f, 0.5f);
        instructionRect.anchorMax = new Vector2(0.5f, 0.5f);
        instructionRect.pivot = new Vector2(0.5f, 0.5f);
        instructionRect.anchoredPosition = new Vector2(0f, -290f);
        instructionRect.sizeDelta = new Vector2(540f, 94f);

        instructionText = instructionObj.GetComponent<TextMeshProUGUI>();
        instructionText.font = tmpFont;
        instructionText.fontSize = 20f;
        instructionText.fontStyle = FontStyles.Bold;
        instructionText.alignment = TextAlignmentOptions.Center;
        instructionText.color = Color.white;
        instructionText.outlineColor = new Color(0f, 0.08f, 0.14f, 1f);
        instructionText.outlineWidth = 0.22f;
        instructionText.raycastTarget = false;
        instructionText.textWrappingMode = TextWrappingModes.Normal;

        BuildPracticePanel(containerObj, tmpFont);

        // Navigation Bar Container
        GameObject navObj = new GameObject("NavBar", typeof(RectTransform));
        navObj.transform.SetParent(containerObj.transform, false);
        RectTransform navRect = navObj.GetComponent<RectTransform>();
        navRect.anchorMin = new Vector2(0.5f, 0f);
        navRect.anchorMax = new Vector2(0.5f, 0f);
        navRect.pivot = new Vector2(0.5f, 0f);
        navRect.anchoredPosition = new Vector2(0, 15);
        navRect.sizeDelta = new Vector2(610, 60);

        // Prev Button
        prevButton = CreateNavButton(navObj, "BtnPrev", "< PREV", new Vector2(-205, 0), standardFont, new Color(0.1f, 0.2f, 0.35f, 0.95f), Color.white);
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
        pageIndicatorText.raycastTarget = false;

        // Next Button
        nextButton = CreateNavButton(navObj, "BtnNext", "NEXT >", new Vector2(205, 0), standardFont, new Color(0.12f, 0.45f, 0.7f, 0.95f), new Color(0.3f, 1f, 0.9f, 1f));
        nextButtonText = nextButton.GetComponentInChildren<Text>();
        nextButton.onClick.AddListener(OnNextClicked);

        // Close 'X' Button at top-right
        closeButton = CreateNavButton(containerObj, "BtnClose", "✕", new Vector2(285, 425), standardFont, new Color(0.2f, 0.05f, 0.1f, 0.8f), new Color(1f, 0.3f, 0.5f, 1f));
        closeButton.GetComponent<RectTransform>().sizeDelta = new Vector2(44, 44);
        closeButton.onClick.AddListener(CloseTutorial);

        tutorialPanel.SetActive(false);
    }

    private void BuildPracticePanel(GameObject container, TMP_FontAsset font)
    {
        practicePanel = new GameObject(
            "PracticePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        practicePanel.transform.SetParent(container.transform, false);

        RectTransform panelRect = practicePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 5f);
        panelRect.sizeDelta = new Vector2(600f, 760f);

        Image panelImage = practicePanel.GetComponent<Image>();
        panelImage.color = new Color(0.015f, 0.04f, 0.08f, 0.98f);
        panelImage.raycastTarget = true;

        GameObject promptObj = new GameObject(
            "Prompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        promptObj.transform.SetParent(practicePanel.transform, false);
        RectTransform promptRect = promptObj.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 1f);
        promptRect.anchorMax = new Vector2(0.5f, 1f);
        promptRect.pivot = new Vector2(0.5f, 1f);
        promptRect.anchoredPosition = new Vector2(0f, -45f);
        promptRect.sizeDelta = new Vector2(540f, 110f);

        practicePrompt = promptObj.GetComponent<TextMeshProUGUI>();
        practicePrompt.font = font;
        practicePrompt.fontSize = 25f;
        practicePrompt.fontStyle = FontStyles.Bold;
        practicePrompt.alignment = TextAlignmentOptions.Center;
        practicePrompt.color = new Color(0.24f, 0.86f, 1f, 1f);
        practicePrompt.raycastTarget = false;

        Vector2[] positions =
        {
            new Vector2(-170f, 110f),
            new Vector2(-30f, 110f),
            new Vector2(-30f, -30f),
            new Vector2(110f, -30f)
        };

        _practiceNodeSprite = CreatePracticeNodeSprite();
        for (int i = 0; i < _practiceNodes.Length; i++)
        {
            if (i > 0)
                _practiceLinks[i - 1] =
                    CreatePracticeLink(practicePanel.transform, positions[i - 1], positions[i]);

            GameObject node = new GameObject(
                $"PracticeNode_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            node.transform.SetParent(practicePanel.transform, false);
            RectTransform rect = node.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = positions[i];
            rect.sizeDelta = new Vector2(78f, 78f);
            _practiceNodes[i] = rect;

            Image image = node.GetComponent<Image>();
            image.sprite = _practiceNodeSprite;
            image.raycastTarget = false;
            _practiceNodeImages[i] = image;
        }

        GameObject readyObj = new GameObject(
            "PracticeReadyButton", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button));
        readyObj.transform.SetParent(practicePanel.transform, false);
        RectTransform readyRect = readyObj.GetComponent<RectTransform>();
        readyRect.anchorMin = readyRect.anchorMax = new Vector2(0.5f, 0.5f);
        readyRect.anchoredPosition = new Vector2(0f, -15f);
        readyRect.sizeDelta = new Vector2(290f, 100f);

        Image readyImage = readyObj.GetComponent<Image>();
        readyImage.color = new Color(0.1f, 0.52f, 0.78f, 1f);
        practiceReadyButton = readyObj.GetComponent<Button>();
        practiceReadyButton.targetGraphic = readyImage;
        practiceReadyButton.onClick.AddListener(OnPracticeReadyPressed);

        GameObject readyTextObj = new GameObject(
            "Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        readyTextObj.transform.SetParent(readyObj.transform, false);
        RectTransform readyTextRect = readyTextObj.GetComponent<RectTransform>();
        readyTextRect.anchorMin = Vector2.zero;
        readyTextRect.anchorMax = Vector2.one;
        readyTextRect.offsetMin = Vector2.zero;
        readyTextRect.offsetMax = Vector2.zero;

        TMP_Text readyText = readyTextObj.GetComponent<TextMeshProUGUI>();
        readyText.font = font;
        readyText.fontSize = 30f;
        readyText.fontStyle = FontStyles.Bold;
        readyText.alignment = TextAlignmentOptions.Center;
        readyText.color = Color.white;
        readyText.text = "TAP READY";
        readyText.raycastTarget = false;

        practicePanel.SetActive(false);
    }

    private static Image CreatePracticeLink(Transform parent, Vector2 from, Vector2 to)
    {
        GameObject link = new GameObject(
            "PracticeLink", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        link.transform.SetParent(parent, false);
        RectTransform rect = link.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = (from + to) * 0.5f;
        rect.sizeDelta = new Vector2(Vector2.Distance(from, to), 8f);
        rect.localRotation = Quaternion.Euler(
            0f, 0f, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
        Image image = link.GetComponent<Image>();
        image.color = new Color(0.18f, 0.55f, 0.68f, 0.45f);
        image.raycastTarget = false;
        return image;
    }

    private static Sprite CreatePracticeNodeSprite()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "SurgePracticeNode",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - center) / center;
            float dy = (y - center) / center;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Mathf.Clamp01((1f - distance) * 10f);
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size),
                             new Vector2(0.5f, 0.5f), size);
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
        img.raycastTarget = true;

        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = img;

        ColorBlock cb = btn.colors;
        cb.normalColor = btnColor;
        cb.highlightedColor = new Color(Mathf.Min(btnColor.r * 1.35f, 1f), Mathf.Min(btnColor.g * 1.35f, 1f), Mathf.Min(btnColor.b * 1.35f, 1f), 1f);
        cb.pressedColor = new Color(btnColor.r * 0.7f, btnColor.g * 0.7f, btnColor.b * 0.7f, 1f);
        cb.disabledColor = new Color(btnColor.r * 0.4f, btnColor.g * 0.4f, btnColor.b * 0.4f, 0.4f);
        btn.colors = cb;

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
        txt.raycastTarget = false; // Do not intercept clicks from button

        return btn;
    }

    public void OpenTutorial()
    {
        if (SkillzCrossPlatform.IsMatchInProgress())
            return;

        if (driver == null)
            driver = FindAnyObjectByType<MatchDriver>();

        LoadCardsIfEmpty();
        EnsureUI();

        _currentIndex = 0;
        _practiceStep = -1;
        if (practicePanel != null) practicePanel.SetActive(false);
        if (cardDisplay != null) cardDisplay.gameObject.SetActive(true);
        if (instructionText != null) instructionText.gameObject.SetActive(true);
        if (instructionBackdrop != null) instructionBackdrop.SetActive(true);
        if (nextButton != null) nextButton.gameObject.SetActive(true);
        if (prevButton != null) prevButton.gameObject.SetActive(true);
        if (pageIndicatorText != null) pageIndicatorText.gameObject.SetActive(true);
        if (tutorialPanel != null)
            tutorialPanel.SetActive(true);

        UpdateCardView();

        // Pause match clock during tutorial reading
        if (driver != null && driver.MatchRunning)
        {
            _pausedMatch = true;
            driver.PauseMatch();
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
        _practiceStep = -1;
        _practiceDragging = false;

        // Resume match clock
        if (_pausedMatch && driver != null)
        {
            _pausedMatch = false;
            driver.ResumeMatch();
        }

        if (SurgeAudioManager.Instance != null)
            SurgeAudioManager.Instance.PlayClear(5, false);
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
            BeginPractice();
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
            nextButtonText.text = (_currentIndex == cards.Length - 1)
                ? "PRACTICE >"
                : "NEXT >";
        }

        if (instructionText != null)
            instructionText.text = _currentIndex < CardInstructions.Length
                ? CardInstructions[_currentIndex]
                : string.Empty;
    }

    private void BeginPractice()
    {
        if (practicePanel == null)
        {
            CloseTutorial();
            return;
        }

        if (cardDisplay != null) cardDisplay.gameObject.SetActive(false);
        if (instructionText != null) instructionText.gameObject.SetActive(false);
        if (instructionBackdrop != null) instructionBackdrop.SetActive(false);
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (prevButton != null) prevButton.gameObject.SetActive(false);
        if (pageIndicatorText != null) pageIndicatorText.gameObject.SetActive(false);
        practicePanel.SetActive(true);
        ShowPracticeStep(0);
    }

    private void ShowPracticeStep(int step)
    {
        _practiceStep = step;
        _practiceProgress = 0;
        _practiceDragging = false;

        bool readyStep = step == 1;
        if (practiceReadyButton != null)
            practiceReadyButton.gameObject.SetActive(readyStep);

        Color nodeColor = step == 2
            ? new Color(1f, 0.16f, 0.52f, 1f)
            : new Color(0.24f, 0.86f, 1f, 1f);
        for (int i = 0; i < _practiceNodeImages.Length; i++)
        {
            if (_practiceNodeImages[i] == null) continue;
            _practiceNodeImages[i].gameObject.SetActive(
                !readyStep && (step == 2 || i < 3));
            _practiceNodeImages[i].color = nodeColor * 0.48f;
        }
        for (int i = 0; i < _practiceLinks.Length; i++)
        {
            if (_practiceLinks[i] == null) continue;
            _practiceLinks[i].gameObject.SetActive(
                !readyStep && (step == 2 || i < 2));
        }

        if (practicePrompt == null) return;
        practicePrompt.text = step switch
        {
            0 => "1 / 3  CONNECT\nDRAG THROUGH THE 3 CYAN CAPACITORS",
            1 => "2 / 3  SURGE\nTHE METER IS FULL — ACTIVATE IT",
            _ => "3 / 3  COLOR PURGE\nDURING SURGE, DRAG THROUGH ALL 4 PINK CAPACITORS"
        };
        practicePrompt.color = nodeColor;
    }

    private void UpdatePractice(Pointer pointer)
    {
        if (_practiceStep == 1 || pointer == null)
            return;

        Vector2 position = pointer.position.ReadValue();
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (pointer.press.wasPressedThisFrame)
        {
            _practiceDragging = ContainsPracticeNode(0, position, camera);
            _practiceProgress = _practiceDragging ? 1 : 0;
            RefreshPracticeNodes();
        }
        else if (pointer.press.isPressed && _practiceDragging)
        {
            int target = _practiceProgress;
            int required = _practiceStep == 0 ? 3 : 4;
            if (target < required && ContainsPracticeNode(target, position, camera))
            {
                _practiceProgress++;
                RefreshPracticeNodes();
            }
        }
        else if (pointer.press.wasReleasedThisFrame && _practiceDragging)
        {
            int required = _practiceStep == 0 ? 3 : 4;
            if (_practiceProgress >= required)
            {
                if (SurgeAudioManager.Instance != null)
                    SurgeAudioManager.Instance.PlayClear(required, _practiceStep == 2);

                if (_practiceStep == 0) ShowPracticeStep(1);
                else CloseTutorial();
            }
            else
            {
                _practiceProgress = 0;
                _practiceDragging = false;
                RefreshPracticeNodes();
            }
        }
    }

    private bool ContainsPracticeNode(int index, Vector2 position, Camera camera)
    {
        return index >= 0 && index < _practiceNodes.Length &&
               _practiceNodes[index] != null &&
               RectTransformUtility.RectangleContainsScreenPoint(
                   _practiceNodes[index], position, camera);
    }

    private void RefreshPracticeNodes()
    {
        Color active = _practiceStep == 2
            ? new Color(1f, 0.16f, 0.52f, 1f)
            : new Color(0.24f, 0.86f, 1f, 1f);
        for (int i = 0; i < _practiceNodeImages.Length; i++)
        {
            if (_practiceNodeImages[i] == null) continue;
            _practiceNodeImages[i].color = i < _practiceProgress
                ? Color.Lerp(active, Color.white, 0.42f)
                : active * 0.48f;
        }
    }

    private void OnPracticeReadyPressed()
    {
        if (_practiceStep != 1) return;
        if (HapticManager.Instance != null)
            HapticManager.Instance.PlayHeavy();
        if (SurgeAudioManager.Instance != null)
            SurgeAudioManager.Instance.PlaySurgeActivation();
        ShowPracticeStep(2);
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Surge/Tutorial/Reset Tutorial Seen (Show Cards Next Play)", priority = 100)]
    public static void ResetTutorialSeen()
    {
        PlayerPrefs.DeleteKey(PrefKeyTutorialSeen);
        PlayerPrefs.Save();
        Debug.Log("[Surge] Tutorial seen flag reset. Tutorial cards will display on next match start.");
    }

    [UnityEditor.MenuItem("Surge/Tutorial/Open Tutorial Cards Now", priority = 101)]
    public static void OpenTutorialNow()
    {
        var controller = Object.FindAnyObjectByType<SurgeTutorialController>();
        if (controller != null)
        {
            controller.OpenTutorial();
        }
        else
        {
            Debug.LogWarning("[Surge] No SurgeTutorialController found in active scene.");
        }
    }
#endif
}
