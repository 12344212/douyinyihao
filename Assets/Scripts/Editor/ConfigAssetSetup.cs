using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 生成 Assets/Config/ItemConfig.asset 和 GemPackConfig.asset，并写进 LoadScene / 商店 / 背包 / 充值的序列化引用。
/// 菜单：Tools/Template/Build Config Assets
/// 自动执行：项目根目录存在 ConfigAssetSetup.run 时执行一次。
/// </summary>
[InitializeOnLoad]
public static class ConfigAssetSetup
{
    private const string LogTag = "[ConfigAssetSetup]";
    public const string ItemConfigPath = "Assets/Config/ItemConfig.asset";
    public const string GemPackConfigPath = "Assets/Config/GemPackConfig.asset";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string LoadScenePath = "Assets/Scenes/LoadScene.unity";

    private static string MarkerFilePath => Path.Combine(Directory.GetCurrentDirectory(), "ConfigAssetSetup.run");

    static ConfigAssetSetup()
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

    [MenuItem("Tools/Template/Build Config Assets")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Run(true);
    }

    public static void EnsureAssets()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Config"))
        {
            AssetDatabase.CreateFolder("Assets", "Config");
        }

        EnsureItemConfig();
        EnsureGemPackConfig();
        AssetDatabase.SaveAssets();
    }

    public static void AssignLoadScene(LoadSceneController ctrl)
    {
        if (ctrl == null)
        {
            return;
        }

        EnsureAssets();
        ItemConfig items = AssetDatabase.LoadAssetAtPath<ItemConfig>(ItemConfigPath);
        GemPackConfig packs = AssetDatabase.LoadAssetAtPath<GemPackConfig>(GemPackConfigPath);
        SerializedObject so = new SerializedObject(ctrl);
        SetRef(so, "itemConfig", items);
        SetRef(so, "gemPackConfig", packs);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);
    }

    public static void AssignPanels(ShopPanelController shop, BagPanelController bag, TopUpPanelController topUp)
    {
        EnsureAssets();
        ItemConfig items = AssetDatabase.LoadAssetAtPath<ItemConfig>(ItemConfigPath);
        GemPackConfig packs = AssetDatabase.LoadAssetAtPath<GemPackConfig>(GemPackConfigPath);
        if (shop != null)
        {
            SerializedObject so = new SerializedObject(shop);
            SetRef(so, "itemConfig", items);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);
        }

        if (bag != null)
        {
            SerializedObject so = new SerializedObject(bag);
            SetRef(so, "itemConfig", items);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(bag.gameObject.scene);
        }

        if (topUp != null)
        {
            SerializedObject so = new SerializedObject(topUp);
            SetRef(so, "gemPackConfig", packs);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(topUp.gameObject.scene);
        }
    }

    private static void Run(bool interactive)
    {
        try
        {
            EnsureAssets();
            AssignLoad(interactive);
            AssignMain(interactive);
            Debug.Log($"{LogTag} Config assets ready. Addresses: {GameItemDefs.ConfigAddress}, {GemPackDefs.ConfigAddress}");
        }
        catch (Exception e)
        {
            Debug.LogError($"{LogTag} {e}");
        }
    }

    private static void AssignLoad(bool interactive)
    {
        Scene scene = OpenScene(LoadScenePath, interactive, out bool openedHere);
        if (!scene.IsValid())
        {
            return;
        }

        LoadSceneController ctrl = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            ctrl = roots[i].GetComponentInChildren<LoadSceneController>(true);
            if (ctrl != null)
            {
                break;
            }
        }

        if (ctrl == null)
        {
            Debug.LogError($"{LogTag} LoadSceneController not found.");
            CloseIfOpened(scene, openedHere);
            return;
        }

        AssignLoadScene(ctrl);
        EditorSceneManager.SaveScene(scene);
        CloseIfOpened(scene, openedHere);
    }

    private static void AssignMain(bool interactive)
    {
        Scene scene = OpenScene(MainScenePath, interactive, out bool openedHere);
        if (!scene.IsValid())
        {
            return;
        }

        ShopPanelController shop = null;
        BagPanelController bag = null;
        TopUpPanelController topUp = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (shop == null)
            {
                shop = roots[i].GetComponentInChildren<ShopPanelController>(true);
            }

            if (bag == null)
            {
                bag = roots[i].GetComponentInChildren<BagPanelController>(true);
            }

            if (topUp == null)
            {
                topUp = roots[i].GetComponentInChildren<TopUpPanelController>(true);
            }
        }

        AssignPanels(shop, bag, topUp);
        EditorSceneManager.SaveScene(scene);
        CloseIfOpened(scene, openedHere);
    }

    private static Scene OpenScene(string path, bool interactive, out bool openedHere)
    {
        openedHere = false;
        if (!File.Exists(path))
        {
            Debug.LogError($"{LogTag} {path} not found.");
            return default;
        }

        Scene scene = SceneManager.GetSceneByPath(path);
        if (!(scene.IsValid() && scene.isLoaded))
        {
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            openedHere = true;
        }
        else if (scene.isDirty && !interactive)
        {
            Debug.LogWarning($"{LogTag} {path} has unsaved changes, auto-run skipped.");
            return default;
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

    private static void EnsureItemConfig()
    {
        ItemConfig existing = AssetDatabase.LoadAssetAtPath<ItemConfig>(ItemConfigPath);
        if (existing != null)
        {
            return;
        }

        ItemConfig cfg = ScriptableObject.CreateInstance<ItemConfig>();
        AssetDatabase.CreateAsset(cfg, ItemConfigPath);
        SerializedObject so = new SerializedObject(cfg);
        SerializedProperty items = so.FindProperty("items");
        items.arraySize = 1;
        SerializedProperty entry = items.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("id").stringValue = GameItemDefs.HealPotionId;
        entry.FindPropertyRelative("displayName").stringValue = "Healing Potion";
        entry.FindPropertyRelative("description").stringValue = "Restores 30% of max HP.";
        entry.FindPropertyRelative("price").intValue = 150;
        entry.FindPropertyRelative("currency").enumValueIndex = (int)CurrencyType.Gold;
        entry.FindPropertyRelative("iconLocation").stringValue = "Item_HealPotion";
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cfg);
        Debug.Log($"{LogTag} Created {ItemConfigPath}");
    }

    private static void EnsureGemPackConfig()
    {
        GemPackConfig existing = AssetDatabase.LoadAssetAtPath<GemPackConfig>(GemPackConfigPath);
        if (existing != null)
        {
            return;
        }

        GemPackConfig cfg = ScriptableObject.CreateInstance<GemPackConfig>();
        AssetDatabase.CreateAsset(cfg, GemPackConfigPath);
        SerializedObject so = new SerializedObject(cfg);
        SerializedProperty packs = so.FindProperty("packs");
        string[] names = { "Package 1", "Package 2", "Package 3", "Package 4", "Package 5", "Package 6" };
        string[] ids =
        {
            "com.demontw.id2254.pay30",
            "com.demontw.id2254.pay120",
            "com.demontw.id2254.pay300",
            "com.demontw.id2254.pay1200",
            "com.demontw.id2254.pay3000",
            "com.demontw.id2254.pay6000"
        };
        int[] gems = { 30, 120, 300, 1280, 3280, 6480 };
        float[] prices = { 0.49f, 1.99f, 4.99f, 19.99f, 49.99f, 99.99f };
        string[] icons = { "HB1", "HB2", "HB3", "HB4", "HB5", "HB6" };
        packs.arraySize = 6;
        for (int i = 0; i < 6; i++)
        {
            SerializedProperty entry = packs.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("index").intValue = i + 1;
            entry.FindPropertyRelative("displayName").stringValue = names[i];
            entry.FindPropertyRelative("productId").stringValue = ids[i];
            entry.FindPropertyRelative("gems").intValue = gems[i];
            entry.FindPropertyRelative("priceUsd").floatValue = prices[i];
            entry.FindPropertyRelative("iconLocation").stringValue = icons[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cfg);
        Debug.Log($"{LogTag} Created {GemPackConfigPath}");
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
