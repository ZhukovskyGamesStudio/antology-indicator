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

    private static int _stage;
    private static int _sceneSettleFrames;
    private static int _warmupFrames;

    public static void CaptureGameSceneScreenshotAndQuit()
    {
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
        _stage = 0;
        _sceneSettleFrames = 0;
        _warmupFrames = 0;
    }

    private static void OnUpdate()
    {
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
            _stage = 2;
            return;
        }

        if (_stage == 2)
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
            _stage = 3;
            return;
        }

        if (_stage == 3)
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }

            EditorApplication.update -= OnUpdate;
            EditorApplication.Exit(0);
        }
    }
}
