using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 生成 MainScene 主界面（1920x1080 横屏），层级参照参考项目 mainScenes：
/// - Canvas（order 0）：bg / Hero 占位 / 主界面按钮（StartBattle=Play、Stage、Shop、Bag、Upgrade），挂 MainUIController
/// - CanvasPanel（order 10）：StagePanel / ShopPanel / BagPanel / TopUpPanel / UpgradePanel / ProfilePanel（UIPanel，默认隐藏）
/// - CanvasMax（order 20）：TopBarPrefab 预制体实例（头像 + 金币 / 钻石 + "+"，SafeAreaTopBar 安全区适配）
/// TopBarPrefab 不存在时自动创建（Assets/Prefab/UI/TopBarPrefab.prefab）；已存在则直接复用（删掉预制体再执行即可重新生成）。
/// 菜单：Tools/Template/Build MainScene UI（已存在时询问是否重建）
/// 自动执行：项目根目录存在 MainSceneUISetup.run 时，编译加载后执行一次并删除该标记文件（已存在 UI 时跳过）。
/// TemplateSceneSetup 生成 MainScene 时也会调用 BuildUI。
/// </summary>
[InitializeOnLoad]
public static class MainSceneUISetup
{
    private const string LogTag = "[MainSceneUISetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    public const string TopBarPrefabPath = "Assets/Prefab/UI/TopBarPrefab.prefab";

    private const string CanvasName = "Canvas";
    private const string CanvasPanelName = "CanvasPanel";
    private const string CanvasMaxName = "CanvasMax";
    private const int PanelSortingOrder = 10;
    private const int MaxSortingOrder = 20;
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    private static readonly string[] MainUINames = { "bg", "StartBattle", "Stage", "Shop", "Bag", "Upgrade" };

    private static readonly Vector2 PanelWindowSize = new Vector2(1500f, 820f);
    private static readonly Vector2 PopupWindowSize = new Vector2(900f, 600f);
    private static readonly Vector2 WindowOffset = new Vector2(0f, -40f); // 稍微下移，避开顶栏

    // 占位配色
    private static readonly Color BgColor = new Color(0.16f, 0.24f, 0.30f, 1f);
    private static readonly Color HeroColor = new Color(0.35f, 0.47f, 0.60f, 1f);
    private static readonly Color PlayColor = new Color(0.96f, 0.62f, 0.18f, 1f);
    private static readonly Color SideButtonColor = new Color(0.22f, 0.45f, 0.75f, 1f);
    private static readonly Color UpgradeColor = new Color(0.55f, 0.35f, 0.80f, 1f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color WindowColor = new Color(0.93f, 0.90f, 0.82f, 1f);
    private static readonly Color HeaderColor = new Color(0.40f, 0.28f, 0.18f, 1f);
    private static readonly Color CloseColor = new Color(0.85f, 0.30f, 0.30f, 1f);
    private static readonly Color TopStripColor = new Color(0f, 0f, 0f, 0.35f);
    private static readonly Color AvatarColor = new Color(0.85f, 0.85f, 0.90f, 1f);
    private static readonly Color CurrencyBgColor = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color GoldIconColor = new Color(1f, 0.80f, 0.20f, 1f);
    private static readonly Color GemsIconColor = new Color(0.35f, 0.85f, 1f, 1f);
    private static readonly Color PlusColor = new Color(0.30f, 0.75f, 0.35f, 1f);

    private static string MarkerFilePath => Path.Combine(Directory.GetCurrentDirectory(), "MainSceneUISetup.run");

    static MainSceneUISetup()
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

    [MenuItem("Tools/Template/Build MainScene UI")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Run(true);
    }

    private static void Run(bool interactive)
    {
        try
        {
            if (!File.Exists(MainScenePath))
            {
                Debug.LogError($"{LogTag} {MainScenePath} not found. Create template scenes first.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(MainScenePath);
            bool openedHere = false;
            if (scene.IsValid() && scene.isLoaded)
            {
                if (scene.isDirty && !interactive)
                {
                    Debug.LogWarning($"{LogTag} MainScene has unsaved changes, auto-run skipped. Save it and use Tools/Template/Build MainScene UI.");
                    return;
                }
            }
            else
            {
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
                openedHere = true;
            }

            Scene previousActive = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                Canvas canvas = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    if (roots[i].name == CanvasName)
                    {
                        canvas = roots[i].GetComponent<Canvas>();
                        break;
                    }
                }

                if (canvas == null)
                {
                    Debug.LogError($"{LogTag} Root '{CanvasName}' with Canvas not found in MainScene.");
                    return;
                }

                if (HasExistingUI(scene, canvas))
                {
                    if (!interactive)
                    {
                        Debug.Log($"{LogTag} MainScene UI already exists, auto-run skipped.");
                        return;
                    }

                    if (!EditorUtility.DisplayDialog("Build MainScene UI", "MainScene UI already exists. Rebuild it? (TopBarPrefab asset is reused)", "Rebuild", "Cancel"))
                    {
                        return;
                    }

                    RemoveExistingUI(scene, canvas);
                }

                BuildUI(canvas);
                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene))
                {
                    Debug.Log($"{LogTag} MainScene UI built and saved.");
                }
                else
                {
                    Debug.LogError($"{LogTag} Failed to save {MainScenePath}.");
                }
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded && previousActive != scene)
                {
                    SceneManager.SetActiveScene(previousActive);
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

    private static bool HasExistingUI(Scene scene, Canvas canvas)
    {
        if (canvas.GetComponent<MainUIController>() != null)
        {
            return true;
        }

        for (int i = 0; i < MainUINames.Length; i++)
        {
            if (canvas.transform.Find(MainUINames[i]) != null)
            {
                return true;
            }
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == CanvasPanelName || roots[i].name == CanvasMaxName)
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveExistingUI(Scene scene, Canvas canvas)
    {
        MainUIController ctrl = canvas.GetComponent<MainUIController>();
        if (ctrl != null)
        {
            UnityEngine.Object.DestroyImmediate(ctrl);
        }

        for (int i = 0; i < MainUINames.Length; i++)
        {
            Transform t;
            while ((t = canvas.transform.Find(MainUINames[i])) != null)
            {
                UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == CanvasPanelName || roots[i].name == CanvasMaxName)
            {
                UnityEngine.Object.DestroyImmediate(roots[i]);
            }
        }
    }

    /// <summary>在 MainScene 中生成主界面、面板与顶栏，并挂 MainUIController、绑定引用。</summary>
    public static MainUIController BuildUI(Canvas mainCanvas)
    {
        Scene scene = mainCanvas.gameObject.scene;
        Camera uiCamera = mainCanvas.worldCamera;
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            Debug.LogWarning($"{LogTag} TMP default font not found. Import TMP Essential Resources (Window/TextMeshPro).");
        }

        // 1. Canvas：主界面
        Transform main = mainCanvas.transform;
        Image bg = CreateImage("bg", main, BgColor, false);
        Stretch(bg.rectTransform, 0f);
        Image hero = CreateImage("Hero", bg.transform, HeroColor, false);
        SetRect(hero.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-120f, 40f), new Vector2(420f, 560f));

        Button play = CreateButton("StartBattle", main, "PLAY", 64f, PlayColor, font);
        SetRect((RectTransform)play.transform, new Vector2(0.5f, 0f), new Vector2(-120f, 140f), new Vector2(440f, 140f));

        // 右侧竖排按钮，离右边缘留足距离，避开横屏刘海 / 挖孔
        Button stages = CreateButton("Stage", main, "Stages", 44f, SideButtonColor, font);
        SetRect((RectTransform)stages.transform, new Vector2(1f, 0.5f), new Vector2(-270f, 240f), new Vector2(240f, 130f));
        Button shop = CreateButton("Shop", main, "Shop", 44f, SideButtonColor, font);
        SetRect((RectTransform)shop.transform, new Vector2(1f, 0.5f), new Vector2(-270f, 80f), new Vector2(240f, 130f));
        Button bag = CreateButton("Bag", main, "Bag", 44f, SideButtonColor, font);
        SetRect((RectTransform)bag.transform, new Vector2(1f, 0.5f), new Vector2(-270f, -80f), new Vector2(240f, 130f));
        Button upgrade = CreateButton("Upgrade", main, "Upgrade", 44f, UpgradeColor, font);
        SetRect((RectTransform)upgrade.transform, new Vector2(1f, 0.5f), new Vector2(-270f, -240f), new Vector2(240f, 130f));

        // 2. CanvasPanel：功能面板（空占位）
        Canvas panelCanvas = CreateCanvas(scene, CanvasPanelName, uiCamera, PanelSortingOrder);
        UIPanel stagePanel = CreatePanel(panelCanvas.transform, "StagePanel", "Stages", PanelWindowSize, font);
        UIPanel shopPanel = CreatePanel(panelCanvas.transform, "ShopPanel", "Shop", PanelWindowSize, font);
        UIPanel bagPanel = CreatePanel(panelCanvas.transform, "BagPanel", "Bag", PanelWindowSize, font);
        UIPanel topUpPanel = CreatePanel(panelCanvas.transform, "TopUpPanel", "Top Up", PanelWindowSize, font);
        UIPanel upgradePanel = CreatePanel(panelCanvas.transform, "UpgradePanel", "Upgrade", PanelWindowSize, font);
        UIPanel profilePanel = CreatePanel(panelCanvas.transform, "ProfilePanel", "Profile", PopupWindowSize, font);

        // 3. CanvasMax：顶栏预制体实例
        Canvas maxCanvas = CreateCanvas(scene, CanvasMaxName, uiCamera, MaxSortingOrder);
        GameObject prefab = EnsureTopBarPrefab(font);
        GameObject topBarGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        topBarGo.transform.SetParent(maxCanvas.transform, false);
        TopBarUI topBar = topBarGo.GetComponent<TopBarUI>();

        // 4. MainUIController 挂在 Canvas 上（与参考项目一致）
        MainUIController ctrl = mainCanvas.gameObject.AddComponent<MainUIController>();
        SerializedObject so = new SerializedObject(ctrl);
        SetRef(so, "playButton", play);
        SetRef(so, "stagesButton", stages);
        SetRef(so, "shopButton", shop);
        SetRef(so, "bagButton", bag);
        SetRef(so, "upgradeButton", upgrade);
        SetRef(so, "topBar", topBar);
        SetRef(so, "stagePanel", stagePanel);
        SetRef(so, "shopPanel", shopPanel);
        SetRef(so, "bagPanel", bagPanel);
        SetRef(so, "topUpPanel", topUpPanel);
        SetRef(so, "upgradePanel", upgradePanel);
        SetRef(so, "profilePanel", profilePanel);
        so.FindProperty("battleSceneName").stringValue = MainUIController.BattleSceneName;
        so.ApplyModifiedPropertiesWithoutUndo();
        StagePanelSetup.BuildInto(stagePanel); // 关卡选择填进 StagePanel 的 Content
        ShopPanelSetup.BuildInto(shopPanel); // 商店列表（ShopPrefab）+ 购买确认
        BagPanelSetup.BuildInto(bagPanel); // 背包列表（BagPrefab）+ 详情
        return ctrl;
    }

    // ---------- 顶栏预制体 ----------

    /// <summary>返回 TopBarPrefab；不存在时按占位样式生成。</summary>
    public static GameObject EnsureTopBarPrefab(TMP_FontAsset font)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(TopBarPrefabPath);
        if (existing != null && existing.GetComponent<TopBarUI>() != null)
        {
            return existing;
        }

        EnsureFolder(Path.GetDirectoryName(TopBarPrefabPath).Replace('\\', '/'));

        GameObject root = new GameObject("TopBarPrefab", typeof(RectTransform));
        try
        {
            root.layer = LayerMask.NameToLayer("UI");
            RectTransform rt = (RectTransform)root.transform;
            // 顶部横向拉伸，左右各留 20，高 120；运行时 SafeAreaTopBar 按安全区改锚点
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(-40f, 120f);

            Image strip = root.AddComponent<Image>();
            strip.color = TopStripColor;
            strip.raycastTarget = false;
            root.AddComponent<SafeAreaTopBar>();
            TopBarUI topBar = root.AddComponent<TopBarUI>();

            // 头像（左侧）
            Image avatarImg = CreateImage("Avatar", root.transform, AvatarColor, true);
            SetRect(avatarImg.rectTransform, new Vector2(0f, 0.5f), new Vector2(70f, 0f), new Vector2(96f, 96f));
            Button avatar = avatarImg.gameObject.AddComponent<Button>();
            avatar.targetGraphic = avatarImg;

            // 金币 / 钻石（右侧）
            Image goldIcon, gemsIcon;
            TMP_Text goldText, gemsText;
            Button goldPlus = CreateCurrency("GoldBg", root.transform, new Vector2(-560f, 0f), GoldIconColor, font, out goldIcon, out goldText);
            Button gemsPlus = CreateCurrency("GemsBg", root.transform, new Vector2(-200f, 0f), GemsIconColor, font, out gemsIcon, out gemsText);

            SerializedObject so = new SerializedObject(topBar);
            SetRef(so, "avatarButton", avatar);
            SetRef(so, "goldText", goldText);
            SetRef(so, "gemsText", gemsText);
            SetRef(so, "goldPlusButton", goldPlus);
            SetRef(so, "gemsPlusButton", gemsPlus);
            SetRef(so, "goldIcon", goldIcon);
            SetRef(so, "gemsIcon", gemsIcon);
            so.ApplyModifiedPropertiesWithoutUndo();

            bool ok;
            PrefabUtility.SaveAsPrefabAsset(root, TopBarPrefabPath, out ok);
            if (!ok)
            {
                throw new Exception("Failed to save " + TopBarPrefabPath);
            }

            Debug.Log($"{LogTag} Created {TopBarPrefabPath}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(TopBarPrefabPath);
    }

    private static Button CreateCurrency(string name, Transform parent, Vector2 position, Color iconColor, TMP_FontAsset font, out Image icon, out TMP_Text amount)
    {
        Image bg = CreateImage(name, parent, CurrencyBgColor, false);
        SetRect(bg.rectTransform, new Vector2(1f, 0.5f), position, new Vector2(300f, 72f));

        icon = CreateImage("Icon", bg.transform, iconColor, false);
        SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(56f, 56f));

        amount = CreateText("Amount", bg.transform, "0", 36f, Color.white, font);
        RectTransform art = amount.rectTransform;
        art.anchorMin = Vector2.zero;
        art.anchorMax = Vector2.one;
        art.offsetMin = new Vector2(72f, 0f);
        art.offsetMax = new Vector2(-72f, 0f);
        amount.enableWordWrapping = false;

        Button plus = CreateButton("PlusButton", bg.transform, "+", 44f, PlusColor, font);
        SetRect((RectTransform)plus.transform, new Vector2(1f, 0.5f), new Vector2(-36f, 0f), new Vector2(56f, 56f));
        return plus;
    }

    // ---------- 面板 ----------

    private static UIPanel CreatePanel(Transform parent, string name, string title, Vector2 windowSize, TMP_FontAsset font)
    {
        GameObject root = CreateUIObject(name, parent);
        Stretch((RectTransform)root.transform, 0f);
        CanvasGroup group = root.AddComponent<CanvasGroup>();
        UIPanel panel = root.AddComponent<UIPanel>();

        Image dim = CreateImage("Dim", root.transform, DimColor, true); // 拦截点击
        Stretch(dim.rectTransform, 0f);

        Image window = CreateImage("Window", root.transform, WindowColor, true);
        SetRect(window.rectTransform, new Vector2(0.5f, 0.5f), WindowOffset, windowSize);

        Image header = CreateImage("Header", window.transform, HeaderColor, false);
        RectTransform hrt = header.rectTransform;
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.anchoredPosition = Vector2.zero;
        hrt.sizeDelta = new Vector2(0f, 100f);

        TMP_Text titleText = CreateText("Title", header.transform, title, 52f, Color.white, font);
        titleText.fontStyle = FontStyles.Bold;
        Stretch(titleText.rectTransform, 0f);

        Button close = CreateButton("CloseButton", header.transform, "X", 48f, CloseColor, font);
        SetRect((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(-55f, 0f), new Vector2(80f, 80f));

        // 内容区：后续模块往这里填具体 UI
        RectTransform content = (RectTransform)CreateUIObject("Content", window.transform).transform;
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 0.5f);
        content.offsetMin = new Vector2(30f, 30f);
        content.offsetMax = new Vector2(-30f, -120f);

        SerializedObject so = new SerializedObject(panel);
        SetRef(so, "canvasGroup", group);
        SetRef(so, "window", window.rectTransform);
        SetRef(so, "closeButton", close);
        SetRef(so, "titleText", titleText);
        SetRef(so, "content", content);
        so.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false);
        return panel;
    }

    // ---------- 通用 ----------

    private static Canvas CreateCanvas(Scene scene, string name, Camera uiCamera, int sortingOrder)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (go.scene != scene)
        {
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        go.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = uiCamera;
        canvas.planeDistance = 100f;
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

    private static Image CreateImage(string name, Transform parent, Color color, bool raycastTarget)
    {
        Image img = CreateUIObject(name, parent).AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycastTarget;
        return img;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float fontSize, Color color, TMP_FontAsset font)
    {
        TextMeshProUGUI t = CreateUIObject(name, parent).AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            t.font = font;
        }

        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        return t;
    }

    private static Button CreateButton(string name, Transform parent, string label, float fontSize, Color color, TMP_FontAsset font)
    {
        Image img = CreateImage(name, parent, color, true);
        Button button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        TMP_Text text = CreateText("Text (TMP)", img.transform, label, fontSize, Color.white, font);
        text.fontStyle = FontStyles.Bold;
        Stretch(text.rectTransform, 0f);
        return button;
    }

    private static void SetRect(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(-inset * 2f, -inset * 2f);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}