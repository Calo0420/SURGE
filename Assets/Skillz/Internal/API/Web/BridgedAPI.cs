using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SkillzSDK.MiniJSON;
using Random = System.Random;
using SkillzSDK.Extensions;
using SkillzSDK.Internal.Encryption;
using SkillzSDK.Internal.API.UnityEditor;
using SkillzSDK.Settings;

using JSONDict = System.Collections.Generic.Dictionary<string, object>;

// You'll find below many references to Debug.isDebugBuild and SideKick.
// When the WebSDK is built for Debug, it will utilize the SideKick
// integration to allow developers to more easily test their web integration.

namespace SkillzSDK.Internal.API.Web
{

    public struct SkillzMatch
    {
        public string id;
        public string playerMatchId;
    }

    public struct RandomSeed
    {
        public uint[] seed;
    }

    internal sealed class BridgedAPI : IBridgedAPI, IRandom
    {
        private const float DefaultVolume = 0.5f;

        // <Begin SideKick Support Variables>
        System.Random SeededRandom;
        private bool matchInProgress;
        private bool inMatch = false;
        private Match matchInfo;
        private const string DefaultScoreString = "0";
#pragma warning disable IDE0052 // Remove unread private members
        private string currentScore;
#pragma warning restore IDE0052 // Remove unread private members
        APIResponseSimulator responseSimulator;

        // <End SideKick Support Variables>

        public IRandom Random
        {
            get
            {
                return this;
            }
        }



        bool IAsyncAPI.IsMatchInProgress
        {
            get
            {
                if (Debug.isDebugBuild)
                {
                    return matchInProgress;
                }
                else
                {
#if UNITY_WEBGL
                string jsonMatch = JsBridge.RunSyncTask("GetMatch");
                if (string.IsNullOrEmpty(jsonMatch))
                {
                    return false;
                } else {
                    return true;
                }
#else
                    return false;
#endif

                }
            }
        }

        public float SkillzMusicVolume
        {
            get
            {
                return skillzMusicVolume;
            }
            set
            {
                skillzMusicVolume = SanitizeVolume(value);
            }
        }

        public float SoundEffectsVolume
        {
            get
            {
                return soundEffectsVolume;
            }
            set
            {
                soundEffectsVolume = SanitizeVolume(value);
            }
        }

        private float skillzMusicVolume;
        private float soundEffectsVolume;


        private readonly Random random;

        public BridgedAPI()
        {
            random = new Random();

            skillzMusicVolume = DefaultVolume;
            soundEffectsVolume = DefaultVolume;
            if (Debug.isDebugBuild)
            {
                responseSimulator = new APIResponseSimulator();
            }
        }

        public void Initialize(int gameID, Environment environment, Orientation orientation)
        {
            if (Debug.isDebugBuild)
            {
                currentScore = DefaultScoreString;
            }
        }

        public void LaunchSkillz()
        {
            if (Debug.isDebugBuild)
            {
                try
                {
                    CoroutineRunner.Instance.RunCoroutine(SDKScenesLoader.Load(SDKScenesLoader.TournamentSelectionScene));
                }
                catch (Exception e)
                {
                    Debug.Log("Tried to load scene, failed: " + e);
                }

            }
            else
            {
                JsBridge.RunTask("LaunchSkillz");
            }
        }

        public Hashtable GetMatchRules()
        {
            if (Debug.isDebugBuild)
            {
                Debug.Log("Debug match info");
                if (matchInfo != null)
                {
                    Debug.Log($"Match Info: '{matchInfo.ToString()}'");

                    return new Hashtable(matchInfo.GameParams);
                }
                else
                {
                    Debug.Log("No match, return null");
                    return null;
                }
            }
            else
            {
            #if UNITY_WEBGL
                string jsonMatch = JsBridge.RunSyncTask("GetMatch");
                if (string.IsNullOrEmpty(jsonMatch))
                {
                    return null;
                }
                JSONDict matchDictionary = DeserializeJSONToDictionary(jsonMatch);
                if (matchDictionary == null)
                {
                    return null;
                }
                object rules = matchDictionary.SafeGetValue("gameParameters");
                if (rules == null || rules.GetType() != typeof(JSONDict))
                {
                    return null;
                }
                return new Hashtable((JSONDict)rules);
            #else
                return null;
            #endif
            }
        }

        private static Dictionary<string, object> DeserializeJSONToDictionary(string jsonString)
        {
            return MiniJSON.Json.Deserialize(jsonString) as Dictionary<string, object>;
        }

        public Match GetMatchInfo()
        {
            if (Debug.isDebugBuild)
            {
                if (matchInfo != null)
                {
                    SkillzDebug.Log(SkillzDebug.Type.SKILLZ, $"Match Info: '{matchInfo.ToString()}'");
                    return matchInfo;
                }
                else
                {
                    return null;
                }
            }
            else
            {
            #if UNITY_WEBGL
                string jsonMatch = JsBridge.RunSyncTask("GetMatch");
            
                //Debug.Log("got jsonMatch: " + jsonMatch);
                if (string.IsNullOrEmpty(jsonMatch))
                {
                    return null;
                }
                Dictionary<string, object> matchDictionary = DeserializeJSONToDictionary(jsonMatch);
                //Debug.Log("got matchDictionary: " + matchDictionary);
                return new Match(matchDictionary);
            #else
                return null;
            #endif
            }
        }

        public void AbortMatch()
        {
            if (Debug.isDebugBuild)
            {
                FinishSimulatedMatch();
                inMatch = false;
                CoroutineRunner.Instance.RunCoroutine(SDKScenesLoader.Load(SDKScenesLoader.MatchAbortedScene));
            }
            else
            {
                Match matchDetails = GetMatchInfo();
                ulong? playerMatchId = matchDetails.Players.FindLast((p) => p.IsCurrentPlayer)?.TournamentPlayerID;
                string scorePayload = matchDetails.ID + "---" + playerMatchId;
                //Debug.Log("dataPayloadString: " + scorePayload);
                string matchToken = JsBridge.SyncTask("GetMatchToken");
                if (string.IsNullOrEmpty(matchToken))
                {
                    Debug.LogError("Match token is null or empty");
                    return;
                }
                EncryptionUtility.SetLogFunction(Debug.Log);
                string encryptedScore = EncryptionUtility.Encrypt(scorePayload, matchDetails, matchToken);
                JsBridge.RunTask("AbortMatch", encryptedScore);
                ReturnToSkillz();
            }
        }

        public void AbortBotMatch(string botScore)
        {
            AbortMatch();
        }

        public void AbortBotMatch(int botScore)
        {
            AbortMatch();
        }

        public void AbortBotMatch(float botScore)
        {
            AbortMatch();
        }

        public void UpdatePlayersCurrentScore(string score)
        {
            if (Debug.isDebugBuild)
            {
                currentScore = score;
            }
            else
            {
                Match matchDetails = GetMatchInfo();
                ulong? playerMatchId = matchDetails.Players.FindLast((p) => p.IsCurrentPlayer)?.TournamentPlayerID;
                string scorePayload = matchDetails.ID + "---" + playerMatchId + "---" + score;
                //Debug.Log("scorePayloadString: " + scorePayload);
                string matchToken = JsBridge.SyncTask("GetMatchToken");
                if (string.IsNullOrEmpty(matchToken))
                {
                    Debug.LogError("Match token is null or empty");
                    return;
                }
                EncryptionUtility.SetLogFunction(Debug.Log);
                string encryptedScore = EncryptionUtility.Encrypt(scorePayload, matchDetails, matchToken);
                JsBridge.RunTask("UpdateScore", encryptedScore);
            }
        }

        public void UpdatePlayersCurrentScore(int score)
        {
            UpdatePlayersCurrentScore(score.ToString());
        }

        public void UpdatePlayersCurrentScore(float score)
        {
            UpdatePlayersCurrentScore(score.ToString());
        }

        public void DisplayTournamentResultsWithScore(string score)
        {
            if (Debug.isDebugBuild)
            {
                currentScore = score;
                FinishSimulatedMatch();
                CoroutineRunner.Instance.RunCoroutine(SDKScenesLoader.Load(SDKScenesLoader.MatchCompletedScene));
            }
            else
            {
                SubmitScore(score, () =>
                {
                    ReturnToSkillz();
                }, (string error) =>
                {
                    ReturnToSkillz();
                });
            }
        }

        public void DisplayTournamentResultsWithScore(int score)
        {
            DisplayTournamentResultsWithScore(score.ToString());

        }

        public void DisplayTournamentResultsWithScore(float score)
        {
            DisplayTournamentResultsWithScore(score.ToString());
        }

        public void ReportFinalScoreForBotMatch(string playerScore, string botScore)
        {
            ReportFinalScoreForBotMatch(float.Parse(playerScore), float.Parse(botScore));
        }

        public void ReportFinalScoreForBotMatch(int playerScore, int botScore)
        {
            ReportFinalScoreForBotMatch((float)playerScore, (float)botScore);
        }

        public void ReportFinalScoreForBotMatch(float playerScore, float botScore)
        {
            if (Debug.isDebugBuild)
            {
                FinishSimulatedMatch();
                CoroutineRunner.Instance.RunCoroutine(SDKScenesLoader.Load(SDKScenesLoader.MatchCompletedScene));
            }
            else
            {
                Debug.Log("Report Final Score For Bot Match is not yet implemented on Web");
            }
        }

        public void SubmitScore(string score, Action successCallback, Action<string> failureCallback)
        {
            if (Debug.isDebugBuild)
            {
                SkillzSettings.Instance.Score = score;
                FinishSimulatedMatch();

                if (successCallback != null)
                {
                    SkillzDebug.Log(SkillzDebug.Type.SIDEKICK, $"SkillzCrossPlatform.SubmitScore() Success Callback");
                    successCallback();
                }
            }
            else
            {
                Utils.HandleAsyncTaskToDelegate(
                async () =>
                {
                    Match matchDetails = GetMatchInfo();
                    ulong? playerMatchId = matchDetails.Players.FindLast((p) => p.IsCurrentPlayer)?.TournamentPlayerID;
                    string scorePayload = matchDetails.ID + "---" + playerMatchId + "---" + score;
                    //Debug.Log("scorePayloadString: " + scorePayload);
                    string matchToken = JsBridge.SyncTask("GetMatchToken");
                    if (string.IsNullOrEmpty(matchToken))
                    {
                        //Debug.LogError("Match token is null or empty");
                        return null;
                    }
                    EncryptionUtility.SetLogFunction(Debug.Log);
                    string encryptedScore = EncryptionUtility.Encrypt(scorePayload, matchDetails, matchToken);
                    return await JsBridge.RunTask("SubmitScore", encryptedScore);
                },
                successCallback,
                failureCallback);
            }
        }

        public void SubmitScore(int score, Action successCallback, Action<string> failureCallback)
        {
            SubmitScore(score.ToString(), successCallback, failureCallback);
        }

        public void SubmitScore(float score, Action successCallback, Action<string> failureCallback)
        {
            SubmitScore(score.ToString(), successCallback, failureCallback);
        }

        public bool EndReplay()
        {
            SkillzDebug.Log(SkillzDebug.Type.SKILLZ, "EndReplay is not supported on the Web yet");
            return true;
        }

        public bool ReturnToSkillz()
        {
            if (Debug.isDebugBuild)
            {
                bool hasSubmittedScore = !matchInProgress;

                Debug.Log("Return to Skillz: " + inMatch);
                //If in Progression or Season room return to the tournament selection screen
                if (!inMatch)
                {
                    Debug.Log("Show touranment selection");
                    CoroutineRunner.Instance.RunCoroutine(SDKScenesLoader.Load(SDKScenesLoader.TournamentSelectionScene));
                    return false;
                }

                inMatch = false;

                if (hasSubmittedScore)
                {
                    Debug.Log("Show match completed");
                    CoroutineRunner.Instance.RunCoroutine(SDKScenesLoader.Load(SDKScenesLoader.MatchCompletedScene));
                }
                return hasSubmittedScore;
            }
            else
            {
                JsBridge.RunTask("ReturnToSkillz");
            }
            return true;
        }

        public string SDKVersionShort()
        {
            return string.Empty;
        }

        public Player GetPlayer()
        {
            if (Debug.isDebugBuild)
            {
                var mockPlayerJSON = new JSONDict {
                    { "id", 12345 },
                    { "displayName", "MockPlayer" },
                    { "avatarURL", "https://example.com/mock-avatar.png" },
                    { "flagURL", "https://example.com/mock-flag.png" },
                    { "isCurrentPlayer", true },
                    { "playerMatchId", 987654321UL }, // Note the UL suffix for ulong
                    { "isNewPayingUser", false }
                };

                return new Player(mockPlayerJSON);
            }
            else
            {
                var playerJson = JsBridge.SyncTask("GetPlayer");
                if (string.IsNullOrEmpty(playerJson))
                {
                    return null;
                }
                Dictionary<string, object> playerDict = MiniJSON.Json.Deserialize(playerJson) as Dictionary<string, object>;
                return new Player(playerDict);
            }
        }

        public void AddMetadataForMatchInProgress(string metadataJson, bool forMatchInProgress)
        {
            SkillzDebug.LogWarning(SkillzDebug.Type.SKILLZ, "AddMetadataForMatchInProgress is not supported in web mode");
        }

        public void SetSkillzBackgroundMusic(string fileName)
        {
            SkillzDebug.Log(SkillzDebug.Type.SKILLZ, "SetSkillzBackgroundMusic is not supported in web mode");
        }

        private static float SanitizeVolume(float volume)
        {
            if (volume > 1f)
            {
                return 1f;
            }

            if (volume < 0f)
            {
                return 0f;
            }

            return volume;
        }

        public async void GetProgressionUserData(string progressionNamespace, List<string> userDataKeys, Action<Dictionary<string, ProgressionValue>> successCallback, Action<string> failureCallback)
        {
            if (Debug.isDebugBuild)
            {
                if (successCallback != null)
                {
                    responseSimulator.SimulateGetProgressionUserData(
                        progressionNamespace,
                        userDataKeys,
                        successCallback,
                        failureCallback
                    );
                }
            }
            else
            {
                try
                {
                    var payloadDictionary = new Dictionary<string, object>
                {
                    { "progressionNamespace", progressionNamespace },
                    { "userDataKeys", userDataKeys }
                };
                    string jsonPayload = MiniJSON.Json.Serialize(payloadDictionary);
                    string response = await JsBridge.RunTask("GetProgressionUserData", jsonPayload);
                    Debug.Log("GetProgressionUserData got Result: " + response);
                    var jsonDict = MiniJSON.Json.Deserialize(response) as Dictionary<string, object>;
                    Dictionary<string, ProgressionValue> formattedData = ProgressionValue.GetProgressionValuesFromJSON(jsonDict);
                    Debug.Log("GetProgressionUserData Parsed Result: " + formattedData);
                    successCallback(formattedData);
                }
                catch (Exception e)
                {
                    Debug.Log($"Error in GetProgressionUserData: {e.Message}");
                    failureCallback($"Error in GetProgressionUserData: {e.Message}");
                }
            }
        }


        public async void UpdateProgressionUserData(string progressionNamespace, Dictionary<string, object> userDataUpdates, Action successCallback, Action<string> failureCallback)
        {
            if (Debug.isDebugBuild)
            {
                responseSimulator.SimulateUpdateProgressionUserData(
                    progressionNamespace,
                    userDataUpdates,
                    successCallback,
                    failureCallback
                );
            }
            else
            {
                try
                {
                    var payloadDictionary = new Dictionary<string, object>
                {
                    { "progressionNamespace", progressionNamespace },
                    { "userDataUpdates", userDataUpdates }
                };
                    string jsonPayload = MiniJSON.Json.Serialize(payloadDictionary);
                    await JsBridge.RunTask("UpdateProgressionUserData", jsonPayload);
                    Debug.Log("UpdateProgressionUserData");
                    successCallback();
                }
                catch (Exception e)
                {
                    Debug.Log($"Error in UpdateProgressionUserData: {e.Message}");
                    failureCallback($"Error in UpdateProgressionUserData: {e.Message}");
                }
            }
        }

        public async void GetCurrentSeason(Action<Season> successCallback, Action<string> failureCallback)
        {
            if (Debug.isDebugBuild)
            {
                responseSimulator.GetCurrentSeason(successCallback, failureCallback);
            }
            else
            {
                try
                {
                    string response = await JsBridge.RunTask("GetCurrentSeason");
                    var jsonObj = MiniJSON.Json.Deserialize(response) as Dictionary<string, object>;
                    Season season = new Season(jsonObj);
                    Debug.Log($"GetCurrentSeason() Success callback, Season: '{SkillzDebug.Format(season)}'");
                    successCallback(season);
                }
                catch (Exception e)
                {
                    Debug.Log($"Error in GetCurrentSeason: {e.Message} {e}");
                    failureCallback($"Error in GetCurrentSeason: {e.Message}");
                }
            }
        }

        public async void GetPreviousSeasons(int count, Action<List<Season>> successCallback, Action<string> failureCallback)
        {
            if (Debug.isDebugBuild)
            {
                responseSimulator.GetPreviousSeasons(count, successCallback, failureCallback); ;
            }
            else
            {
                try
                {
                    var payloadDictionary = new Dictionary<string, object>
                {
                    { "count", count }
                };
                    string jsonPayload = MiniJSON.Json.Serialize(payloadDictionary);
                    string response = await JsBridge.RunTask("GetPreviousSeasons", jsonPayload);
                    var jsonDict = MiniJSON.Json.Deserialize(response) as Dictionary<string, object>;
                    List<Season> seasons = Season.ParseSeasonsFromJSON(jsonDict);
                    Debug.Log($"GetPreviousSeasons() Success callback, Season: '{SkillzDebug.Format(seasons)}'");
                    successCallback(seasons);
                }
                catch (Exception e)
                {
                    Debug.Log($"Error in GetPreviousSeasons: {e.Message} {e}");
                    failureCallback($"Error in GetPreviousSeasons: {e.Message}");
                }
            }
        }

        public async void GetNextSeasons(int count, Action<List<Season>> successCallback, Action<string> failureCallback)
        {
            if (Debug.isDebugBuild)
            {
                responseSimulator.GetNextSeasons(count, successCallback, failureCallback);
            }
            else
            {
                try
                {
                    var payloadDictionary = new Dictionary<string, object>
               {
                   { "count", count }
               };
                    string jsonPayload = MiniJSON.Json.Serialize(payloadDictionary);
                    string response = await JsBridge.RunTask("GetNextSeasons", jsonPayload);
                    var jsonDict = MiniJSON.Json.Deserialize(response) as Dictionary<string, object>;
                    List<Season> seasons = Season.ParseSeasonsFromJSON(jsonDict);
                    Debug.Log($"GetNextSeasons() Success callback, Season: '{SkillzDebug.Format(seasons)}'");
                    successCallback(seasons);
                }
                catch (Exception e)
                {
                    Debug.Log($"Error in GetNextSeasons: {e.Message} {e}");
                    failureCallback($"Error in GetNextSeasons: {e.Message}");
                }
            }
        }

        public void InitializeSimulatedMatch(string matchInfoJson, int randomSeed)
        {
            Debug.Log("initialize simulated match");
            SkillzSettings.Instance.Score = "null";
            matchInProgress = true;
            inMatch = true;
            matchInfo = new Match((Dictionary<string, object>)Json.Deserialize(matchInfoJson));
            SeededRandom = new System.Random(randomSeed);
            SkillzDebug.Log(SkillzDebug.Type.SIDEKICK, "Skillz random seeded with: " + randomSeed);
        }


        private void FinishSimulatedMatch()
        {
            matchInProgress = false;
            matchInfo = null;
        }

        float IRandom.Value()
        {
            if (Debug.isDebugBuild)
            {
                if (matchInProgress)
                {
                    return (float)SeededRandom.NextDouble();
                }
                else
                {
                    return UnityEngine.Random.value;
                }
            }
            else
            {
                return ClangBridge.GetRandomFloat();
            }
        }

        public float Value()
        {
            if (Debug.isDebugBuild)
            {
                if (matchInProgress)
                {
                    return (float)SeededRandom.NextDouble();
                }
                else
                {
                    return UnityEngine.Random.value;
                }
            }
            else
            {
                return ClangBridge.GetRandomFloat();
            }
        }
    }
}
