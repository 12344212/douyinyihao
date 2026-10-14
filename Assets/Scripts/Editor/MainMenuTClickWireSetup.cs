using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Wire MainScene main-menu T hit areas (except Stage).
/// Collection / Shop / Bag / Upgrade / TopUp:
/// - child T gets Button + Image alpha 0 (raycastable)
/// - host + sibling Graphics lose raycast so only T receives clicks
/// MainUIController binds T at runtime and plays click scale punch on parent.
/// Menu: Tools/Template/Wire Main Menu T Click
/// One-shot: MainMenuTClickWireSetup.run
/// </summary>
[InitializeOnLoad]
public static class MainMenuTClickWireSetup
{
    private const string LogTag = "[MainMenuTClickWireSetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string TouchChildName = "T";

    private static readonly string[] MenuHostNames =
    {
        "Collection",
        "Shop",
        "Bag",
        "Upgrade",
        "TopUp",
    };

    private static string MarkerPath => Path.Combine(Directory.GetCurrentDirectory(), "MainMenuTClickWireSetup.run");
    private static string OkPath => Path.Combine(Directory.GetCurrentDirectory(), "MainMenuTClickWireSetup.ok");
    private static string ErrPath => Path.Combine(Directory.GetCurrentDirectory(), "MainMenuTClickWireSetup.err");

    static MainMenuTClickWireSetup()
    {
        if (!File.Exists(MarkerPath))
        {
            return;
        }

        EditorApplication.delayCall += TryAutoRun;
    }

    private static void TryAutoRun()
    {
        if (!File.Exists(MarkerPath))
        {
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoRun;
            return;
        }

        try
        {
            File.Delete(MarkerPath);
        }
        catch (Exception e)
        {
            Debug.LogError(LogTag + " marker delete failed: " + e.Message);
            return;
        }

        Debug.Log(LogTag + " Auto-run.");
        Run();
    }

    [MenuItem("Tools/Template/Wire Main Menu T Click")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Run();
    }

    public static void Run()
    {
        try
        {
            string report = WireMainScene();
            Debug.Log(LogTag + " Done. " + report);
            File.WriteAllText(OkPath, DateTime.Now.ToString("o") + " " + report + "\n");
            if (File.Exists(ErrPath))
            {
                File.Delete(ErrPath);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(LogTag + " " + e);
            try
            {
                File.WriteAllText(ErrPath, e.ToString());
            }
            catch
            {
            }
        }
    }

    private static string WireMainScene()
    {
        Scene scene = SceneManager.GetSceneByPath(MainScenePath);
        bool openedHere = false;
        if (!(scene.IsValid() && scene.isLoaded))
        {
            scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }

        Scene previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);
        try
        {
            Transform canvas = FindRoot(scene, "Canvas");
            if (canvas == null)
            {
                throw new Exception("Canvas root not found in MainScene.");
            }

            StringBuilder sb = new StringBuilder();
            int ok = 0;
            for (int i = 0; i < MenuHostNames.Length; i++)
            {
                string hostName = MenuHostNames[i];
                Transform hostTf = canvas.Find(hostName);
                if (hostTf == null)
                {
                    sb.Append(hostName).Append("=MISSING; ");
                    continue;
                }

                Button hostBtn = hostTf.GetComponent<Button>();
                if (hostBtn == null)
                {
                    sb.Append(hostName).Append("=NO_BUTTON; ");
                    continue;
                }

                Transform touch = hostTf.Find(TouchChildName);
                if (touch == null)
                {
                    sb.Append(hostName).Append("=NO_T; ");
                    continue;
                }

                Undo.RegisterFullObjectHierarchyUndo(hostTf.gameObject, "Wire Main Menu T Click");
                MainUIController.ConfigureTouchChild(touch);
                MainUIController.DisableHostRaycast(hostBtn);
                EditorUtility.SetDirty(hostTf.gameObject);
                EditorUtility.SetDirty(touch.gameObject);
                ok++;
                sb.Append(hostName).Append("=OK; ");
            }

            Transform stage = canvas.Find("Stage");
            sb.Append("Stage=").Append(stage != null ? "SKIP" : "MISSING");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new Exception("Failed to save MainScene.");
            }

            return "wired=" + ok + "/" + MenuHostNames.Length + " " + sb;
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded && previous != scene)
            {
                SceneManager.SetActiveScene(previous);
            }

            if (openedHere && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static Transform FindRoot(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].name == name)
            {
                return roots[i].transform;
            }
        }

        return null;
    }
}