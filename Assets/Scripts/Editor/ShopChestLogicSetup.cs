using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 给已有 ShopPanel 的 Chest1/2/3 加 Button，并写进 ShopPanelController。
/// 菜单：Tools/Template/Bind Shop Chest Logic
/// 一键：ShopChestLogicSetup.run
/// </summary>
[InitializeOnLoad]
public static class ShopChestLogicSetup
{
    private const string LogTag = "[ShopChestLogicSetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private static string MarkerPath => Path.Combine(Directory.GetCurrentDirectory(), "ShopChestLogicSetup.run");

    static ShopChestLogicSetup()
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

        try { File.Delete(MarkerPath); }
        catch (Exception e)
        {
            Debug.LogError(LogTag + " marker delete failed: " + e.Message);
            return;
        }

        Debug.Log(LogTag + " Auto-run.");
        Run();
    }

    [MenuItem("Tools/Template/Bind Shop Chest Logic")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

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

            UIPanel panel = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name != "CanvasPanel") continue;
                Transform t = roots[i].transform.Find("ShopPanel");
                if (t != null) { panel = t.GetComponent<UIPanel>(); break; }
            }

            if (panel == null || panel.Content == null)
            {
                Debug.LogError(LogTag + " ShopPanel/Content missing.");
                if (openedHere && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                Wire(panel);
                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene))
                {
                    Debug.Log(LogTag + " Chests wired and saved.");
                    File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "ShopChestLogicSetup.ok"),
                        DateTime.Now.ToString("o"));
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "ShopChestLogicSetup.err"), e.ToString()); } catch { }
        }
    }

    private static void Wire(UIPanel panel)
    {
        Transform c = panel.Content;
        Image i1 = FindImage(c, "Chest1");
        Image i2 = FindImage(c, "Chest2");
        Image i3 = FindImage(c, "Chest3");
        if (i1 == null || i2 == null || i3 == null)
        {
            throw new Exception("Chest1/2/3 images not found.");
        }

        Button b1 = EnsureButton(i1);
        Button b2 = EnsureButton(i2);
        Button b3 = EnsureButton(i3);

        ShopPanelController ctrl = panel.GetComponent<ShopPanelController>();
        if (ctrl == null) ctrl = panel.gameObject.AddComponent<ShopPanelController>();

        SerializedObject so = new SerializedObject(ctrl);
        SerializedProperty btns = so.FindProperty("chestButtons");
        SerializedProperty imgs = so.FindProperty("chestImages");
        if (btns == null || imgs == null)
        {
            throw new Exception("chestButtons/chestImages fields missing on ShopPanelController.");
        }

        btns.arraySize = 3;
        btns.GetArrayElementAtIndex(0).objectReferenceValue = b1;
        btns.GetArrayElementAtIndex(1).objectReferenceValue = b2;
        btns.GetArrayElementAtIndex(2).objectReferenceValue = b3;
        imgs.arraySize = 3;
        imgs.GetArrayElementAtIndex(0).objectReferenceValue = i1;
        imgs.GetArrayElementAtIndex(1).objectReferenceValue = i2;
        imgs.GetArrayElementAtIndex(2).objectReferenceValue = i3;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Image FindImage(Transform root, string name)
    {
        Transform t = FindDeep(root, name);
        return t != null ? t.GetComponent<Image>() : null;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform d = FindDeep(root.GetChild(i), name);
            if (d != null) return d;
        }
        return null;
    }

    private static Button EnsureButton(Image img)
    {
        Button btn = img.GetComponent<Button>();
        if (btn == null) btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;
        img.raycastTarget = true;
        return btn;
    }
}