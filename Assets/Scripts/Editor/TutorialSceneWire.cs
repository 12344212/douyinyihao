using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 编辑器工具：在 MainScene 预置新手教程节点（TutorialPanel + Overlay + Manager），
/// 适配 StagePanel / UpgradePanel / Equit，含 BattleScene 战斗教程。
/// 菜单：Tools/Template/Wire Main Tutorial
/// 一键：项目根目录放 MainTutorialWireSetup.run，进编辑器自动跑一次。
/// </summary>
public static class TutorialSceneWire
{
    const string MainScenePath = "Assets/Scenes/MainScene.unity";
    const string BattleScenePath = "Assets/Scenes/BattleScene.unity";
    const string FontPath = "Assets/TextMesh Pro/Fonts/RuiZiTaiKongLiXianXiangSuJian-Shan(REEJI-TaikoRiskGB-Flash)-2 SDF.asset";
    const string RequestFlagPath = "Temp/REQUEST_TUTORIAL_WIRE";
    const string ReportPath = "Temp/TutorialWireReport.txt";
    const string MarkerPath = "MainTutorialWireSetup.run";
    const string BattleMarkerPath = "BattleTutorialWireSetup.run";
    const string BattleOkPath = "BattleTutorialWireSetup.ok";
    const string OkPath = "MainTutorialWireSetup.ok";

    static readonly Color ColMask = new Color(0f, 0f, 0f, 0.72f);
    static readonly Color ColRing = Hex("FFD54F");
    static readonly Color ColDialog = new Color(0.149f, 0.196f, 0.22f, 0.95f);
    static readonly Color ColPortrait = Hex("90A4AE");
    static readonly Color ColHint = Hex("B0BEC5");

    static readonly Vector2 DialogSize = new Vector2(920f, 280f);
    const float RingThickness = 8f;
    const int StepsVersion = 1;

    static readonly StringBuilder Log = new StringBuilder(4096);
    static TMP_FontAsset _font;

    [InitializeOnLoadMethod]
    static void AutoWireIfRequested()
    {
        string flag = Path.GetFullPath(Path.Combine(Application.dataPath, "../" + RequestFlagPath));
        string marker = Path.GetFullPath(Path.Combine(Application.dataPath, "../" + MarkerPath));
        if (!File.Exists(flag) && !File.Exists(marker))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    EditorApplication.delayCall += AutoWireIfRequested;
                    return;
                }

                if (File.Exists(flag))
                {
                    File.Delete(flag);
                }

                if (File.Exists(marker))
                {
                    File.Delete(marker);
                }

                Debug.Log("[TutorialSceneWire] auto-run WireTutorial");
                WireTutorial();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[TutorialSceneWire] AutoWire failed: " + e);
            }
        };
    }

    [MenuItem("Tools/Template/Wire Main Tutorial")]
    public static void WireTutorial()
    {
        Log.Clear();
        try
        {
            if (EditorApplication.isPlaying)
            {
                Log.AppendLine("ABORT: editor is in Play Mode");
                return;
            }

            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Log.AppendLine("font=" + (_font != null ? _font.name : "MISSING"));
            ProcessMainScene();
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../" + OkPath)),
                System.DateTime.Now.ToString("o") + "\n" + Log, Encoding.UTF8);
        }
        catch (System.Exception e)
        {
            Log.AppendLine("EXCEPTION: " + e);
            Debug.LogError("[TutorialSceneWire] " + e);
        }
        finally
        {
            string report = Log.ToString();
            Debug.Log("[TutorialSceneWire]\n" + report);
            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../" + ReportPath));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, report, Encoding.UTF8);
        }
    }

    static void ProcessMainScene()
    {
        Log.AppendLine("---- " + MainScenePath);
        bool openedHere = false;
        Scene scene = SceneManager.GetSceneByPath(MainScenePath);
        try
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                if (scene.isDirty)
                {
                    Log.AppendLine("SKIPPED: scene is open with unsaved changes; save it and run again");
                    return;
                }
            }
            else
            {
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
                openedHere = true;
            }

            Log.AppendLine("openedHere=" + openedHere + " dirtyBefore=" + scene.isDirty);
            if (!WireMain(scene))
            {
                Log.AppendLine("saved=False (wiring failed)");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Log.AppendLine("saved=" + saved);
        }
        finally
        {
            if (openedHere && scene.IsValid() && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    static bool WireMain(Scene scene)
    {
        GameObject canvasMax = FindRoot(scene, "CanvasMax");
        if (canvasMax == null)
        {
            Log.AppendLine("ERROR: root CanvasMax not found");
            return false;
        }

        Transform panel = EnsurePanel(canvasMax.transform, out TutorialOverlay overlay);
        MainTutorialTargets provider = panel.GetComponent<MainTutorialTargets>();
        if (provider == null)
        {
            provider = panel.gameObject.AddComponent<MainTutorialTargets>();
        }

        // Equit list root: UpgradePanel/.../Equit/E
        Transform equitList = FindDeep(scene, "Equit");
        if (equitList != null)
        {
            Transform e = equitList.Find("E");
            if (e == null)
            {
                e = FindDeepUnder(equitList, "E");
            }

            equitList = e != null ? e : equitList;
        }

        SerializedObject pso = new SerializedObject(provider);
        SetRef(pso, "equitListRoot", equitList);
        SerializedProperty spearProp = pso.FindProperty("starterSpearId");
        if (spearProp != null)
        {
            spearProp.stringValue = GameItemDefs.IronPikeId;
        }

        pso.ApplyModifiedPropertiesWithoutUndo();
        Log.AppendLine("MainTutorialTargets equitListRoot=" + (equitList != null) + " path=" + (equitList != null ? PathOf(equitList) : "null"));

        // --- main：引导进 Stage 1-1 ---
        TutorialManager main = EnsureManager(panel, TutorialSession.MainKey, overlay, provider, out SerializedObject mso);
        StepsAction mainAction = GetStepsAction(mso);
        if (mainAction == StepsAction.Patch)
        {
            PatchKeepBlocking(mso, true);
            EndSteps(mso, main);
        }
        else if (mainAction == StepsAction.Full)
        {
            SerializedProperty steps = BeginSteps(mso);
            Transform stageBtn = ResolveMenuTarget(scene, "Stage");
            Transform slot1 = FindDeep(scene, "Slot1");
            // Prefer Slot1 under StagePanel
            Transform stagePanel = FindDeep(scene, "StagePanel");
            if (stagePanel != null)
            {
                Transform under = FindDeepUnder(stagePanel, "Slot1");
                if (under != null)
                {
                    slot1 = under;
                }
            }

            AddStep(steps, "Main 1: open Stages", stageBtn, null,
                "Tap Stages to choose a battle.", TutorialAdvance.Auto, null, null, 0f, false);
            AddStep(steps, "Main 2: Stage 1-1", slot1, null,
                "Pick Stage 1-1 to start your first battle.", TutorialAdvance.Auto, null, null, 0.15f, false);
            ConfigureManager(mso, 0.5f, new string[0], true, new GameObject[0], -1, true);
            EndSteps(mso, main);
        }

        // --- role：回 Main 后升级角色一次 + 装备 Iron Pike ---
        TutorialManager upgrade = EnsureManager(panel, TutorialSession.RoleKey, overlay, provider, out SerializedObject uso);
        // Remove legacy "upgrade"-keyed managers from earlier wire.
        TutorialManager[] allMgr = panel.GetComponents<TutorialManager>();
        for (int mi = 0; mi < allMgr.Length; mi++)
        {
            SerializedObject probe = new SerializedObject(allMgr[mi]);
            SerializedProperty kp = probe.FindProperty("tutorialKey");
            if (kp != null && kp.stringValue == "upgrade" && allMgr[mi] != upgrade)
            {
                Object.DestroyImmediate(allMgr[mi]);
                Log.AppendLine("removed legacy TutorialManager key=upgrade");
            }
        }
        StepsAction upAction = GetStepsAction(uso);
        if (upAction == StepsAction.Patch)
        {
            PatchKeepBlocking(uso, false);
            EndSteps(uso, upgrade);
        }
        else if (upAction == StepsAction.Full)
        {
            SerializedProperty steps = BeginSteps(uso);
            Transform upgradeBtn = ResolveMenuTarget(scene, "Upgrade");
            Transform upgradePanel = FindDeep(scene, "UpgradePanel");
            Transform charUpgrade = upgradePanel != null ? FindDeepUnder(upgradePanel, "UpgradeButton") : FindDeep(scene, "UpgradeButton");
            Transform spearSlot = upgradePanel != null ? FindDeepUnder(upgradePanel, "EquipSlot3") : FindDeep(scene, "EquipSlot3");
            Transform zb = null;
            Transform equitInfo = upgradePanel != null ? FindDeepUnder(upgradePanel, "EquitInfo") : FindDeep(scene, "EquitInfo");
            if (equitInfo != null)
            {
                zb = FindDeepUnder(equitInfo, "ZB");
            }

            AddStep(steps, "Role 1: open UpgradePanel", upgradeBtn, null,
                "Tap Upgrade to strengthen your hero.", TutorialAdvance.Auto, null, null, 0.3f, false);
            AddStep(steps, "Role 2: upgrade character once", charUpgrade, null,
                "Tap Upgrade once to raise your stats.", TutorialAdvance.Auto, null, null, 0.2f, true,
                "upgrade.alreadyUpgraded");
            AddStep(steps, "Role 3: open spear slot", spearSlot, null,
                "Tap the spear slot to open your equipment.", TutorialAdvance.Auto, null, null, 0.2f, false,
                "upgrade.spearEquipped");
            AddStep(steps, "Role 4: select Iron Pike", null, "upgrade.spearItem",
                "Tap your Iron Pike.", TutorialAdvance.Auto, null, null, 0.2f, false,
                "upgrade.spearEquipped");
            AddStep(steps, "Role 5: equip spear", zb, null,
                "Tap Equip to arm your spear. You're ready!", TutorialAdvance.Auto, null, null, 0.2f, false,
                "upgrade.spearEquipped");
            Transform character = upgradePanel != null ? FindDeepUnder(upgradePanel, "Character") : FindDeep(scene, "Character");
            AddStep(steps, "Role 6: close Equit", character, null,
                "Tap your character to close the equipment list.", TutorialAdvance.Auto, null, null, 0.2f, false,
                "upgrade.equitClosed");
            ConfigureManager(uso, 0.8f, new[] { TutorialSession.MainKey, TutorialSession.BattleKey }, true, new GameObject[0], -1, false);
            EndSteps(uso, upgrade);
        }

        // Tip canvas stays above (order 25); move TutorialPanel last under CanvasMax
        panel.SetAsLastSibling();
        return true;
    }

    /// <summary>主菜单按钮：优先子节点 T（可点），否则按钮本体。</summary>
    static Transform ResolveMenuTarget(Scene scene, string rootName)
    {
        Transform root = FindPath(scene, "Canvas/" + rootName);
        if (root == null)
        {
            root = FindDeep(scene, rootName);
        }

        if (root == null)
        {
            return null;
        }

        Transform t = root.Find("T");
        if (t != null && t.GetComponent<Button>() != null)
        {
            return t;
        }

        return root;
    }

    enum StepsAction
    {
        Full,
        Patch,
        Skip
    }

    static StepsAction GetStepsAction(SerializedObject so)
    {
        SerializedProperty ver = so.FindProperty("wiredStepsVersion");
        SerializedProperty steps = so.FindProperty("steps");
        int current = ver != null ? ver.intValue : 0;
        int count = steps != null ? steps.arraySize : 0;
        if (count == 0)
        {
            return StepsAction.Full;
        }

        if (current < StepsVersion)
        {
            return StepsAction.Patch;
        }

        Log.AppendLine("  steps already at version " + current + " count=" + count + " (skip rewrite)");
        return StepsAction.Skip;
    }

    static void PatchKeepBlocking(SerializedObject so, bool keep)
    {
        SerializedProperty p = so.FindProperty("keepBlockingAfterFinish");
        if (p != null)
        {
            p.boolValue = keep;
        }

        SerializedProperty ver = so.FindProperty("wiredStepsVersion");
        if (ver != null)
        {
            ver.intValue = StepsVersion;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        Log.AppendLine("  patched keepBlockingAfterFinish=" + keep + " version=" + StepsVersion);
    }

    static SerializedProperty BeginSteps(SerializedObject so)
    {
        SerializedProperty steps = so.FindProperty("steps");
        steps.ClearArray();
        return steps;
    }

    static void EndSteps(SerializedObject so, TutorialManager mgr)
    {
        SerializedProperty ver = so.FindProperty("wiredStepsVersion");
        if (ver != null)
        {
            ver.intValue = StepsVersion;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mgr);
    }

    static void ConfigureManager(SerializedObject so, float startDelay, string[] requiredKeys, bool resume,
        GameObject[] hideWhileRunning, int releaseAtStep, bool keepBlocking)
    {
        so.FindProperty("startDelay").floatValue = startDelay;
        so.FindProperty("autoStart").boolValue = true;
        so.FindProperty("resumeFromSavedStep").boolValue = resume;
        so.FindProperty("releaseAtStep").intValue = releaseAtStep;
        so.FindProperty("keepBlockingAfterFinish").boolValue = keepBlocking;
        so.FindProperty("keepBlockingTimeout").floatValue = 5f;

        SerializedProperty req = so.FindProperty("requiredDoneKeys");
        req.ClearArray();
        if (requiredKeys != null)
        {
            for (int i = 0; i < requiredKeys.Length; i++)
            {
                req.InsertArrayElementAtIndex(i);
                req.GetArrayElementAtIndex(i).stringValue = requiredKeys[i];
            }
        }

        SerializedProperty hide = so.FindProperty("hideWhileRunning");
        hide.ClearArray();
        if (hideWhileRunning != null)
        {
            for (int i = 0; i < hideWhileRunning.Length; i++)
            {
                hide.InsertArrayElementAtIndex(i);
                hide.GetArrayElementAtIndex(i).objectReferenceValue = hideWhileRunning[i];
            }
        }
    }

    static TutorialManager EnsureManager(Transform panel, string key, TutorialOverlay overlay,
        TutorialTargetProvider provider, out SerializedObject so)
    {
        TutorialManager[] all = panel.GetComponents<TutorialManager>();
        TutorialManager found = null;
        for (int i = 0; i < all.Length; i++)
        {
            SerializedObject probe = new SerializedObject(all[i]);
            SerializedProperty keyProp = probe.FindProperty("tutorialKey");
            if (keyProp != null && keyProp.stringValue == key)
            {
                found = all[i];
                break;
            }
        }

        if (found == null)
        {
            found = panel.gameObject.AddComponent<TutorialManager>();
        }

        so = new SerializedObject(found);
        so.FindProperty("tutorialKey").stringValue = key;
        SetRef(so, "overlay", overlay);
        SetRef(so, "targetProvider", provider);
        so.ApplyModifiedPropertiesWithoutUndo();
        Log.AppendLine("TutorialManager key=" + key + " ok");
        return found;
    }

    static Transform EnsurePanel(Transform canvasMax, out TutorialOverlay overlay)
    {
        Transform panel = canvasMax.Find("TutorialPanel");
        bool created = panel == null;
        if (created)
        {
            panel = NewUI("TutorialPanel", canvasMax).transform;
            Stretch(panel as RectTransform);
        }

        // Blocker
        Transform blockerT = EnsureChild(panel, "Blocker", out bool blockerCreated);
        Image blockerImg = EnsureImage(blockerT.gameObject, new Color(0f, 0f, 0f, 0.01f), blockerCreated);
        blockerImg.raycastTarget = true;
        Stretch(blockerT as RectTransform);
        blockerT.gameObject.SetActive(false);

        Transform overlayRoot = EnsureChild(panel, "Overlay", out bool overlayCreated);
        Stretch(overlayRoot as RectTransform);

        RectTransform maskTop = EnsureMask(overlayRoot, "MaskTop", Vector2.zero, new Vector2(100f, 100f));
        RectTransform maskBottom = EnsureMask(overlayRoot, "MaskBottom", Vector2.zero, new Vector2(100f, 100f));
        RectTransform maskLeft = EnsureMask(overlayRoot, "MaskLeft", Vector2.zero, new Vector2(100f, 100f));
        RectTransform maskRight = EnsureMask(overlayRoot, "MaskRight", Vector2.zero, new Vector2(100f, 100f));

        Transform ringT = EnsureChild(overlayRoot, "Ring", out bool ringCreated);
        RectTransform ring = ringT as RectTransform;
        if (ringCreated)
        {
            Place(ring, Vector2.zero, new Vector2(200f, 200f));
            EnsureRingEdge(ring, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, RingThickness));
            EnsureRingEdge(ring, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, RingThickness));
            EnsureRingEdge(ring, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(RingThickness, 0f));
            EnsureRingEdge(ring, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(RingThickness, 0f));
        }

        Transform dialogT = EnsureChild(overlayRoot, "Dialog", out bool dialogCreated);
        RectTransform dialog = dialogT as RectTransform;
        Image dialogBg = EnsureImage(dialogT.gameObject, ColDialog, dialogCreated);
        dialogBg.raycastTarget = false;
        if (dialogCreated)
        {
            Place(dialog, new Vector2(0f, -280f), DialogSize);
        }

        Transform portraitT = EnsureChild(dialogT, "Portrait", out bool portraitCreated);
        Image portrait = EnsureImage(portraitT.gameObject, ColPortrait, portraitCreated);
        portrait.raycastTarget = false;
        if (portraitCreated)
        {
            Place(portraitT as RectTransform, new Vector2(-360f, 0f), new Vector2(160f, 160f));
        }

        TMP_Text body = EnsureText(dialogT, "Text", "Tutorial", 36f, TextAlignmentOptions.Left, Color.white,
            new Vector2(80f, 10f), new Vector2(640f, 200f));
        TMP_Text hint = EnsureText(dialogT, "Hint", "Tap to continue", 26f, TextAlignmentOptions.BottomRight, ColHint,
            new Vector2(250f, -110f), new Vector2(360f, 40f));

        overlayRoot.gameObject.SetActive(false);

        overlay = panel.GetComponent<TutorialOverlay>();
        if (overlay == null)
        {
            overlay = panel.gameObject.AddComponent<TutorialOverlay>();
        }

        SerializedObject oso = new SerializedObject(overlay);
        SetRef(oso, "canvasRect", panel as RectTransform);
        SetRef(oso, "overlayRoot", overlayRoot.gameObject);
        SetRef(oso, "maskTop", maskTop);
        SetRef(oso, "maskBottom", maskBottom);
        SetRef(oso, "maskLeft", maskLeft);
        SetRef(oso, "maskRight", maskRight);
        SetRef(oso, "ring", ring);
        SetRef(oso, "dialog", dialog);
        SetRef(oso, "portraitImage", portrait);
        SetRef(oso, "bodyText", body);
        SetRef(oso, "tapHint", hint.gameObject);
        SetRef(oso, "blocker", blockerT.gameObject);
        oso.ApplyModifiedPropertiesWithoutUndo();

        BindStrip(maskTop, overlay);
        BindStrip(maskBottom, overlay);
        BindStrip(maskLeft, overlay);
        BindStrip(maskRight, overlay);

        Log.AppendLine("TutorialPanel created=" + created);
        return panel;
    }

    static void AddStep(SerializedProperty steps, string note, Transform target, string targetKey, string text,
        TutorialAdvance advance, string eventKey, string waitFor, float showDelay, bool skipOnReplay,
        string skipWhen = null, bool skipIfTargetGone = false, bool freePlay = false, string conditionKey = null)
    {
        if (!freePlay && target == null && string.IsNullOrEmpty(targetKey) && advance != TutorialAdvance.Tap)
        {
            Log.AppendLine("  WARN: step '" + note + "' target missing; kept as Tap text-only");
            advance = TutorialAdvance.Tap;
        }

        int i = steps.arraySize;
        steps.InsertArrayElementAtIndex(i);
        SerializedProperty s = steps.GetArrayElementAtIndex(i);
        s.FindPropertyRelative("note").stringValue = note;
        s.FindPropertyRelative("target").objectReferenceValue = target as RectTransform;
        s.FindPropertyRelative("targetKey").stringValue = targetKey ?? string.Empty;
        s.FindPropertyRelative("text").stringValue = text;
        s.FindPropertyRelative("portrait").objectReferenceValue = null;
        s.FindPropertyRelative("advance").enumValueIndex = (int)advance;
        s.FindPropertyRelative("eventKey").stringValue = eventKey ?? string.Empty;
        s.FindPropertyRelative("waitFor").stringValue = waitFor ?? string.Empty;
        s.FindPropertyRelative("showDelay").floatValue = showDelay;
        s.FindPropertyRelative("skipOnReplay").boolValue = skipOnReplay;
        s.FindPropertyRelative("skipWhen").stringValue = skipWhen ?? string.Empty;
        s.FindPropertyRelative("skipIfTargetGone").boolValue = skipIfTargetGone;
        s.FindPropertyRelative("holePadding").floatValue = 16f;
        s.FindPropertyRelative("freePlay").boolValue = freePlay;
        s.FindPropertyRelative("conditionKey").stringValue = conditionKey ?? string.Empty;

        string where = freePlay ? "(freePlay)" : target != null ? PathOf(target) : (!string.IsNullOrEmpty(targetKey) ? "key:" + targetKey : "(text only)");
        Log.AppendLine("  step[" + i + "] " + note + " -> " + where + " advance=" + advance
            + (string.IsNullOrEmpty(skipWhen) ? "" : " skipWhen=" + skipWhen));
    }

    static RectTransform EnsureMask(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        Transform t = EnsureChild(parent, name, out bool created);
        Image img = EnsureImage(t.gameObject, ColMask, created);
        img.raycastTarget = true;
        if (created)
        {
            Place(t as RectTransform, pos, size);
        }

        return t as RectTransform;
    }

    static void EnsureRingEdge(RectTransform ring, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 size)
    {
        Transform t = EnsureChild(ring, name, out bool created);
        Image img = EnsureImage(t.gameObject, ColRing, created);
        img.raycastTarget = false;
        if (created)
        {
            RectTransform rt = t as RectTransform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
        }
    }

    static void BindStrip(RectTransform strip, TutorialOverlay overlay)
    {
        TutorialMaskStrip s = strip.GetComponent<TutorialMaskStrip>();
        if (s == null)
        {
            s = strip.gameObject.AddComponent<TutorialMaskStrip>();
        }

        SerializedObject so = new SerializedObject(s);
        SetRef(so, "overlay", overlay);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static TMP_Text EnsureText(Transform parent, string name, string text, float size, TextAlignmentOptions align, Color color, Vector2 pos, Vector2 box)
    {
        Transform t = EnsureChild(parent, name, out bool created);
        TextMeshProUGUI tmp = t.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            tmp = t.gameObject.AddComponent<TextMeshProUGUI>();
            created = true;
        }

        if (created)
        {
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = color;
            tmp.enableWordWrapping = true;
            if (_font != null)
            {
                tmp.font = _font;
                tmp.fontSharedMaterial = _font.material;
            }

            Place(t as RectTransform, pos, box);
        }

        tmp.raycastTarget = false;
        return tmp;
    }

    static Image EnsureImage(GameObject go, Color color, bool setColor)
    {
        Image img = go.GetComponent<Image>();
        if (img == null)
        {
            img = go.AddComponent<Image>();
            setColor = true;
        }

        if (setColor)
        {
            img.color = color;
        }

        return img;
    }

    static Transform EnsureChild(Transform parent, string name, out bool created)
    {
        Transform t = parent.Find(name);
        created = t == null;
        if (created)
        {
            t = NewUI(name, parent).transform;
        }

        return t;
    }

    static GameObject NewUI(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
    }

    static RectTransform FindPath(Scene scene, string path)
    {
        int slash = path.IndexOf('/');
        string rootName = slash < 0 ? path : path.Substring(0, slash);
        GameObject root = FindRoot(scene, rootName);
        if (root == null)
        {
            Log.AppendLine("WARN: target root not found: " + path);
            return null;
        }

        Transform t = slash < 0 ? root.transform : root.transform.Find(path.Substring(slash + 1));
        if (t == null)
        {
            Log.AppendLine("WARN: target not found: " + path);
            return null;
        }

        return t as RectTransform;
    }

    static Transform FindDeep(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindDeepUnder(roots[i].transform, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    static Transform FindDeepUnder(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == name)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepUnder(parent.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == name)
            {
                return roots[i];
            }
        }

        return null;
    }

    static void SetRef(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Log.AppendLine("WARN: field not found " + so.targetObject.GetType().Name + "." + field);
            return;
        }

        p.objectReferenceValue = value;
    }

    static string PathOf(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        return path;
    }

    static Color Hex(string hex)
    {
        Color c;
        ColorUtility.TryParseHtmlString("#" + hex, out c);
        return c;
    }
}