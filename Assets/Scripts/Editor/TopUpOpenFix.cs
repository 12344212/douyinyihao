using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Wire main Canvas TopUp button to OpenTopUp; wire TopUpPanel Back as closeButton.
/// One-shot: TopUpOpenFix.run
/// </summary>
[InitializeOnLoad]
public static class TopUpOpenFix
{
    private const string LogTag = "[TopUpOpenFix]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private static string MarkerPath => Path.Combine(Directory.GetCurrentDirectory(), "TopUpOpenFix.run");

    static TopUpOpenFix()
    {
        if (!File.Exists(MarkerPath)) return;
        EditorApplication.delayCall += TryAutoRun;
    }

    private static void TryAutoRun()
    {
        if (!File.Exists(MarkerPath)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoRun;
            return;
        }

        try { File.Delete(MarkerPath); }
        catch (Exception e)
        {
            Debug.LogError(LogTag + " marker delete failed: " + e.Message);
            return;
        }

        Debug.Log(LogTag + " Auto-run.");
        Run();
    }

    [MenuItem("Tools/Template/Fix TopUp Open Button")]
    public static void Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run();
    }

    private static void Run()
    {
        try
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
                string report = Apply(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene))
                {
                    Debug.Log(LogTag + " Done. " + report);
                    File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "TopUpOpenFix.ok"),
                        DateTime.Now.ToString("o") + " " + report);
                }
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (openedHere && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(LogTag + " " + e);
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "TopUpOpenFix.err"), e.ToString()); } catch { }
        }
    }

    private static string Apply(Scene scene)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        MainUIController main = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            main = roots[i].GetComponentInChildren<MainUIController>(true);
            if (main != null) break;
        }

        if (main == null) return "MainUIController missing";

        // Find Canvas/TopUp button
        Button topUpBtn = null;
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name != "Canvas") continue;
            Transform t = roots[i].transform.Find("TopUp");
            if (t != null) topUpBtn = t.GetComponent<Button>();
            break;
        }

        if (topUpBtn == null)
        {
            // deep find
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < all.Length; j++)
                {
                    if (all[j].name == "TopUp")
                    {
                        topUpBtn = all[j].GetComponent<Button>();
                        if (topUpBtn != null) break;
                    }
                }

                if (topUpBtn != null) break;
            }
        }

        SerializedObject so = new SerializedObject(main);
        if (topUpBtn != null)
        {
            SerializedProperty p = so.FindProperty("topUpButton");
            if (p != null)
            {
                p.objectReferenceValue = topUpBtn;
                sb.Append("topUpButton<-TopUp; ");
            }
            else sb.Append("topUpButton prop missing; ");
        }
        else sb.Append("TopUp Button missing; ");

        // TopUpPanel closeButton <- Back
        UIPanel topUpPanel = so.FindProperty("topUpPanel") != null
            ? so.FindProperty("topUpPanel").objectReferenceValue as UIPanel
            : null;
        if (topUpPanel != null)
        {
            Transform back = topUpPanel.transform.Find("Window/Back");
            if (back == null)
            {
                Transform[] all = topUpPanel.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name == "Back") { back = all[i]; break; }
                }
            }

            if (back != null)
            {
                Button backBtn = back.GetComponent<Button>();
                if (backBtn == null)
                {
                    backBtn = back.gameObject.AddComponent<Button>();
                    Image img = back.GetComponent<Image>();
                    if (img != null)
                    {
                        img.raycastTarget = true;
                        backBtn.targetGraphic = img;
                    }

                    backBtn.transition = Selectable.Transition.None;
                    sb.Append("added Back Button; ");
                }

                SerializedObject panelSo = new SerializedObject(topUpPanel);
                SerializedProperty close = panelSo.FindProperty("closeButton");
                if (close != null)
                {
                    close.objectReferenceValue = backBtn;
                    panelSo.ApplyModifiedPropertiesWithoutUndo();
                    sb.Append("closeButton<-Back; ");
                }
            }
            else sb.Append("Back missing; ");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(main);
        return sb.ToString().Trim();
    }
}