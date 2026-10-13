using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Fold BuyPanelController into ShopPanelController; wire rebuilt ShopPanel/Buy nodes
/// and ShopPrefab BUY button. No new Buy class.
/// Menu: Tools/Template/Fold Buy Into Shop And Wire
/// One-shot: ShopBuyFoldWireSetup.run
/// </summary>
[InitializeOnLoad]
public static class ShopBuyFoldWireSetup
{
    private const string LogTag = "[ShopBuyFoldWireSetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string SlotPrefabPath = "Assets/Prefab/UI/ShopPrefab.prefab";
    private static string MarkerPath => Path.Combine(Directory.GetCurrentDirectory(), "ShopBuyFoldWireSetup.run");

    static ShopBuyFoldWireSetup()
    {
        if (!File.Exists(MarkerPath)) return;
        EditorApplication.delayCall += TryAutoRun;
    }

    private static void TryAutoRun()
    {
        if (!File.Exists(MarkerPath)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoRun;
            return;
        }

        try { File.Delete(MarkerPath); }
        catch (Exception e)
        {
            Debug.LogError(LogTag + " marker delete failed: " + e.Message);
            return;
        }

        Debug.Log(LogTag + " Auto-run.");
        Run();
    }

    [MenuItem("Tools/Template/Fold Buy Into Shop And Wire")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run();
    }

    private static void Run()
    {
        try
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(WireSlotPrefab()).Append("; ");
            sb.Append(WireMainScene());

            string report = sb.ToString();
            Debug.Log(LogTag + " Done. " + report);
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "ShopBuyFoldWireSetup.ok"),
                DateTime.Now.ToString("o") + " " + report);
        }
        catch (Exception e)
        {
            Debug.LogError(LogTag + " " + e);
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "ShopBuyFoldWireSetup.err"), e.ToString()); } catch { }
        }
    }

    private static string WireSlotPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SlotPrefabPath);
        try
        {
            ShopItemSlot slot = root.GetComponent<ShopItemSlot>();
            if (slot == null)
            {
                return "ShopPrefab missing ShopItemSlot";
            }

            Transform buyT = root.transform.Find("BUY");
            Button buyBtn = buyT != null ? buyT.GetComponent<Button>() : null;
            SerializedObject so = new SerializedObject(slot);
            SetRef(so, "buyButton", buyBtn);
            // Keep existing price/name/icon/click if present
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
            return buyBtn != null ? "ShopPrefab BUY wired" : "ShopPrefab BUY missing";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
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
            UIPanel shopUi = FindShopPanel(scene);
            if (shopUi == null)
            {
                return "ShopPanel not found";
            }

            Transform shop = shopUi.transform;
            ShopPanelController ctrl = shop.GetComponent<ShopPanelController>();
            if (ctrl == null)
            {
                return "ShopPanelController missing";
            }

            Transform buy = shop.Find("Buy");
            if (buy == null)
            {
                return "ShopPanel/Buy missing";
            }

            // Remove missing BuyPanelController (class deleted) and any leftover Buy script
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(buy.gameObject);
            if (removed > 0) Debug.Log(LogTag + " removed missing scripts: " + removed);

            UIPanel buyUi = buy.GetComponent<UIPanel>();
            Image icon = FindImage(buy, "Window/Icon");
            TMP_Text desc = FindTmp(buy, "Window/InfoBg/Description");
            if (desc == null) desc = FindTmp(buy, "Window/Description");
            TMP_Text qty = FindTmp(buy, "Window/QtyFrame/Quantity");
            TMP_Text total = FindTmp(buy, "Window/UnitBg/UnitPrice");
            if (total == null) total = FindTmp(buy, "Window/UnitPrice");
            Image currency = FindImage(buy, "Window/UnitBg/UnitGold");
            if (currency == null) currency = FindImage(buy, "Window/UnitGold");
            Button minus = FindButton(buy, "Window/Minus");
            Button plus = FindButton(buy, "Window/Plus");
            Button confirm = FindButton(buy, "Window/Confirm");
            Button cancel = FindButton(buy, "Window/Cancel");

            if (qty != null && string.IsNullOrEmpty(qty.text)) qty.text = "1";
            if (total != null) total.text = "0";
            if (desc != null && (string.IsNullOrEmpty(desc.text) || desc.text.Contains("描述") || desc.text.Contains("说明")))
            {
                desc.text = "Item description";
            }

            SerializedObject so = new SerializedObject(ctrl);
            // Clear obsolete buyPanel BuyPanelController ref if property still existed — now buyPanel is UIPanel
            SetRef(so, "buyPanel", buyUi);
            SetRef(so, "buyRoot", buy.gameObject);
            SetRef(so, "buyIconImage", icon);
            SetRef(so, "buyDescLabel", desc);
            SetRef(so, "buyQtyLabel", qty);
            SetRef(so, "buyTotalLabel", total);
            SetRef(so, "buyCurrencyIcon", currency);
            SetRef(so, "buyMinusButton", minus);
            SetRef(so, "buyPlusButton", plus);
            SetRef(so, "buyConfirmButton", confirm);
            SetRef(so, "buyCancelButton", cancel);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ctrl);
            EditorUtility.SetDirty(buy.gameObject);

            // Ensure Buy starts hidden
            buy.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                return "scene save failed";
            }

            return string.Format(
                "Buy wired icon={0} desc={1} qty={2} total={3} currency={4} -={5} +={6} ok={7} cancel={8} panel={9}",
                icon != null, desc != null, qty != null, total != null, currency != null,
                minus != null, plus != null, confirm != null, cancel != null, buyUi != null);
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (openedHere && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static UIPanel FindShopPanel(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name != "CanvasPanel") continue;
            Transform t = roots[i].transform.Find("ShopPanel");
            if (t != null) return t.GetComponent<UIPanel>();
        }
        return null;
    }

    private static Image FindImage(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<Image>() : null;
    }

    private static TMP_Text FindTmp(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private static Button FindButton(Transform root, string path)
    {
        Transform t = root.Find(path);
        return t != null ? t.GetComponent<Button>() : null;
    }

    private static void SetRef(SerializedObject so, string prop, UnityEngine.Object value)
    {
        SerializedProperty p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
    }
}
