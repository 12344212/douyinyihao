using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 往已有的 ShopPanel 里填商品列表和购买确认框。
/// 购买确认是 ShopPanel 的子节点（同一 Canvas，画在窗口上面）；奖励弹窗在 CanvasReward(15)，仍能盖在上面。
/// 菜单：Tools/Template/Build Shop Panel
/// 自动执行：项目根目录存在 ShopPanelSetup.run 时执行一次（已有则跳过）。
/// MainSceneUISetup.BuildUI 生成主界面后也会调用 BuildInto。
/// </summary>
[InitializeOnLoad]
public static class ShopPanelSetup
{
    private const string LogTag = "[ShopPanelSetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    public const string ShopPrefabPath = "Assets/Prefab/UI/ShopPrefab.prefab";

    private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.72f);
    private static readonly Color IconColor = new Color(0.55f, 0.40f, 0.85f, 1f);
    private static readonly Color BuyColor = new Color(0.20f, 0.62f, 0.36f, 1f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color WindowColor = new Color(0.96f, 0.94f, 0.88f, 1f);
    private static readonly Color HeaderColor = new Color(0.40f, 0.28f, 0.18f, 1f);
    private static readonly Color StepColor = new Color(0.25f, 0.42f, 0.68f, 1f);
    private static readonly Color CancelColor = new Color(0.45f, 0.40f, 0.36f, 1f);

    private static string MarkerFilePath => Path.Combine(Directory.GetCurrentDirectory(), "ShopPanelSetup.run");

    static ShopPanelSetup()
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

    [MenuItem("Tools/Template/Build Shop Panel")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Run(true);
    }

    public static void BuildInto(UIPanel shopPanel)
    {
        if (shopPanel == null || shopPanel.Content == null)
        {
            throw new Exception("ShopPanel UIPanel or Content is missing.");
        }

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        ClearBuilt(shopPanel);

        RectTransform list = CreateRect("ItemList", shopPanel.Content);
        Stretch(list, 10f);
        VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 16f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ShopItemSlot template = EnsureShopPrefab(font);
        UIPanel buy = null; // BuyPanelController removed

        ShopPanelController ctrl = shopPanel.GetComponent<ShopPanelController>();
        if (ctrl == null)
        {
            ctrl = shopPanel.gameObject.AddComponent<ShopPanelController>();
        }

        SerializedObject so = new SerializedObject(ctrl);
        SetRef(so, "listRoot", list);
        SetRef(so, "slotPrefab", template);
        // buyPanel UIPanel wired by ShopBuyFoldWireSetup
        so.ApplyModifiedPropertiesWithoutUndo();
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

            UIPanel panel = FindShopPanel(scene);
            if (panel == null)
            {
                Debug.LogError($"{LogTag} ShopPanel not found. Build MainScene UI first.");
                CloseIfOpened(scene, openedHere);
                return;
            }

            ShopPanelController existing = panel.GetComponent<ShopPanelController>();
            if (existing != null)
            {
                if (!interactive && HasSlotPrefab(existing))
                {
                    Debug.Log($"{LogTag} Shop panel already uses ShopPrefab, auto-run skipped.");
                    CloseIfOpened(scene, openedHere);
                    return;
                }

                if (interactive && !EditorUtility.DisplayDialog("Build Shop Panel", "Shop panel content already exists. Rebuild it?", "Rebuild", "Cancel"))
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
                    Debug.Log($"{LogTag} Shop panel built and saved.");
                }
                else
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

                CloseIfOpened(scene, openedHere);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{LogTag} Failed: {e}");
        }
    }

    private static void CloseIfOpened(Scene scene, bool openedHere)
    {
        if (openedHere && SceneManager.sceneCount > 1)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static UIPanel FindShopPanel(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name != "CanvasPanel")
            {
                continue;
            }

            Transform t = roots[i].transform.Find("ShopPanel");
            if (t != null)
            {
                return t.GetComponent<UIPanel>();
            }
        }

        return null;
    }

    private static void ClearBuilt(UIPanel panel)
    {
        ShopPanelController old = panel.GetComponent<ShopPanelController>();
        if (old != null)
        {
            UnityEngine.Object.DestroyImmediate(old);
        }

        DestroyChild(panel.Content, "ItemList");
        DestroyChild(panel.transform, "ShopSlotTemplate");
        DestroyChild(panel.transform, "BuyPanel");
    }

    private static void DestroyChild(Transform parent, string name)
    {
        Transform t;
        while ((t = parent.Find(name)) != null)
        {
            UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
    }

    private static ShopItemSlot EnsureShopPrefab(TMP_FontAsset font)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefabPath);
        if (existing != null && existing.GetComponent<ShopItemSlot>() != null)
        {
            return existing.GetComponent<ShopItemSlot>();
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefab"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefab");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefab/UI"))
        {
            AssetDatabase.CreateFolder("Assets/Prefab", "UI");
        }

        ShopItemSlot slot = CreateSlot(font);
        bool ok;
        PrefabUtility.SaveAsPrefabAsset(slot.gameObject, ShopPrefabPath, out ok);
        UnityEngine.Object.DestroyImmediate(slot.gameObject);
        if (!ok)
        {
            throw new Exception("Failed to save " + ShopPrefabPath);
        }

        Debug.Log($"{LogTag} Created {ShopPrefabPath}");
        return AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefabPath).GetComponent<ShopItemSlot>();
    }

    private static bool HasSlotPrefab(ShopPanelController ctrl)
    {
        if (ctrl == null)
        {
            return false;
        }

        SerializedObject so = new SerializedObject(ctrl);
        UnityEngine.Object value = so.FindProperty("slotPrefab").objectReferenceValue;
        return value != null && PrefabUtility.IsPartOfPrefabAsset(value);
    }

    private static ShopItemSlot CreateSlot(TMP_FontAsset font)
    {
        Image bg = CreateImage("ShopPrefab", null, RowColor, false);
        RectTransform rt = bg.rectTransform;
        rt.sizeDelta = new Vector2(900f, 150f);
        LayoutElement layout = bg.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 150f;

        Image icon = CreateImage("Icon", bg.transform, IconColor, false);
        SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(90f, 0f), new Vector2(110f, 110f));

        TMP_Text name = CreateText("Name", bg.transform, "Healing Potion", 36f, new Color(0.2f, 0.15f, 0.1f, 1f), font, TextAlignmentOptions.Left);
        name.fontStyle = FontStyles.Bold;
        SetRect(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(420f, 24f), new Vector2(520f, 50f));

        TMP_Text price = CreateText("Price", bg.transform, "150 Gold", 30f, new Color(0.45f, 0.32f, 0.12f, 1f), font, TextAlignmentOptions.Left);
        SetRect(price.rectTransform, new Vector2(0f, 0.5f), new Vector2(420f, -28f), new Vector2(520f, 40f));

        Button buy = CreateButton("BuyButton", bg.transform, "Buy", 32f, BuyColor, font);
        SetRect((RectTransform)buy.transform, new Vector2(1f, 0.5f), new Vector2(-120f, 0f), new Vector2(180f, 72f));

        ShopItemSlot slot = bg.gameObject.AddComponent<ShopItemSlot>();
        SerializedObject so = new SerializedObject(slot);
        SetRef(so, "iconImage", icon);
        SetRef(so, "nameLabel", name);
        SetRef(so, "priceLabel", price);
        SetRef(so, "clickButton", buy);
        so.ApplyModifiedPropertiesWithoutUndo();
        return slot;
    }

    private static UIPanel CreateBuyPanelObsolete(Transform parent, TMP_FontAsset font)
    {
        Debug.LogWarning("[Editor] CreateBuyPanel obsolete; Buy folded into ShopPanelController. Use ShopBuyFoldWireSetup.");
        return null;
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

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image CreateImage(string name, Transform parent, Color color, bool raycast)
    {
        Image img = CreateRect(name, parent).gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float size, Color color, TMP_FontAsset font, TextAlignmentOptions align)
    {
        TextMeshProUGUI t = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
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

    private static Button CreateButton(string name, Transform parent, string label, float size, Color color, TMP_FontAsset font)
    {
        Image img = CreateImage(name, parent, color, true);
        Button button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        TMP_Text text = CreateText("Text (TMP)", img.transform, label, size, Color.white, font, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        Stretch(text.rectTransform, 0f);
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

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }
}