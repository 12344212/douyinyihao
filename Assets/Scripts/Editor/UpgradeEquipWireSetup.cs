using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Wire UpgradeEquipController on UpgradePanel: EquipSlots, Equit list, EquitInfo.
/// Adds Buttons on ZB/XX if missing; starts Equit/EquitInfo inactive.
/// Menu: Tools/Template/Wire Upgrade Equipment UI
/// One-shot: UpgradeEquipWireSetup.run
/// </summary>
[InitializeOnLoad]
public static class UpgradeEquipWireSetup
{
    private const string LogTag = "[UpgradeEquipWireSetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string BagPrefabPath = "Assets/Prefab/UI/BagPrefab.prefab";
    private const string ItemConfigPath = ConfigAssetSetup.ItemConfigPath;

    private static string MarkerPath => Path.Combine(Directory.GetCurrentDirectory(), "UpgradeEquipWireSetup.run");
    private static string OkPath => Path.Combine(Directory.GetCurrentDirectory(), "UpgradeEquipWireSetup.ok");
    private static string ErrPath => Path.Combine(Directory.GetCurrentDirectory(), "UpgradeEquipWireSetup.err");

    static UpgradeEquipWireSetup()
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

    [MenuItem("Tools/Template/Wire Upgrade Equipment UI")]
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
            UIPanel upgrade = FindUpgradePanel(scene);
            if (upgrade == null)
            {
                throw new Exception("UpgradePanel not found in MainScene.");
            }

            Transform root = upgrade.transform;
            UpgradeEquipController ctrl = upgrade.GetComponent<UpgradeEquipController>();
            if (ctrl == null)
            {
                ctrl = undoAdd(upgrade.gameObject);
            }

            SlotBundle s1 = ResolveSlot(FindDeep(root, "EquipSlot1"), "bg", "equi");
            SlotBundle s2 = ResolveSlot(FindDeep(root, "EquipSlot2"), "bg", "equi");
            SlotBundle s3 = ResolveSlot(FindDeep(root, "EquipSlot3"), "bg", "equi");
            SlotBundle s4 = ResolveSlot(FindDeep(root, "EquipSlot4"), "bg (1)", "equi");
            if (s4.bg == null)
            {
                s4 = ResolveSlot(FindDeep(root, "EquipSlot4"), "bg", "equi");
            }

            Transform equit = FindDeep(root, "Equit");
            Transform equitInfo = FindDeep(root, "EquitInfo");
            if (equit == null || equitInfo == null)
            {
                throw new Exception("Equit / EquitInfo nodes missing under UpgradePanel.");
            }

            Transform listE = FindDeep(equit, "E");
            TMP_Text emptyHint = FindTmp(equit, "Text (TMP)");
            if (emptyHint == null)
            {
                emptyHint = equit.GetComponentInChildren<TMP_Text>(true);
            }

            Transform nameT = FindDeep(equitInfo, "NAME");
            Transform infoT = FindDeep(equitInfo, "info");
            Transform iconT = FindDeep(equitInfo, "Icon");
            Transform zbT = FindDeep(equitInfo, "ZB");
            Transform xxT = FindDeep(equitInfo, "XX");

            Button zbBtn = EnsureButton(zbT != null ? zbT.gameObject : null);
            Button xxBtn = EnsureButton(xxT != null ? xxT.gameObject : null);
            Button infoClose = EnsureButton(equitInfo.gameObject);

            // Dim overlay should close info; child kUANG keeps raycasts so content clicks are fine.
            Image infoImg = equitInfo.GetComponent<Image>();
            if (infoImg != null && infoClose != null)
            {
                infoClose.targetGraphic = infoImg;
                infoClose.transition = Selectable.Transition.None;
            }

            equit.gameObject.SetActive(false);
            equitInfo.gameObject.SetActive(false);
            if (listE != null)
            {
                listE.gameObject.SetActive(false);
            }

            if (emptyHint != null)
            {
                emptyHint.text = "No corresponding equipment yet. Head to the shop to buy some.";
            }

            BagItemSlot bagPrefab = null;
            GameObject bagGo = AssetDatabase.LoadAssetAtPath<GameObject>(BagPrefabPath);
            if (bagGo != null)
            {
                bagPrefab = bagGo.GetComponent<BagItemSlot>();
            }

            ItemConfig itemConfig = AssetDatabase.LoadAssetAtPath<ItemConfig>(ItemConfigPath);

            SerializedObject so = new SerializedObject(ctrl);
            SetRef(so, "panel", upgrade);
            SetSlot(so, "slot1", s1);
            SetSlot(so, "slot2", s2);
            SetSlot(so, "slot3", s3);
            SetSlot(so, "slot4", s4);
            SetRef(so, "equitRoot", equit != null ? equit.gameObject : null);
            SetRef(so, "listRoot", listE != null ? listE.GetComponent<RectTransform>() : null);
            SetRef(so, "bagPrefab", bagPrefab);
            SetRef(so, "emptyHint", emptyHint);
            SetRef(so, "itemConfig", itemConfig);
            SetRef(so, "equitInfoRoot", equitInfo.gameObject);
            SetRef(so, "infoIcon", iconT != null ? iconT.GetComponent<Image>() : null);
            SetRef(so, "infoName", nameT != null ? nameT.GetComponent<TMP_Text>() : null);
            SetRef(so, "infoDescription", infoT != null ? infoT.GetComponent<TMP_Text>() : null);
            SetRef(so, "equipButton", zbBtn);
            SetRef(so, "unequipButton", xxBtn);
            SetRef(so, "infoCloseButton", infoClose);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(ctrl);
            EditorUtility.SetDirty(upgrade.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            return "UpgradeEquipController wired. slots="
                + SlotOk(s1) + SlotOk(s2) + SlotOk(s3) + SlotOk(s4)
                + " equit=" + (equit != null)
                + " E=" + (listE != null)
                + " emptyHint=" + (emptyHint != null)
                + " bagPrefab=" + (bagPrefab != null)
                + " itemConfig=" + (itemConfig != null)
                + " infoIcon=" + (iconT != null)
                + " ZB=" + (zbBtn != null)
                + " XX=" + (xxBtn != null)
                + " Equit/EquitInfo set inactive.";
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded)
            {
                SceneManager.SetActiveScene(previous);
            }

            if (openedHere && scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static UpgradeEquipController undoAdd(GameObject go)
    {
        return go.AddComponent<UpgradeEquipController>();
    }

    private struct SlotBundle
    {
        public Button button;
        public GameObject bg;
        public Image icon;
    }

    private static SlotBundle ResolveSlot(Transform slot, string bgName, string iconName)
    {
        SlotBundle b = new SlotBundle();
        if (slot == null)
        {
            return b;
        }

        b.button = slot.GetComponent<Button>();
        Transform bg = slot.Find(bgName);
        if (bg == null)
        {
            bg = FindDeep(slot, bgName);
        }

        if (bg == null)
        {
            // any child starting with bg
            for (int i = 0; i < slot.childCount; i++)
            {
                Transform c = slot.GetChild(i);
                if (c.name.StartsWith("bg", StringComparison.OrdinalIgnoreCase))
                {
                    bg = c;
                    break;
                }
            }
        }

        if (bg != null)
        {
            b.bg = bg.gameObject;
            Transform icon = bg.Find(iconName);
            if (icon == null)
            {
                icon = FindDeep(bg, iconName);
            }

            if (icon != null)
            {
                b.icon = icon.GetComponent<Image>();
            }

            if (b.icon == null)
            {
                b.icon = bg.GetComponentInChildren<Image>(true);
                if (b.icon != null && b.icon.gameObject == bg.gameObject)
                {
                    // prefer child image over bg itself
                    Image[] imgs = bg.GetComponentsInChildren<Image>(true);
                    for (int i = 0; i < imgs.Length; i++)
                    {
                        if (imgs[i] != null && imgs[i].gameObject != bg.gameObject)
                        {
                            b.icon = imgs[i];
                            break;
                        }
                    }
                }
            }
        }

        return b;
    }

    private static string SlotOk(SlotBundle s)
    {
        return (s.button != null ? "B" : "-") + (s.bg != null ? "G" : "-") + (s.icon != null ? "I" : "-") + " ";
    }

    private static void SetSlot(SerializedObject so, string field, SlotBundle s)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            throw new Exception("Field not found: " + field);
        }

        p.FindPropertyRelative("slotButton").objectReferenceValue = s.button;
        p.FindPropertyRelative("bgRoot").objectReferenceValue = s.bg;
        p.FindPropertyRelative("iconImage").objectReferenceValue = s.icon;
    }

    private static Button EnsureButton(GameObject go)
    {
        if (go == null)
        {
            return null;
        }

        Button btn = go.GetComponent<Button>();
        if (btn == null)
        {
            btn = go.AddComponent<Button>();
        }

        Image img = go.GetComponent<Image>();
        if (img != null)
        {
            btn.targetGraphic = img;
            img.raycastTarget = true;
        }

        btn.transition = Selectable.Transition.None;
        return btn;
    }

    private static UIPanel FindUpgradePanel(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform t = FindDeep(roots[i].transform, "UpgradePanel");
            if (t != null)
            {
                return t.GetComponent<UIPanel>();
            }
        }

        return null;
    }

    private static Transform FindDeep(Transform parent, string name)
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
            Transform found = FindDeep(parent.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static TMP_Text FindTmp(Transform parent, string name)
    {
        Transform t = FindDeep(parent, name);
        return t != null ? t.GetComponent<TMP_Text>() : null;
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
}