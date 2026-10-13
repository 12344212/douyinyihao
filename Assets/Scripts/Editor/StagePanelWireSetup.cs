using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Wire existing StagePanel hierarchy (user-placed nodes). Does not recreate slots.
/// Binds StageSlot (Number/Lock/Clear/TZ), StagePanelController (5 slots + L/Next),
/// StageWaterFloat (5 slots), and StageCloudFly (yun1/yun2/yun3).
/// Menu: Tools/Template/Wire Stage Panel
/// One-shot: StagePanelWireSetup.run
/// </summary>
[InitializeOnLoad]
public static class StagePanelWireSetup
{
    private const string LogTag = "[StagePanelWireSetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";

    private static string MarkerPath => Path.Combine(Directory.GetCurrentDirectory(), "StagePanelWireSetup.run");
    private static string OkPath => Path.Combine(Directory.GetCurrentDirectory(), "StagePanelWireSetup.ok");
    private static string ErrPath => Path.Combine(Directory.GetCurrentDirectory(), "StagePanelWireSetup.err");

    static StagePanelWireSetup()
    {
        if (!File.Exists(MarkerPath))
        {
            return;
        }

        EditorApplication.delayCall += TryAutoRun;
    }

    private static void TryAutoRun()
    {
        if (!File.Exists(MarkerPath))
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
            File.Delete(MarkerPath);
        }
        catch (Exception e)
        {
            Debug.LogError(LogTag + " marker delete failed: " + e.Message);
            return;
        }

        Debug.Log(LogTag + " Auto-run.");
        Run();
    }

    [MenuItem("Tools/Template/Wire Stage Panel")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Run();
    }

    public static void Run()
    {
        try
        {
            string report = WireMainScene();
            Debug.Log(LogTag + " Done. " + report);
            File.WriteAllText(OkPath, DateTime.Now.ToString("o") + " " + report + "\n");
            if (File.Exists(ErrPath))
            {
                File.Delete(ErrPath);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(LogTag + " " + e);
            try
            {
                File.WriteAllText(ErrPath, e.ToString());
            }
            catch
            {
                // ignore
            }
        }
    }

    private static string WireMainScene()
    {
        Scene scene = SceneManager.GetSceneByPath(MainScenePath);
        bool openedHere = false;
        if (!(scene.IsValid() && scene.isLoaded))
        {
            scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }

        Scene previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);
        try
        {
            Transform panelTf = FindStagePanel(scene);
            if (panelTf == null)
            {
                throw new Exception("StagePanel not found under CanvasPanel.");
            }

            UIPanel uiPanel = panelTf.GetComponent<UIPanel>();
            Transform content = uiPanel != null && uiPanel.Content != null
                ? uiPanel.Content
                : panelTf.Find("Window/Content");
            if (content == null)
            {
                throw new Exception("StagePanel Content missing.");
            }

            Transform slotRow = content.Find("SlotRow");
            if (slotRow == null)
            {
                throw new Exception("SlotRow not found under Content.");
            }

            StageSlot[] slots = new StageSlot[StageDefs.SlotsPerPage];
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < StageDefs.SlotsPerPage; i++)
            {
                Transform slotTf = slotRow.Find("Slot" + (i + 1));
                if (slotTf == null)
                {
                    throw new Exception("Slot" + (i + 1) + " missing.");
                }

                slots[i] = WireSlot(slotTf);
                sb.Append("Slot").Append(i + 1).Append(" ok; ");
            }

            Button prev = FindButton(panelTf, "L");
            Button next = FindButton(panelTf, "Next");
            if (next == null)
            {
                next = FindButton(panelTf, "R");
            }

            if (prev == null || next == null)
            {
                throw new Exception("L / Next (or R) paging buttons missing.");
            }

            StagePanelController ctrl = panelTf.GetComponent<StagePanelController>();
            if (ctrl == null)
            {
                ctrl = panelTf.gameObject.AddComponent<StagePanelController>();
            }

            SerializedObject so = new SerializedObject(ctrl);
            SetArray(so, "slots", slots);
            SetRef(so, "prevButton", prev);
            SetRef(so, "nextButton", next);
            SerializedProperty battle = so.FindProperty("battleSceneName");
            if (battle != null && string.IsNullOrEmpty(battle.stringValue))
            {
                battle.stringValue = "BattleScene";
            }

            StageWaterFloat water = panelTf.GetComponent<StageWaterFloat>();
            if (water == null)
            {
                water = panelTf.gameObject.AddComponent<StageWaterFloat>();
            }

            StageWaterFloat.Target[] floatTargets = BuildFloatTargets(slotRow);
            SerializedObject wso = new SerializedObject(water);
            SerializedProperty targetsProp = wso.FindProperty("targets");
            targetsProp.arraySize = floatTargets.Length;
            for (int i = 0; i < floatTargets.Length; i++)
            {
                SerializedProperty el = targetsProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("rect").objectReferenceValue = floatTargets[i].rect;
                el.FindPropertyRelative("phaseOffset").floatValue = floatTargets[i].phaseOffset;
                el.FindPropertyRelative("amplitudeScale").floatValue = floatTargets[i].amplitudeScale;
            }

            wso.ApplyModifiedPropertiesWithoutUndo();

            StageCloudFly cloudFly = panelTf.GetComponent<StageCloudFly>();
            if (cloudFly == null)
            {
                cloudFly = panelTf.gameObject.AddComponent<StageCloudFly>();
            }

            RectTransform[] yunClouds = CollectYunClouds(slotRow);
            SerializedObject cso = new SerializedObject(cloudFly);
            SerializedProperty cloudsProp = cso.FindProperty("clouds");
            cloudsProp.arraySize = yunClouds.Length;
            for (int i = 0; i < yunClouds.Length; i++)
            {
                cloudsProp.GetArrayElementAtIndex(i).objectReferenceValue = yunClouds[i];
            }

            SerializedProperty boundsProp = cso.FindProperty("boundsRoot");
            if (boundsProp != null)
            {
                boundsProp.objectReferenceValue = slotRow as RectTransform;
            }

            cso.ApplyModifiedPropertiesWithoutUndo();

            SetRef(so, "waterFloat", water);
            SetRef(so, "cloudFly", cloudFly);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Default TZ / Lock / Clear visibility until runtime Refresh.
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    continue;
                }

                Transform tz = slots[i].transform.Find("TZ");
                if (tz != null)
                {
                    tz.gameObject.SetActive(false);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new Exception("Failed to save MainScene.");
            }

            sb.Append("prev=").Append(prev.name).Append("; next=").Append(next.name);
            sb.Append("; floatTargets=").Append(floatTargets.Length);
            sb.Append("; yunClouds=").Append(yunClouds.Length);
            return sb.ToString();
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded && previous != scene)
            {
                SceneManager.SetActiveScene(previous);
            }

            if (openedHere && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static StageSlot WireSlot(Transform slotTf)
    {
        Button button = slotTf.GetComponent<Button>();
        if (button == null)
        {
            button = slotTf.gameObject.AddComponent<Button>();
            Image img = slotTf.GetComponent<Image>();
            if (img != null)
            {
                button.targetGraphic = img;
            }
        }

        Image background = slotTf.GetComponent<Image>();
        Transform numBg = slotTf.Find("numBg");
        TMPro.TMP_Text number = null;
        if (numBg != null)
        {
            Transform numberTf = numBg.Find("Number");
            if (numberTf != null)
            {
                number = numberTf.GetComponent<TMPro.TMP_Text>();
            }
        }

        if (number == null)
        {
            Transform numberTf = slotTf.Find("Number");
            if (numberTf != null)
            {
                number = numberTf.GetComponent<TMPro.TMP_Text>();
            }
        }

        Transform lockTf = slotTf.Find("Lock");
        Transform clearTf = slotTf.Find("Clear");
        Transform tzTf = slotTf.Find("TZ");

        StageSlot slot = slotTf.GetComponent<StageSlot>();
        if (slot == null)
        {
            slot = slotTf.gameObject.AddComponent<StageSlot>();
        }

        SerializedObject so = new SerializedObject(slot);
        SetRef(so, "button", button);
        SetRef(so, "background", background);
        SetRef(so, "numberLabel", number);
        SetRef(so, "lockMarker", lockTf != null ? lockTf.gameObject : null);
        SetRef(so, "clearMarker", clearTf != null ? clearTf.gameObject : null);
        SetRef(so, "challengeMarker", tzTf != null ? tzTf.gameObject : null);
        SerializedProperty tint = so.FindProperty("tintBackground");
        if (tint != null)
        {
            tint.boolValue = false;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return slot;
    }

    private static StageWaterFloat.Target[] BuildFloatTargets(Transform slotRow)
    {
        StageWaterFloat.Target[] list = new StageWaterFloat.Target[5];
        for (int i = 0; i < 5; i++)
        {
            Transform slotTf = slotRow.Find("Slot" + (i + 1));
            list[i] = new StageWaterFloat.Target
            {
                rect = slotTf as RectTransform,
                phaseOffset = i * 0.14f,
                amplitudeScale = 1f
            };
        }

        return list;
    }

    private static RectTransform[] CollectYunClouds(Transform slotRow)
    {
        string[] yuns = { "yun1", "yun2", "yun3" };
        RectTransform[] list = new RectTransform[yuns.Length];
        for (int i = 0; i < yuns.Length; i++)
        {
            Transform y = slotRow.Find(yuns[i]);
            if (y == null)
            {
                throw new Exception(yuns[i] + " missing under SlotRow.");
            }

            list[i] = y as RectTransform;
        }

        return list;
    }

    private static Button FindButton(Transform root, string name)
    {
        Transform t = root.Find(name);
        if (t == null)
        {
            return null;
        }

        Button b = t.GetComponent<Button>();
        if (b == null)
        {
            b = t.gameObject.AddComponent<Button>();
            Image img = t.GetComponent<Image>();
            if (img != null)
            {
                b.targetGraphic = img;
            }
        }

        return b;
    }

    private static Transform FindStagePanel(Scene scene)
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
                return t;
            }
        }

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
}