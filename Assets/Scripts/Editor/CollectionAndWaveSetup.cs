using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 1) 把 MainUIController.collectionPanel 指到 CollectionPanel，Collection 按钮走 OpenCollection。
/// 2) BeachWaveBob 只绑 Stage；Lang 单独挂 LangWaveMotion（扑过来再慢退）。
/// 菜单：Tools/Template/Build Collection And Beach Wave
/// 自动执行：项目根目录存在 CollectionAndWaveSetup.run 时执行一次。
/// </summary>
[InitializeOnLoad]
public static class CollectionAndWaveSetup
{
    private const string LogTag = "[CollectionAndWaveSetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string BeachWaveHostName = "BeachWaveBob";

    private static string MarkerFilePath => Path.Combine(Directory.GetCurrentDirectory(), "CollectionAndWaveSetup.run");

    static CollectionAndWaveSetup()
    {
        if (!File.Exists(MarkerFilePath))
        {
            return;
        }

        EditorApplication.delayCall += TryAutoRun;
    }

    private static void TryAutoRun()
    {
        if (!File.Exists(MarkerFilePath))
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
            File.Delete(MarkerFilePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"{LogTag} Failed to delete marker file, auto-run aborted: {e.Message}");
            return;
        }

        Debug.Log($"{LogTag} Auto-run triggered by marker file.");
        Run(false);
    }

    [MenuItem("Tools/Template/Build Collection And Beach Wave")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Run(true);
    }

    [MenuItem("Tools/Template/Build Beach Wave Bob")]
    public static void BuildBeachWaveMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        RunBeachWaveOnly(true);
    }

    public static void WireCollection(MainUIController ctrl, UIPanel collectionPanel)
    {
        if (ctrl == null)
        {
            throw new Exception("MainUIController is missing.");
        }

        SerializedObject so = new SerializedObject(ctrl);
        SetRef(so, "collectionPanel", collectionPanel);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
    }

    public static void BuildBeachWave(Canvas mainCanvas)
    {
        if (mainCanvas == null)
        {
            throw new Exception("Main Canvas is missing.");
        }

        RectTransform stageRt = FindNamedRect(mainCanvas.transform, "Stage");
        RectTransform langRt = FindNamedRect(mainCanvas.transform, "Lang");
        if (stageRt == null)
        {
            throw new Exception("Stage button not found under main Canvas.");
        }

        if (langRt == null)
        {
            throw new Exception("Lang image not found under main Canvas.");
        }

        BeachWaveBob bob = mainCanvas.GetComponentInChildren<BeachWaveBob>(true);
        GameObject host;
        if (bob != null)
        {
            host = bob.gameObject;
        }
        else
        {
            Transform existing = mainCanvas.transform.Find(BeachWaveHostName);
            if (existing != null)
            {
                host = existing.gameObject;
            }
            else
            {
                host = new GameObject(BeachWaveHostName, typeof(RectTransform));
                host.layer = LayerMask.NameToLayer("UI");
                host.transform.SetParent(mainCanvas.transform, false);
                RectTransform rt = host.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
            }

            bob = host.GetComponent<BeachWaveBob>();
            if (bob == null)
            {
                bob = host.AddComponent<BeachWaveBob>();
            }
        }

        // Stage 仍用 BeachWaveBob（X 轻晃 + 倾角）；Lang 从池里拿掉
        SerializedObject so = new SerializedObject(bob);
        SerializedProperty targets = so.FindProperty("targets");
        targets.arraySize = 1;
        SerializedProperty t0 = targets.GetArrayElementAtIndex(0);
        t0.FindPropertyRelative("rect").objectReferenceValue = stageRt;
        t0.FindPropertyRelative("phaseOffset").floatValue = 0f;
        t0.FindPropertyRelative("amplitudeScale").floatValue = 0.7f;
        so.FindProperty("bobAmplitude").floatValue = 10f;
        so.FindProperty("bobPeriod").floatValue = 2.4f;
        so.FindProperty("tiltAngle").floatValue = 1.2f;
        so.FindProperty("tiltPeriod").floatValue = 2.8f;
        so.ApplyModifiedPropertiesWithoutUndo();

        EnsureLangWave(langRt);
        EditorSceneManager.MarkSceneDirty(mainCanvas.gameObject.scene);
    }

    private static void EnsureLangWave(RectTransform langRt)
    {
        LangWaveMotion wave = langRt.GetComponent<LangWaveMotion>();
        if (wave == null)
        {
            wave = langRt.gameObject.AddComponent<LangWaveMotion>();
        }

        // 清掉可能残留的倾角
        Vector3 e = langRt.localEulerAngles;
        langRt.localEulerAngles = new Vector3(e.x, e.y, 0f);

        SerializedObject so = new SerializedObject(wave);
        SetRef(so, "target", langRt);
        so.FindProperty("xMin").floatValue = 725f;
        so.FindProperty("xMax").floatValue = 875f;
        so.FindProperty("restAtMax").boolValue = true;
        so.FindProperty("surgeDurationRange").vector2Value = new Vector2(0.6f, 1.2f);
        so.FindProperty("retreatDurationRange").vector2Value = new Vector2(1.5f, 3.0f);
        SerializedProperty surgeEase = so.FindProperty("surgeEase");
        if (surgeEase != null)
        {
            surgeEase.intValue = (int)DG.Tweening.Ease.OutCubic;
        }

        SerializedProperty retreatEase = so.FindProperty("retreatEase");
        if (retreatEase != null)
        {
            retreatEase.intValue = (int)DG.Tweening.Ease.InOutSine;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        // 把 Lang 的初始 X 放到休息端，避免超出区间
        Vector2 ap = langRt.anchoredPosition;
        ap.x = 875f;
        langRt.anchoredPosition = ap;
    }

    private static void Run(bool interactive)
    {
        try
        {
            Scene scene = OpenMain(interactive, out bool openedHere);
            if (!scene.IsValid())
            {
                return;
            }

            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                MainUIController ctrl = FindMainUI(scene);
                UIPanel collection = FindCollectionPanel(scene);
                Canvas canvas = FindMainCanvas(scene);
                if (ctrl == null || collection == null || canvas == null)
                {
                    Debug.LogError($"{LogTag} Missing MainUIController / CollectionPanel / Canvas.");
                    return;
                }

                WireCollection(ctrl, collection);
                BuildBeachWave(canvas);
                if (EditorSceneManager.SaveScene(scene))
                {
                    Debug.Log($"{LogTag} CollectionPanel wired; BeachWaveBob=Stage, LangWaveMotion=Lang.");
                }
                else
                {
                    Debug.LogError($"{LogTag} Failed to save MainScene.");
                }
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                }

                CloseIfOpened(scene, openedHere);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{LogTag} {e}");
        }
    }

    private static void RunBeachWaveOnly(bool interactive)
    {
        try
        {
            Scene scene = OpenMain(interactive, out bool openedHere);
            if (!scene.IsValid())
            {
                return;
            }

            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                Canvas canvas = FindMainCanvas(scene);
                BuildBeachWave(canvas);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"{LogTag} BeachWaveBob(Stage) + LangWaveMotion rebuilt.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                }

                CloseIfOpened(scene, openedHere);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{LogTag} {e}");
        }
    }

    private static Scene OpenMain(bool interactive, out bool openedHere)
    {
        openedHere = false;
        if (!File.Exists(MainScenePath))
        {
            Debug.LogError($"{LogTag} {MainScenePath} not found.");
            return default;
        }

        Scene scene = SceneManager.GetSceneByPath(MainScenePath);
        if (!(scene.IsValid() && scene.isLoaded))
        {
            scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }
        else if (scene.isDirty && !interactive)
        {
            Debug.LogWarning($"{LogTag} MainScene has unsaved changes; auto-run will apply wave wiring and save.");
        }

        return scene;
    }

    private static void CloseIfOpened(Scene scene, bool openedHere)
    {
        if (openedHere && scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static MainUIController FindMainUI(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            MainUIController c = roots[i].GetComponentInChildren<MainUIController>(true);
            if (c != null)
            {
                return c;
            }
        }

        return null;
    }

    private static Canvas FindMainCanvas(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == "Canvas")
            {
                return roots[i].GetComponent<Canvas>();
            }
        }

        return null;
    }

    private static UIPanel FindCollectionPanel(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name != "CanvasPanel")
            {
                continue;
            }

            Transform t = roots[i].transform.Find("CollectionPanel");
            if (t != null)
            {
                return t.GetComponent<UIPanel>();
            }
        }

        return null;
    }

    private static RectTransform FindNamedRect(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        Transform direct = root.Find(name);
        if (direct != null)
        {
            return direct as RectTransform;
        }

        // Lang 可能在 bg 下，Stage 在 Canvas 下
        RectTransform[] all = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == name)
            {
                return all[i];
            }
        }

        return null;
    }

    private static void SetRef(SerializedObject so, string field, UnityEngine.Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            throw new Exception("Field not found: " + field);
        }

        p.objectReferenceValue = value;
    }
}
