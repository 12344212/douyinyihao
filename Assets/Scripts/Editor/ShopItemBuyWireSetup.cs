using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ItemBUY opens BuyPanel; ItemData only shows selection; remove old ItemData purchase UI.
/// Menu: Tools/Template/Wire ItemBUY To BuyPanel
/// One-shot: ShopItemBuyWireSetup.run
/// </summary>
[InitializeOnLoad]
public static class ShopItemBuyWireSetup
{
    private const string LogTag = "[ShopItemBuyWireSetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string GuidBuy = "164a3c5bc6bd79a45a27313a52777722"; // SD-GMANN
    private const string GuidCancel = "9587847749d98ad4fb79f117b79183e9";
    private const string GuidConfirmFrame = "758956f8407e6f1438c58b26a4692610";
    private const string GuidQtyFrame = "7eff8bf3c20ba2f4b97df2c89c216b67";
    private const string GuidMinus = "197b3b3250612da438746828e3acba6c";
    private const string GuidPlus = "5cc4a5b2001556941af5f4d2b34e5d7b";
    private const string GuidGold = "1afc5e6dd14599f42a6f755fc9c67858";
    private static readonly Color LabelDark = new Color(0.22f, 0.14f, 0.08f, 1f);
    private static string MarkerPath => Path.Combine(Directory.GetCurrentDirectory(), "ShopItemBuyWireSetup.run");

    static ShopItemBuyWireSetup()
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

    [MenuItem("Tools/Template/Wire ItemBUY To BuyPanel")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run();
    }

    private static void Run()
    {
        try
        {
            Scene scene = SceneManager.GetSceneByPath(MainScenePath);
            bool openedHere = false;
            if (!(scene.IsValid() && scene.isLoaded))
            {
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
                openedHere = true;
            }

            UIPanel shopUi = FindShopPanel(scene);
            if (shopUi == null)
            {
                Debug.LogError(LogTag + " ShopPanel not found.");
                if (openedHere && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                string report = Apply(shopUi);
                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene))
                {
                    Debug.Log(LogTag + " Done. " + report);
                    File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "ShopItemBuyWireSetup.ok"),
                        DateTime.Now.ToString("o") + " " + report);
                }
                else Debug.LogError(LogTag + " Save failed.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (openedHere && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(LogTag + " " + e);
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "ShopItemBuyWireSetup.err"), e.ToString()); } catch { }
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

    private static string Apply(UIPanel shopUi)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        Transform shop = shopUi.transform;
        Transform window = shop.Find("Window");

        // Detail panel: ItemBUY or ItemData with ItemIcon
        Transform detail = FindDetailPanel(shop);
        if (detail == null)
        {
            return "ItemData/ItemBUY detail missing";
        }

        if (detail.name != "ItemData")
        {
            sb.Append("rename ").Append(detail.name).Append("->ItemData; ");
            detail.name = "ItemData";
        }

        // Keep detail under Window (not full-screen over ShopPanel)
        if (window != null && detail.parent != window)
        {
            detail.SetParent(window, false);
            sb.Append("ItemData under Window; ");
        }

        // Remove full-screen dim Image on detail root (blocks list clicks)
        Image rootImg = detail.GetComponent<Image>();
        if (rootImg != null)
        {
            UnityEngine.Object.DestroyImmediate(rootImg);
            sb.Append("removed ItemData root Image; ");
        }
        CanvasRenderer rootCr = detail.GetComponent<CanvasRenderer>();
        if (rootCr != null)
        {
            UnityEngine.Object.DestroyImmediate(rootCr);
        }

        RectTransform detailRt = detail as RectTransform;
        if (detailRt != null)
        {
            detailRt.anchorMin = Vector2.zero;
            detailRt.anchorMax = Vector2.one;
            detailRt.offsetMin = Vector2.zero;
            detailRt.offsetMax = Vector2.zero;
            detailRt.localScale = Vector3.one;
        }

        // Remove old purchase UI under ItemData (qty/cancel/total — BuyPanel owns these)
        string[] junk = { "Cancel", "TotleBg", "+", "-", "ItemBuyButton" };
        for (int i = 0; i < junk.Length; i++)
        {
            Transform t = detail.Find(junk[i]);
            if (t != null)
            {
                UnityEngine.Object.DestroyImmediate(t.gameObject);
                sb.Append("removed ").Append(junk[i]).Append("; ");
            }
        }

        // ItemBUY button under ItemData (opens BuyPanel)
        Button itemBuy = null;
        Transform buyT = detail.Find("ItemBUY");
        if (buyT == null) buyT = shop.Find("ItemBUY");
        if (buyT != null && buyT != detail)
        {
            itemBuy = buyT.GetComponent<Button>();
            if (buyT.parent != detail)
            {
                buyT.SetParent(detail, true);
            }
        }

        if (itemBuy == null)
        {
            Sprite buySp = LoadSprite(GuidBuy, "SD-GMANN");
            Image buyImg = CreateSprite("ItemBUY", detail, buySp, true);
            SetRect(buyImg.rectTransform, new Vector2(428f, -354f), new Vector2(320f, 110f));
            itemBuy = buyImg.gameObject.AddComponent<Button>();
            itemBuy.targetGraphic = buyImg;
            itemBuy.transition = Selectable.Transition.None;
            sb.Append("created ItemBUY; ");
        }
        else
        {
            sb.Append("ItemBUY exists; ");
        }

        // Restore BuyPanel
        UIPanel buyPanel = null; // BuyPanelController removed
        if (buyPanel == null)
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            buyPanel = null;
            sb.Append("recreated BuyPanel; ");
        }
        else
        {
            sb.Append("BuyPanel ok; ");
        }

        ShopPanelController ctrl = shop.GetComponent<ShopPanelController>();
        if (ctrl == null)
        {
            sb.Append("ShopPanelController missing");
            return sb.ToString();
        }

        SerializedObject so = new SerializedObject(ctrl);
        // buyPanel UIPanel wired by ShopBuyFoldWireSetup
        SetRef(so, "itemBuyButton", itemBuy);
        // Keep ItemData display refs if still valid
        EnsureRef(so, "itemIcon", detail, "ItemIcon", typeof(Image));
        EnsureRef(so, "itemName", detail, "ItemName", typeof(TMP_Text));
        EnsureRef(so, "itemDescription", detail, "DescBox/ItemDescription", typeof(TMP_Text));
        Transform goldBg = detail.Find("GoldBg");
        if (goldBg != null) EnsureRef(so, "itemPrice", goldBg, "ItemPrice", typeof(TMP_Text));
        else EnsureRef(so, "itemPrice", detail, "ItemPrice", typeof(TMP_Text));
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ctrl);
        sb.Append("wired buyPanel+ItemBUY");
        return sb.ToString();
    }

    private static Transform FindDetailPanel(Transform shop)
    {
        Transform t = shop.Find("ItemData");
        if (t != null && t.Find("ItemIcon") != null) return t;
        t = shop.Find("ItemBUY");
        if (t != null && t.Find("ItemIcon") != null) return t;
        Transform window = shop.Find("Window");
        if (window != null)
        {
            t = window.Find("ItemData");
            if (t != null) return t;
            t = window.Find("ItemBUY");
            if (t != null && t.Find("ItemIcon") != null) return t;
        }
        // Deep search
        return DeepFindWithChild(shop, "ItemIcon");
    }

    private static Transform DeepFindWithChild(Transform root, string childName)
    {
        if (root == null) return null;
        if (root.Find(childName) != null && (root.name == "ItemData" || root.name == "ItemBUY" || root.Find("ItemDescription") != null || root.Find("DescBox") != null))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform f = DeepFindWithChild(root.GetChild(i), childName);
            if (f != null) return f;
        }
        return null;
    }

    private static void EnsureRef(SerializedObject so, string prop, Transform root, string path, Type type)
    {
        SerializedProperty p = so.FindProperty(prop);
        if (p == null) return;
        if (p.objectReferenceValue != null) return;
        Transform t = root.Find(path);
        if (t == null) return;
        if (type == typeof(Image)) p.objectReferenceValue = t.GetComponent<Image>();
        else if (type == typeof(TMP_Text)) p.objectReferenceValue = t.GetComponent<TMP_Text>();
        else if (type == typeof(Button)) p.objectReferenceValue = t.GetComponent<Button>();
    }

    private static UIPanel CreateBuyPanelObsolete(Transform parent, TMP_FontAsset font)
    {
        Debug.LogWarning("[Editor] CreateBuyPanel obsolete; Buy folded into ShopPanelController. Use ShopBuyFoldWireSetup.");
        return null;
    }

    private static Sprite LoadSprite(string guid, string label)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) throw new Exception("Missing sprite " + label + " guid=" + guid);
        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sp == null) throw new Exception("Sprite load failed " + label + " " + path);
        return sp;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go.transform as RectTransform;
    }

    private static Image CreateImage(string name, Transform parent, Color color, bool raycast)
    {
        Image img = CreateRect(name, parent).gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private static Image CreateSprite(string name, Transform parent, Sprite sprite, bool raycast)
    {
        Image img = CreateRect(name, parent).gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = raycast;
        img.color = Color.white;
        return img;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float size, Color color, TMP_FontAsset font, TextAlignmentOptions align)
    {
        TextMeshProUGUI t = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    private static void SetRect(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void StretchFull(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private static void SetRef(SerializedObject so, string prop, UnityEngine.Object value)
    {
        SerializedProperty p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
    }
}