using System;
using System.Collections;
using System.IO;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

// NOTE: The wonky pragmas here are for getting generation of the Asset Bundles
// and compilation to work without forcing each consumer of this class to use
// the pragma.

namespace SkillzSDK.Internal
{
    internal static class SDKScenesLoader
    {
        public const string TournamentSelectionScene = "TournamentSelection";
        public const string MatchCompletedScene = "MatchComplete";
        public const string MatchAbortedScene = "MatchAborted";

        private static AssetBundle scenesAssetBundle;
        private static AssetBundle ancillaryAssetBundle;

        static SDKScenesLoader()
        {
            Debug.Log("[SkillzSDK] Accessing Scenes Loader");
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += PlayModeStateChanged;
#endif
        }

        /// <summary>
        /// Loads the specified scene using platform-appropriate methods.
        /// </summary>
        /// <param name="sdkSceneName">The name of the scene to load.</param>
        public static IEnumerator Load(string sdkSceneName)
        {
            Debug.Log($"[SkillzSDK] Begin loading scene: {sdkSceneName}");

            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                Debug.Log("[SkillzSDK] Loading scene from StreamingAssets for WebGL");
                yield return LoadAssetBundleFromStreamingAssets(sdkSceneName);
            }
            else
            {
                Debug.Log("[SkillzSDK] Loading scene from AssetBundles for Editor");
                yield return LoadAssetBundleForEditor(sdkSceneName);
            }
        }

        /// <summary>
        /// Loads the AssetBundles and scene for the Unity Editor.
        /// </summary>
        private static IEnumerator LoadAssetBundleForEditor(string sdkSceneName)
        {
            string basePath;
            #if UNITY_EDITOR_OSX
                basePath = Path.Combine("Assets", "Skillz", "Internal", "AssetBundles", "Standalone", "macOS");
            #elif UNITY_EDITOR_WIN
                basePath = Path.Combine("Assets", "Skillz", "Internal", "AssetBundles", "Standalone", "Windows");
            #else
                throw new PlatformNotSupportedException("Unsupported editor platform for Standalone AssetBundles");
            #endif

            try
            {
                if (ancillaryAssetBundle == null)
                {
                    ancillaryAssetBundle = AssetBundle.LoadFromFile(Path.Combine(basePath, "skillz-assets"));
                }

                if (scenesAssetBundle == null)
                {
                    scenesAssetBundle = AssetBundle.LoadFromFile(Path.Combine(basePath, "skillz-scenes"));
                }

                string sdkScenePath = GetScenePath(scenesAssetBundle, sdkSceneName);
                if (string.IsNullOrEmpty(sdkScenePath))
                {
                    Debug.LogWarning($"[SkillzSDK] Scene '{sdkSceneName}' not found in AssetBundle");
                    yield break;
                }

                Debug.Log($"[SkillzSDK] Loading scene: {sdkSceneName}");
                SceneManager.LoadScene(sdkScenePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SkillzSDK] Error loading scene for Editor: {e.Message}");
            }

            yield break;
        }

        /// <summary>
        /// Loads the AssetBundles and scene for WebGL.
        /// </summary>
        private static IEnumerator LoadAssetBundleFromStreamingAssets(string sdkSceneName)
        {
            string basePath = Application.streamingAssetsPath;

            // Load ancillary AssetBundle
            if (ancillaryAssetBundle == null)
            {
                string ancillaryBundleUrl = $"{basePath}/skillz-assets";
                yield return LoadAssetBundleWithUnityWebRequest(ancillaryBundleUrl, bundle => ancillaryAssetBundle = bundle);
            }

            // Load scenes AssetBundle
            if (scenesAssetBundle == null)
            {
                string scenesBundleUrl = $"{basePath}/skillz-scenes";
                yield return LoadAssetBundleWithUnityWebRequest(scenesBundleUrl, bundle => scenesAssetBundle = bundle);
            }

            string sdkScenePath = GetScenePath(scenesAssetBundle, sdkSceneName);
            if (string.IsNullOrEmpty(sdkScenePath))
            {
                Debug.LogWarning($"[SkillzSDK] Scene '{sdkSceneName}' not found in AssetBundle");
                yield break;
            }

            Debug.Log($"[SkillzSDK] Loading scene: {sdkSceneName}");
            SceneManager.LoadScene(sdkScenePath);
        }

        /// <summary>
        /// Loads an AssetBundle using UnityWebRequest.
        /// </summary>
        private static IEnumerator LoadAssetBundleWithUnityWebRequest(string bundleUrl, Action<AssetBundle> onSuccess)
        {
            Debug.Log($"[SkillzSDK] Attempting to load AssetBundle from URL: {bundleUrl}");
            UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(bundleUrl);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[SkillzSDK] Failed to load AssetBundle from {bundleUrl}: {request.error}");
                yield break;
            }

            AssetBundle bundle = DownloadHandlerAssetBundle.GetContent(request);
            if (bundle == null)
            {
                Debug.LogError($"[SkillzSDK] AssetBundle loaded from {bundleUrl} is null");
                yield break;
            }

            Debug.Log($"[SkillzSDK] Successfully loaded AssetBundle from {bundleUrl}");
            onSuccess(bundle);
        }

        /// <summary>
        /// Retrieves the full scene path from an AssetBundle.
        /// </summary>
        private static string GetScenePath(AssetBundle bundle, string sdkSceneName)
        {
            return bundle.GetAllScenePaths()
                .FirstOrDefault(scenePath =>
                    string.Equals(Path.GetFileNameWithoutExtension(scenePath), sdkSceneName, StringComparison.InvariantCulture));
        }

#if UNITY_EDITOR
        /// <summary>
        /// Unloads AssetBundles when exiting Play mode in the Editor.
        /// </summary>
        private static void PlayModeStateChanged(PlayModeStateChange playmodeState)
        {
            if (playmodeState != PlayModeStateChange.ExitingPlayMode)
            {
                return;
            }

            EditorApplication.playModeStateChanged -= PlayModeStateChanged;

            Debug.Log("[SkillzSDK] Unloading AssetBundles in Editor");

            scenesAssetBundle?.Unload(true);
            ancillaryAssetBundle?.Unload(true);
        }
#endif
    }
}