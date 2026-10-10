using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 往已有的 BagPanel 里填背包列表和详情。详情是 BagPanel 的子节点，盖在列表上；奖励弹窗在更上层。
/// 菜单：Tools/Template/Build Bag Panel
/// 自动执行：项目根目录存在 BagPanelSetup.run 时执行一次（已有则跳过）。
/// MainSceneUISetup.BuildUI 生成主界面后也会调用 BuildInto。
/// </summary>
[InitializeOnLoad]
public static class BagPanelSetup
{
    private const string LogTag = "[BagPanelSetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    public const string BagPrefabPath = "Assets/Prefab/UI/BagPrefab.prefab";

    private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.72f);
    private static readonly Color IconColor = new Color(0.55f, 0.40f, 0.85f, 1f);
    private static readonly Color UseColor = new Color(0.20f, 0.62f, 0.36f, 1f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color WindowColor = new Color(0.96f, 0.94f, 0.88f, 1f);
    private static readonly Color HeaderColor = new Color(0.40f, 0.28f, 0.18f, 1f);
    private static readonly Color CloseColor = new Color(0.45f, 0.40f, 0.36f, 1f);

    private static string MarkerFilePath => Path.Combine(Directory.GetCurrentDirectory(), "BagPanelSetup.run");

    static BagPanelSetup()
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

    [MenuItem("Tools/Template/Build Bag Panel")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Run(true);
    }

    public static void BuildInto(UIPanel bagPanel)
    {
        if (bagPanel == null || bagPanel.Content == null)
        {
            throw new Exception("BagPanel UIPanel or Content is missing.");
        }

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        ClearBuilt(bagPanel);

        TMP_Text empty = CreateText("EmptyLabel", bagPanel.Content, "Your bag is empty.", 36f, new Color(0.35f, 0.30f, 0.24f, 1f), font, TextAlignmentOptions.Center);
        Stretch(empty.rectTransform, 20f);

        RectTransform list = CreateRect("ItemList", bagPanel.Content);
        Stretch(list, 10f);
        VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 16f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        BagItemSlot template = EnsureBagPrefab(font);
        BagDetailView detail = CreateDetail(bagPanel.transform, font);

        BagPanelController ctrl = bagPanel.GetComponent<BagPanelController>();
        if (ctrl == null)
        {
            ctrl = bagPanel.gameObject.AddComponent<BagPanelController>();
        }

        SerializedObject so = new SerializedObject(ctrl);
        SetRef(so, "listRoot", list);
        SetRef(so, "slotPrefab", template);
        SetRef(so, "emptyLabel", empty);
        SetRef(so, "itemData", detail);
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

            UIPanel panel = FindBagPanel(scene);
            if (panel == null)
            {
                Debug.LogError($"{LogTag} BagPanel not found. Build MainScene UI first.");
                CloseIfOpened(scene, openedHere);
                return;
            }

            BagPanelController existing = panel.GetComponent<BagPanelController>();
            if (existing != null)
            {
                if (!interactive && HasSlotPrefab(existing))
                {
                    Debug.Log($"{LogTag} Bag panel already uses BagPrefab, auto-run skipped.");
                    CloseIfOpened(scene, openedHere);
                    return;
                }

                if (interactive && !EditorUtility.DisplayDialog("Build Bag Panel", "Bag panel content already exists. Rebuild it?", "Rebuild", "Cancel"))
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
                    Debug.Log($"{LogTag} Bag panel built and saved.");
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

    private static UIPanel FindBagPanel(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name != "CanvasPanel")
            {
                continue;
            }

            Transform t = roots[i].transform.Find("BagPanel");
            if (t != null)
            {
                return t.GetComponent<UIPanel>();
            }
        }

        return null;
    }

    private static void ClearBuilt(UIPanel panel)
    {
        BagPanelController old = panel.GetComponent<BagPanelController>();
        if (old != null)
        {
            UnityEngine.Object.DestroyImmediate(old);
        }

        DestroyChild(panel.Content, "EmptyLabel");
        DestroyChild(panel.Content, "ItemList");
        DestroyChild(panel.transform, "BagSlotTemplate");
        DestroyChild(panel.transform, "BagDetail");
        DestroyChild(panel.transform, "ItemData");
    }

    private static void DestroyChild(Transform parent, string name)
    {
        Transform t;
        while ((t = parent.Find(name)) != null)
        {
            UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
    }

    private static BagItemSlot EnsureBagPrefab(TMP_FontAsset font)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BagPrefabPath);
        if (existing != null && existing.GetComponent<BagItemSlot>() != null)
        {
            return existing.GetComponent<BagItemSlot>();
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefab"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefab");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefab/UI"))
        {
            AssetDatabase.CreateFolder("Assets/Prefab", "UI");
        }

        BagItemSlot slot = CreateSlot(font);
        bool ok;
        PrefabUtility.SaveAsPrefabAsset(slot.gameObject, BagPrefabPath, out ok);
        UnityEngine.Object.DestroyImmediate(slot.gameObject);
        if (!ok)
        {
            throw new Exception("Failed to save " + BagPrefabPath);
        }

        Debug.Log($"{LogTag} Created {BagPrefabPath}");
        return AssetDatabase.LoadAssetAtPath<GameObject>(BagPrefabPath).GetComponent<BagItemSlot>();
    }

    private static bool HasSlotPrefab(BagPanelController ctrl)
    {
        if (ctrl == null)
        {
            return false;
        }

        SerializedObject so = new SerializedObject(ctrl);
        UnityEngine.Object value = so.FindProperty("slotPrefab").objectReferenceValue;
        return value != null && PrefabUtility.IsPartOfPrefabAsset(value);
    }

    private static BagItemSlot CreateSlot(TMP_FontAsset font)
    {
        Image bg = CreateImage("BagPrefab", null, RowColor, true);
        bg.rectTransform.sizeDelta = new Vector2(247f, 246f);
        LayoutElement layout = bg.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 247f;
        layout.preferredHeight = 246f;
        Button button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;

        Image icon = CreateImage("Icon", bg.transform, IconColor, false);
        SetRect(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));

        TMP_Text count = CreateText("Count", bg.transform, "x1", 32f, Color.white, font, TextAlignmentOptions.BottomRight);
        count.fontStyle = FontStyles.Bold;
        RectTransform countRt = count.rectTransform;
        countRt.anchorMin = new Vector2(1f, 0f);
        countRt.anchorMax = new Vector2(1f, 0f);
        countRt.pivot = new Vector2(0.5f, 0.5f);
        countRt.anchoredPosition = new Vector2(-117.7f, 66.3f);
        countRt.sizeDelta = new Vector2(140f, 48f);

        Image select = CreateImage("Select", bg.transform, Color.white, false);
        SetRect(select.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(247f, 246f));
        select.gameObject.SetActive(false);

        BagItemSlot slot = bg.gameObject.AddComponent<BagItemSlot>();
        SerializedObject so = new SerializedObject(slot);
        SetRef(so, "iconImage", icon);
        SetRef(so, "countLabel", count);
        SetRef(so, "button", button);
        SetRef(so, "selectVisual", select.gameObject);
        so.ApplyModifiedPropertiesWithoutUndo();
        return slot;
    }

    private static BagDetailView CreateDetail(Transform parent, TMP_FontAsset font)
    {
        // Side ItemData block (not fullscreen overlay)
        Image rootImg = CreateImage("ItemData", parent, WindowColor, true);
        RectTransform root = rootImg.rectTransform;
        root.anchorMin = new Vector2(0f, 0f);
        root.anchorMax = new Vector2(0f, 0f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = new Vector2(1536f, 504f);
        root.sizeDelta = new Vector2(532f, 800f);

        BagDetailView view = rootImg.gameObject.AddComponent<BagDetailView>();

        TMP_Text name = CreateText("Name", root, "Item", 36f, new Color(0.2f, 0.15f, 0.1f, 1f), font, TextAlignmentOptions.Center);
        name.fontStyle = FontStyles.Bold;
        SetRect(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(460f, 50f));

        Image icon = CreateImage("Icon", root, IconColor, false);
        SetRect(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(160f, 160f));

        TMP_Text count = CreateText("Count", root, "x1", 30f, new Color(0.35f, 0.28f, 0.16f, 1f), font, TextAlignmentOptions.Center);
        SetRect(count.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(200f, 40f));

        TMP_Text desc = CreateText("Description", root, "Description", 26f, new Color(0.35f, 0.30f, 0.24f, 1f), font, TextAlignmentOptions.Top);
        SetRect(desc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(440f, 280f));

        SerializedObject so = new SerializedObject(view);
        SetRef(so, "iconImage", icon);
        SetRef(so, "nameLabel", name);
        SetRef(so, "descLabel", desc);
        SetRef(so, "countLabel", count);
        so.ApplyModifiedPropertiesWithoutUndo();

        root.SetAsLastSibling();
        return view;
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