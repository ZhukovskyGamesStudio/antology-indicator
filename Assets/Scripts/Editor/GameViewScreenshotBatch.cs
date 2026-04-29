using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameViewScreenshotBatch
{
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";
    private const string OutputFolder = "Assets/Screenshots";
    private const int PostPlayWarmupFrames = 120;
    private const double MaxTotalSeconds = 180;
    private const double MaxWaitEnterPlaySeconds = 60;
    private const double MaxWaitExitPlaySeconds = 90;

    private static int _stage;
    private static int _sceneSettleFrames;
    private static int _warmupFrames;
    private static double _batchStartTime;
    private static double _enterPlayRequestedTime;
    private static double _exitPlayRequestedTime;

    public static void CaptureGameSceneScreenshotAndQuit()
    {
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
        _stage = 0;
        _sceneSettleFrames = 0;
        _warmupFrames = 0;
        _batchStartTime = EditorApplication.timeSinceStartup;
        _enterPlayRequestedTime = 0;
        _exitPlayRequestedTime = 0;
    }

    private static void FailAndExit(string message)
    {
        Debug.LogError(message);
        EditorApplication.update -= OnUpdate;
        EditorApplication.Exit(1);
    }

    private static void OnUpdate()
    {
        if (EditorApplication.timeSinceStartup - _batchStartTime > MaxTotalSeconds)
        {
            FailAndExit("Batch screenshot aborted: total time limit exceeded.");
            return;
        }

        if (_stage == 0)
        {
            EditorSceneManager.OpenScene(GameScenePath);
            _stage = 1;
            return;
        }

        if (_stage == 1)
        {
            if (EditorApplication.isCompiling)
            {
                return;
            }

            _sceneSettleFrames++;
            if (_sceneSettleFrames < 3)
            {
                return;
            }

            EditorApplication.isPlaying = true;
            _enterPlayRequestedTime = EditorApplication.timeSinceStartup;
            _stage = 2;
            return;
        }

        if (_stage == 2)
        {
            if (!EditorApplication.isPlaying)
            {
                if (_enterPlayRequestedTime > 0
                    && EditorApplication.timeSinceStartup - _enterPlayRequestedTime > MaxWaitEnterPlaySeconds)
                {
                    FailAndExit(
                        "Batch screenshot aborted: Play Mode did not start (batch mode without license, domain reload stuck, or Play Mode blocked).");
                }

                return;
            }

            _warmupFrames++;
            if (_warmupFrames < PostPlayWarmupFrames)
            {
                return;
            }

            Directory.CreateDirectory(OutputFolder);
            string fileName = $"batch_gamescene_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string assetPath = $"{OutputFolder}/{fileName}";
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string fullPath = Path.Combine(projectRoot, assetPath);

            Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
            byte[] pngData = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);

            File.WriteAllBytes(fullPath, pngData);
            AssetDatabase.Refresh();
            Debug.Log($"Batch screenshot saved: {assetPath}");

            EditorApplication.isPlaying = false;
            _exitPlayRequestedTime = EditorApplication.timeSinceStartup;
            _stage = 3;
            return;
        }

        if (_stage == 3)
        {
            if (EditorApplication.isPlaying)
            {
                if (EditorApplication.timeSinceStartup - _exitPlayRequestedTime > MaxWaitExitPlaySeconds)
                {
                    FailAndExit("Batch screenshot aborted: Play Mode did not stop.");
                }

                return;
            }

            EditorApplication.update -= OnUpdate;
            EditorApplication.Exit(0);
        }
    }
}
