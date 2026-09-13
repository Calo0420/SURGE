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
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SurgeHUDController : MonoBehaviour
{
    [Header("Engine Seam")]
    [SerializeField] private MatchDriver driver;

    [Header("UI Elements")]
    [SerializeField] private Text scoreText;
    [SerializeField] private Text timerText;
    [SerializeField] private Text surgeMeterText;

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

    public void OpenTutorial()
    {
        if (tutorialController != null)
            tutorialController.OpenTutorial();
    }

    private void Awake()
    {
        ResolveReferences();
        CacheInitialColors();
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
        UpdateHUD(forceImmediate: false);
    }

    private void ResolveReferences()
    {
        if (driver == null)
            driver = FindAnyObjectByType<MatchDriver>();

        if (scoreText == null)
        {
            Transform t = transform.Find("ScoreText");
            if (t != null) scoreText = t.GetComponent<Text>();
        }

        if (timerText == null)
            timerText = GetComponentInChildren<Text>();

        if (surgeMeterText == null)
        {
            Transform t = transform.Find("SurgeMeterText");
            if (t != null) surgeMeterText = t.GetComponent<Text>();
        }

        if (tutorialController == null)
            tutorialController = FindAnyObjectByType<SurgeTutorialController>();
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
            scoreText.text = $"SCORE {Mathf.RoundToInt(_displayedScore):D6}";
        }

        // 2. Timer Countdown (mm:ss) + 10s urgent pulse
        long remainingMs = driver.RemainingMs;
        int remainingSec = (int)((remainingMs + 999L) / 1000L);
        int minutes = remainingSec / 60;
        int seconds = remainingSec % 60;

        if (timerText != null)
        {
            timerText.text = $"{minutes:D2}:{seconds:D2}";

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
        if (surgeMeterText != null)
        {
            if (driver.SurgeActive)
            {
                // Active frenzy mode: pulse accent color
                float pulse = Mathf.PingPong(Time.unscaledTime * 6f, 1f);
                surgeMeterText.color = Color.Lerp(palette != null ? palette.neonPink : Color.magenta, Color.white, pulse);
                surgeMeterText.text = "SURGE ACTIVE!";
            }
            else if (driver.Banked)
            {
                // Banked & ready to pop
                float pulse = Mathf.PingPong(Time.unscaledTime * 3f, 1f);
                surgeMeterText.color = Color.Lerp(surgeReadyColor, Color.white, pulse);
                surgeMeterText.text = "SURGE READY!";
            }
            else
            {
                surgeMeterText.color = _initialMeterColor;
                surgeMeterText.text = $"SURGE {driver.Meter}%";
            }
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
            scoreText.text = $"SCORE {result.Score:D6}";
        }
    }
}
