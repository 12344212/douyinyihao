using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 将 PurchaseLoading 移到 CanvasMax（与 TopBar 同画布），补 CanvasGroup，重绑 TopUpPanelController.loading。
/// One-shot: TopUpIapFlowSetup.run
/// </summary>
[InitializeOnLoad]
public static class TopUpIapFlowSetup
{
    private const string LogTag = "[TopUpIapFlowSetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private static string MarkerPath => Path.Combine(Directory.GetCurrentDirectory(), "TopUpIapFlowSetup.run");

    static TopUpIapFlowSetup()
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

    [MenuItem("Tools/Template/TopUp IAP Flow Setup")]
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
                    File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "TopUpIapFlowSetup.ok"),
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "TopUpIapFlowSetup.err"), e.ToString()); } catch { }
        }
    }

    private static string Apply(Scene scene)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        Transform canvasMax = FindDeep(scene, "CanvasMax");
        Transform purchaseLoading = FindDeep(scene, "PurchaseLoading");
        TopUpPanelController topUp = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            topUp = roots[i].GetComponentInChildren<TopUpPanelController>(true);
            if (topUp != null) break;
        }

        if (canvasMax == null)
        {
            return "CanvasMax missing";
        }

        if (purchaseLoading == null)
        {
            return "PurchaseLoading missing";
        }

        RectTransform loadRt = purchaseLoading as RectTransform;
        if (loadRt == null)
        {
            loadRt = purchaseLoading.GetComponent<RectTransform>();
        }

        if (purchaseLoading.parent != canvasMax)
        {
            Undo.SetTransformParent(purchaseLoading, canvasMax, "Move PurchaseLoading to CanvasMax");
            sb.Append("reparented; ");
        }
        else
        {
            sb.Append("already under CanvasMax; ");
        }

        if (loadRt != null)
        {
            loadRt.anchorMin = Vector2.zero;
            loadRt.anchorMax = Vector2.one;
            loadRt.offsetMin = Vector2.zero;
            loadRt.offsetMax = Vector2.zero;
            loadRt.localScale = Vector3.one;
            loadRt.localRotation = Quaternion.identity;
            loadRt.SetAsLastSibling();
            sb.Append("stretch+lastSibling; ");
        }

        CanvasGroup cg = purchaseLoading.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = Undo.AddComponent<CanvasGroup>(purchaseLoading.gameObject);
            sb.Append("added CanvasGroup; ");
        }

        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        PurchaseLoadingUI loadingUi = purchaseLoading.GetComponent<PurchaseLoadingUI>();
        if (loadingUi != null)
        {
            SerializedObject soLoad = new SerializedObject(loadingUi);
            SerializedProperty spin = soLoad.FindProperty("spinTarget");
            SerializedProperty cgProp = soLoad.FindProperty("canvasGroup");
            if (spin != null && spin.objectReferenceValue == null)
            {
                Transform spinner = purchaseLoading.Find("Spinner");
                if (spinner != null)
                {
                    spin.objectReferenceValue = spinner.GetComponent<RectTransform>();
                    sb.Append("spinTarget; ");
                }
            }

            if (cgProp != null)
            {
                cgProp.objectReferenceValue = cg;
                sb.Append("canvasGroup ref; ");
            }

            soLoad.ApplyModifiedPropertiesWithoutUndo();
        }

        // 保持 active，靠 CanvasGroup 隐藏，便于协程常驻
        if (!purchaseLoading.gameObject.activeSelf)
        {
            purchaseLoading.gameObject.SetActive(true);
            sb.Append("activated; ");
        }

        if (topUp != null && loadingUi != null)
        {
            SerializedObject so = new SerializedObject(topUp);
            SerializedProperty loadingProp = so.FindProperty("loading");
            if (loadingProp != null)
            {
                loadingProp.objectReferenceValue = loadingUi;
                so.ApplyModifiedPropertiesWithoutUndo();
                sb.Append("wired TopUp.loading; ");
            }

            // 旧字段 purchaseTip 若仍在场景里会被 Unity 丢弃；无需处理
        }
        else
        {
            sb.Append("TopUpPanelController or loadingUi missing; ");
        }

        EditorUtility.SetDirty(purchaseLoading.gameObject);
        if (topUp != null) EditorUtility.SetDirty(topUp);
        return sb.ToString().Trim();
    }

    private static Transform FindDeep(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform t = FindDeep(roots[i].transform, name);
            if (t != null) return t;
        }

        return null;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform t = FindDeep(root.GetChild(i), name);
            if (t != null) return t;
        }

        return null;
    }
}