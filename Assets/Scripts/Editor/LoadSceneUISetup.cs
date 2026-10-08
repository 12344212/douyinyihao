using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 在 LoadScene 的 Canvas 下生成 Loading 界面（1920x1080 横屏），并挂 LoadSceneController、绑定全部序列化引用。
/// 层级参照参考项目 LoadScenes：bg(角色图) / Logo / JDTBG(进度条底) → JD(填充) / Start 按钮，另加 ErrorPanel(错误 + Retry)。
/// 图片全部是纯色占位 Image，之后替换 Sprite 即可；文字使用 TextMeshPro 默认字体。
/// 菜单：Tools/Template/Build LoadScene UI（已存在时询问是否重建）
/// 自动执行：项目根目录存在 LoadSceneUISetup.run 时，编辑器编译加载后执行一次并删除该标记文件（已存在 UI 时跳过）。
/// TemplateSceneSetup 生成 LoadScene 时也会调用 BuildUI，保证重新生成模板得到同样的界面。
/// </summary>
[InitializeOnLoad]
public static class LoadSceneUISetup
{
    private const string LogTag = "[LoadSceneUISetup]";
    private const string LoadScenePath = "Assets/Scenes/LoadScene.unity";
    private const string CanvasName = "Canvas";

    private const string BgName = "bg";
    private const string LogoName = "Logo";
    private const string ProgressBarName = "JDTBG";
    private const string StartName = "Start";
    private const string ErrorPanelName = "ErrorPanel";
    private static readonly string[] RootUINames = { BgName, LogoName, ProgressBarName, StartName, ErrorPanelName };

    // 占位配色
    private static readonly Color BgColor = new Color(0.10f, 0.13f, 0.20f, 1f);
    private static readonly Color CharacterColor = new Color(0.33f, 0.42f, 0.62f, 1f);
    private static readonly Color PlateColor = new Color(0.78f, 0.58f, 0.25f, 1f);
    private static readonly Color LogoColor = new Color(0.92f, 0.72f, 0.30f, 1f);
    private static readonly Color LogoTextColor = new Color(0.18f, 0.12f, 0.05f, 1f);
    private static readonly Color BarBgColor = new Color(0.05f, 0.07f, 0.11f, 1f);
    private static readonly Color BarFillColor = new Color(0.30f, 0.82f, 0.45f, 1f);
    private static readonly Color StatusColor = new Color(0.85f, 0.88f, 0.95f, 1f);
    private static readonly Color StartColor = new Color(0.96f, 0.62f, 0.18f, 1f);
    private static readonly Color ErrorBgColor = new Color(0f, 0f, 0f, 0.8f);
    private static readonly Color RetryColor = new Color(0.85f, 0.30f, 0.30f, 1f);

    private static string MarkerFilePath => Path.Combine(Directory.GetCurrentDirectory(), "LoadSceneUISetup.run");

    static LoadSceneUISetup()
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

    [MenuItem("Tools/Template/Build LoadScene UI")]
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
            if (!File.Exists(LoadScenePath))
            {
                Debug.LogError($"{LogTag} {LoadScenePath} not found. Create template scenes first.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(LoadScenePath);
            bool openedHere = false;
            if (scene.IsValid() && scene.isLoaded)
            {
                if (scene.isDirty && !interactive)
                {
                    Debug.LogWarning($"{LogTag} LoadScene has unsaved changes, auto-run skipped. Save it and use Tools/Template/Build LoadScene UI.");
                    return;
                }
            }
            else
            {
                scene = EditorSceneManager.OpenScene(LoadScenePath, OpenSceneMode.Additive);
                openedHere = true;
            }

            try
            {
                Canvas canvas = null;
                YooAssetManager yoo = null;
                LocalSaveManager save = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    if (canvas == null && roots[i].name == CanvasName)
                    {
                        canvas = roots[i].GetComponent<Canvas>();
                    }

                    if (yoo == null)
                    {
                        yoo = roots[i].GetComponentInChildren<YooAssetManager>(true);
                    }

                    if (save == null)
                    {
                        save = roots[i].GetComponentInChildren<LocalSaveManager>(true);
                    }
                }

                if (canvas == null)
                {
                    Debug.LogError($"{LogTag} Root '{CanvasName}' with Canvas not found in LoadScene.");
                    return;
                }

                if (yoo == null)
                {
                    Debug.LogError($"{LogTag} YooAssetManager not found in LoadScene.");
                    return;
                }

                if (save == null)
                {
                    Debug.LogWarning($"{LogTag} LocalSaveManager not found in LoadScene (run Tools/Template/Add LocalSaveManager To LoadScene). Field left empty.");
                }

                if (HasExistingUI(canvas))
                {
                    if (!interactive)
                    {
                        Debug.Log($"{LogTag} LoadScene UI already exists, auto-run skipped.");
                        return;
                    }

                    if (!EditorUtility.DisplayDialog("Build LoadScene UI", "LoadScene UI already exists. Rebuild it?", "Rebuild", "Cancel"))
                    {
                        return;
                    }

                    RemoveExistingUI(canvas);
                }

                BuildUI(canvas, yoo, save);
                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene))
                {
                    Debug.Log($"{LogTag} LoadScene UI built and saved.");
                }
                else
                {
                    Debug.LogError($"{LogTag} Failed to save {LoadScenePath}.");
                }
            }
            finally
            {
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

    private static bool HasExistingUI(Canvas canvas)
    {
        if (canvas.GetComponent<LoadSceneController>() != null)
        {
            return true;
        }

        for (int i = 0; i < RootUINames.Length; i++)
        {
            if (canvas.transform.Find(RootUINames[i]) != null)
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveExistingUI(Canvas canvas)
    {
        LoadSceneController ctrl = canvas.GetComponent<LoadSceneController>();
        if (ctrl != null)
        {
            UnityEngine.Object.DestroyImmediate(ctrl);
        }

        for (int i = 0; i < RootUINames.Length; i++)
        {
            Transform t;
            while ((t = canvas.transform.Find(RootUINames[i])) != null)
            {
                UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
        }
    }

    /// <summary>在指定 Canvas 下生成 Loading UI，并挂上 LoadSceneController、绑定引用。</summary>
    public static LoadSceneController BuildUI(Canvas canvas, YooAssetManager yoo, LocalSaveManager save)
    {
        Transform root = canvas.transform;
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            Debug.LogWarning($"{LogTag} TMP default font not found. Import TMP Essential Resources (Window/TextMeshPro).");
        }

        // 1. 背景 + 角色占位（参考项目：bg 下挂角色图与底座图）
        Image bg = CreateImage(BgName, root, BgColor, false);
        Stretch(bg.rectTransform, 0f);

        Image character = CreateImage("Character", bg.transform, CharacterColor, false);
        SetRect(character.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-480f, 40f), new Vector2(520f, 700f));

        Image plate = CreateImage("CharacterPlate", bg.transform, PlateColor, false);
        SetRect(plate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-480f, -360f), new Vector2(380f, 90f));

        // 2. Logo（顶部）
        Image logo = CreateImage(LogoName, root, LogoColor, false);
        SetRect(logo.rectTransform, new Vector2(0.5f, 1f), new Vector2(380f, -260f), new Vector2(880f, 240f));
        TMP_Text logoText = CreateText("LogoText", logo.transform, "GAME TITLE", 96f, LogoTextColor, font);
        logoText.fontStyle = FontStyles.Bold;
        Stretch(logoText.rectTransform, 0f);

        // 3. 进度条（底部）：JDTBG 底 → JD 填充 + 百分比 + 状态文字
        Image barBg = CreateImage(ProgressBarName, root, BarBgColor, false);
        SetRect(barBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1100f, 44f));

        Image fill = CreateImage("JD", barBg.transform, BarFillColor, false);
        Stretch(fill.rectTransform, 6f);
        fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); // Filled 类型需要 Sprite，占位用内置白图
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;

        TMP_Text progressText = CreateText("ProgressText", barBg.transform, "0%", 28f, Color.white, font);
        Stretch(progressText.rectTransform, 0f);

        TMP_Text statusText = CreateText("StatusText", barBg.transform, "Loading...", 34f, StatusColor, font);
        RectTransform statusRt = statusText.rectTransform;
        statusRt.anchorMin = new Vector2(0.5f, 1f);
        statusRt.anchorMax = new Vector2(0.5f, 1f);
        statusRt.pivot = new Vector2(0.5f, 0f);
        statusRt.anchoredPosition = new Vector2(0f, 16f);
        statusRt.sizeDelta = new Vector2(1100f, 50f);

        // 4. Start 按钮（加载完成后淡入 + 呼吸）
        TMP_Text startLabel;
        Button start = CreateButton(StartName, root, "START", 60f, StartColor, font, out startLabel);
        SetRect((RectTransform)start.transform, new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(440f, 130f));
        CanvasGroup startGroup = start.gameObject.AddComponent<CanvasGroup>();
        start.gameObject.SetActive(false);

        // 5. 错误面板（YooAsset 初始化失败时显示）
        Image errorPanel = CreateImage(ErrorPanelName, root, ErrorBgColor, true); // 拦截点击
        Stretch(errorPanel.rectTransform, 0f);
        TMP_Text errorText = CreateText("ErrorText", errorPanel.transform, "Failed to load game resources.\nPlease try again.", 44f, Color.white, font);
        SetRect(errorText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1300f, 320f));
        TMP_Text retryLabel;
        Button retry = CreateButton("RetryButton", errorPanel.transform, "RETRY", 52f, RetryColor, font, out retryLabel);
        SetRect((RectTransform)retry.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(380f, 120f));
        errorPanel.gameObject.SetActive(false);

        // 6. 控制器挂在 Canvas 上（与参考项目一致），引用全部序列化
        LoadSceneController ctrl = canvas.gameObject.AddComponent<LoadSceneController>();
        SerializedObject so = new SerializedObject(ctrl);
        SetRef(so, "yooAssetManager", yoo);
        SetRef(so, "localSaveManager", save);
        SetRef(so, "progressBarRoot", barBg.rectTransform);
        SetRef(so, "progressFillImage", fill);
        SetRef(so, "progressText", progressText);
        SetRef(so, "statusText", statusText);
        SetRef(so, "startButton", start);
        SetRef(so, "startButtonGroup", startGroup);
        SetRef(so, "startButtonLabel", startLabel);
        SetRef(so, "errorPanel", errorPanel.gameObject);
        SetRef(so, "errorText", errorText);
        SetRef(so, "retryButton", retry);
        so.FindProperty("nextSceneName").stringValue = LoadSceneController.MainSceneName;
        so.ApplyModifiedPropertiesWithoutUndo();
        TopUpPanelSetup.EnsureIap(ctrl); // LoadScene 上挂 IapPurchaseManager 并写回引用
        return ctrl;
    }

    private static void SetRef(SerializedObject so, string field, UnityEngine.Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            throw new Exception("LoadSceneController field not found: " + field);
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

    private static Button CreateButton(string name, Transform parent, string label, float fontSize, Color color, TMP_FontAsset font, out TMP_Text labelText)
    {
        Image img = CreateImage(name, parent, color, true);
        Button button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        labelText = CreateText("Text (TMP)", img.transform, label, fontSize, Color.white, font);
        labelText.fontStyle = FontStyles.Bold;
        Stretch(labelText.rectTransform, 0f);
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
}