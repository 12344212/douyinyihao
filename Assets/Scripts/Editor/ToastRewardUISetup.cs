using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 在 MainScene 生成 Toast（TipPrefab 预制体实例，CanvasTip）和奖励弹窗（CanvasReward / GetPanel）。
/// 菜单：Tools/Template/Build Toast And Reward UI
/// 自动执行：项目根目录存在 ToastRewardUISetup.run 时，编译加载后执行一次并删除标记（已存在则跳过）。
/// TemplateSceneSetup 生成 MainScene 时也会调用 Build。
/// Play 模式测试：Tools/Template/Test Toast 、Tools/Template/Test Reward Popup。
/// </summary>
[InitializeOnLoad]
public static class ToastRewardUISetup
{
    private const string LogTag = "[ToastRewardUISetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    public const string TipPrefabPath = "Assets/Prefab/UI/TipPrefab.prefab";

    private const string TipCanvasName = "CanvasTip";
    private const string RewardCanvasName = "CanvasReward";
    private const int TipSortOrder = 25;
    private const int RewardSortOrder = 15;
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    private static readonly Color TipColor = new Color(0.08f, 0.10f, 0.14f, 0.92f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color WindowColor = new Color(0.93f, 0.90f, 0.82f, 1f);
    private static readonly Color HeaderColor = new Color(0.40f, 0.28f, 0.18f, 1f);
    private static readonly Color ClaimColor = new Color(0.20f, 0.62f, 0.36f, 1f);
    private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.65f);
    private static readonly Color IconColor = new Color(0.55f, 0.40f, 0.85f, 1f);

    private static string MarkerFilePath => Path.Combine(Directory.GetCurrentDirectory(), "ToastRewardUISetup.run");

    static ToastRewardUISetup()
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

    [MenuItem("Tools/Template/Build Toast And Reward UI")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Run(true);
    }

    [MenuItem("Tools/Template/Test Toast")]
    public static void TestToast()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ToastRewardUISetup] Test Toast only works in Play mode.");
            EditorUtility.DisplayDialog("Test Toast", "Enter Play mode first (start from LoadScene or MainScene).", "OK");
            return;
        }

        TipPanelUI.ShowTip("Not enough gold.");
    }

    [MenuItem("Tools/Template/Test Reward Popup")]
    public static void TestReward()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ToastRewardUISetup] Test Reward Popup only works in Play mode.");
            EditorUtility.DisplayDialog("Test Reward Popup", "Enter Play mode first (start from LoadScene or MainScene).", "OK");
            return;
        }

        string equipIcon = null;
        GameItemDefs.Def equip = GameItemDefs.Get(GameItemDefs.IronPikeId);
        if (equip != null)
        {
            equipIcon = equip.iconLocation;
        }

        GetPanelController.Show(
            new GetPanelController.Reward { useCurrency = true, currency = CurrencyType.Gold, displayName = "Gold", count = 200 },
            new GetPanelController.Reward { useCurrency = true, currency = CurrencyType.Gems, displayName = "Gems", count = 10 },
            new GetPanelController.Reward { iconLocation = equipIcon, displayName = "Iron Pike", count = 1 });
    }

    /// <summary>生成 Toast 与奖励弹窗。mainCanvas 用来取 UI 相机。</summary>
    public static void Build(Canvas mainCanvas)
    {
        Scene scene = mainCanvas.gameObject.scene;
        RemoveExisting(scene);

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            Debug.LogWarning($"{LogTag} TMP default font not found.");
        }

        BuildTip(scene, font);
        BuildReward(scene, mainCanvas.worldCamera, font);
        Debug.Log($"{LogTag} Toast and reward popup built.");
    }

    private static void Run(bool interactive)
    {
        try
        {
            if (!File.Exists(MainScenePath))
            {
                Debug.LogError($"{LogTag} {MainScenePath} not found.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(MainScenePath);
            bool openedHere = false;
            if (!(scene.IsValid() && scene.isLoaded))
            {
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
                openedHere = true;
            }
            else if (scene.isDirty && !interactive)
            {
                Debug.LogWarning($"{LogTag} MainScene has unsaved changes, auto-run skipped.");
                return;
            }

            if (!interactive && HasExisting(scene))
            {
                Debug.Log($"{LogTag} Toast / reward UI already exists, auto-run skipped.");
                return;
            }

            Canvas main = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == "Canvas")
                {
                    main = roots[i].GetComponent<Canvas>();
                    break;
                }
            }

            if (main == null)
            {
                Debug.LogError($"{LogTag} Main Canvas not found.");
                return;
            }

            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                Build(main);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    Debug.LogError($"{LogTag} Failed to save {MainScenePath}.");
                }
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
        catch (Exception e)
        {
            Debug.LogError($"{LogTag} Failed: {e}");
        }
    }

    private static bool HasExisting(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == TipCanvasName || roots[i].name == RewardCanvasName)
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveExisting(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && (roots[i].name == TipCanvasName || roots[i].name == RewardCanvasName))
            {
                UnityEngine.Object.DestroyImmediate(roots[i]);
            }
        }
    }

    // ---------- Toast ----------

    private static void BuildTip(Scene scene, TMP_FontAsset font)
    {
        GameObject prefab = EnsureTipPrefab(font);
        Canvas canvas = CreateCanvas(scene, TipCanvasName, null, TipSortOrder, overlay: true);
        GameObject tip = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        tip.transform.SetParent(canvas.transform, false);
    }

    private static GameObject EnsureTipPrefab(TMP_FontAsset font)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(TipPrefabPath);
        if (existing != null && existing.GetComponent<TipPanelUI>() != null)
        {
            return existing;
        }

        EnsureFolder("Assets/Prefab/UI");
        GameObject root = new GameObject("TipPrefab", typeof(RectTransform));
        try
        {
            root.layer = LayerMask.NameToLayer("UI");
            RectTransform rt = (RectTransform)root.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -160f);
            rt.sizeDelta = new Vector2(900f, 96f);

            Image bg = root.AddComponent<Image>();
            bg.color = TipColor;
            bg.raycastTarget = true;
            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            TMP_Text text = CreateText("Text", root.transform, "Tip", 36f, Color.white, font);
            Stretch(text.rectTransform, 24f, 8f);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;

            TipPanelUI tip = root.AddComponent<TipPanelUI>();
            SerializedObject so = new SerializedObject(tip);
            SetRef(so, "messageText", text);
            SetRef(so, "canvasGroup", group);
            so.FindProperty("autoHideSeconds").floatValue = 2f;
            so.ApplyModifiedPropertiesWithoutUndo();

            bool ok;
            PrefabUtility.SaveAsPrefabAsset(root, TipPrefabPath, out ok);
            if (!ok)
            {
                throw new Exception("Failed to save " + TipPrefabPath);
            }

            Debug.Log($"{LogTag} Created {TipPrefabPath}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(TipPrefabPath);
    }

    // ---------- Reward popup ----------

    private static void BuildReward(Scene scene, Camera uiCamera, TMP_FontAsset font)
    {
        Canvas canvas = CreateCanvas(scene, RewardCanvasName, uiCamera, RewardSortOrder, overlay: false);
        Transform rootT = canvas.transform;

        GameObject panelGo = CreateUIObject("GetPanel", rootT);
        Stretch((RectTransform)panelGo.transform, 0f, 0f);
        CanvasGroup group = panelGo.AddComponent<CanvasGroup>();
        UIPanel panel = panelGo.AddComponent<UIPanel>();
        GetPanelController ctrl = panelGo.AddComponent<GetPanelController>();

        Image dim = CreateImage("Dim", panelGo.transform, DimColor, true);
        Stretch(dim.rectTransform, 0f, 0f);

        Image window = CreateImage("Window", panelGo.transform, WindowColor, true);
        SetRect(window.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 640f));

        Image header = CreateImage("Header", window.transform, HeaderColor, false);
        RectTransform hrt = header.rectTransform;
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.anchoredPosition = Vector2.zero;
        hrt.sizeDelta = new Vector2(0f, 100f);
        TMP_Text title = CreateText("Title", header.transform, "Rewards", 48f, Color.white, font);
        title.fontStyle = FontStyles.Bold;
        Stretch(title.rectTransform, 0f, 0f);

        Button claim = CreateButton("ClaimButton", window.transform, "Claim", 40f, ClaimColor, font);
        SetRect((RectTransform)claim.transform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(320f, 90f));

        RectTransform rows = (RectTransform)CreateUIObject("Rows", window.transform).transform;
        rows.anchorMin = new Vector2(0f, 0f);
        rows.anchorMax = new Vector2(1f, 1f);
        rows.offsetMin = new Vector2(40f, 140f);
        rows.offsetMax = new Vector2(-40f, -120f);
        HorizontalLayoutGroup layout = rows.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 28f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        RewardRowView template = CreateRowTemplate(panelGo.transform, font);

        SerializedObject panelSo = new SerializedObject(panel);
        SetRef(panelSo, "canvasGroup", group);
        SetRef(panelSo, "window", window.rectTransform);
        SetRef(panelSo, "closeButton", claim);
        SetRef(panelSo, "titleText", title);
        SetRef(panelSo, "content", rows);
        panelSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject ctrlSo = new SerializedObject(ctrl);
        SetRef(ctrlSo, "panel", panel);
        SetRef(ctrlSo, "rowRoot", rows);
        SetRef(ctrlSo, "rowTemplate", template);
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        // 保持激活（不要 SetActive(false)）：隐藏靠 CanvasGroup，Awake 才能登记单例。
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private static RewardRowView CreateRowTemplate(Transform parent, TMP_FontAsset font)
    {
        Image bg = CreateImage("RewardRowTemplate", parent, RowColor, false);
        SetRect(bg.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 300f));

        Image icon = CreateImage("Icon", bg.transform, IconColor, false);
        SetRect(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(140f, 140f));

        TMP_Text name = CreateText("Name", bg.transform, "Item", 30f, new Color(0.2f, 0.16f, 0.1f, 1f), font);
        SetRect(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 78f), new Vector2(220f, 44f));

        TMP_Text count = CreateText("Count", bg.transform, "x1", 36f, new Color(0.15f, 0.15f, 0.15f, 1f), font);
        count.fontStyle = FontStyles.Bold;
        SetRect(count.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(220f, 44f));

        RewardRowView view = bg.gameObject.AddComponent<RewardRowView>();
        SerializedObject so = new SerializedObject(view);
        SetRef(so, "icon", icon);
        SetRef(so, "nameText", name);
        SetRef(so, "countText", count);
        so.ApplyModifiedPropertiesWithoutUndo();
        bg.gameObject.SetActive(false);
        return view;
    }

    // ---------- helpers ----------

    private static Canvas CreateCanvas(Scene scene, string name, Camera uiCamera, int sortingOrder, bool overlay)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (go.scene != scene)
        {
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        go.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = overlay ? RenderMode.ScreenSpaceOverlay : RenderMode.ScreenSpaceCamera;
        if (!overlay)
        {
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 100f;
        }

        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.referencePixelsPerUnit = 100f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void SetRef(SerializedObject so, string field, UnityEngine.Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            throw new Exception(so.targetObject.GetType().Name + " field not found: " + field);
        }

        p.objectReferenceValue = value;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Image CreateImage(string name, Transform parent, Color color, bool raycast)
    {
        Image img = CreateUIObject(name, parent).AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float size, Color color, TMP_FontAsset font)
    {
        TextMeshProUGUI t = CreateUIObject(name, parent).AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            t.font = font;
        }

        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    private static Button CreateButton(string name, Transform parent, string label, float size, Color color, TMP_FontAsset font)
    {
        Image img = CreateImage(name, parent, color, true);
        Button button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        TMP_Text text = CreateText("Text (TMP)", img.transform, label, size, Color.white, font);
        text.fontStyle = FontStyles.Bold;
        Stretch(text.rectTransform, 0f, 0f);
        return button;
    }

    private static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt, float x, float y)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(x, y);
        rt.offsetMax = new Vector2(-x, -y);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
