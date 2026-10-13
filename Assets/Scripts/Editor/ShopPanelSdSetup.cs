using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 按 SD-CK / SD-CK2 重装 ShopPanel + BuyPanel 视觉，并接线现有购买逻辑。
/// 菜单：Tools/Template/Build Shop Panel SD UI
/// 一键：项目根目录 ShopPanelSdSetup.run
/// </summary>
[InitializeOnLoad]
public static class ShopPanelSdSetup
{
    private const string LogTag = "[ShopPanelSdSetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    public const string ShopPrefabPath = "Assets/Prefab/UI/ShopPrefab.prefab";

    private const string GuidMerchant = "86631fd4f55077a4a8aea93693ea17cb"; // SD-TP
    private const string GuidChest = "6ddd233c2338dbc4fbad11c4c588fd60"; // SD-BX
    private const string GuidChestAlt = "388656390f8fd294c8eee888cd8dae44"; // SD-BX1
    private const string GuidBarBg = "1e38e8e6162d30e42b99ebf71abb8fe3"; // SD-JDT
    private const string GuidBarFill = "01063caacbc60dd41a404857a13400d8"; // SD-JDTD
    private const string GuidRightPanel = "0ee4f2afa84c62d46b774d32e753df18"; // SD-SPK
    private const string GuidRow = "90421d9ab6fcd564dbf07dd699b34bd1"; // SD-SDDK
    private const string GuidBuy = "164a3c5bc6bd79a45a27313a52777722"; // SD-GMANN
    private const string GuidCancel = "9587847749d98ad4fb79f117b79183e9"; // SD-QXANN
    private const string GuidConfirmFrame = "758956f8407e6f1438c58b26a4692610"; // SD-GMDK
    private const string GuidQtyFrame = "7eff8bf3c20ba2f4b97df2c89c216b67"; // SD-SLK
    private const string GuidMinus = "197b3b3250612da438746828e3acba6c"; // SD-JH
    private const string GuidPlus = "5cc4a5b2001556941af5f4d2b34e5d7b"; // SD-JH1
    private const string GuidGold = "1afc5e6dd14599f42a6f755fc9c67858"; // ZJM-JB
    private const string GuidBack = "dab8873160ec5fb4b989047629fc47c9"; // BB-FH
    private const string GuidDescBox = "f1dd3ca7fe6292b43a7f7dfa40f5d0e5"; // SD-JSK

    private static readonly Color LabelDark = new Color(0.22f, 0.14f, 0.08f, 1f);
    private static readonly Color LabelLight = new Color(1f, 1f, 1f, 1f);

    private static string MarkerFilePath => Path.Combine(Directory.GetCurrentDirectory(), "ShopPanelSdSetup.run");

    static ShopPanelSdSetup()
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

        try { File.Delete(MarkerFilePath); }
        catch (Exception e)
        {
            Debug.LogError(LogTag + " Failed to delete marker: " + e.Message);
            return;
        }

        Debug.Log(LogTag + " Auto-run triggered.");
        Run(false);
    }

    [MenuItem("Tools/Template/Build Shop Panel SD UI")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Run(true);
    }

    public static void BuildInto(UIPanel shopPanel)
    {
        if (shopPanel == null)
        {
            throw new Exception("ShopPanel missing.");
        }

        EnsureWindowAndContent(shopPanel);
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        ClearBuilt(shopPanel);
        PrepareWindow(shopPanel);

        Sprite merchant = LoadSprite(GuidMerchant, "SD-TP");
        Sprite chest = LoadSprite(GuidChest, "SD-BX");
        Sprite chestAlt = LoadSprite(GuidChestAlt, "SD-BX1");
        Sprite barBg = LoadSprite(GuidBarBg, "SD-JDT");
        Sprite barFill = LoadSprite(GuidBarFill, "SD-JDTD");
        Sprite rightPanel = LoadSprite(GuidRightPanel, "SD-SPK");
        Sprite buySp = LoadSprite(GuidBuy, "SD-GMANN");
        Sprite gold = LoadSprite(GuidGold, "ZJM-JB");
        Sprite back = LoadSprite(GuidBack, "BB-FH");
        Sprite descBox = LoadSprite(GuidDescBox, "SD-JSK");

        RectTransform content = shopPanel.Content;
        StretchFull(content);

        // Left merchant + chests + progress
        Image tp = CreateSprite("Merchant", content, merchant, false);
        SetRect(tp.rectTransform, new Vector2(-520f, 80f), new Vector2(700f, 450f));

        Image c1 = CreateSprite("Chest1", content, chest, true);
        SetRect(c1.rectTransform, new Vector2(-700f, -220f), new Vector2(130f, 130f));
        Image c2 = CreateSprite("Chest2", content, chest, true);
        SetRect(c2.rectTransform, new Vector2(-520f, -220f), new Vector2(130f, 130f));
        Image c3 = CreateSprite("Chest3", content, chestAlt, true);
        SetRect(c3.rectTransform, new Vector2(-340f, -220f), new Vector2(130f, 130f));

        Image pBg = CreateSprite("ProgressBg", content, barBg, false);
        SetRect(pBg.rectTransform, new Vector2(-520f, -340f), new Vector2(620f, 70f));
        Image pFill = CreateSprite("ProgressFill", pBg.transform, barFill, false);
        StretchFull(pFill.rectTransform);
        pFill.type = Image.Type.Filled;
        pFill.fillMethod = Image.FillMethod.Horizontal;
        pFill.fillAmount = 1f;
        TMP_Text progressLabel = CreateText("ProgressLabel", pBg.transform, "5/5", 32f, LabelLight, font, TextAlignmentOptions.Center);
        StretchFull(progressLabel.rectTransform);

        TMP_Text refresh = CreateText("RefreshTimer", content, "Refresh in 24:00:00", 28f, LabelDark, font, TextAlignmentOptions.Center);
        SetRect(refresh.rectTransform, new Vector2(-520f, -410f), new Vector2(620f, 40f));

        // Right panel
        Image spk = CreateSprite("RightPanel", content, rightPanel, false);
        SetRect(spk.rectTransform, new Vector2(400f, -10f), new Vector2(1000f, 820f));

        RectTransform list = CreateRect("ItemList", spk.transform);
        list.anchorMin = new Vector2(0.08f, 0.42f);
        list.anchorMax = new Vector2(0.92f, 0.92f);
        list.offsetMin = Vector2.zero;
        list.offsetMax = Vector2.zero;
        VerticalLayoutGroup vlg = list.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 12f;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        ContentSizeFitter csf = list.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ItemData
        RectTransform itemData = CreateRect("ItemData", spk.transform);
        itemData.anchorMin = new Vector2(0.08f, 0.06f);
        itemData.anchorMax = new Vector2(0.92f, 0.40f);
        itemData.offsetMin = Vector2.zero;
        itemData.offsetMax = Vector2.zero;

        Image descBg = CreateSprite("DescBox", itemData, descBox, false);
        SetRect(descBg.rectTransform, new Vector2(120f, 40f), new Vector2(520f, 200f));

        Image itemIcon = CreateImage("ItemIcon", itemData, new Color(0.55f, 0.40f, 0.85f, 1f), false);
        SetRect(itemIcon.rectTransform, new Vector2(-280f, 40f), new Vector2(140f, 140f));

        TMP_Text itemName = CreateText("ItemName", itemData, "Item", 34f, LabelDark, font, TextAlignmentOptions.Left);
        SetRect(itemName.rectTransform, new Vector2(80f, 100f), new Vector2(480f, 40f));

        TMP_Text itemDesc = CreateText("ItemDescription", itemData, "Description", 24f, LabelDark, font, TextAlignmentOptions.TopLeft);
        SetRect(itemDesc.rectTransform, new Vector2(120f, 20f), new Vector2(480f, 120f));
        itemDesc.enableWordWrapping = true;

        Image goldIcon = CreateSprite("PriceGold", itemData, gold, false);
        SetRect(goldIcon.rectTransform, new Vector2(-60f, -90f), new Vector2(56f, 56f));
        TMP_Text itemPrice = CreateText("ItemPrice", itemData, "150 Gold", 30f, LabelDark, font, TextAlignmentOptions.Left);
        SetRect(itemPrice.rectTransform, new Vector2(80f, -90f), new Vector2(280f, 50f));

        Image buyImg = CreateSprite("ItemBUY", itemData, buySp, true);
        SetRect(buyImg.rectTransform, new Vector2(280f, -95f), new Vector2(320f, 110f));
        Button itemBuy = buyImg.gameObject.AddComponent<Button>();
        itemBuy.targetGraphic = buyImg;
        itemBuy.transition = Selectable.Transition.None;

        Image backImg = CreateSprite("BackButton", shopPanel.Window, back, true);
        SetRect(backImg.rectTransform, new Vector2(-860f, -430f), new Vector2(165f, 166f));
        Button backBtn = backImg.gameObject.AddComponent<Button>();
        backBtn.targetGraphic = backImg;
        backBtn.transition = Selectable.Transition.None;

        ShopItemSlot prefab = EnsureShopPrefab(font);
        UIPanel buy = null; // BuyPanelController removed

        ShopPanelController ctrl = shopPanel.GetComponent<ShopPanelController>();
        if (ctrl == null)
        {
            ctrl = shopPanel.gameObject.AddComponent<ShopPanelController>();
        }

        SerializedObject so = new SerializedObject(ctrl);
        ItemConfig itemConfig = AssetDatabase.LoadAssetAtPath<ItemConfig>("Assets/Config/ItemConfig.asset");
        SetRef(so, "listRoot", list);
        if (itemConfig != null)
        {
            SetRef(so, "itemConfig", itemConfig);
        }
        SetRef(so, "slotPrefab", prefab);
        // buyPanel UIPanel wired by ShopBuyFoldWireSetup
        SetRef(so, "itemIcon", itemIcon);
        SetRef(so, "itemName", itemName);
        SetRef(so, "itemDescription", itemDesc);
        SetRef(so, "itemPrice", itemPrice);
        SetRef(so, "itemBuyButton", itemBuy);
        SetRef(so, "progressFill", pFill);
        SetRef(so, "progressLabel", progressLabel);
        SetRef(so, "refreshTimerLabel", refresh);
        so.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject panelSo = new SerializedObject(shopPanel);
        SetRef(panelSo, "closeButton", backBtn);
        panelSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Run(bool interactive)
    {
        try
        {
            if (!File.Exists(MainScenePath))
            {
                Debug.LogError(LogTag + " MainScene missing.");
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
                Debug.LogWarning(LogTag + " MainScene dirty; auto-run will rebuild and save.");
            }

            UIPanel panel = FindShopPanel(scene);
            if (panel == null)
            {
                Debug.LogError(LogTag + " ShopPanel not found.");
                CloseIfOpened(scene, openedHere);
                return;
            }

            if (interactive)
            {
                ShopPanelController existing = panel.GetComponent<ShopPanelController>();
                if (existing != null && panel.Content != null && panel.Content.Find("RightPanel") != null)
                {
                    if (!EditorUtility.DisplayDialog("Build Shop Panel SD UI", "Shop SD UI exists. Rebuild?", "Rebuild", "Cancel"))
                    {
                        return;
                    }
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
                    Debug.Log(LogTag + " Shop SD UI built and saved.");
                    try
                    {
                        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "ShopPanelSdSetup.ok"),
                            DateTime.Now.ToString("o") + " ok");
                    }
                    catch { }
                }
                else
                {
                    Debug.LogError(LogTag + " Save failed.");
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "ShopPanelSdSetup.err"), e.ToString()); } catch { }
        }
    }

    private static UIPanel CreateBuyPanelObsolete(Transform parent, TMP_FontAsset font)
    {
        Debug.LogWarning("[Editor] CreateBuyPanel obsolete; Buy folded into ShopPanelController. Use ShopBuyFoldWireSetup.");
        return null;
    }

    private static ShopItemSlot EnsureShopPrefab(TMP_FontAsset font)
    {
        Sprite row = LoadSprite(GuidRow, "SD-SDDK");
        if (!AssetDatabase.IsValidFolder("Assets/Prefab"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefab");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Prefab/UI"))
        {
            AssetDatabase.CreateFolder("Assets/Prefab", "UI");
        }

        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefabPath);
        if (existing != null)
        {
            AssetDatabase.DeleteAsset(ShopPrefabPath);
        }

        Image bg = CreateSprite("ShopPrefab", null, row, true);
        SetRect(bg.rectTransform, Vector2.zero, new Vector2(900f, 140f));
        LayoutElement le = bg.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 140f;
        le.minHeight = 140f;

        Button click = bg.gameObject.AddComponent<Button>();
        click.targetGraphic = bg;
        click.transition = Selectable.Transition.ColorTint;

        Image icon = CreateImage("Icon", bg.transform, new Color(0.55f, 0.40f, 0.85f, 1f), false);
        SetRect(icon.rectTransform, new Vector2(-360f, 0f), new Vector2(100f, 100f));

        TMP_Text name = CreateText("Name", bg.transform, "Healing Potion", 34f, LabelDark, font, TextAlignmentOptions.Left);
        SetRect(name.rectTransform, new Vector2(-80f, 20f), new Vector2(480f, 44f));

        TMP_Text price = CreateText("Price", bg.transform, "150 Gold", 28f, LabelDark, font, TextAlignmentOptions.Left);
        SetRect(price.rectTransform, new Vector2(-80f, -30f), new Vector2(480f, 36f));

        ShopItemSlot slot = bg.gameObject.AddComponent<ShopItemSlot>();
        SerializedObject so = new SerializedObject(slot);
        SetRef(so, "iconImage", icon);
        SetRef(so, "nameLabel", name);
        SetRef(so, "priceLabel", price);
        SetRef(so, "clickButton", click);
        so.ApplyModifiedPropertiesWithoutUndo();

        bool ok;
        PrefabUtility.SaveAsPrefabAsset(bg.gameObject, ShopPrefabPath, out ok);
        UnityEngine.Object.DestroyImmediate(bg.gameObject);
        if (!ok)
        {
            throw new Exception("Failed to save ShopPrefab.");
        }

        Debug.Log(LogTag + " Created " + ShopPrefabPath);
        return AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefabPath).GetComponent<ShopItemSlot>();
    }

    private static void ClearBuilt(UIPanel panel)
    {
        ShopPanelController old = panel.GetComponent<ShopPanelController>();
        if (old != null)
        {
            UnityEngine.Object.DestroyImmediate(old);
        }

        if (panel.Content != null)
        {
            for (int i = panel.Content.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(panel.Content.GetChild(i).gameObject);
            }
        }

        DestroyChild(panel.Window, "BackButton");
        DestroyChild(panel.transform, "BuyPanel");
        DestroyChild(panel.transform, "ShopSlotTemplate");
        DestroyChild(panel.Content, "ItemList");
    }

    private static void PrepareWindow(UIPanel panel)
    {
        StretchFull(panel.Window);
        Image winImg = panel.Window.GetComponent<Image>();
        if (winImg != null)
        {
            winImg.color = new Color(1f, 1f, 1f, 0f);
            winImg.raycastTarget = false;
            winImg.sprite = null;
        }

        Transform header = panel.Window.Find("Header");
        if (header != null)
        {
            header.gameObject.SetActive(false);
        }
    }

    private static void EnsureWindowAndContent(UIPanel panel)
    {
        SerializedObject so = new SerializedObject(panel);
        SerializedProperty windowProp = so.FindProperty("window");
        SerializedProperty contentProp = so.FindProperty("content");
        RectTransform window = windowProp != null ? windowProp.objectReferenceValue as RectTransform : null;
        if (window == null)
        {
            Transform t = panel.transform.Find("Window");
            if (t == null)
            {
                Image winImg = CreateImage("Window", panel.transform, new Color(1f, 1f, 1f, 0f), false);
                StretchFull(winImg.rectTransform);
                t = winImg.transform;
            }

            window = t as RectTransform;
            if (windowProp != null) windowProp.objectReferenceValue = window;
        }

        RectTransform content = contentProp != null ? contentProp.objectReferenceValue as RectTransform : null;
        if (content == null && window != null)
        {
            Transform t = window.Find("Content");
            if (t == null)
            {
                t = CreateRect("Content", window).transform;
                StretchFull(t as RectTransform);
            }

            content = t as RectTransform;
            if (contentProp != null) contentProp.objectReferenceValue = content;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
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

    private static Sprite LoadSprite(string guid, string label)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) throw new Exception("Missing sprite " + label);
        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sp == null) throw new Exception("Load fail " + label + " @ " + path);
        return sp;
    }

    private static void DestroyChild(Transform parent, string name)
    {
        if (parent == null) return;
        Transform t;
        while ((t = parent.Find(name)) != null)
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
        if (p == null) throw new Exception("Field not found: " + field);
        p.objectReferenceValue = value;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
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
        img.color = Color.white;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.raycastTarget = raycast;
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

    private static void SetSize(RectTransform rt, float w, float h)
    {
        if (rt == null) return;
        rt.sizeDelta = new Vector2(w, h);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }
}