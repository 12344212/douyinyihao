using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Wire TopUp listRoot / TopPrefab buyButton / mr daily claim; update GemPackConfig icons.
/// Menu: Tools/Template/Wire TopUp Packs And Daily Mr
/// One-shot: TopUpWireSetup.run
/// </summary>
[InitializeOnLoad]
public static class TopUpWireSetup
{
    private const string LogTag = "[TopUpWireSetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string TopPrefabPath = "Assets/Prefab/UI/TopPrefab.prefab";
    private const string GemPackConfigPath = "Assets/Config/GemPackConfig.asset";
    private static string MarkerPath => Path.Combine(Directory.GetCurrentDirectory(), "TopUpWireSetup.run");

    static TopUpWireSetup()
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

    [MenuItem("Tools/Template/Wire TopUp Packs And Daily Mr")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run();
    }

    private static void Run()
    {
        try
        {
            string report = FixGemPackConfig() + " | " + FixTopPrefab() + " | " + FixScene();
            Debug.Log(LogTag + " Done. " + report);
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "TopUpWireSetup.ok"),
                DateTime.Now.ToString("o") + " " + report);
        }
        catch (Exception e)
        {
            Debug.LogError(LogTag + " " + e);
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "TopUpWireSetup.err"), e.ToString()); } catch { }
        }
    }

    private static string FixGemPackConfig()
    {
        GemPackConfig cfg = AssetDatabase.LoadAssetAtPath<GemPackConfig>(GemPackConfigPath);
        if (cfg == null) return "GemPackConfig missing";

        SerializedObject so = new SerializedObject(cfg);
        SerializedProperty packs = so.FindProperty("packs");
        int n = packs != null ? packs.arraySize : 0;
        for (int i = 0; i < n; i++)
        {
            SerializedProperty entry = packs.GetArrayElementAtIndex(i);
            SerializedProperty icon = entry.FindPropertyRelative("iconLocation");
            if (icon != null)
            {
                icon.stringValue = "hb (" + (i + 1) + ")";
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        return "icons hb (1..6)";
    }

    private static string FixTopPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(TopPrefabPath);
        if (root == null) return "TopPrefab missing";

        try
        {
            TopUpItemSlot slot = root.GetComponent<TopUpItemSlot>();
            if (slot == null) return "TopUpItemSlot missing";

            Image bg = root.GetComponent<Image>();
            if (bg != null) bg.raycastTarget = true;

            Button buy = root.GetComponent<Button>();
            if (buy == null)
            {
                buy = root.AddComponent<Button>();
            }

            if (bg != null) buy.targetGraphic = bg;
            buy.transition = Selectable.Transition.None;

            // Prefer PriceBg as click target if present
            Transform priceBg = root.transform.Find("PriceBg");
            if (priceBg != null)
            {
                Image priceImg = priceBg.GetComponent<Image>();
                Button priceBtn = priceBg.GetComponent<Button>();
                if (priceBtn == null) priceBtn = priceBg.gameObject.AddComponent<Button>();
                if (priceImg != null)
                {
                    priceImg.raycastTarget = true;
                    priceBtn.targetGraphic = priceImg;
                }

                priceBtn.transition = Selectable.Transition.None;
                buy = priceBtn;
            }

            SerializedObject so = new SerializedObject(slot);
            SerializedProperty p = so.FindProperty("buyButton");
            if (p != null) p.objectReferenceValue = buy;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, TopPrefabPath);
            return "TopPrefab buyButton wired";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string FixScene()
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
            TopUpPanelController ctrl = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name != "CanvasPanel") continue;
                Transform t = roots[i].transform.Find("TopUpPanel");
                if (t != null)
                {
                    ctrl = t.GetComponent<TopUpPanelController>();
                    break;
                }
            }

            if (ctrl == null) return "TopUpPanelController missing";

            Transform panel = ctrl.transform;
            Transform window = panel.Find("Window");
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            // listRoot = Window/k/Scroll View/Viewport/Content
            Transform content = null;
            if (window != null)
            {
                Transform k = window.Find("k");
                if (k != null)
                {
                    content = k.Find("Scroll View/Viewport/Content");
                }
            }

            if (content == null)
            {
                content = DeepFind(panel, "Content");
            }

            if (content != null)
            {
                // Clear leftover non-pack children (e.g. BarFill)
                for (int i = content.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(content.GetChild(i).gameObject);
                }

                sb.Append("listRoot<-Content; cleared; ");
            }
            else sb.Append("Content missing; ");

            // mr daily
            Transform mr = window != null ? window.Find("mr") : null;
            CanvasGroup mrGroup = null;
            Button mrBtn = null;
            if (mr != null)
            {
                mrGroup = mr.GetComponent<CanvasGroup>();
                if (mrGroup == null) mrGroup = mr.gameObject.AddComponent<CanvasGroup>();

                Transform lq = mr.Find("LQ");
                if (lq != null)
                {
                    mrBtn = lq.GetComponent<Button>();
                    if (mrBtn == null) mrBtn = lq.gameObject.AddComponent<Button>();
                    Image lqImg = lq.GetComponent<Image>();
                    if (lqImg != null)
                    {
                        lqImg.raycastTarget = true;
                        mrBtn.targetGraphic = lqImg;
                    }

                    mrBtn.transition = Selectable.Transition.None;
                    sb.Append("mr LQ button; ");
                }
                else
                {
                    mrBtn = mr.GetComponent<Button>();
                    if (mrBtn == null) mrBtn = mr.gameObject.AddComponent<Button>();
                    Image mrImg = mr.GetComponent<Image>();
                    if (mrImg != null) mrBtn.targetGraphic = mrImg;
                    mrBtn.transition = Selectable.Transition.None;
                    sb.Append("mr root button; ");
                }
            }
            else sb.Append("mr missing; ");

            SerializedObject so = new SerializedObject(ctrl);
            if (content != null) SetRef(so, "listRoot", content);
            if (mrGroup != null) SetRef(so, "mrRoot", mrGroup);
            if (mrBtn != null) SetRef(so, "mrClaimButton", mrBtn);

            // Keep existing topPrefab / loading / gemPackConfig if set
            TopUpItemSlot prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TopPrefabPath).GetComponent<TopUpItemSlot>();
            if (prefab != null) SetRef(so, "topPrefab", prefab);

            GemPackConfig cfg = AssetDatabase.LoadAssetAtPath<GemPackConfig>(GemPackConfigPath);
            if (cfg != null) SetRef(so, "gemPackConfig", cfg);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ctrl);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            sb.Append("controller wired");
            return sb.ToString();
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (openedHere && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static Transform DeepFind(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform f = DeepFind(root.GetChild(i), name);
            if (f != null) return f;
        }

        return null;
    }

    private static void SetRef(SerializedObject so, string prop, UnityEngine.Object value)
    {
        SerializedProperty p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
    }
}
// force-refresh
