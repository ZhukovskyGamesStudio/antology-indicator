using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class GameViewScreenshotMenu
{
    private const string OutputFolder = "Assets/Screenshots";

    [MenuItem("Tools/Screenshots/Capture Game View", false, 0)]
    private static void CaptureGameView()
    {
        Directory.CreateDirectory(OutputFolder);
        string fileName = $"game_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        string assetPath = Path.Combine(OutputFolder, fileName).Replace("\\", "/");
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string fullPath = Path.Combine(projectRoot, OutputFolder, fileName);

        Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
        byte[] pngData = texture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(texture);

        File.WriteAllBytes(fullPath, pngData);
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath));
        Debug.Log($"Screenshot saved: {assetPath}");
    }

    [MenuItem("Tools/Screenshots/Capture Game View", true)]
    private static bool CaptureGameViewValidate()
    {
        return EditorApplication.isPlaying;
    }
}
