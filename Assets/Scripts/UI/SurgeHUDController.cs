// ============================================================================
// SURGE — SurgeHUDController
//
// Drives the runtime HUD display (Score, 90s Timer countdown, Surge Meter %).
// Automatically discovers references on PF_HUDBaseline if not assigned.
// Provides smooth animated rolling score and high-tension timer pulsing in the
// final 10 seconds.
// ============================================================================

using Surge.Runtime;
using SurgeCore;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SurgeHUDController : MonoBehaviour
{
    private static Sprite _roundedButtonSprite;

    [Header("Engine Seam")]
    [SerializeField] private MatchDriver driver;

    [Header("UI Elements")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text surgeMeterText;
    [SerializeField] private Image surgeMeterFill;
    [SerializeField] private Image surgeMeterGlow;
    [SerializeField] private RectTransform safeAreaRoot;
    [SerializeField] private RectTransform scoreValueRoot;
    [SerializeField] private RectTransform timerValueRoot;
    [SerializeField] private RectTransform surgeValueRoot;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Button surgeButton;
    [SerializeField] private RectTransform surgeButtonRoot;
    [SerializeField] private Image surgeButtonBackground;
    [SerializeField] private Image surgeButtonAccent;
    [SerializeField] private TMP_Text surgeButtonText;
    [SerializeField] private CanvasGroup surgeButtonGroup;
    [SerializeField] private Button tutorialButton;

    [Header("Styling")]
    [SerializeField] private SurgePalette palette;
    [SerializeField] private Color warningTimerColor = new Color(1.0f, 0.25f, 0.25f, 1.0f);
    [SerializeField] private Color surgeReadyColor = new Color(1.0f, 0.85f, 0.2f, 1.0f);

    [Header("Tutorial")]
    [SerializeField] private SurgeTutorialController tutorialController;

    private float _displayedScore;
    private Color _initialTimerColor;
    private Color _initialMeterColor;
    private bool _hasInitialColors;
    private int _lastTargetScore = -1;
    private float _scorePunch;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreenSize;
    private int _lastMeter = -1;
    private bool _wasBanked;
    private bool _wasSurgeActive;
    private float _surgePunch;
    private float _meterGainPulse;
    private float _feedbackLife;
    private Vector2 _feedbackBasePosition;
    private Vector2 _surgeButtonBasePosition;
    private bool _surgeButtonUsesBottomLayout;

    public void OpenTutorial()
    {
        if (!SkillzCrossPlatform.IsMatchInProgress() && tutorialController != null)
            tutorialController.OpenTutorial();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureSurgeButton();
        EnsureTutorialButton();
        EnsureFeedbackText();
        CacheInitialColors();
        ApplySafeArea(force: true);
    }

    private void Start()
    {
        if (driver != null)
        {
            driver.MatchEnded += OnMatchEnded;
        }
        UpdateHUD(forceImmediate: true);
    }

    private void OnDestroy()
    {
        if (driver != null)
        {
            driver.MatchEnded -= OnMatchEnded;
        }
    }

    private void Update()
    {
        ApplySafeArea(force: false);
        UpdateHUD(forceImmediate: false);
        AnimatePresentation();
    }

    private void ResolveReferences()
    {
        if (driver == null)
            driver = FindAnyObjectByType<MatchDriver>();

        if (scoreText == null)
        {
            Transform t = transform.Find("SafeArea/CommandBar/ScoreModule/ScoreValue");
            if (t != null) scoreText = t.GetComponent<TMP_Text>();
        }

        if (timerText == null)
        {
            Transform t = transform.Find("SafeArea/CommandBar/TimerModule/TimerValue");
            if (t != null) timerText = t.GetComponent<TMP_Text>();
        }

        if (surgeMeterText == null)
        {
            Transform t = transform.Find("SafeArea/CommandBar/SurgeModule/SurgeState");
            if (t != null) surgeMeterText = t.GetComponent<TMP_Text>();
        }

        if (surgeMeterFill == null)
        {
            Transform t = transform.Find("SafeArea/CommandBar/SurgeModule/MeterTrack/MeterFill");
            if (t != null) surgeMeterFill = t.GetComponent<Image>();
        }

        if (surgeMeterGlow == null)
        {
            Transform t = transform.Find("SafeArea/CommandBar/SurgeModule/MeterTrack/MeterGlow");
            if (t != null) surgeMeterGlow = t.GetComponent<Image>();
        }

        if (safeAreaRoot == null)
        {
            Transform t = transform.Find("SafeArea");
            if (t != null) safeAreaRoot = t as RectTransform;
        }

        if (scoreValueRoot == null && scoreText != null)
            scoreValueRoot = scoreText.rectTransform;
        if (timerValueRoot == null && timerText != null)
            timerValueRoot = timerText.rectTransform;
        if (surgeValueRoot == null && surgeMeterText != null)
            surgeValueRoot = surgeMeterText.rectTransform;

        if (tutorialController == null)
            tutorialController = FindAnyObjectByType<SurgeTutorialController>();
    }

    private void EnsureSurgeButton()
    {
        if (surgeButton == null && safeAreaRoot != null)
        {
            GameObject go = new GameObject(
                "SurgeActionButton", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button), typeof(CanvasGroup));
            go.transform.SetParent(safeAreaRoot, false);

            surgeButtonRoot = go.GetComponent<RectTransform>();
            surgeButtonBackground = go.GetComponent<Image>();
            surgeButtonBackground.sprite = CreateRoundedButtonSprite();
            surgeButtonBackground.type = Image.Type.Sliced;
            surgeButtonBackground.color = new Color(0.025f, 0.08f, 0.14f, 0.98f);

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.24f, 0.86f, 1f, 0.82f);
            outline.effectDistance = new Vector2(2f, -2f);

            surgeButton = go.GetComponent<Button>();
            surgeButton.targetGraphic = surgeButtonBackground;

            surgeButtonGroup = go.GetComponent<CanvasGroup>();
            surgeButtonGroup.alpha = 0f;
            surgeButtonGroup.interactable = false;
            surgeButtonGroup.blocksRaycasts = false;

            GameObject textGo = new GameObject(
                "Text", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 8f);
            textRect.offsetMax = new Vector2(-10f, -8f);

            surgeButtonText = textGo.GetComponent<TextMeshProUGUI>();
            surgeButtonText.font = scoreText != null ? scoreText.font : null;
            surgeButtonText.fontSize = 24f;
            surgeButtonText.fontStyle = FontStyles.Bold;
            surgeButtonText.alignment = TextAlignmentOptions.Center;
            surgeButtonText.color = Color.white;
            surgeButtonText.text = "<size=14>SURGE</size>\nACTIVATE";
            surgeButtonText.raycastTarget = false;

            GameObject accentGo = new GameObject(
                "ChargeRail", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            accentGo.transform.SetParent(go.transform, false);
            RectTransform accentRect = accentGo.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0.16f, 0f);
            accentRect.anchorMax = new Vector2(0.84f, 0f);
            accentRect.pivot = new Vector2(0.5f, 0f);
            accentRect.anchoredPosition = new Vector2(0f, 6f);
            accentRect.sizeDelta = new Vector2(0f, 5f);

            surgeButtonAccent = accentGo.GetComponent<Image>();
            surgeButtonAccent.sprite = surgeButtonBackground.sprite;
            surgeButtonAccent.type = Image.Type.Sliced;
            surgeButtonAccent.color = new Color(1f, 0.16f, 0.52f, 0.95f);
            surgeButtonAccent.raycastTarget = false;
        }

        if (surgeButton == null)
            return;

        surgeButton.onClick.RemoveListener(OnSurgePressed);
        surgeButton.onClick.AddListener(OnSurgePressed);
        surgeButton.interactable = false;
        UpdateSurgeButtonLayout();
    }

    private void UpdateSurgeButtonLayout()
    {
        if (surgeButtonRoot == null || Screen.height <= 0)
            return;

        _surgeButtonUsesBottomLayout = (float)Screen.width / Screen.height < 1.35f;
        if (_surgeButtonUsesBottomLayout)
        {
            surgeButtonRoot.anchorMin = surgeButtonRoot.anchorMax =
                new Vector2(0.5f, 0f);
            surgeButtonRoot.pivot = new Vector2(0.5f, 0f);
            surgeButtonRoot.sizeDelta = new Vector2(280f, 88f);
            surgeButtonRoot.anchoredPosition = new Vector2(0f, 32f);
        }
        else
        {
            surgeButtonRoot.anchorMin = surgeButtonRoot.anchorMax =
                new Vector2(1f, 0.5f);
            surgeButtonRoot.pivot = new Vector2(1f, 0.5f);
            surgeButtonRoot.sizeDelta = new Vector2(230f, 96f);
            surgeButtonRoot.anchoredPosition = new Vector2(-34f, -18f);
        }

        _surgeButtonBasePosition = surgeButtonRoot.anchoredPosition;
    }

    private void EnsureTutorialButton()
    {
        if (tutorialButton != null || safeAreaRoot == null)
            return;

        Transform commandBar = safeAreaRoot.Find("CommandBar");
        if (commandBar == null)
            return;

        GameObject go = new GameObject(
            "TutorialButton", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button));
        go.transform.SetParent(commandBar, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-8f, -8f);
        rect.sizeDelta = new Vector2(42f, 42f);

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.06f, 0.18f, 0.27f, 0.96f);
        tutorialButton = go.GetComponent<Button>();
        tutorialButton.targetGraphic = image;
        tutorialButton.onClick.AddListener(OpenTutorial);
        tutorialButton.gameObject.SetActive(!SkillzCrossPlatform.IsMatchInProgress());

        GameObject textGo = new GameObject(
            "Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TMP_Text text = textGo.GetComponent<TextMeshProUGUI>();
        text.font = scoreText != null ? scoreText.font : null;
        text.fontSize = 25f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = palette != null ? palette.hudPrimary : Color.cyan;
        text.text = "?";
        text.raycastTarget = false;
    }

    private void OnSurgePressed()
    {
        if (driver == null || !driver.TryCommitSurge())
            return;

        _surgePunch = 1f;
        if (surgeButton != null)
            surgeButton.interactable = false;
        if (surgeButtonGroup != null)
        {
            surgeButtonGroup.alpha = 0f;
            surgeButtonGroup.interactable = false;
            surgeButtonGroup.blocksRaycasts = false;
        }
        if (feedbackText != null)
        {
            feedbackText.text = "SURGE ENGAGED";
            feedbackText.color = palette != null ? palette.neonPink : Color.magenta;
            feedbackText.alpha = 1f;
            feedbackText.rectTransform.anchoredPosition = _feedbackBasePosition;
            _feedbackLife = 1f;
        }
    }

    private void EnsureFeedbackText()
    {
        if (feedbackText != null)
        {
            _feedbackBasePosition = feedbackText.rectTransform.anchoredPosition;
            return;
        }

        if (safeAreaRoot == null)
            return;

        Transform commandBar = safeAreaRoot.Find("CommandBar");
        if (commandBar == null)
            return;

        GameObject go = new GameObject("FeedbackText", typeof(RectTransform),
                                       typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(commandBar, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -5f);
        rect.sizeDelta = new Vector2(0f, 34f);
        _feedbackBasePosition = rect.anchoredPosition;

        feedbackText = go.GetComponent<TextMeshProUGUI>();
        feedbackText.alignment = TextAlignmentOptions.Center;
        feedbackText.font = scoreText != null ? scoreText.font : null;
        feedbackText.fontSize = 20f;
        feedbackText.fontStyle = FontStyles.Bold;
        feedbackText.raycastTarget = false;
        feedbackText.textWrappingMode = TextWrappingModes.NoWrap;
        feedbackText.alpha = 0f;
    }

    public void PresentClear(ClearResult result, SurgeFeedbackTier tier)
    {
        if (result == null || feedbackText == null)
            return;

        string label;
        if (result.Purge)
            label = "COLOR PURGE";
        else if (result.ChainMult > 1)
            label = $"CHAIN x{result.ChainMult}";
        else if (tier == SurgeFeedbackTier.Exceptional)
            label = "OVERLOAD";
        else if (tier == SurgeFeedbackTier.Strong)
            label = "POWER LINK";
        else
            return;

        feedbackText.text = $"{label}  +{result.Points + result.BonusPoints}";
        feedbackText.color = tier == SurgeFeedbackTier.Exceptional
            ? (palette != null ? palette.neonPink : Color.magenta)
            : surgeReadyColor;
        feedbackText.alpha = 1f;
        feedbackText.rectTransform.anchoredPosition = _feedbackBasePosition;
        feedbackText.rectTransform.localScale = Vector3.one * 0.92f;
        _feedbackLife = 1f;
        _scorePunch = 1f;
    }

    private void ApplySafeArea(bool force)
    {
        if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        Rect safe = Screen.safeArea;
        Vector2Int screen = new Vector2Int(Screen.width, Screen.height);
        if (!force && safe == _lastSafeArea && screen == _lastScreenSize)
            return;

        safeAreaRoot.anchorMin = new Vector2(
            Mathf.Clamp01(safe.xMin / Screen.width),
            Mathf.Clamp01(safe.yMin / Screen.height));
        safeAreaRoot.anchorMax = new Vector2(
            Mathf.Clamp01(safe.xMax / Screen.width),
            Mathf.Clamp01(safe.yMax / Screen.height));
        safeAreaRoot.offsetMin = Vector2.zero;
        safeAreaRoot.offsetMax = Vector2.zero;

        _lastSafeArea = safe;
        _lastScreenSize = screen;
        UpdateSurgeButtonLayout();
    }

    private void CacheInitialColors()
    {
        if (_hasInitialColors) return;

        if (timerText != null)
            _initialTimerColor = timerText.color;
        else if (palette != null)
            _initialTimerColor = palette.hudPrimary;
        else
            _initialTimerColor = Color.white;

        if (surgeMeterText != null)
            _initialMeterColor = surgeMeterText.color;
        else if (palette != null)
            _initialMeterColor = palette.hudAccent;
        else
            _initialMeterColor = Color.white;

        _hasInitialColors = true;
    }

    private void UpdateHUD(bool forceImmediate)
    {
        if (driver == null || !driver.MatchRunning)
        {
            // If match has not begun or driver is missing, keep baseline display
            if (driver != null && timerText != null)
            {
                int totalSec = driver.MatchDurationSeconds;
                timerText.text = $"{totalSec / 60:D2}:{totalSec % 60:D2}";
            }
            return;
        }

        // 1. Score Counter with smooth arcade roll
        int targetScore = driver.Score;
        if (_lastTargetScore >= 0 && targetScore > _lastTargetScore)
            _scorePunch = 1f;
        _lastTargetScore = targetScore;

        if (forceImmediate)
        {
            _displayedScore = targetScore;
        }
        else
        {
            // Roll towards target score smoothly (fast catch-up on big combos)
            float step = Mathf.Max(100f * Time.unscaledDeltaTime, (targetScore - _displayedScore) * 8f * Time.unscaledDeltaTime);
            _displayedScore = Mathf.MoveTowards(_displayedScore, targetScore, step);
        }

        if (scoreText != null)
        {
            scoreText.SetText("{0:000000}", Mathf.RoundToInt(_displayedScore));
        }

        // 2. Timer Countdown (mm:ss) + 10s urgent pulse
        long remainingMs = driver.RemainingMs;
        int remainingSec = (int)((remainingMs + 999L) / 1000L);
        int minutes = remainingSec / 60;
        int seconds = remainingSec % 60;

        if (timerText != null)
        {
            timerText.SetText("{0:00}:{1:00}", minutes, seconds);

            if (remainingSec <= 10 && remainingSec > 0)
            {
                // Pulse warning color when clock is running out
                float pulse = Mathf.PingPong(Time.unscaledTime * 4f, 1f);
                timerText.color = Color.Lerp(_initialTimerColor, warningTimerColor, pulse);
            }
            else
            {
                timerText.color = _initialTimerColor;
            }
        }

        // 3. Surge Meter (% fill and surge states)
        bool isSurgeActive = driver.SurgeActive;
        bool isBanked = driver.Banked;
        if (isSurgeActive && !_wasSurgeActive)
            _surgePunch = 1f;
        else if (isBanked && !_wasBanked)
            _surgePunch = 0.8f;
        else if (!isSurgeActive && _wasSurgeActive)
            _surgePunch = 0.55f;

        if (_lastMeter >= 0 && driver.Meter > _lastMeter)
            _meterGainPulse = Mathf.Clamp01((driver.Meter - _lastMeter) / 20f + 0.35f);
        _lastMeter = driver.Meter;
        _wasSurgeActive = isSurgeActive;
        _wasBanked = isBanked;

        if (surgeMeterText != null)
        {
            if (isSurgeActive)
            {
                // Active frenzy mode: pulse accent color
                float pulse = Mathf.PingPong(Time.unscaledTime * 6f, 1f);
                surgeMeterText.color = Color.Lerp(palette != null ? palette.neonPink : Color.magenta, Color.white, pulse);
                surgeMeterText.SetText("ACTIVE");
            }
            else if (isBanked)
            {
                // Banked & ready to pop
                float pulse = Mathf.PingPong(Time.unscaledTime * 3f, 1f);
                surgeMeterText.color = Color.Lerp(surgeReadyColor, Color.white, pulse);
                int fuseSeconds = Mathf.Max(1,
                    Mathf.CeilToInt(driver.BankFuseRemainingMs / 1000f));
                surgeMeterText.SetText("READY  {0}", fuseSeconds);
            }
            else
            {
                surgeMeterText.color = _initialMeterColor;
                surgeMeterText.SetText("{0}%", driver.Meter);
            }
        }

        if (surgeMeterFill != null)
        {
            float targetFill = isSurgeActive || isBanked
                ? 1f
                : Mathf.Clamp01(driver.Meter / 100f);
            surgeMeterFill.fillAmount = forceImmediate
                ? targetFill
                : Mathf.MoveTowards(surgeMeterFill.fillAmount, targetFill, Time.unscaledDeltaTime * 1.8f);
        }

        if (surgeMeterGlow != null)
        {
            bool energized = isSurgeActive || isBanked;
            float pulse = energized
                ? 0.24f + Mathf.PingPong(Time.unscaledTime * 1.8f, 0.34f)
                : Mathf.Lerp(0.05f, 0.18f, driver.Meter / 100f);
            pulse = Mathf.Clamp01(pulse + _meterGainPulse * 0.22f);
            Color glow = isSurgeActive
                ? (palette != null ? palette.neonPink : Color.magenta)
                : surgeReadyColor;
            glow.a = pulse;
            surgeMeterGlow.color = glow;
        }

        bool canActivate = isBanked && !driver.IsPaused && !driver.IsFrozen;
        if (surgeButton != null)
            surgeButton.interactable = canActivate;
        if (surgeButtonGroup != null)
        {
            surgeButtonGroup.interactable = canActivate;
            surgeButtonGroup.blocksRaycasts = canActivate;
            float targetAlpha = isBanked ? 1f : 0f;
            surgeButtonGroup.alpha = forceImmediate
                ? targetAlpha
                : Mathf.MoveTowards(
                    surgeButtonGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 8f);
        }
        if (surgeButtonText != null && isBanked)
        {
            int fuseSeconds = Mathf.Max(1,
                Mathf.CeilToInt(driver.BankFuseRemainingMs / 1000f));
            surgeButtonText.SetText("<size=14>SURGE</size>\nACTIVATE  {0}", fuseSeconds);
        }
    }

    private void AnimatePresentation()
    {
        float dt = Time.unscaledDeltaTime;
        _surgePunch = Mathf.MoveTowards(_surgePunch, 0f, dt * 2.8f);
        _meterGainPulse = Mathf.MoveTowards(_meterGainPulse, 0f, dt * 3.5f);

        if (scoreValueRoot != null)
        {
            _scorePunch = Mathf.MoveTowards(_scorePunch, 0f, dt * 4.5f);
            float scale = 1f + Mathf.Sin(_scorePunch * Mathf.PI) * 0.075f;
            scoreValueRoot.localScale = Vector3.one * scale;
        }

        if (timerValueRoot != null)
        {
            bool urgent = driver != null && driver.MatchRunning
                && driver.RemainingMs > 0 && driver.RemainingMs <= 10000;
            float pulse = urgent
                ? 1f + Mathf.Sin(Time.unscaledTime * 8f) * 0.035f
                : 1f;
            timerValueRoot.localScale = Vector3.one * pulse;
        }

        if (surgeValueRoot != null)
        {
            bool energized = driver != null && (driver.SurgeActive || driver.Banked);
            float pulse = energized
                ? 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.03f
                : 1f;
            float punch = 1f + Mathf.Sin(_surgePunch * Mathf.PI) * 0.11f;
            surgeValueRoot.localScale = Vector3.one * pulse * punch;
        }

        if (surgeMeterFill != null)
        {
            float meterPulse = 1f + Mathf.Sin(_meterGainPulse * Mathf.PI) * 0.08f;
            surgeMeterFill.rectTransform.localScale = new Vector3(1f, meterPulse, 1f);
        }

        if (surgeButtonRoot != null && surgeButtonGroup != null)
        {
            float reveal = surgeButtonGroup.alpha;
            Vector2 hiddenOffset = _surgeButtonUsesBottomLayout
                ? Vector2.down * 28f
                : Vector2.right * 34f;
            surgeButtonRoot.anchoredPosition =
                _surgeButtonBasePosition + hiddenOffset * (1f - reveal);

            float readyPulse = reveal > 0.01f
                ? 1f + Mathf.Sin(Time.unscaledTime * 7f) * 0.045f
                : 0.94f;
            surgeButtonRoot.localScale = Vector3.one * readyPulse;

            if (surgeButtonBackground != null)
            {
                float colorPulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                Color bright = Color.Lerp(
                    new Color(0.025f, 0.08f, 0.14f, 0.98f),
                    new Color(0.08f, 0.28f, 0.38f, 0.98f),
                    colorPulse);
                surgeButtonBackground.color = bright;
            }

            if (surgeButtonAccent != null)
            {
                float accentPulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
                surgeButtonAccent.color = Color.Lerp(
                    new Color(1f, 0.16f, 0.52f, 0.82f),
                    surgeReadyColor,
                    accentPulse);
            }
        }

        if (feedbackText != null && _feedbackLife > 0f)
        {
            _feedbackLife = Mathf.MoveTowards(_feedbackLife, 0f, dt * 1.25f);
            float reveal = 1f - _feedbackLife;
            feedbackText.alpha = Mathf.Clamp01(_feedbackLife * 1.8f);
            feedbackText.rectTransform.anchoredPosition =
                _feedbackBasePosition + Vector2.up * reveal * 8f;
            feedbackText.rectTransform.localScale =
                Vector3.one * Mathf.Lerp(0.92f, 1f, Mathf.Clamp01(reveal * 4f));
        }
    }

    private void OnMatchEnded(MatchResult result)
    {
        if (timerText != null)
        {
            timerText.text = "00:00";
            timerText.color = warningTimerColor;
        }

        if (scoreText != null && result != null)
        {
            scoreText.SetText("{0:000000}", result.Score);
        }

        if (surgeMeterFill != null)
        {
            surgeMeterFill.fillAmount = 0f;
        }
    }

    private static Sprite CreateRoundedButtonSprite()
    {
        if (_roundedButtonSprite != null)
            return _roundedButtonSprite;

        const int width = 128;
        const int height = 64;
        const float radius = 18f;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "SurgeActionButton_Rounded",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float cx = x < radius ? radius : x >= width - radius ? width - radius - 1f : x;
            float cy = y < radius ? radius : y >= height - radius ? height - radius - 1f : y;
            float dx = x - cx;
            float dy = y - cy;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Mathf.Clamp01(radius - distance + 1f);
            pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        _roundedButtonSprite = Sprite.Create(
            texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(20f, 20f, 20f, 20f));
        _roundedButtonSprite.name = "SurgeActionButton_Rounded";
        return _roundedButtonSprite;
    }
}
