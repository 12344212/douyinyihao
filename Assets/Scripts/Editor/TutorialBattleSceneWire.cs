#if UNITY_EDITOR
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Editor tool: wire BattleScene TutorialPanel + battle TutorialManager steps (raft combat).
/// Menu: Tools/Template/Wire Battle Tutorial
/// One-shot: place BattleTutorialWireSetup.run at project root, focus Editor.
/// BACKUP: copies BattleScene.unity to Temp/BattleScene.unity.bak before save.
/// </summary>
public static class TutorialBattleSceneWire
{
    const string BattleScenePath = "Assets/Scenes/BattleScene.unity";
    const string FontPath = "Assets/TextMesh Pro/Fonts/RuiZiTaiKongLiXianXiangSuJian-Shan(REEJI-TaikoRiskGB-Flash)-2 SDF.asset";
    const string MarkerPath = "BattleTutorialWireSetup.run";
    const string OkPath = "BattleTutorialWireSetup.ok";
    const string ReportPath = "Temp/BattleTutorialWireReport.txt";
    const string BackupPath = "Temp/BattleScene.unity.bak";
    const int StepsVersion = 2;

    static readonly Color ColMask = new Color(0f, 0f, 0f, 0.72f);
    static readonly Color ColRing = Hex("FFD54F");
    static readonly Color ColDialog = new Color(0.149f, 0.196f, 0.22f, 0.95f);
    static readonly Color ColPortrait = Hex("90A4AE");
    static readonly Color ColHint = Hex("B0BEC5");
    static readonly Vector2 DialogSize = new Vector2(920f, 280f);
    const float RingThickness = 8f;

    static readonly StringBuilder Log = new StringBuilder(8192);
    static TMP_FontAsset _font;

    [InitializeOnLoadMethod]
    static void AutoWireIfRequested()
    {
        string marker = Path.GetFullPath(Path.Combine(Application.dataPath, "../" + MarkerPath));
        if (!File.Exists(marker))
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

                if (File.Exists(marker))
                {
                    File.Delete(marker);
                }

                Debug.Log("[TutorialBattleSceneWire] auto-run WireBattleTutorial");
                WireBattleTutorial();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[TutorialBattleSceneWire] AutoWire failed: " + e);
            }
        };
    }

    [MenuItem("Tools/Template/Wire Battle Tutorial")]
    public static void WireBattleTutorial()
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
            ProcessBattleScene();
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../" + OkPath)),
                System.DateTime.Now.ToString("o") + "\n" + Log, Encoding.UTF8);
        }
        catch (System.Exception e)
        {
            Log.AppendLine("EXCEPTION: " + e);
            Debug.LogError("[TutorialBattleSceneWire] " + e);
        }
        finally
        {
            string report = Log.ToString();
            Debug.Log("[TutorialBattleSceneWire]\n" + report);
            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../" + ReportPath));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            File.WriteAllText(outPath, report, Encoding.UTF8);
        }
    }

    static void ProcessBattleScene()
    {
        Log.AppendLine("---- " + BattleScenePath);
        string full = Path.GetFullPath(Path.Combine(Application.dataPath, "../" + BattleScenePath));
        string bak = Path.GetFullPath(Path.Combine(Application.dataPath, "../" + BackupPath));
        Directory.CreateDirectory(Path.GetDirectoryName(bak));
        File.Copy(full, bak, true);
        Log.AppendLine("backup=" + bak);

        bool openedHere = false;
        Scene scene = SceneManager.GetSceneByPath(BattleScenePath);
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
                scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Additive);
                openedHere = true;
            }

            if (!WireBattle(scene))
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

    static bool WireBattle(Scene scene)
    {
        GameObject canvas = FindRoot(scene, "CanvasPanel");
        if (canvas == null)
        {
            Log.AppendLine("ERROR: root CanvasPanel not found");
            return false;
        }

        Transform panel = EnsurePanel(canvas.transform, out TutorialOverlay overlay);
        BattleTutorialTargets provider = panel.GetComponent<BattleTutorialTargets>();
        if (provider == null)
        {
            provider = panel.gameObject.AddComponent<BattleTutorialTargets>();
        }

        BattleDirector director = Object.FindObjectOfType<BattleDirector>();
        BattleBuildShop shop = Object.FindObjectOfType<BattleBuildShop>(true);
        BattleHudView hud = Object.FindObjectOfType<BattleHudView>(true);
        BattleResultPanel result = Object.FindObjectOfType<BattleResultPanel>(true);

        SerializedObject pso = new SerializedObject(provider);
        SetRef(pso, "director", director);
        SetRef(pso, "buildShop", shop);
        SetRef(pso, "hud", hud);
        SetRef(pso, "resultPanel", result);

        // Shop buttons from BattleBuildShop serialized fields
        if (shop != null)
        {
            SerializedObject sso = new SerializedObject(shop);
            Button open = sso.FindProperty("openButton").objectReferenceValue as Button;
            SetRef(pso, "shopOpenButton", open != null ? open.transform as RectTransform : null);
            SetRef(pso, "plankBuyButton", GoRect(sso, "plankButtonObject"));
            SetRef(pso, "captureBuyButton", GoRect(sso, "captureButtonObject"));
            SetRef(pso, "attackBuyButton", GoRect(sso, "attackButtonObject"));
        }

        SetRef(pso, "spearButton", FindDeep(scene, "SpearButton") as RectTransform);
        SetRef(pso, "netButton", FindDeep(scene, "NetButton") as RectTransform);
        SetRef(pso, "hpRoot", FindDeep(scene, "Hp") as RectTransform);
        SetRef(pso, "woodText", FindDeep(scene, "WoodText") as RectTransform);
        SetRef(pso, "leafText", FindDeep(scene, "LeafText") as RectTransform);
        SetRef(pso, "xpLabel", FindDeep(scene, "XpLabel") as RectTransform);

        // Level card 0: first "buff" under LevelPanel
        Transform levelPanel = FindDeep(scene, "LevelPanel");
        Transform buff0 = levelPanel != null ? FindDeepUnder(levelPanel, "buff") : FindDeep(scene, "buff");
        SetRef(pso, "levelCard0", buff0 as RectTransform);

        // Win Home under Reslut/Win
        Transform reslut = FindDeep(scene, "Reslut");
        Transform winHome = null;
        if (reslut != null)
        {
            Transform win = FindDeepUnder(reslut, "Win");
            if (win != null)
            {
                winHome = FindDeepUnder(win, "Home");
            }
        }

        SetRef(pso, "winHomeButton", winHome as RectTransform);
        pso.ApplyModifiedPropertiesWithoutUndo();
        Log.AppendLine("BattleTutorialTargets director=" + (director != null) + " shop=" + (shop != null)
            + " spear=" + (FindDeep(scene, "SpearButton") != null) + " home=" + (winHome != null));

        TutorialManager battle = EnsureManager(panel, TutorialSession.BattleKey, overlay, provider, out SerializedObject bso);
        // BattleScene must not keep leftover main/role TutorialManagers (wrong key/steps block battle flow).
        StripNonBattleManagers(panel, TutorialSession.BattleKey);
        SerializedProperty battleOnly = bso.FindProperty("battleTutorialOnly");
        if (battleOnly != null)
        {
            battleOnly.boolValue = true;
        }

        Transform pause = FindDeep(scene, "Pause");
        GameObject[] hide = pause != null ? new[] { pause.gameObject } : new GameObject[0];

        StepsAction action = GetStepsAction(bso);
        if (action == StepsAction.Patch)
        {
            PatchKeepBlocking(bso, true);
            EndSteps(bso, battle);
        }
        else if (action == StepsAction.Full)
        {
            SerializedProperty steps = BeginSteps(bso);
            // 1 Guide click resource
            AddStep(steps, "B1 click resource", null, "battle.resource.inRange",
                "Tap the resource inside your circle, then wait for the net to catch it.",
                TutorialAdvance.Event, TutorialBattleHooks.EvResourceCaptured, "battle.guideResourceReady", 0.4f, false);
            // 2 Hold to reel. World clicks pass through the overlay.
            AddStep(steps, "B2 reel in", null, "battle.net.player",
                "Hold to reel the net back and bring the resource in.",
                TutorialAdvance.Event, TutorialBattleHooks.EvResourceDelivered, "battle.resourceCaptured", 0.1f, false);
            // 3 Already delivered by B2; skip straight to the shop step.
            AddStep(steps, "B3 reel in", null, null,
                "Hold to reel the net back and collect your loot.",
                TutorialAdvance.Auto, null, null, 0f, false, TutorialBattleHooks.EvResourceDelivered, false, false, null);
            // 4 Tap the upgrade button. Skip if the shop is already open.
            AddStep(steps, "B4 open shop", null, "battle.ui.shopOpen",
                "Tap the upgrade button to open the shop.",
                TutorialAdvance.Auto, null, "battle.economyReady", 0.2f, false, "battle.shopOpen");
            // 4b Buy a plank after the shop is open.
            AddStep(steps, "B4 buy plank", null, "battle.ui.plankBuy",
                "Buy a plank.",
                TutorialAdvance.Auto, null, "battle.shopOpen", 0.2f, false);
            AddStep(steps, "B4 place plank", null, "battle.build.ghost",
                "Tap a highlighted spot to place the plank.",
                TutorialAdvance.Event, TutorialBattleHooks.EvPlankBuilt, null, 0.1f, false);
            // 5 Buy then place the capture tower.
            AddStep(steps, "B5 buy capture", null, "battle.ui.captureBuy",
                "Buy a Capture Tower.",
                TutorialAdvance.Auto, null, null, 0.2f, false);
            AddStep(steps, "B5 place capture", null, "battle.build.emptyPlank",
                "Tap an empty plank to place the Capture Tower.",
                TutorialAdvance.Event, TutorialBattleHooks.EvCaptureBuilt, null, 0.1f, false);
            // 6 Watch the tower haul a resource.
            AddStep(steps, "B6 wait harvest", null, "battle.resource.harvest",
                "Wait for the Capture Tower to bring that resource in.",
                TutorialAdvance.Event, TutorialBattleHooks.EvCaptureHarvested, "battle.harvestReady", 0.2f, false);
            // 7 Force level-up + pick
            AddStep(steps, "B7 pick upgrade", null, "battle.ui.levelCard0",
                "Level up! Pick one upgrade.",
                TutorialAdvance.Event, TutorialBattleHooks.EvUpgradePicked, "battle.forceLevelUp", 0.2f, false);
            // 8 Showcase HUD
            AddStep(steps, "B8 showcase HP", null, "battle.ui.hp",
                "This is your HP. Keep it above zero!",
                TutorialAdvance.Tap, null, null, 0.15f, true);
            AddStep(steps, "B8b showcase resources", null, "battle.ui.wood",
                "Wood and iron (leaf) are spent in the build shop.",
                TutorialAdvance.Tap, null, null, 0.1f, true);
            AddStep(steps, "B8c showcase XP", null, "battle.ui.xp",
                "Gather resources to earn XP and level up.",
                TutorialAdvance.Tap, null, null, 0.1f, true);
            // 9 Spawn enemy + spear
            AddStep(steps, "B9 switch spear", null, "battle.ui.spear",
                "An enemy is near! Switch to your spear.",
                TutorialAdvance.Event, TutorialBattleHooks.EvWeaponSpear, "battle.spawnEnemy1", 0.2f, false);
            // 10 Kill enemy
            AddStep(steps, "B10 kill enemy", null, "battle.enemy.first",
                "Throw your spear at the enemy.",
                TutorialAdvance.Event, TutorialBattleHooks.EvEnemyKilled, null, 0.1f, false);
            // 12 Buy then place the attack tower. Meat pickup is not part of the tutorial.
            AddStep(steps, "B12 buy attack", null, "battle.ui.attackBuy",
                "Buy an Attack Tower.",
                TutorialAdvance.Auto, null, "battle.economyReady", 0.2f, false);
            AddStep(steps, "B12 place attack", null, "battle.build.emptyPlank",
                "Tap an empty plank to place the Attack Tower.",
                TutorialAdvance.Event, TutorialBattleHooks.EvAttackBuilt, null, 0.1f, false);
            // 13 Second enemy. Event, not freePlay, so the first kill does not skip it.
            AddStep(steps, "B13 spawn near tower", null, "battle.enemy.first",
                "Another enemy is here. Let your tower defeat it.",
                TutorialAdvance.Event, TutorialBattleHooks.EvEnemyKilled, "battle.spawnEnemy2", 0.2f, false);
            // 14 Victory panel, then Home. Completing battle unlocks the role tutorial on MainScene.
            AddStep(steps, "B14 return home", null, "battle.ui.home",
                "Victory! Tap Home to return to the main menu.",
                TutorialAdvance.Auto, null, "battle.forceVictory", 0.45f, false);

            ConfigureManager(bso, 0.6f, new[] { TutorialSession.MainKey }, false, hide, -1, true);
            if (battleOnly != null)
            {
                battleOnly.boolValue = true;
            }

            EndSteps(bso, battle);
        }
        else
        {
            // Still ensure battleTutorialOnly
            if (battleOnly != null)
            {
                battleOnly.boolValue = true;
                bso.ApplyModifiedPropertiesWithoutUndo();
            }

            Log.AppendLine("  battle steps skipped (already wired)");
        }

        panel.SetAsLastSibling();
        return true;
    }

    // ---- shared helpers (duplicated lightly to keep file standalone) ----

    enum StepsAction { Full, Patch, Skip }

    static StepsAction GetStepsAction(SerializedObject so)
    {
        SerializedProperty ver = so.FindProperty("wiredStepsVersion");
        SerializedProperty steps = so.FindProperty("steps");
        SerializedProperty key = so.FindProperty("tutorialKey");
        SerializedProperty battleOnly = so.FindProperty("battleTutorialOnly");
        int current = ver != null ? ver.intValue : 0;
        int count = steps != null ? steps.arraySize : 0;
        if (count == 0) return StepsAction.Full;
        // Leftover role/main content or missing battle markers -> full rewrite.
        bool wrongKey = key == null || key.stringValue != TutorialSession.BattleKey;
        bool notBattleOnly = battleOnly == null || !battleOnly.boolValue;
        bool missingB1 = true;
        if (steps != null)
        {
            for (int i = 0; i < steps.arraySize; i++)
            {
                SerializedProperty note = steps.GetArrayElementAtIndex(i).FindPropertyRelative("note");
                if (note != null && note.stringValue != null && note.stringValue.StartsWith("B1 "))
                {
                    missingB1 = false;
                    break;
                }
            }
        }
        if (wrongKey || notBattleOnly || missingB1)
        {
            Log.AppendLine("  force Full rewrite (wrongKey=" + wrongKey + " notBattleOnly=" + notBattleOnly + " missingB1=" + missingB1 + ")");
            return StepsAction.Full;
        }
        if (current < StepsVersion) return StepsAction.Patch;
        Log.AppendLine("  steps already at version " + current + " count=" + count + " (skip rewrite)");
        return StepsAction.Skip;
    }

    static void StripNonBattleManagers(Transform panel, string keepKey)
    {
        TutorialManager[] all = panel.GetComponents<TutorialManager>();
        for (int i = 0; i < all.Length; i++)
        {
            SerializedObject probe = new SerializedObject(all[i]);
            SerializedProperty keyProp = probe.FindProperty("tutorialKey");
            string key = keyProp != null ? keyProp.stringValue : null;
            if (key != keepKey)
            {
                Log.AppendLine("  destroy leftover TutorialManager key=" + key);
                Object.DestroyImmediate(all[i], true);
            }
        }
    }

    static void PatchKeepBlocking(SerializedObject so, bool keep)
    {
        SerializedProperty p = so.FindProperty("keepBlockingAfterFinish");
        if (p != null) p.boolValue = keep;
        SerializedProperty ver = so.FindProperty("wiredStepsVersion");
        if (ver != null) ver.intValue = StepsVersion;
        so.ApplyModifiedPropertiesWithoutUndo();
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
        if (ver != null) ver.intValue = StepsVersion;
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
            Log.AppendLine("  created TutorialManager key=" + key);
        }

        so = new SerializedObject(found);
        so.FindProperty("tutorialKey").stringValue = key;
        SetRef(so, "overlay", overlay);
        SetRef(so, "targetProvider", provider);
        return found;
    }

    static Transform EnsurePanel(Transform canvas, out TutorialOverlay overlay)
    {
        Transform panel = canvas.Find("TutorialPanel");
        bool created = panel == null;
        if (created)
        {
            GameObject go = new GameObject("TutorialPanel", typeof(RectTransform));
            go.layer = canvas.gameObject.layer;
            go.transform.SetParent(canvas, false);
            panel = go.transform;
            Stretch(panel as RectTransform);
        }

        Transform overlayRoot = EnsureChild(panel, "Overlay", out _);
        Stretch(overlayRoot as RectTransform);
        Transform blockerT = EnsureChild(overlayRoot, "Blocker", out bool blockerCreated);
        Image blockerImg = EnsureImage(blockerT.gameObject, new Color(0, 0, 0, 0), blockerCreated);
        blockerImg.raycastTarget = true;
        Stretch(blockerT as RectTransform);

        RectTransform maskTop = EnsureMask(overlayRoot, "MaskTop", Vector2.zero, Vector2.zero);
        RectTransform maskBottom = EnsureMask(overlayRoot, "MaskBottom", Vector2.zero, Vector2.zero);
        RectTransform maskLeft = EnsureMask(overlayRoot, "MaskLeft", Vector2.zero, Vector2.zero);
        RectTransform maskRight = EnsureMask(overlayRoot, "MaskRight", Vector2.zero, Vector2.zero);
        RectTransform ring = EnsureChild(overlayRoot, "Ring", out bool ringCreated) as RectTransform;
        if (ringCreated)
        {
            Stretch(ring);
            EnsureRingEdge(ring, "T", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f), new Vector2(0, RingThickness));
            EnsureRingEdge(ring, "B", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f), new Vector2(0, RingThickness));
            EnsureRingEdge(ring, "L", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0f, 0.5f), new Vector2(RingThickness, 0));
            EnsureRingEdge(ring, "R", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1f, 0.5f), new Vector2(RingThickness, 0));
        }

        RectTransform dialog = EnsureChild(overlayRoot, "Dialog", out bool dialogCreated) as RectTransform;
        if (dialogCreated)
        {
            Image dlg = EnsureImage(dialog.gameObject, ColDialog, true);
            dlg.raycastTarget = false;
            Place(dialog, new Vector2(0f, -280f), DialogSize);
        }

        Transform dialogT = dialog;
        Image portrait = EnsureImage(EnsureChild(dialogT, "Portrait", out bool pCreated).gameObject, ColPortrait, pCreated);
        if (pCreated)
        {
            Place(portrait.transform as RectTransform, new Vector2(-340f, 0f), new Vector2(160f, 160f));
        }

        TMP_Text body = EnsureText(dialogT, "Text", "Tutorial", 36f, TextAlignmentOptions.Left, Color.white,
            new Vector2(80f, 10f), new Vector2(640f, 200f));
        TMP_Text hint = EnsureText(dialogT, "Hint", "Tap to continue", 26f, TextAlignmentOptions.BottomRight, ColHint,
            new Vector2(250f, -110f), new Vector2(360f, 40f));
        overlayRoot.gameObject.SetActive(false);

        overlay = panel.GetComponent<TutorialOverlay>();
        if (overlay == null) overlay = panel.gameObject.AddComponent<TutorialOverlay>();
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
        string skipWhen = null, bool skipIfTargetGone = false, bool freePlay = false, string conditionKey = null,
        string waitForExtra = null)
    {
        if (!string.IsNullOrEmpty(waitForExtra))
        {
            waitFor = waitForExtra;
        }

        if (!freePlay && target == null && string.IsNullOrEmpty(targetKey) && advance != TutorialAdvance.Tap
            && string.IsNullOrEmpty(waitFor) && string.IsNullOrEmpty(conditionKey) && advance != TutorialAdvance.Event)
        {
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
        Log.AppendLine("  step[" + i + "] " + note + " advance=" + advance
            + (freePlay ? " freePlay=" + conditionKey : "")
            + (!string.IsNullOrEmpty(targetKey) ? " key=" + targetKey : "")
            + (!string.IsNullOrEmpty(waitFor) ? " waitFor=" + waitFor : ""));
    }

    static RectTransform GoRect(SerializedObject shopSo, string field)
    {
        SerializedProperty p = shopSo.FindProperty(field);
        GameObject go = p != null ? p.objectReferenceValue as GameObject : null;
        return go != null ? go.transform as RectTransform : null;
    }

    static RectTransform EnsureMask(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        Transform t = EnsureChild(parent, name, out bool created);
        Image img = EnsureImage(t.gameObject, ColMask, created);
        img.raycastTarget = true;
        if (created) Place(t as RectTransform, pos, size);
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
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = size;
        }
    }

    static void BindStrip(RectTransform strip, TutorialOverlay overlay)
    {
        TutorialMaskStrip s = strip.GetComponent<TutorialMaskStrip>();
        if (s == null) s = strip.gameObject.AddComponent<TutorialMaskStrip>();
        SerializedObject so = new SerializedObject(s);
        SetRef(so, "overlay", overlay);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static TMP_Text EnsureText(Transform parent, string name, string text, float size, TextAlignmentOptions align, Color color, Vector2 pos, Vector2 box)
    {
        Transform t = EnsureChild(parent, name, out bool created);
        TextMeshProUGUI tmp = t.GetComponent<TextMeshProUGUI>();
        if (tmp == null) { tmp = t.gameObject.AddComponent<TextMeshProUGUI>(); created = true; }
        if (created)
        {
            tmp.text = text; tmp.fontSize = size; tmp.alignment = align; tmp.color = color; tmp.enableWordWrapping = true;
            if (_font != null) { tmp.font = _font; tmp.fontSharedMaterial = _font.material; }
            Place(t as RectTransform, pos, box);
        }
        tmp.raycastTarget = false;
        return tmp;
    }

    static Image EnsureImage(GameObject go, Color color, bool setColor)
    {
        Image img = go.GetComponent<Image>();
        if (img == null) { img = go.AddComponent<Image>(); setColor = true; }
        if (setColor) img.color = color;
        return img;
    }

    static Transform EnsureChild(Transform parent, string name, out bool created)
    {
        Transform t = parent.Find(name);
        created = t == null;
        if (created)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            t = go.transform;
        }
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = Vector2.zero; rt.localScale = Vector3.one;
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size; rt.localScale = Vector3.one;
    }

    static void SetRef(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Log.AppendLine("WARN: field not found " + so.targetObject.GetType().Name + "." + field); return; }
        p.objectReferenceValue = value;
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++) if (roots[i].name == name) return roots[i];
        return null;
    }

    static Transform FindDeep(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindDeepUnder(roots[i].transform, name);
            if (found != null) return found;
        }
        return null;
    }

    static Transform FindDeepUnder(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepUnder(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    static Color Hex(string hex)
    {
        Color c; ColorUtility.TryParseHtmlString("#" + hex, out c); return c;
    }
}
#endif
