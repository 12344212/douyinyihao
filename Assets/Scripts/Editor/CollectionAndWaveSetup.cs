using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 1) 把 MainUIController.collectionPanel 指到 CollectionPanel，Collection 按钮走 OpenCollection。
/// 2) Lang 挂 LangWaveMotion；Stage 作为 follower 跟着浪漂（较小幅度）。停用 BeachWaveBob。
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

        // 木筏改跟浪：清空 / 禁用 BeachWaveBob，避免和 LangWaveMotion 抢 Stage
        if (bob != null)
        {
            SerializedObject bobSo = new SerializedObject(bob);
            SerializedProperty targets = bobSo.FindProperty("targets");
            if (targets != null)
            {
                targets.arraySize = 0;
            }

            bobSo.ApplyModifiedPropertiesWithoutUndo();
            bob.enabled = false;
        }

        EnsureLangWave(langRt, stageRt);
        EditorSceneManager.MarkSceneDirty(mainCanvas.gameObject.scene);
    }

    private static void EnsureLangWave(RectTransform langRt, RectTransform stageRt)
    {
        LangWaveMotion wave = langRt.GetComponent<LangWaveMotion>();
        if (wave == null)
        {
            wave = langRt.gameObject.AddComponent<LangWaveMotion>();
        }

        Vector3 e = langRt.localEulerAngles;
        langRt.localEulerAngles = new Vector3(e.x, e.y, 0f);
        if (stageRt != null)
        {
            Vector3 se = stageRt.localEulerAngles;
            stageRt.localEulerAngles = new Vector3(se.x, se.y, 0f);
        }

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

        SerializedProperty followers = so.FindProperty("followers");
        followers.arraySize = stageRt != null ? 1 : 0;
        if (stageRt != null)
        {
            SerializedProperty f0 = followers.GetArrayElementAtIndex(0);
            f0.FindPropertyRelative("rect").objectReferenceValue = stageRt;
            f0.FindPropertyRelative("followScale").floatValue = 0.4f;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

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
                    Debug.Log($"{LogTag} CollectionPanel wired; LangWaveMotion + Stage follower.");
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
                Debug.Log($"{LogTag} LangWaveMotion + Stage follower rebuilt.");
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
