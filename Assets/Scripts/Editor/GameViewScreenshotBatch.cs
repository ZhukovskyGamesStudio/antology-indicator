using System;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GameViewScreenshotBatch
{
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";
    private const string OutputFolder = "Assets/Screenshots";
    private const int PostPlayWarmupFrames = 120;
    private const double MaxTotalSeconds = 1200;
    private const double MaxWaitEnterPlaySeconds = 120;

    private static double _batchStartTime;
    private static double _playRequestedTime;
    private static int _warmupFrames;
    private static bool _captureWritten;
    private static bool _scheduledShutdown;
    private static bool _pendingEnterPlayAfterCompile;

    public static void CaptureGameSceneScreenshotAndQuit()
    {
        Debug.Log("GameViewScreenshotBatch: CaptureGameSceneScreenshotAndQuit invoked.");
        EditorApplication.update -= WatchdogUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeChangedAfterCapture;

        _batchStartTime = EditorApplication.timeSinceStartup;
        _playRequestedTime = 0;
        _warmupFrames = 0;
        _captureWritten = false;
        _scheduledShutdown = false;
        _pendingEnterPlayAfterCompile = false;

        EditorApplication.update += WatchdogUpdate;
        EditorApplication.delayCall += OpenSceneDelayed;
    }

    private static void PollUntilCompilationAllowsEnterPlay()
    {
        if (!_pendingEnterPlayAfterCompile)
        {
            EditorApplication.update -= PollUntilCompilationAllowsEnterPlay;
            return;
        }

        if (EditorApplication.isCompiling)
        {
            return;
        }

        EditorApplication.update -= PollUntilCompilationAllowsEnterPlay;
        EditorApplication.delayCall += TryScheduleEnterPlayAfterCompile;
    }

    private static void WatchdogUpdate()
    {
        if (EditorApplication.timeSinceStartup - _batchStartTime > MaxTotalSeconds)
        {
            FailAndExit("Batch screenshot aborted: total time limit exceeded.");
            return;
        }

        if (_playRequestedTime > 0
            && !_captureWritten
            && !EditorApplication.isPlaying
            && EditorApplication.timeSinceStartup - _playRequestedTime > MaxWaitEnterPlaySeconds)
        {
            FailAndExit(
                "Batch screenshot aborted: Play Mode did not start (batch/nographics may block play, or scene errors).");
        }
    }

    private static void FailAndExit(string message)
    {
        Debug.LogError(message);
        EditorApplication.update -= WatchdogUpdate;
        EditorApplication.update -= WarmupAfterPlay;
        EditorApplication.update -= PollUntilCompilationAllowsEnterPlay;
        EditorApplication.delayCall -= OpenSceneDelayed;
        EditorApplication.delayCall -= QuitAfterPlayStopped;
        EditorApplication.delayCall -= TryScheduleEnterPlayAfterCompile;
        CompilationPipeline.compilationFinished -= OnCompilationFinishedForEnterPlay;
        EditorApplication.playModeStateChanged -= OnEnteredPlayModeOnce;
        EditorApplication.playModeStateChanged -= OnPlayModeChangedAfterCapture;
        _pendingEnterPlayAfterCompile = false;
        EditorApplication.Exit(1);
    }

    private static void OpenSceneDelayed()
    {
        Debug.Log("GameViewScreenshotBatch: OpenSceneDelayed.");
        EditorSceneManager.OpenScene(GameScenePath);
        _pendingEnterPlayAfterCompile = true;
        CompilationPipeline.compilationFinished += OnCompilationFinishedForEnterPlay;
        EditorApplication.delayCall += TryScheduleEnterPlayAfterCompile;
        EditorApplication.update += PollUntilCompilationAllowsEnterPlay;
    }

    private static void OnCompilationFinishedForEnterPlay(object obj)
    {
        EditorApplication.delayCall += TryScheduleEnterPlayAfterCompile;
    }

    private static void TryScheduleEnterPlayAfterCompile()
    {
        if (!_pendingEnterPlayAfterCompile)
        {
            return;
        }

        if (EditorApplication.isCompiling)
        {
            return;
        }

        _pendingEnterPlayAfterCompile = false;
        CompilationPipeline.compilationFinished -= OnCompilationFinishedForEnterPlay;

        EditorApplication.playModeStateChanged += OnEnteredPlayModeOnce;
        _playRequestedTime = EditorApplication.timeSinceStartup;
        Debug.Log("GameViewScreenshotBatch: requesting Play Mode.");
        EditorApplication.isPlaying = true;
    }

    private static void OnEnteredPlayModeOnce(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode)
        {
            return;
        }

        EditorApplication.playModeStateChanged -= OnEnteredPlayModeOnce;
        _warmupFrames = 0;
        EditorApplication.update += WarmupAfterPlay;
    }

    private static void WarmupAfterPlay()
    {
        if (!EditorApplication.isPlaying)
        {
            return;
        }

        _warmupFrames++;
        if (_warmupFrames < PostPlayWarmupFrames)
        {
            return;
        }

        EditorApplication.update -= WarmupAfterPlay;
        WriteScreenshotAndStopPlay();
    }

    private static void WriteScreenshotAndStopPlay()
    {
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

        _captureWritten = true;

        EditorApplication.playModeStateChanged += OnPlayModeChangedAfterCapture;
        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += QuitAfterPlayStopped;
    }

    private static void QuitAfterPlayStopped()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.delayCall += QuitAfterPlayStopped;
            return;
        }

        if (!_scheduledShutdown)
        {
            _scheduledShutdown = true;
            EditorApplication.playModeStateChanged -= OnPlayModeChangedAfterCapture;
            EditorApplication.update -= WatchdogUpdate;
            EditorApplication.Exit(0);
        }
    }

    private static void OnPlayModeChangedAfterCapture(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode || !_captureWritten || _scheduledShutdown)
        {
            return;
        }

        _scheduledShutdown = true;
        EditorApplication.playModeStateChanged -= OnPlayModeChangedAfterCapture;
        EditorApplication.update -= WatchdogUpdate;
        EditorApplication.Exit(0);
    }
}
