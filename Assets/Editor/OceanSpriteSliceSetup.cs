#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 将 bol / gaoliang 切为 Multiple Sprite，并写回 BattleWater 预制体的 Sprite[]。
/// 菜单：Tools/Ocean/Slice Bol+Gaoliang And Rebind BattleWater
/// </summary>
public static class OceanSpriteSliceSetup
{
    private const string BolPath = "Assets/UI/US-1010-BYDR-GM-GQ/bol.png";
    private const string GaoliangPath = "Assets/UI/US-1010-BYDR-GM-GQ/gaoliang.png";
    private const string DiPath = "Assets/UI/US-1010-BYDR-GM-GQ/haidi.png";
    private const string PrefabPath = "Assets/Prefab/Battle/BattleWater.prefab";

    [MenuItem("Tools/Ocean/Slice Bol+Gaoliang And Rebind BattleWater")]
    public static void SliceAndRebind()
    {
        // bol: 500x500 -> 4x4 cells of 125
        SliceGrid(BolPath, "bol", 4, 4, 125, 125);
        // gaoliang: 612x408 -> 4x3 cells of 153x136
        SliceGrid(GaoliangPath, "gaoliang", 4, 3, 153, 136);

        AssetDatabase.Refresh();

        Sprite[] bolSprites = LoadSortedSprites(BolPath, "bol_");
        Sprite[] gaoSprites = LoadSortedSprites(GaoliangPath, "gaoliang_");
        Sprite diSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DiPath);

        if (bolSprites.Length == 0)
        {
            Debug.LogError("[OceanSlice] bol slices missing after reimport.");
            return;
        }

        if (gaoSprites.Length == 0)
        {
            Debug.LogError("[OceanSlice] gaoliang slices missing after reimport.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            OceanCartoonRoot ocean = root.GetComponent<OceanCartoonRoot>();
            if (ocean == null)
            {
                Debug.LogError("[OceanSlice] OceanCartoonRoot missing on BattleWater.");
                return;
            }

            SerializedObject so = new SerializedObject(ocean);
            so.FindProperty("backgroundSprite").objectReferenceValue = diSprite;
            AssignSpriteArray(so, "rippleSprites", bolSprites);
            so.FindProperty("rippleSprite").objectReferenceValue = bolSprites[0];
            AssignSpriteArray(so, "highlightSprites", gaoSprites);
            so.FindProperty("highlightSprite").objectReferenceValue = gaoSprites[0];
            so.ApplyModifiedPropertiesWithoutUndo();

            OceanHighlightController hc = root.GetComponentInChildren<OceanHighlightController>(true);
            if (hc != null)
            {
                SerializedObject hso = new SerializedObject(hc);
                AssignSpriteArray(hso, "highlightSprites", gaoSprites);
                hso.FindProperty("highlightSprite").objectReferenceValue = gaoSprites[0];
                hso.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[OceanSlice] OK. bol={bolSprites.Length} gaoliang={gaoSprites.Length}. Prefab rebound: {PrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AssignSpriteArray(SerializedObject so, string propName, Sprite[] sprites)
    {
        SerializedProperty prop = so.FindProperty(propName);
        if (prop == null || !prop.isArray)
        {
            Debug.LogWarning($"[OceanSlice] property missing: {propName}");
            return;
        }

        prop.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }
    }

    private static void SliceGrid(string assetPath, string prefix, int cols, int rows, int cellW, int cellH)
    {
        TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti == null)
        {
            Debug.LogError($"[OceanSlice] TextureImporter missing: {assetPath}");
            return;
        }

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear;
        ti.spritePixelsPerUnit = 100;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.wrapMode = TextureWrapMode.Clamp;

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        int texH = tex != null ? tex.height : rows * cellH;

        var metas = new List<SpriteMetaData>();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int i = r * cols + c;
                // Unity rect origin = bottom-left
                float x = c * cellW;
                float y = (rows - 1 - r) * cellH;
                metas.Add(new SpriteMetaData
                {
                    name = $"{prefix}_{i}",
                    rect = new Rect(x, y, cellW, cellH),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero
                });
            }
        }

        ti.spritesheet = metas.ToArray();
        EditorUtility.SetDirty(ti);
        ti.SaveAndReimport();
        Debug.Log($"[OceanSlice] sliced {assetPath} -> {metas.Count} ({cols}x{rows} cell {cellW}x{cellH}, texH={texH})");
    }

    private static Sprite[] LoadSortedSprites(string assetPath, string namePrefix)
    {
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        List<Sprite> list = new List<Sprite>();
        foreach (Object o in all)
        {
            Sprite s = o as Sprite;
            if (s != null && s.name.StartsWith(namePrefix))
            {
                list.Add(s);
            }
        }

        return list
            .OrderBy(s =>
            {
                string n = s.name.Substring(namePrefix.Length);
                int idx;
                return int.TryParse(n, out idx) ? idx : 9999;
            })
            .ToArray();
    }
}
#endif
