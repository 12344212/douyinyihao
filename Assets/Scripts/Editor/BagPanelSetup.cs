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

        BagItemSlot template = CreateSlot(bagPanel.transform, font);
        BagDetailView detail = CreateDetail(bagPanel.transform, font);

        BagPanelController ctrl = bagPanel.GetComponent<BagPanelController>();
        if (ctrl == null)
        {
            ctrl = bagPanel.gameObject.AddComponent<BagPanelController>();
        }

        SerializedObject so = new SerializedObject(ctrl);
        SetRef(so, "listRoot", list);
        SetRef(so, "slotTemplate", template);
        SetRef(so, "emptyLabel", empty);
        SetRef(so, "detailView", detail);
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

            if (panel.GetComponent<BagPanelController>() != null)
            {
                if (!interactive)
                {
                    Debug.Log($"{LogTag} Bag panel already built, auto-run skipped.");
                    CloseIfOpened(scene, openedHere);
                    return;
                }

                if (!EditorUtility.DisplayDialog("Build Bag Panel", "Bag panel content already exists. Rebuild it?", "Rebuild", "Cancel"))
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
    }

    private static void DestroyChild(Transform parent, string name)
    {
        Transform t;
        while ((t = parent.Find(name)) != null)
        {
            UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
    }

    private static BagItemSlot CreateSlot(Transform parent, TMP_FontAsset font)
    {
        Image bg = CreateImage("BagSlotTemplate", parent, RowColor, true);
        bg.rectTransform.sizeDelta = new Vector2(0f, 140f);
        Button button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;

        Image icon = CreateImage("Icon", bg.transform, IconColor, false);
        SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(86f, 0f), new Vector2(100f, 100f));

        TMP_Text name = CreateText("Name", bg.transform, "Healing Potion", 34f, new Color(0.2f, 0.15f, 0.1f, 1f), font, TextAlignmentOptions.Left);
        name.fontStyle = FontStyles.Bold;
        SetRect(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(420f, 0f), new Vector2(560f, 50f));

        TMP_Text count = CreateText("Count", bg.transform, "x1", 32f, new Color(0.25f, 0.22f, 0.16f, 1f), font, TextAlignmentOptions.Right);
        count.fontStyle = FontStyles.Bold;
        SetRect(count.rectTransform, new Vector2(1f, 0.5f), new Vector2(-90f, 0f), new Vector2(140f, 48f));

        BagItemSlot slot = bg.gameObject.AddComponent<BagItemSlot>();
        SerializedObject so = new SerializedObject(slot);
        SetRef(so, "iconImage", icon);
        SetRef(so, "nameLabel", name);
        SetRef(so, "countLabel", count);
        SetRef(so, "button", button);
        so.ApplyModifiedPropertiesWithoutUndo();
        bg.gameObject.SetActive(false);
        return slot;
    }

    private static BagDetailView CreateDetail(Transform parent, TMP_FontAsset font)
    {
        GameObject root = CreateRect("BagDetail", parent).gameObject;
        Stretch((RectTransform)root.transform, 0f);
        CanvasGroup group = root.AddComponent<CanvasGroup>();
        UIPanel panel = root.AddComponent<UIPanel>();
        BagDetailView view = root.AddComponent<BagDetailView>();

        Image dim = CreateImage("Dim", root.transform, DimColor, true);
        Stretch(dim.rectTransform, 0f);
        Image window = CreateImage("Window", root.transform, WindowColor, true);
        SetRect(window.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 520f));

        Image header = CreateImage("Header", window.transform, HeaderColor, false);
        RectTransform hrt = header.rectTransform;
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.anchoredPosition = Vector2.zero;
        hrt.sizeDelta = new Vector2(0f, 90f);
        TMP_Text title = CreateText("Title", header.transform, "Item", 40f, Color.white, font, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        Stretch(title.rectTransform, 0f);
        Button close = CreateButton("Close", header.transform, "X", 36f, CloseColor, font);
        SetRect((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(-50f, 0f), new Vector2(70f, 70f));

        Image icon = CreateImage("Icon", window.transform, IconColor, false);
        SetRect(icon.rectTransform, new Vector2(0f, 1f), new Vector2(120f, -200f), new Vector2(140f, 140f));
        TMP_Text name = CreateText("Name", window.transform, "Item", 36f, new Color(0.2f, 0.15f, 0.1f, 1f), font, TextAlignmentOptions.Left);
        name.fontStyle = FontStyles.Bold;
        SetRect(name.rectTransform, new Vector2(0f, 1f), new Vector2(480f, -160f), new Vector2(480f, 50f));
        TMP_Text count = CreateText("Count", window.transform, "x1", 30f, new Color(0.35f, 0.28f, 0.16f, 1f), font, TextAlignmentOptions.Left);
        SetRect(count.rectTransform, new Vector2(0f, 1f), new Vector2(480f, -210f), new Vector2(480f, 40f));
        TMP_Text desc = CreateText("Description", window.transform, "Description", 26f, new Color(0.35f, 0.30f, 0.24f, 1f), font, TextAlignmentOptions.Left);
        SetRect(desc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(40f, -20f), new Vector2(700f, 80f));

        Button use = CreateButton("Use", window.transform, "Use", 34f, UseColor, font);
        SetRect((RectTransform)use.transform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(280f, 76f));

        SerializedObject panelSo = new SerializedObject(panel);
        SetRef(panelSo, "canvasGroup", group);
        SetRef(panelSo, "window", window.rectTransform);
        SetRef(panelSo, "closeButton", close);
        SetRef(panelSo, "titleText", title);
        SetRef(panelSo, "content", window.rectTransform);
        panelSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject so = new SerializedObject(view);
        SetRef(so, "panel", panel);
        SetRef(so, "iconImage", icon);
        SetRef(so, "nameLabel", name);
        SetRef(so, "descLabel", desc);
        SetRef(so, "countLabel", count);
        SetRef(so, "useButton", use);
        so.ApplyModifiedPropertiesWithoutUndo();

        root.transform.SetAsLastSibling();
        root.SetActive(false);
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