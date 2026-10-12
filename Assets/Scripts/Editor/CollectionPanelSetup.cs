using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 把 CollectionPanel 已有的 3×5 Image 格子接成 CP1–CP15：
/// 剪影 HDJ (n)、彩图 CPn、Button、CollectionCpSlot、CollectionPanelController、进度文案。
/// 菜单：Tools/Template/Build Collection Panel CP Slots
/// 自动执行：项目根目录存在 CollectionPanelSetup.run 时执行一次。
/// </summary>
[InitializeOnLoad]
public static class CollectionPanelSetup
{
    private const string LogTag = "[CollectionPanelSetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";

    private static string MarkerFilePath => Path.Combine(Directory.GetCurrentDirectory(), "CollectionPanelSetup.run");

    static CollectionPanelSetup()
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

    [MenuItem("Tools/Template/Build Collection Panel CP Slots")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Run(true);
    }

    public static void Run(bool interactive)
    {
        try
        {
            Scene scene = OpenMain(interactive, out bool openedHere);
            if (!scene.IsValid())
            {
                return;
            }

            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                UIPanel panel = FindCollectionPanel(scene);
                if (panel == null)
                {
                    Debug.LogError($"{LogTag} CollectionPanel not found under CanvasPanel.");
                    return;
                }

                BuildInto(panel);
                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene))
                {
                    Debug.Log($"{LogTag} CollectionPanel CP1–CP15 wired and saved.");
                }
                else
                {
                    Debug.LogError($"{LogTag} Failed to save MainScene.");
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
            Debug.LogError($"{LogTag} {e}");
        }
    }

    public static void BuildInto(UIPanel collectionPanel)
    {
        if (collectionPanel == null)
        {
            throw new Exception("CollectionPanel is null.");
        }

        Transform root = collectionPanel.transform;
        Transform itemDescription = root.Find("Kuang");
        if (itemDescription == null)
        {
            itemDescription = root.Find("ItemDescription");
        }

        if (itemDescription == null)
        {
            throw new Exception("Kuang/ItemDescription not found under CollectionPanel.");
        }

        CollectionCpSlot[] slots = new CollectionCpSlot[CollectionDefs.Count];
        int slotIndex = 0;
        for (int row = 1; row <= 3; row++)
        {
            Transform rowTf = itemDescription.Find(row.ToString());
            if (rowTf == null)
            {
                throw new Exception("Row '" + row + "' not found under ItemDescription.");
            }

            // GridLayout 子节点顺序即 CP 顺序
            for (int c = 0; c < rowTf.childCount; c++)
            {
                if (slotIndex >= CollectionDefs.Count)
                {
                    break;
                }

                Transform child = rowTf.GetChild(c);
                slots[slotIndex] = WireSlot(child.gameObject, slotIndex);
                slotIndex++;
            }
        }

        if (slotIndex != CollectionDefs.Count)
        {
            throw new Exception("Expected 15 slot images, found " + slotIndex + ".");
        }

        TMP_Text progress = FindProgressLabel(root);

        CollectionPanelController ctrl = collectionPanel.GetComponent<CollectionPanelController>();
        if (ctrl == null)
        {
            ctrl = collectionPanel.gameObject.AddComponent<CollectionPanelController>();
        }

        SerializedObject so = new SerializedObject(ctrl);
        SerializedProperty slotsProp = so.FindProperty("slots");
        slotsProp.arraySize = CollectionDefs.Count;
        for (int i = 0; i < CollectionDefs.Count; i++)
        {
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
        }

        SetRef(so, "progressLabel", progress);
        SetRef(so, "uiPanel", collectionPanel);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(ctrl);
        EditorUtility.SetDirty(collectionPanel.gameObject);
    }

    private static CollectionCpSlot WireSlot(GameObject go, int index)
    {
        go.name = "CP" + (index + 1);

        Image image = go.GetComponent<Image>();
        if (image == null)
        {
            throw new Exception(go.name + " missing Image.");
        }

        image.preserveAspect = true;
        image.raycastTarget = true;

        Button button = go.GetComponent<Button>();
        if (button == null)
        {
            button = go.AddComponent<Button>();
        }

        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;

        Sprite silhouette = LoadSprite(CollectionDefs.SilhouetteAssetPath(index));
        Sprite owned = LoadSprite(CollectionDefs.OwnedAssetPath(index));
        if (silhouette == null)
        {
            Debug.LogWarning($"{LogTag} Missing silhouette: {CollectionDefs.SilhouetteAssetPath(index)}");
        }

        if (owned == null)
        {
            Debug.LogWarning($"{LogTag} Missing owned art: {CollectionDefs.OwnedAssetPath(index)}");
        }

        // 默认显示剪影（未拥有）
        if (silhouette != null)
        {
            image.sprite = silhouette;
            image.color = Color.white;
        }

        CollectionCpSlot slot = go.GetComponent<CollectionCpSlot>();
        if (slot == null)
        {
            slot = go.AddComponent<CollectionCpSlot>();
        }

        SerializedObject so = new SerializedObject(slot);
        so.FindProperty("index").intValue = index;
        SetRef(so, "iconImage", image);
        SetRef(so, "button", button);
        SetRef(so, "silhouetteSprite", silhouette);
        SetRef(so, "ownedSprite", owned);
        so.FindProperty("silhouetteAddress").stringValue = CollectionDefs.SilhouetteAddress(index);
        so.FindProperty("ownedAddress").stringValue = CollectionDefs.OwnedAddress(index);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(go);
        return slot;
    }

    private static Sprite LoadSprite(string assetPath)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static TMP_Text FindProgressLabel(Transform collectionRoot)
    {
        // Role/Image/Text (TMP) 当前文案 18/18 → 改为 owned/15
        Transform role = collectionRoot.Find("Role");
        if (role == null)
        {
            // 兼容旧名 Number
            role = collectionRoot.Find("Number");
        }

        if (role == null)
        {
            Debug.LogWarning($"{LogTag} Role/Number progress root not found.");
            return null;
        }

        TMP_Text[] texts = role.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
            {
                texts[i].text = "0/" + CollectionDefs.Count;
                return texts[i];
            }
        }

        Debug.LogWarning($"{LogTag} Progress TMP_Text not found under Role.");
        return null;
    }

    private static Scene OpenMain(bool interactive, out bool openedHere)
    {
        openedHere = false;
        if (!File.Exists(MainScenePath))
        {
            Debug.LogError($"{LogTag} {MainScenePath} not found.");
            return default;
        }

        Scene scene = SceneManager.GetSceneByPath(MainScenePath);
        if (!(scene.IsValid() && scene.isLoaded))
        {
            scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }
        else if (scene.isDirty && !interactive)
        {
            Debug.LogWarning($"{LogTag} MainScene has unsaved changes; auto-run will wire CP slots and save.");
        }

        return scene;
    }

    private static void CloseIfOpened(Scene scene, bool openedHere)
    {
        if (openedHere && scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static UIPanel FindCollectionPanel(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name != "CanvasPanel")
            {
                continue;
            }

            Transform t = roots[i].transform.Find("CollectionPanel");
            if (t != null)
            {
                return t.GetComponent<UIPanel>();
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
}