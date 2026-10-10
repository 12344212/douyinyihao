using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Assemble UpgradePanel visuals from JS-CK (character / equip slots / stat bars / gold cost / upgrade / back).
/// Visual only, no upgrade spend logic. Menu: Tools/Template/Build Upgrade Panel UI
/// One-shot: UpgradePanelSetup.run at project root.
/// </summary>
[InitializeOnLoad]
public static class UpgradePanelSetup
{
    private const string LogTag = "[UpgradePanelSetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";

    private const string GuidCharacter = "d55fe2d4bb9e77b4880ac922d4967218";
    private const string GuidSlot1 = "8cfeaa667218ded4f80e2a72f74aaaea";
    private const string GuidSlot2 = "c89b204ffbc2ab144bc8382763d729a3";
    private const string GuidSlot3 = "6d3698167138c7445a6de34961374fd4";
    private const string GuidSlot4 = "005136cff7a7e6e4b98941f792588a87";
    private const string GuidIconAttack = "3ddecaf124e506c429f269446b3ea26f";
    private const string GuidIconRange = "76860f16993bc004cb2aaf4b408c95f2";
    private const string GuidIconDefense = "471c51e5f167929468528095c87b4441";
    private const string GuidBarBg = "52a49b4b9bdde1d47a5f9a0ecd51bc10";
    private const string GuidBarAttack = "206ef91a0106fb0498840952f6cad2e2";
    private const string GuidBarRange = "928118560cb84c24fbd8f9a3017b086d";
    private const string GuidBarDefense = "fca3594c960d85147bb119a1882ff70b";
    private const string GuidUpgradeBtn = "47e6836ea97b8c146ae47546776689e3";
    private const string GuidBack = "dab8873160ec5fb4b989047629fc47c9";
    private const string GuidGold = "1afc5e6dd14599f42a6f755fc9c67858";

    private static readonly Color ParchmentColor = new Color(0.902f, 0.788f, 0.631f, 0.96f);
    private static readonly Color LabelColor = new Color(0.22f, 0.14f, 0.08f, 1f);
    private static readonly Color ValueColor = new Color(1f, 1f, 1f, 1f);
    private static readonly Color CostColor = new Color(0.35f, 0.20f, 0.08f, 1f);

    private static string MarkerFilePath => Path.Combine(Directory.GetCurrentDirectory(), "UpgradePanelSetup.run");
    private static string LogicMarkerFilePath => Path.Combine(Directory.GetCurrentDirectory(), "UpgradePanelLogicSetup.run");

    static UpgradePanelSetup()
    {
        if (File.Exists(LogicMarkerFilePath))
        {
            EditorApplication.delayCall += TryAutoBind;
        }

        if (File.Exists(MarkerFilePath))
        {
            EditorApplication.delayCall += TryAutoRun;
        }
    }

    private static void TryAutoBind()
    {
        if (!File.Exists(LogicMarkerFilePath))
        {
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoBind;
            return;
        }

        try
        {
            File.Delete(LogicMarkerFilePath);
        }
        catch (Exception e)
        {
            Debug.LogError(LogTag + " Failed to delete logic marker: " + e.Message);
            return;
        }

        Debug.Log(LogTag + " Auto-bind triggered by logic marker.");
        RunBind(false);
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
            Debug.LogError(LogTag + " Failed to delete marker file, auto-run aborted: " + e.Message);
            return;
        }

        Debug.Log(LogTag + " Auto-run triggered by marker file.");
        Run(false);
    }

    [MenuItem("Tools/Template/Build Upgrade Panel UI")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Run(true);
    }

    [MenuItem("Tools/Template/Bind Upgrade Panel Logic")]
    public static void BindLogicMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        RunBind(true);
    }

    public static void BuildInto(UIPanel panel)
    {
        if (panel == null)
        {
            throw new Exception("UpgradePanel UIPanel is missing.");
        }

        EnsureWindowAndContent(panel);
        if (panel.Content == null || panel.Window == null)
        {
            throw new Exception("UpgradePanel Window / Content is missing after repair.");
        }

        Sprite character = LoadSprite(GuidCharacter, "JS-RW");
        Sprite slot1 = LoadSprite(GuidSlot1, "JS-ZBGZ1");
        Sprite slot2 = LoadSprite(GuidSlot2, "JS-ZBGZ2");
        Sprite slot3 = LoadSprite(GuidSlot3, "JS-ZBGZ3");
        Sprite slot4 = LoadSprite(GuidSlot4, "JS-ZBGZ4");
        Sprite iconAtk = LoadSprite(GuidIconAttack, "JS-GJ");
        Sprite iconRange = LoadSprite(GuidIconRange, "JS-FW");
        Sprite iconDef = LoadSprite(GuidIconDefense, "JS-FY");
        Sprite barBg = LoadSprite(GuidBarBg, "JS-JDTD");
        Sprite barAtk = LoadSprite(GuidBarAttack, "JS-GJJDT");
        Sprite barRange = LoadSprite(GuidBarRange, "JS-FWJDT");
        Sprite barDef = LoadSprite(GuidBarDefense, "JS-FYJDT");
        Sprite upgradeBtn = LoadSprite(GuidUpgradeBtn, "JS-SJ");
        Sprite back = LoadSprite(GuidBack, "BB-FH");
        Sprite gold = LoadSprite(GuidGold, "ZJM-JB");

        Debug.Log(LogTag + " BuildInto start on " + panel.name);
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        ClearBuilt(panel);
        PrepareWindow(panel);

        RectTransform content = panel.Content;
        StretchFull(content);

        Image parchment = CreateImage("Parchment", content, ParchmentColor, false);
        parchment.sprite = null;
        SetRect(parchment.rectTransform, new Vector2(480f, 20f), new Vector2(820f, 900f));

        Image hero = CreateSpriteImage("Character", content, character, false);
        SetRect(hero.rectTransform, new Vector2(-425f, 30f), new Vector2(335f, 697f));

        CreateEquipSlot("EquipSlot1", content, slot1, new Vector2(-756f, 239f));
        CreateEquipSlot("EquipSlot2", content, slot2, new Vector2(-752f, -137f));
        CreateEquipSlot("EquipSlot3", content, slot3, new Vector2(-88f, 239f));
        CreateEquipSlot("EquipSlot4", content, slot4, new Vector2(-88f, -137f));

        CreateStatRow("StatAttack", content, font, iconAtk, barBg, barAtk,
            "Attack", "10/108", new Vector2(223f, 315f), new Vector2(587f, 286f));
        CreateStatRow("StatRange", content, font, iconRange, barBg, barRange,
            "Range", "2.0/6.9", new Vector2(217f, 111f), new Vector2(587f, 94f));
        CreateStatRow("StatNetSpeed", content, font, iconDef, barBg, barDef,
            "Net Speed", "1.0/S/10.8/S", new Vector2(223f, -87f), new Vector2(587f, -111f));

        RectTransform cost = CreateUIObject("CostRow", content).GetComponent<RectTransform>();
        SetRect(cost, new Vector2(480f, -250f), new Vector2(360f, 80f));
        Image goldIcon = CreateSpriteImage("GoldIcon", cost, gold, false);
        SetRect(goldIcon.rectTransform, new Vector2(-90f, 0f), new Vector2(72f, 72f));
        TMP_Text costText = CreateText("CostText", cost, "999", 48f, CostColor, font, TextAlignmentOptions.Left);
        SetRect(costText.rectTransform, new Vector2(40f, 0f), new Vector2(200f, 70f));

        Image upgradeImg = CreateSpriteImage("UpgradeButton", content, upgradeBtn, true);
        SetRect(upgradeImg.rectTransform, new Vector2(521f, -352f), new Vector2(454f, 128f));
        Button upgradeButton = upgradeImg.gameObject.AddComponent<Button>();
        upgradeButton.targetGraphic = upgradeImg;
        upgradeButton.transition = Selectable.Transition.None;
        TMP_Text upgradeLabel = CreateText("Label", upgradeImg.transform, "Upgrade", 40f, Color.white, font, TextAlignmentOptions.Center);
        StretchFull(upgradeLabel.rectTransform);
        upgradeLabel.fontStyle = FontStyles.Bold;
        upgradeLabel.raycastTarget = false;

        Image backImg = CreateSpriteImage("BackButton", panel.Window, back, true);
        SetRect(backImg.rectTransform, new Vector2(-834f, -431f), new Vector2(165f, 166f));
        Button backButton = backImg.gameObject.AddComponent<Button>();
        backButton.targetGraphic = backImg;
        backButton.transition = Selectable.Transition.None;

        SerializedObject so = new SerializedObject(panel);
        SetRef(so, "closeButton", backButton);
        so.ApplyModifiedPropertiesWithoutUndo();

        WireController(panel);
    }

    /// <summary>给已有视觉节点挂 UpgradePanelController 并串引用（可单独再跑）。</summary>
    public static void WireController(UIPanel panel)
    {
        if (panel == null)
        {
            throw new Exception("UpgradePanel missing for WireController.");
        }

        EnsureWindowAndContent(panel);
        Transform root = panel.transform;

        // Visual may sit under Content or Parchment; net-speed row: StatNetSpeed / StatDefense / StatDEF
        Transform atk = FindDeep(root, "StatAttack");
        Transform range = FindDeep(root, "StatRange");
        Transform def = FindDeep(root, "StatNetSpeed");
        if (def == null)
        {
            def = FindDeep(root, "StatDefense");
        }

        if (def == null)
        {
            def = FindDeep(root, "StatDEF");
        }

        Transform cost = FindDeep(root, "CostRow");
        Transform upgradeTf = FindDeep(root, "UpgradeButton");
        if (atk == null || range == null || def == null || cost == null || upgradeTf == null)
        {
            throw new Exception("UpgradePanel visual nodes missing. Build Upgrade Panel UI first. atk="
                + (atk != null) + " range=" + (range != null) + " def=" + (def != null)
                + " cost=" + (cost != null) + " upgrade=" + (upgradeTf != null));
        }

        Image attackFill = FindImage(atk, "BarFill");
        TMP_Text attackValue = FindTmp(atk, "Value");
        TMP_Text attackName = FindTmp(atk, "Name");
        Image rangeFill = FindImage(range, "BarFill");
        TMP_Text rangeValue = FindTmp(range, "Value");
        TMP_Text rangeName = FindTmp(range, "Name");
        Image netSpeedFill = FindImage(def, "BarFill");
        TMP_Text netSpeedValue = FindTmp(def, "Value");
        TMP_Text netSpeedName = FindTmp(def, "Name");
        TMP_Text costText = FindTmp(cost, "CostText");

        if (attackName != null) { attackName.text = "Attack"; }
        if (rangeName != null) { rangeName.text = "Range"; }
        if (netSpeedName != null) { netSpeedName.text = "Net Speed"; }
        Button upgradeButton = upgradeTf.GetComponent<Button>();
        if (upgradeButton == null)
        {
            upgradeButton = upgradeTf.gameObject.AddComponent<Button>();
            Image g = upgradeTf.GetComponent<Image>();
            if (g != null)
            {
                upgradeButton.targetGraphic = g;
            }
        }

        UpgradePanelController ctrl = panel.GetComponent<UpgradePanelController>();
        if (ctrl == null)
        {
            ctrl = panel.gameObject.AddComponent<UpgradePanelController>();
        }

        SerializedObject cso = new SerializedObject(ctrl);
        SetRef(cso, "panel", panel);
        SetRef(cso, "upgradeButton", upgradeButton);
        SetRef(cso, "attackFill", attackFill);
        SetRef(cso, "attackValue", attackValue);
        SetRef(cso, "attackName", attackName);
        SetRef(cso, "rangeFill", rangeFill);
        SetRef(cso, "rangeValue", rangeValue);
        SetRef(cso, "rangeName", rangeName);
        SetRef(cso, "netSpeedFill", netSpeedFill);
        SetRef(cso, "netSpeedValue", netSpeedValue);
        SetRef(cso, "netSpeedName", netSpeedName);
        SetRef(cso, "costText", costText);
        cso.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log(LogTag + " UpgradePanelController wired.");
    }

    private static Image FindImage(Transform root, string name)
    {
        Transform t = FindDeep(root, name);
        return t != null ? t.GetComponent<Image>() : null;
    }

    private static TMP_Text FindTmp(Transform root, string name)
    {
        Transform t = FindDeep(root, name);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }


    private static void Run(bool interactive)
    {
        try
        {
            if (!File.Exists(MainScenePath))
            {
                Debug.LogError(LogTag + " " + MainScenePath + " not found.");
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
                Debug.LogWarning(LogTag + " MainScene has unsaved changes; auto-run will rebuild UpgradePanel and save.");
            }

            UIPanel panel = FindUpgradePanel(scene);
            if (panel == null)
            {
                Debug.LogError(LogTag + " UpgradePanel not found. Build MainScene UI first.");
                CloseIfOpened(scene, openedHere);
                return;
            }

            if (!interactive && HasBuiltVisual(panel))
            {
                Debug.Log(LogTag + " UpgradePanel visual already built; auto-run will rebuild anyway.");
            }

            if (interactive && HasBuiltVisual(panel))
            {
                if (!EditorUtility.DisplayDialog("Build Upgrade Panel UI", "Upgrade panel visual already exists. Rebuild it?", "Rebuild", "Cancel"))
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
                    Debug.Log(LogTag + " UpgradePanel UI assembled and saved.");
                    try
                    {
                        bool hasChar = panel.Content != null && FindDeep(panel.transform, "Character") != null;
                        bool hasBack = panel.Window != null && panel.Window.Find("BackButton") != null;
                        File.WriteAllText(
                            Path.Combine(Directory.GetCurrentDirectory(), "UpgradePanelSetup.ok"),
                            DateTime.Now.ToString("o") + " character=" + hasChar + " back=" + hasBack);
                    }
                    catch (Exception writeEx)
                    {
                        Debug.LogWarning(LogTag + " ok-file write failed: " + writeEx.Message);
                    }
                }
                else
                {
                    Debug.LogError(LogTag + " Failed to save MainScene.");
                    File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "UpgradePanelSetup.err"), "SaveScene failed");
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
        catch (Exception e)
        {
            Debug.LogError(LogTag + " " + e);
            try
            {
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "UpgradePanelSetup.err"), e.ToString());
            }
            catch
            {
            }
        }
    }


    private static void RunBind(bool interactive)
    {
        try
        {
            if (!File.Exists(MainScenePath))
            {
                Debug.LogError(LogTag + " " + MainScenePath + " not found.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(MainScenePath);
            bool openedHere = false;
            if (!(scene.IsValid() && scene.isLoaded))
            {
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
                openedHere = true;
            }

            UIPanel panel = FindUpgradePanel(scene);
            if (panel == null)
            {
                Debug.LogError(LogTag + " UpgradePanel not found.");
                CloseIfOpened(scene, openedHere);
                return;
            }

            EnsureWindowAndContent(panel);
            if (!HasBuiltVisual(panel))
            {
                Debug.LogError(LogTag + " Visual not built. Run Build Upgrade Panel UI first.");
                CloseIfOpened(scene, openedHere);
                return;
            }

            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                WireController(panel);
                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene))
                {
                    Debug.Log(LogTag + " UpgradePanel logic bound and saved.");
                    try
                    {
                        File.WriteAllText(
                            Path.Combine(Directory.GetCurrentDirectory(), "UpgradePanelSetup.ok"),
                            DateTime.Now.ToString("o") + " wired=" + (panel.GetComponent<UpgradePanelController>() != null));
                    }
                    catch
                    {
                    }
                }
                else
                {
                    Debug.LogError(LogTag + " Failed to save MainScene.");
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
        catch (Exception e)
        {
            Debug.LogError(LogTag + " " + e);
            try
            {
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "UpgradePanelSetup.err"), e.ToString());
            }
            catch
            {
            }
        }
    }
    private static void EnsureWindowAndContent(UIPanel panel)
    {
        System.Text.StringBuilder childLog = new System.Text.StringBuilder();
        childLog.Append("UpgradePanel children:");
        for (int i = 0; i < panel.transform.childCount; i++)
        {
            childLog.Append(" [").Append(panel.transform.GetChild(i).name).Append("]");
        }
        Debug.Log(LogTag + " " + childLog);

        SerializedObject so = new SerializedObject(panel);
        SerializedProperty windowProp = so.FindProperty("window");
        SerializedProperty contentProp = so.FindProperty("content");
        SerializedProperty closeProp = so.FindProperty("closeButton");
        SerializedProperty titleProp = so.FindProperty("titleText");
        SerializedProperty groupProp = so.FindProperty("canvasGroup");

        if (groupProp != null && groupProp.objectReferenceValue == null)
        {
            CanvasGroup cg = panel.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = panel.gameObject.AddComponent<CanvasGroup>();
            }

            groupProp.objectReferenceValue = cg;
        }

        RectTransform window = windowProp != null ? windowProp.objectReferenceValue as RectTransform : null;
        if (window == null)
        {
            Transform t = panel.transform.Find("Window");
            if (t == null)
            {
                // Deep search: dirty scene may have renamed/moved Window
                t = FindDeep(panel.transform, "Window");
            }

            if (t == null)
            {
                Image winImg = CreateImage("Window", panel.transform, new Color(1f, 1f, 1f, 0f), false);
                StretchFull(winImg.rectTransform);
                t = winImg.transform;
                Debug.LogWarning(LogTag + " Created missing Window under UpgradePanel.");
            }

            window = t as RectTransform;
            if (windowProp != null)
            {
                windowProp.objectReferenceValue = window;
            }
        }

        RectTransform content = contentProp != null ? contentProp.objectReferenceValue as RectTransform : null;
        if (content == null && window != null)
        {
            Transform t = window.Find("Content");
            if (t == null)
            {
                t = FindDeep(window, "Content");
            }

            if (t == null)
            {
                t = CreateUIObject("Content", window).transform;
                StretchFull(t as RectTransform);
                Debug.LogWarning(LogTag + " Created missing Content under Window.");
            }

            content = t as RectTransform;
            if (contentProp != null)
            {
                contentProp.objectReferenceValue = content;
            }
        }

        if (closeProp != null && closeProp.objectReferenceValue == null && window != null)
        {
            Transform header = window.Find("Header");
            if (header != null)
            {
                Transform close = header.Find("CloseButton");
                if (close != null)
                {
                    closeProp.objectReferenceValue = close.GetComponent<Button>();
                }
            }
        }

        if (titleProp != null && titleProp.objectReferenceValue == null && window != null)
        {
            Transform header = window.Find("Header");
            if (header != null)
            {
                Transform title = header.Find("Title");
                if (title != null)
                {
                    titleProp.objectReferenceValue = title.GetComponent<TMP_Text>();
                }
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log(LogTag + " EnsureWindowAndContent window=" + (panel.Window != null) + " content=" + (panel.Content != null));
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform c = root.GetChild(i);
            if (c.name == name)
            {
                return c;
            }

            Transform d = FindDeep(c, name);
            if (d != null)
            {
                return d;
            }
        }

        return null;
    }

    private static void PrepareWindow(UIPanel panel)
    {
        RectTransform window = panel.Window;
        StretchFull(window);
        Image winImg = window.GetComponent<Image>();
        if (winImg != null)
        {
            winImg.color = new Color(1f, 1f, 1f, 0f);
            winImg.raycastTarget = false;
            winImg.sprite = null;
        }

        Transform header = window.Find("Header");
        if (header != null)
        {
            header.gameObject.SetActive(false);
        }

        if (panel.TitleText != null)
        {
            panel.TitleText.text = "Upgrade";
        }
    }

    private static void ClearBuilt(UIPanel panel)
    {
        DestroyChild(panel.Content, "Parchment");
        DestroyChild(panel.Content, "Character");
        DestroyChild(panel.Content, "EquipSlot1");
        DestroyChild(panel.Content, "EquipSlot2");
        DestroyChild(panel.Content, "EquipSlot3");
        DestroyChild(panel.Content, "EquipSlot4");
        DestroyChild(panel.Content, "StatAttack");
        DestroyChild(panel.Content, "StatRange");
        DestroyChild(panel.Content, "StatDefense");
        DestroyChild(panel.Content, "StatNetSpeed");
        DestroyChild(panel.Content, "StatDEF");
        DestroyChild(panel.Content, "CostRow");
        DestroyChild(panel.Content, "UpgradeButton");
        DestroyChild(panel.Window, "BackButton");
    }

    private static bool HasBuiltVisual(UIPanel panel)
    {
        return panel != null
            && panel.Content != null
            && FindDeep(panel.transform, "Character") != null
            && panel.Window != null
            && panel.Window.Find("BackButton") != null;
    }

    private static void CreateEquipSlot(string name, Transform parent, Sprite sprite, Vector2 pos)
    {
        Image img = CreateSpriteImage(name, parent, sprite, true);
        SetRect(img.rectTransform, pos, new Vector2(200f, 210f));
        Button btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;
    }

    private static void CreateStatRow(
        string name,
        Transform parent,
        TMP_FontAsset font,
        Sprite iconSp,
        Sprite barBgSp,
        Sprite barFillSp,
        string label,
        string value,
        Vector2 iconPos,
        Vector2 barPos)
    {
        RectTransform row = CreateUIObject(name, parent).GetComponent<RectTransform>();
        StretchFull(row);

        Image icon = CreateSpriteImage("Icon", row, iconSp, false);
        SetRect(icon.rectTransform, iconPos, new Vector2(170f, 170f));

        TMP_Text nameText = CreateText("Name", row, label, 36f, LabelColor, font, TextAlignmentOptions.Left);
        SetRect(nameText.rectTransform, new Vector2(iconPos.x + 140f, iconPos.y + 55f), new Vector2(220f, 48f));

        Image bg = CreateSpriteImage("BarBg", row, barBgSp, false);
        SetRect(bg.rectTransform, barPos, new Vector2(550f, 93f));

        Image fill = CreateSpriteImage("BarFill", bg.transform, barFillSp, false);
        StretchFull(fill.rectTransform);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0.72f;

        TMP_Text valueText = CreateText("Value", bg.transform, value, 34f, ValueColor, font, TextAlignmentOptions.Center);
        StretchFull(valueText.rectTransform);
        valueText.fontStyle = FontStyles.Bold;
    }

    private static UIPanel FindUpgradePanel(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name != "CanvasPanel")
            {
                continue;
            }

            Transform t = roots[i].transform.Find("UpgradePanel");
            if (t != null)
            {
                return t.GetComponent<UIPanel>();
            }
        }

        return null;
    }

    private static Sprite LoadSprite(string guid, string label)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
        {
            throw new Exception("Sprite guid not found: " + label + " (" + guid + ")");
        }

        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sp == null)
        {
            throw new Exception("Sprite load failed: " + label + " @ " + path);
        }

        return sp;
    }

    private static void DestroyChild(Transform parent, string name)
    {
        if (parent == null)
        {
            return;
        }

        Transform t = parent.Find(name);
        if (t != null)
        {
            UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
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

    private static Image CreateSpriteImage(string name, Transform parent, Sprite sprite, bool raycast)
    {
        Image img = CreateUIObject(name, parent).AddComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
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

    private static void SetRect(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }
}