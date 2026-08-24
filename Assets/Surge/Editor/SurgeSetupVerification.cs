// ============================================================================
// SURGE — Editor-side setup verification.
//
// Three guards, all runnable from the menu or headless via -executeMethod:
//   1. Core self-test: proves the locked SurgeCore engine compiles and behaves
//      identically inside Unity as it does in plain .NET.
//   2. MatchClock self-test: proves game time stays monotonic, freezes without
//      losing surplus, and is invariant to how real time is chunked.
//   3. Render pipeline: proves URP + the 2D Renderer are actually active, not
//      just present in the package manifest. Built-in would silently work
//      until the first bloom/2D-light pass, which is far too late to find out.
// ============================================================================

using System;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Surge.Timing;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Surge.Editor
{
    public static class SurgeSetupVerification
    {
        const string UrpAssetTypeName =
            "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset";
        const string Renderer2DTypeName =
            "UnityEngine.Rendering.Universal.Renderer2DData";

        // ------------------------------------------------------- self-test --
        [MenuItem("Surge/Verify/Run Core Self-Test (quick)", priority = 0)]
        public static void RunSelfTestQuick() => RunSelfTest(200);

        [MenuItem("Surge/Verify/Run Core Self-Test (full)", priority = 1)]
        public static void RunSelfTestFull() => RunSelfTest(2000);

        static bool RunSelfTest(int matches)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                string report = SurgeCore.SelfTest.Run(matches);
                sw.Stop();
                Debug.Log($"[Surge] {report} ({sw.ElapsedMilliseconds} ms)");
                return true;
            }
            catch (Exception e)
            {
                sw.Stop();
                Debug.LogError($"[Surge] Core self-test FAILED after " +
                               $"{sw.ElapsedMilliseconds} ms: {e.Message}");
                return false;
            }
        }

        // -------------------------------------------------- clock self-test --
        [MenuItem("Surge/Verify/Run MatchClock Self-Test", priority = 2)]
        public static void RunClockSelfTestMenu() => RunClockSelfTest(5000);

        static bool RunClockSelfTest(int cases)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                string report = MatchClockSelfTest.Run(cases);
                sw.Stop();
                Debug.Log($"[Surge] {report} ({sw.ElapsedMilliseconds} ms)");
                return true;
            }
            catch (Exception e)
            {
                sw.Stop();
                Debug.LogError($"[Surge] MatchClock self-test FAILED after " +
                               $"{sw.ElapsedMilliseconds} ms: {e.Message}");
                return false;
            }
        }

        // -------------------------------------------------- render pipeline --
        [MenuItem("Surge/Verify/Verify Render Pipeline", priority = 20)]
        public static void VerifyRenderPipelineMenu()
        {
            if (VerifyRenderPipeline(out string report)) Debug.Log($"[Surge] {report}");
            else Debug.LogError($"[Surge] {report}");
        }

        static bool VerifyRenderPipeline(out string report)
        {
            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
            if (rp == null)
            {
                report = "Render pipeline is Built-in (no SRP asset assigned). " +
                         "URP is required — see docs/VISUAL_BRIEF.md.";
                return false;
            }

            string rpType = rp.GetType().FullName;
            if (rpType != UrpAssetTypeName)
            {
                report = $"Active render pipeline is '{rpType}', expected URP " +
                         $"('{UrpAssetTypeName}').";
                return false;
            }

            // Read the renderer list without referencing URP assemblies, so this
            // guard cannot itself break on a URP package upgrade.
            var so = new SerializedObject(rp);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            if (list == null || !list.isArray || list.arraySize == 0)
            {
                report = "URP asset has no renderer data assigned.";
                return false;
            }

            UnityEngine.Object renderer =
                list.GetArrayElementAtIndex(0).objectReferenceValue;
            if (renderer == null)
            {
                report = "URP asset's default renderer slot is empty.";
                return false;
            }

            string rendererType = renderer.GetType().FullName;
            if (rendererType != Renderer2DTypeName)
            {
                report = $"URP default renderer is '{rendererType}', expected the " +
                         $"2D Renderer ('{Renderer2DTypeName}').";
                return false;
            }

            report = $"Render pipeline OK: URP asset '{rp.name}' with 2D Renderer " +
                     $"'{renderer.name}'.";
            return true;
        }

        // ------------------------------------------------------- batch mode --
        // Fast gate: compilation + render pipeline only. Skips the simulation
        // self-tests, which dominate the runtime, so scaffold changes can be
        // checked quickly. Use CI for the full gate.
        //   Unity -batchmode -nographics -quit -projectPath . \
        //         -executeMethod Surge.Editor.SurgeSetupVerification.Setup
        public static void Setup()
        {
            bool ok = VerifyRenderPipeline(out string report);
            if (ok) Debug.Log($"[Surge] {report}");
            else Debug.LogError($"[Surge] {report}");

            Debug.Log(ok ? "[Surge] Project setup verification PASSED."
                         : "[Surge] Project setup verification FAILED.");
            EditorApplication.Exit(ok ? 0 : 1);
        }

        // Full gate: render pipeline + engine + match clock.
        //   Unity -batchmode -nographics -quit -projectPath . \
        //         -executeMethod Surge.Editor.SurgeSetupVerification.CI
        public static void CI()
        {
            bool ok = true;

            if (VerifyRenderPipeline(out string report)) Debug.Log($"[Surge] {report}");
            else { Debug.LogError($"[Surge] {report}"); ok = false; }

            if (!RunSelfTest(2000)) ok = false;
            if (!RunClockSelfTest(5000)) ok = false;

            Debug.Log(ok ? "[Surge] Setup verification PASSED."
                         : "[Surge] Setup verification FAILED.");
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
