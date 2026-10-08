using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 往已有的 TopUpPanel 里填 TopPrefab 列表、购买转圈和结果框。
/// 同时在 LoadScene 挂上 IapRuntime（IapPurchaseManager）并写进 LoadSceneController。
/// 菜单：Tools/Template/Build TopUp Panel
/// 自动执行：项目根目录存在 TopUpPanelSetup.run 时执行一次。
/// MainSceneUISetup.BuildUI 生成主界面后也会调用 BuildInto；LoadSceneUISetup.BuildUI 会调用 EnsureIap。
/// </summary>
[InitializeOnLoad]
public static class TopUpPanelSetup
{
    private const string LogTag = "[TopUpPanelSetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string LoadScenePath = "Assets/Scenes/LoadScene.unity";
    public const string TopPrefabPath = "Assets/Prefab/UI/TopPrefab.prefab";

    private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.72f);
    private static readonly Color IconColor = new Color(0.25f, 0.55f, 0.72f, 1f);
    private static readonly Color BuyColor = new Color(0.20f, 0.62f, 0.36f, 1f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color CardColor = new Color(0.96f, 0.94f, 0.88f, 1f);
    private static readonly Color TextColor = new Color(0.20f, 0.16f, 0.12f, 1f);

    private static string MarkerFilePath => Path.Combine(Directory.GetCurrentDirectory(), "TopUpPanelSetup.run");

    static TopUpPanelSetup()
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

    [MenuItem("Tools/Template/Build TopUp Panel")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Run(true);
    }

    public static void BuildInto(UIPanel topUpPanel)
    {
        if (topUpPanel == null || topUpPanel.Content == null)
        {
            throw new Exception("TopUpPanel UIPanel or Content is missing.");
        }

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        ClearBuilt(topUpPanel);

        RectTransform list = CreateUIObject("PackList", topUpPanel.Content).GetComponent<RectTransform>();
        list.anchorMin = new Vector2(0f, 1f);
        list.anchorMax = new Vector2(1f, 1f);
        list.pivot = new Vector2(0.5f, 1f);
        list.anchoredPosition = Vector2.zero;
        list.sizeDelta = new Vector2(0f, 0f);
        VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = list.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TopUpItemSlot prefab = EnsureTopPrefab(font);
        PurchaseLoadingUI loading = CreateLoading(topUpPanel.transform, font);
        PurchaseTipUI tip = CreateTip(topUpPanel.transform, font);

        TopUpPanelController ctrl = topUpPanel.gameObject.AddComponent<TopUpPanelController>();
        SerializedObject so = new SerializedObject(ctrl);
        SetRef(so, "listRoot", list);
        SetRef(so, "topPrefab", prefab);
        SetRef(so, "loading", loading);
        SetRef(so, "purchaseTip", tip);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>在 LoadSceneController 所在场景放一个 IapRuntime，并把引用写进控制器。</summary>
    public static void EnsureIap(LoadSceneController ctrl)
    {
        if (ctrl == null)
        {
            throw new Exception("LoadSceneController is missing.");
        }

        Scene scene = ctrl.gameObject.scene;
        IapPurchaseManager iap = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            iap = roots[i].GetComponentInChildren<IapPurchaseManager>(true);
            if (iap != null)
            {
                break;
            }
        }

        if (iap == null)
        {
            GameObject go = new GameObject("IapRuntime");
            if (scene.IsValid())
            {
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            iap = go.AddComponent<IapPurchaseManager>();
        }

        SerializedObject so = new SerializedObject(ctrl);
        SetRef(so, "iapPurchaseManager", iap);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void Run(bool interactive)
    {
        try
        {
            BuildMain(interactive);
            EnsureIapInLoadScene();
        }
        catch (Exception e)
        {
            Debug.LogError($"{LogTag} {e}");
        }
    }

    private static void BuildMain(bool interactive)
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
            Debug.LogWarning($"{LogTag} MainScene has unsaved changes, panel auto-run skipped.");
            return;
        }

        UIPanel panel = FindTopUpPanel(scene);
        if (panel == null)
        {
            Debug.LogError($"{LogTag} TopUpPanel not found. Build MainScene UI first.");
            CloseIfOpened(scene, openedHere);
            return;
        }

        TopUpPanelController existing = panel.GetComponent<TopUpPanelController>();
        if (existing != null)
        {
            if (!interactive && HasTopPrefab(existing))
            {
                Debug.Log($"{LogTag} Top-up panel already uses TopPrefab, auto-run skipped.");
                CloseIfOpened(scene, openedHere);
                return;
            }

            if (interactive && !EditorUtility.DisplayDialog("Build TopUp Panel", "Top-up panel content already exists. Rebuild it?", "Rebuild", "Cancel"))
            {
                return;
            }
        }

        Scene previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);
        try
        {
            BuildInto(panel);
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene))
            {
                Debug.Log($"{LogTag} Top-up panel built and saved.");
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

    private static void EnsureIapInLoadScene()
    {
        if (!File.Exists(LoadScenePath))
        {
            Debug.LogError($"{LogTag} {LoadScenePath} not found.");
            return;
        }

        Scene scene = SceneManager.GetSceneByPath(LoadScenePath);
        bool openedHere = false;
        if (!(scene.IsValid() && scene.isLoaded))
        {
            scene = EditorSceneManager.OpenScene(LoadScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }

        LoadSceneController ctrl = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            ctrl = roots[i].GetComponentInChildren<LoadSceneController>(true);
            if (ctrl != null)
            {
                break;
            }
        }

        if (ctrl == null)
        {
            Debug.LogError($"{LogTag} LoadSceneController not found in LoadScene.");
            CloseIfOpened(scene, openedHere);
            return;
        }

        Scene previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);
        try
        {
            EnsureIap(ctrl);
            if (EditorSceneManager.SaveScene(scene))
            {
                Debug.Log($"{LogTag} IapPurchaseManager mounted in LoadScene.");
            }
            else
            {
                Debug.LogError($"{LogTag} Failed to save LoadScene.");
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

    private static UIPanel FindTopUpPanel(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name != "CanvasPanel")
            {
                continue;
            }

            Transform t = roots[i].transform.Find("TopUpPanel");
            if (t != null)
            {
                return t.GetComponent<UIPanel>();
            }
        }

        return null;
    }

    private static void ClearBuilt(UIPanel panel)
    {
        TopUpPanelController old = panel.GetComponent<TopUpPanelController>();
        if (old != null)
        {
            UnityEngine.Object.DestroyImmediate(old);
        }

        DestroyChild(panel.Content, "PackList");
        DestroyChild(panel.transform, "PurchaseLoading");
        DestroyChild(panel.transform, "PurchaseTip");
    }

    private static void DestroyChild(Transform parent, string name)
    {
        if (parent == null)
        {
            return;
        }

        Transform t;
        while ((t = parent.Find(name)) != null)
        {
            UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
    }

    private static bool HasTopPrefab(TopUpPanelController ctrl)
    {
        SerializedObject so = new SerializedObject(ctrl);
        UnityEngine.Object value = so.FindProperty("topPrefab").objectReferenceValue;
        return value != null && PrefabUtility.GetPrefabAssetType(value) != PrefabAssetType.NotAPrefab;
    }

    private static TopUpItemSlot EnsureTopPrefab(TMP_FontAsset font)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(TopPrefabPath);
        if (existing != null && existing.GetComponent<TopUpItemSlot>() != null)
        {
            return existing.GetComponent<TopUpItemSlot>();
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefab"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefab");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefab/UI"))
        {
            AssetDatabase.CreateFolder("Assets/Prefab", "UI");
        }

        TopUpItemSlot slot = CreateSlot(font);
        bool ok;
        PrefabUtility.SaveAsPrefabAsset(slot.gameObject, TopPrefabPath, out ok);
        UnityEngine.Object.DestroyImmediate(slot.gameObject);
        if (!ok)
        {
            throw new Exception("Failed to save " + TopPrefabPath);
        }

        Debug.Log($"{LogTag} Created {TopPrefabPath}");
        return AssetDatabase.LoadAssetAtPath<GameObject>(TopPrefabPath).GetComponent<TopUpItemSlot>();
    }

    private static TopUpItemSlot CreateSlot(TMP_FontAsset font)
    {
        Image row = CreateImage("TopPrefab", null, RowColor, false);
        RectTransform rowRt = row.rectTransform;
        rowRt.sizeDelta = new Vector2(1360f, 108f);
        LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 108f;
        element.minHeight = 108f;

        Image icon = CreateImage("Icon", row.transform, IconColor, false);
        SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(64f, 0f), new Vector2(76f, 76f));

        TMP_Text name = CreateText("Name", row.transform, "Package", 32f, TextColor, font, TextAlignmentOptions.MidlineLeft);
        SetRect(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(360f, 0f), new Vector2(420f, 80f));

        TMP_Text gems = CreateText("Gems", row.transform, "+0", 32f, TextColor, font, TextAlignmentOptions.MidlineLeft);
        SetRect(gems.rectTransform, new Vector2(0f, 0.5f), new Vector2(700f, 0f), new Vector2(180f, 80f));

        TMP_Text price = CreateText("Price", row.transform, "$0.00", 32f, TextColor, font, TextAlignmentOptions.MidlineRight);
        SetRect(price.rectTransform, new Vector2(1f, 0.5f), new Vector2(-280f, 0f), new Vector2(200f, 80f));

        TMP_Text buyLabel;
        Button buy = CreateButton("Buy", row.transform, "Buy", 28f, BuyColor, font, out buyLabel);
        SetRect((RectTransform)buy.transform, new Vector2(1f, 0.5f), new Vector2(-110f, 0f), new Vector2(160f, 72f));

        TopUpItemSlot slot = row.gameObject.AddComponent<TopUpItemSlot>();
        SerializedObject so = new SerializedObject(slot);
        SetRef(so, "nameText", name);
        SetRef(so, "iconImage", icon);
        SetRef(so, "gemText", gems);
        SetRef(so, "priceText", price);
        SetRef(so, "buyButton", buy);
        so.FindProperty("placeholderColor").colorValue = IconColor;
        so.ApplyModifiedPropertiesWithoutUndo();
        return slot;
    }

    private static PurchaseLoadingUI CreateLoading(Transform parent, TMP_FontAsset font)
    {
        Image dim = CreateImage("PurchaseLoading", parent, DimColor, true);
        Stretch(dim.rectTransform, 0f);
        Image spinner = CreateImage("Spinner", dim.transform, new Color(1f, 1f, 1f, 0.95f), false);
        SetRect(spinner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(72f, 72f));
        TMP_Text label = CreateText("Label", dim.transform, "Processing...", 36f, Color.white, font, TextAlignmentOptions.Center);
        SetRect(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(700f, 60f));

        PurchaseLoadingUI ui = dim.gameObject.AddComponent<PurchaseLoadingUI>();
        SerializedObject so = new SerializedObject(ui);
        SetRef(so, "spinTarget", spinner.rectTransform);
        so.FindProperty("spinSpeed").floatValue = 180f;
        so.ApplyModifiedPropertiesWithoutUndo();
        dim.gameObject.SetActive(false);
        return ui;
    }

    private static PurchaseTipUI CreateTip(Transform parent, TMP_FontAsset font)
    {
        Image dim = CreateImage("PurchaseTip", parent, DimColor, true);
        Stretch(dim.rectTransform, 0f);
        Image card = CreateImage("Card", dim.transform, CardColor, true);
        SetRect(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 320f));
        TMP_Text message = CreateText("Message", card.transform, "Purchase failed.", 36f, TextColor, font, TextAlignmentOptions.Center);
        SetRect(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(680f, 140f));
        TMP_Text okLabel;
        Button ok = CreateButton("OkButton", card.transform, "OK", 32f, BuyColor, font, out okLabel);
        SetRect((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(220f, 72f));

        PurchaseTipUI ui = dim.gameObject.AddComponent<PurchaseTipUI>();
        SerializedObject so = new SerializedObject(ui);
        SetRef(so, "messageText", message);
        SetRef(so, "closeButton", ok);
        so.ApplyModifiedPropertiesWithoutUndo();
        dim.gameObject.SetActive(false);
        return ui;
    }

    private static void CloseIfOpened(Scene scene, bool openedHere)
    {
        if (openedHere && scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
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

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }

        return go;
    }

    private static Image CreateImage(string name, Transform parent, Color color, bool raycast)
    {
        Image img = CreateUIObject(name, parent).AddComponent<Image>();
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float size, Color color, TMP_FontAsset font, TextAlignmentOptions align)
    {
        TextMeshProUGUI t = CreateUIObject(name, parent).AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            t.font = font;
        }

        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    private static Button CreateButton(string name, Transform parent, string label, float size, Color color, TMP_FontAsset font, out TMP_Text labelText)
    {
        Image img = CreateImage(name, parent, color, true);
        Button button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        labelText = CreateText("Label", img.transform, label, size, Color.white, font, TextAlignmentOptions.Center);
        Stretch(labelText.rectTransform, 0f);
        return button;
    }

    private static void Stretch(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    private static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
