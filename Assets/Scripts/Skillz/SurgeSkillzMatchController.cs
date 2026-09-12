// ============================================================================
// SURGE — SurgeSkillzMatchController
//
// Bridges Skillz SDK with Surge tournament gameplay:
//   - Manages tournament lifecycle: launch, seed extraction, score submission
//   - Automatically submits score to Skillz when the 90s match expires
// ============================================================================

using Surge.Runtime;
using Surge.Skillz;
using SurgeCore;
using UnityEngine;

[DisallowMultipleComponent]
public class SurgeSkillzMatchController : MonoBehaviour
{
    public static SurgeSkillzMatchController Instance { get; private set; }

    [Header("Engine Seam")]
    [SerializeField] private MatchDriver driver;

    private bool _isSkillzMatch;

    public bool IsSkillzMatch => _isSkillzMatch;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (driver == null)
            driver = FindFirstObjectByType<MatchDriver>();
    }

    private void Start()
    {
        if (driver != null)
        {
            driver.MatchEnded += OnMatchEnded;
        }

        // If running inside Skillz standalone tournament launch
        if (SkillzCrossPlatform.IsMatchInProgress())
        {
            _isSkillzMatch = true;
            if (driver != null && !driver.MatchRunning)
            {
                driver.BeginMatch(new SkillzSeedSource());
            }
        }
    }

    private void OnDestroy()
    {
        if (driver != null)
        {
            driver.MatchEnded -= OnMatchEnded;
        }
    }

    public void LaunchSkillzTournament()
    {
        Debug.Log("[Surge] Launching Skillz tournament UI...");
        SkillzCrossPlatform.LaunchSkillz();
    }

    public void OnSkillzMatchWillBegin(SkillzSDK.Match matchInfo)
    {
        _isSkillzMatch = true;
        Debug.Log($"[Surge] Skillz match will begin. Match ID: {matchInfo?.ID}");

        if (driver != null)
        {
            driver.BeginMatch(new SkillzSeedSource());
        }
    }

    public void OnSkillzWillExit()
    {
        _isSkillzMatch = false;
        Debug.Log("[Surge] Skillz session ended.");
    }

    private void OnMatchEnded(MatchResult result)
    {
        int score = result?.Score ?? (driver != null ? driver.Score : 0);
        ReportScore(score);
    }

    public void ReportScore(int score)
    {
        if (!_isSkillzMatch && !SkillzCrossPlatform.IsMatchInProgress())
        {
            Debug.Log($"[Surge] Test/Practice match ended with score {score} (Skillz report bypassed).");
            return;
        }

        Debug.Log($"[Surge] Submitting final tournament score {score} to Skillz...");
        SkillzCrossPlatform.SubmitScore(score, OnScoreSubmitSuccess, OnScoreSubmitFailure);
    }

    private void OnScoreSubmitSuccess()
    {
        Debug.Log("[Surge] Score successfully submitted to Skillz! Showing tournament results...");
        int score = driver != null ? driver.Score : 0;
        SkillzCrossPlatform.DisplayTournamentResultsWithScore(score);
    }

    private void OnScoreSubmitFailure(string error)
    {
        Debug.LogError($"[Surge] Score submission failed: {error}");
        int score = driver != null ? driver.Score : 0;
        SkillzCrossPlatform.DisplayTournamentResultsWithScore(score);
    }
}
