sing System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 往已有的 StagePanel（CanvasPanel/StagePanel）的 Content 里填关卡选择界面，并挂 StagePanelController。
/// 菜单：Tools/Template/Build Stage Panel（已有内容时询问是否重建）
/// 自动执行：项目根目录存在 StagePanelSetup.run 时执行一次并删除标记（已有则跳过）。
/// MainSceneUISetup.BuildUI 生成主界面后也会调用 BuildInto。
/// </summary>
[InitializeOnLoad]
public static class StagePanelSetup
{
    private const string LogTag = "[StagePanelSetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private static readonly string[] BuiltNames = { "ChapterBar", "SlotRow", "PageBar" };

    private static readonly Color UnlockedColor = new Color(0.25f, 0.45f, 0.72f, 1f);
    private static readonly Color LockColor = new Color(0.12f, 0.12f, 0.14f, 0.72f);
    private static readonly Color ClearColor = new Color(0.95f, 0.78f, 0.25f, 1f);
    private static readonly Color ChapterColor = new Color(0.30f, 0.34f, 0.40f, 1f);
    private static readonly Color PageButtonColor = new Color(0.22f, 0.40f, 0.62f, 1f);

    private static string MarkerFilePath => Path.Combine(Directory.GetCurrentDirectory(), "StagePanelSetup.run");

    static StagePanelSetup()
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

    [MenuItem("Tools/Template/Build Stage Panel")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Run(true);
    }

    /// <summary>往指定 StagePanel 的 Content 里生成关卡界面。</summary>
    public static StagePanelController BuildInto(UIPanel panel)
    {
        if (panel == null || panel.Content == null)
        {
            throw new Exception("StagePanel UIPanel or Content is missing.");
        }

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            Debug.LogWarning($"{LogTag} TMP default font not found.");
        }

        ClearBuilt(panel);
        Transform content = panel.Content;

        Button[] chapterButtons = CreateChapterBar(content, font);
        StageSlot[] slots = CreateSlots(content, font);
        Button prev;
        Button next;
        TMP_Text pageLabel;
        TMP_Text chapterTitle = CreatePageBar(content, font, out prev, out next, out pageLabel);

        StagePanelController ctrl = panel.gameObject.GetComponent<StagePanelController>();
        if (ctrl == null)
        {
            ctrl = panel.gameObject.AddComponent<StagePanelController>();
        }

        SerializedObject so = new SerializedObject(ctrl);
        SetArray(so, "slots", slots);
        SetRef(so, "chapterTitle", chapterTitle);
        SetArray(so, "chapterButtons", chapterButtons);
        SetRef(so, "prevButton", prev);
        SetRef(so, "nextButton", next);
        SetRef(so, "pageLabel", pageLabel);
        so.FindProperty("battleSceneName").stringValue = "BattleScene";
        so.ApplyModifiedPropertiesWithoutUndo();
        return ctrl;
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

            UIPanel panel = FindStagePanel(scene);
            if (panel == null)
            {
                Debug.LogError($"{LogTag} StagePanel not found. Build MainScene UI first.");
                CloseIfOpened(scene, openedHere);
                return;
            }

            // Protect user-placed GQ art hierarchy (SlotRow with TZ / yun). Use Wire Stage Panel instead.
            Transform contentCheck = panel.Content;
            Transform slotRowCheck = contentCheck != null ? contentCheck.Find("SlotRow") : null;
            if (slotRowCheck != null && (slotRowCheck.Find("yun1") != null || (slotRowCheck.Find("Slot1") != null && slotRowCheck.Find("Slot1/TZ") != null)))
            {
                Debug.LogWarning($"{LogTag} Detected user-placed StagePanel art (yun/TZ). Abort rebuild; use Tools/Template/Wire Stage Panel.");
                if (interactive)
                {
                    EditorUtility.DisplayDialog("Build Stage Panel", "User-placed StagePanel art detected (yun/TZ). Use Tools/Template/Wire Stage Panel instead of Rebuild.", "OK");
                }
                CloseIfOpened(scene, openedHere);
                return;
            }
            if (!interactive && panel.GetComponent<StagePanelController>() != null)
            {
                Debug.Log($"{LogTag} Stage panel already built, auto-run skipped.");
                CloseIfOpened(scene, openedHere);
                return;
            }

            if (interactive && panel.GetComponent<StagePanelController>() != null)
            {
                if (!EditorUtility.DisplayDialog("Build Stage Panel", "Stage panel content already exists. Rebuild it?", "Rebuild", "Cancel"))
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
                    Debug.Log($"{LogTag} Stage panel built and saved.");
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

    private static UIPanel FindStagePanel(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name != "CanvasPanel")
            {
                continue;
            }

            Transform t = roots[i].transform.Find("StagePanel");
            if (t != null)
            {
                return t.GetComponent<UIPanel>();
            }
        }

        return null;
    }

    private static void ClearBuilt(UIPanel panel)
    {
        Transform content = panel.Content;
        for (int i = 0; i < BuiltNames.Length; i++)
        {
            Transform t;
            while ((t = content.Find(BuiltNames[i])) != null)
            {
                UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
        }
    }

    private static Button[] CreateChapterBar(Transform content, TMP_FontAsset font)
    {
        RectTransform bar = CreateRect("ChapterBar", content);
        bar.anchorMin = new Vector2(0.5f, 1f);
        bar.anchorMax = new Vector2(0.5f, 1f);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.anchoredPosition = new Vector2(0f, -8f);
        bar.sizeDelta = new Vector2(1100f, 80f);

        Button[] buttons = new Button[StageDefs.ChapterCount];
        float[] xs = { -370f, 0f, 370f };
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i] = CreateButton("Chapter" + (i + 1), bar, StageDefs.ChapterName(i + 1), 32f, ChapterColor, font);
            SetRect((RectTransform)buttons[i].transform, new Vector2(0.5f, 0.5f), new Vector2(xs[i], 0f), new Vector2(320f, 68f));
        }

        return buttons;
    }

    private static StageSlot[] CreateSlots(Transform content, TMP_FontAsset font)
    {
        RectTransform row = CreateRect("SlotRow", content);
        row.anchorMin = new Vector2(0.5f, 0.5f);
        row.anchorMax = new Vector2(0.5f, 0.5f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.anchoredPosition = new Vector2(0f, 10f);
        row.sizeDelta = new Vector2(1200f, 280f);

        StageSlot[] slots = new StageSlot[StageDefs.SlotsPerPage];
        float start = -440f;
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = CreateSlot("Slot" + (i + 1), row, new Vector2(start + i * 220f, 0f), font);
        }

        return slots;
    }

    private static StageSlot CreateSlot(string name, Transform parent, Vector2 pos, TMP_FontAsset font)
    {
        Image bg = CreateImage(name, parent, UnlockedColor, true);
        SetRect(bg.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(200f, 250f));
        Button button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;

        TMP_Text number = CreateText("Number", bg.transform, "1-1", 48f, Color.white, font);
        number.fontStyle = FontStyles.Bold;
        SetRect(number.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(180f, 80f));

        Image lockBg = CreateImage("Lock", bg.transform, LockColor, false);
        Stretch(lockBg.rectTransform);
        TMP_Text lockLabel = CreateText("Label", lockBg.transform, "Lock", 36f, Color.white, font);
        lockLabel.fontStyle = FontStyles.Bold;
        Stretch(lockLabel.rectTransform);

        Image clearBg = CreateImage("Clear", bg.transform, ClearColor, false);
        SetRect(clearBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(150f, 44f));
        TMP_Text clearLabel = CreateText("Label", clearBg.transform, "Clear", 26f, new Color(0.25f, 0.16f, 0.05f, 1f), font);
        clearLabel.fontStyle = FontStyles.Bold;
        Stretch(clearLabel.rectTransform);
        clearBg.gameObject.SetActive(false);

        StageSlot slot = bg.gameObject.AddComponent<StageSlot>();
        SerializedObject so = new SerializedObject(slot);
        SetRef(so, "button", button);
        SetRef(so, "background", bg);
        SetRef(so, "numberLabel", number);
        SetRef(so, "lockMarker", lockBg.gameObject);
        SetRef(so, "lockLabel", lockLabel);
        SetRef(so, "clearMarker", clearBg.gameObject);
        SetRef(so, "clearLabel", clearLabel);
        SetRef(so, "challengeMarker", null);
        SerializedProperty tint = so.FindProperty("tintBackground");
        if (tint != null) tint.boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        return slot;
    }

    private static TMP_Text CreatePageBar(Transform content, TMP_FontAsset font, out Button prev, out Button next, out TMP_Text pageLabel)
    {
        RectTransform bar = CreateRect("PageBar", content);
        bar.anchorMin = new Vector2(0.5f, 0f);
        bar.anchorMax = new Vector2(0.5f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, 8f);
        bar.sizeDelta = new Vector2(1100f, 150f);

        TMP_Text title = CreateText("ChapterTitle", bar, "Chapter 1", 40f, new Color(0.25f, 0.18f, 0.10f, 1f), font);
        title.fontStyle = FontStyles.Bold;
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(500f, 48f));

        prev = CreateButton("Prev", bar, "Prev", 32f, PageButtonColor, font);
        SetRect((RectTransform)prev.transform, new Vector2(0.5f, 0f), new Vector2(-220f, 40f), new Vector2(180f, 64f));
        next = CreateButton("Next", bar, "Next", 32f, PageButtonColor, font);
        SetRect((RectTransform)next.transform, new Vector2(0.5f, 0f), new Vector2(220f, 40f), new Vector2(180f, 64f));
        pageLabel = CreateText("PageLabel", bar, "1 / 2", 32f, new Color(0.2f, 0.16f, 0.1f, 1f), font);
        SetRect(pageLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(160f, 50f));
        return title;
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

    private static void SetArray(SerializedObject so, string field, UnityEngine.Object[] values)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null || !p.isArray)
        {
            throw new Exception(so.targetObject.GetType().Name + " array not found: " + field);
        }

        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
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

    private static TMP_Text CreateText(string name, Transform parent, string text, float size, Color color, TMP_FontAsset font)
    {
        TextMeshProUGUI t = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
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
        Stretch(text.rectTransform);
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

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}