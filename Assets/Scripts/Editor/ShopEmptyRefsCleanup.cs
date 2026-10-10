using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Wire empty shop Inspector refs; drop unused selectedHighlight.
/// Menu: Tools/Template/Fix Shop Empty Refs
/// One-shot: ShopEmptyRefsCleanup.run
/// </summary>
[InitializeOnLoad]
public static class ShopEmptyRefsCleanup
{
    private const string LogTag = "[ShopEmptyRefsCleanup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string ShopPrefabPath = "Assets/Prefab/UI/ShopPrefab.prefab";
    private static string MarkerPath => Path.Combine(Directory.GetCurrentDirectory(), "ShopEmptyRefsCleanup.run");

    static ShopEmptyRefsCleanup()
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

        try { File.Delete(MarkerPath); }
        catch (Exception e)
        {
            Debug.LogError(LogTag + " marker delete failed: " + e.Message);
            return;
        }

        Debug.Log(LogTag + " Auto-run.");
        Run();
    }

    [MenuItem("Tools/Template/Fix Shop Empty Refs")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

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

            UIPanel panel = FindShopPanel(scene);
            if (panel == null)
            {
                Debug.LogError(LogTag + " ShopPanel not found.");
                if (openedHere && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                string report = FixShopPanel(panel) + " | " + FixShopPrefab();
                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene))
                {
                    Debug.Log(LogTag + " Done. " + report);
                    File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "ShopEmptyRefsCleanup.ok"),
                        DateTime.Now.ToString("o") + " " + report);
                }
                else
                {
                    Debug.LogError(LogTag + " Save failed.");
                }
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
            try { File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "ShopEmptyRefsCleanup.err"), e.ToString()); } catch { }
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

    private static string FixShopPanel(UIPanel panel)
    {
        ShopPanelController ctrl = panel.GetComponent<ShopPanelController>();
        if (ctrl == null)
        {
            return "ShopPanelController missing";
        }

        SerializedObject so = new SerializedObject(ctrl);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        // listRoot: Scroll Content or ItemList under Window/RightPanel
        RectTransform listRoot = so.FindProperty("listRoot").objectReferenceValue as RectTransform;
        if (listRoot == null)
        {
            Transform window = panel.transform.Find("Window");
            Transform right = window != null ? window.Find("RightPanel") : null;
            if (right == null && panel.Content != null)
            {
                right = panel.Content.Find("RightPanel");
            }

            Transform found = null;
            if (right != null)
            {
                found = right.Find("Scroll View/Viewport/Content");
                if (found == null) found = right.Find("ItemList");
                if (found == null)
                {
                    found = DeepFind(right, "Content");
                    if (found != null && found.parent != null && found.parent.name != "Viewport")
                    {
                        // prefer Viewport/Content
                        Transform vp = DeepFind(right, "Viewport");
                        if (vp != null && vp.Find("Content") != null) found = vp.Find("Content");
                    }
                }
            }

            if (found != null)
            {
                SetRef(so, "listRoot", found as RectTransform ?? found.GetComponent<RectTransform>());
                sb.Append("listRoot<-").Append(GetPath(found)).Append("; ");
            }
            else
            {
                sb.Append("listRoot STILL EMPTY; ");
            }
        }
        else
        {
            sb.Append("listRoot ok; ");
        }

        // itemName under ItemData
        TMP_Text itemName = so.FindProperty("itemName").objectReferenceValue as TMP_Text;
        if (itemName == null)
        {
            Transform window = panel.transform.Find("Window");
            Transform itemData = window != null ? window.Find("ItemData") : null;
            if (itemData == null && panel.Content != null)
            {
                itemData = panel.Content.Find("ItemData");
            }

            if (itemData != null)
            {
                Transform existing = itemData.Find("ItemName");
                TMP_Text tmp;
                if (existing != null)
                {
                    tmp = existing.GetComponent<TMP_Text>();
                }
                else
                {
                    TMP_FontAsset font = TMP_Settings.defaultFontAsset;
                    GameObject go = new GameObject("ItemName", typeof(RectTransform));
                    go.transform.SetParent(itemData, false);
                    TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
                    if (font != null) t.font = font;
                    t.text = "Item";
                    t.fontSize = 34f;
                    t.color = new Color(0.25f, 0.18f, 0.12f, 1f);
                    t.alignment = TextAlignmentOptions.Left;
                    t.raycastTarget = false;
                    RectTransform rt = t.rectTransform;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(80f, 200f);
                    rt.sizeDelta = new Vector2(480f, 40f);
                    tmp = t;
                    sb.Append("created ItemName; ");
                }

                if (tmp != null)
                {
                    SetRef(so, "itemName", tmp);
                    sb.Append("itemName wired; ");
                }
            }
            else
            {
                sb.Append("ItemData missing; ");
            }
        }
        else
        {
            sb.Append("itemName ok; ");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ctrl);
        return sb.ToString().Trim();
    }

    private static string FixShopPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ShopPrefabPath);
        if (root == null)
        {
            return "ShopPrefab missing";
        }

        try
        {
            ShopItemSlot slot = root.GetComponent<ShopItemSlot>();
            if (slot == null)
            {
                return "ShopItemSlot missing on prefab";
            }

            SerializedObject so = new SerializedObject(slot);
            System.Text.StringBuilder sb = new System.Text.StringBuilder("prefab: ");

            // Info TMP = name; Prite TMP = price (current priceLabel wrongly points at Info)
            Transform info = root.transform.Find("Info");
            Transform prite = root.transform.Find("BUY/Prite");
            if (prite == null) prite = DeepFind(root.transform, "Prite");
            Transform nameGo = root.transform.Find("Name");
            Transform priceGo = root.transform.Find("Price");

            TMP_Text nameTmp = null;
            if (info != null) nameTmp = info.GetComponent<TMP_Text>();
            if (nameTmp == null && nameGo != null) nameTmp = nameGo.GetComponent<TMP_Text>();

            TMP_Text priceTmp = null;
            if (prite != null) priceTmp = prite.GetComponent<TMP_Text>();
            if (priceTmp == null && priceGo != null) priceTmp = priceGo.GetComponent<TMP_Text>();

            TMP_Text curName = so.FindProperty("nameLabel").objectReferenceValue as TMP_Text;
            TMP_Text curPrice = so.FindProperty("priceLabel").objectReferenceValue as TMP_Text;

            if (curName == null && nameTmp != null)
            {
                SetRef(so, "nameLabel", nameTmp);
                sb.Append("nameLabel<-Info; ");
            }
            else if (curName != null)
            {
                sb.Append("nameLabel ok; ");
            }
            else
            {
                sb.Append("nameLabel STILL EMPTY; ");
            }

            if (priceTmp != null && (curPrice == null || curPrice == nameTmp || (info != null && curPrice.gameObject == info.gameObject)))
            {
                SetRef(so, "priceLabel", priceTmp);
                sb.Append("priceLabel<-Prite; ");
            }
            else if (curPrice != null)
            {
                sb.Append("priceLabel ok; ");
            }

            // selectedHighlight field removed from script; nothing to clear
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, ShopPrefabPath);
            return sb.ToString().Trim();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform DeepFind(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform f = DeepFind(root.GetChild(i), name);
            if (f != null) return f;
        }

        return null;
    }

    private static string GetPath(Transform t)
    {
        if (t == null) return "?";
        string p = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            p = t.name + "/" + p;
            if (t.name == "ShopPanel" || t.name == "CanvasPanel") break;
        }

        return p;
    }

    private static void SetRef(SerializedObject so, string prop, UnityEngine.Object value)
    {
        SerializedProperty p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
    }
}
// YAML fallback applied 2026-10-10T18:14:14.8816515+08:00
