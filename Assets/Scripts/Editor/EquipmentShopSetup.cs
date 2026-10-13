using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures ItemConfig has Gold Pouch + 8 equipment shop items (ZB-BQ/MZ/XZ/YF). Removes Healing Potion if present.
/// Menu: Tools/Template/Ensure Equipment Shop Items
/// One-shot: EquipmentShopSetup.run
/// </summary>
[InitializeOnLoad]
public static class EquipmentShopSetup
{
    private const string LogTag = "[EquipmentShopSetup]";
    private const string ItemConfigPath = ConfigAssetSetup.ItemConfigPath;

    private static string MarkerPath => Path.Combine(Directory.GetCurrentDirectory(), "EquipmentShopSetup.run");
    private static string OkPath => Path.Combine(Directory.GetCurrentDirectory(), "EquipmentShopSetup.ok");

    private struct EquipDef
    {
        public string id;
        public string displayName;
        public string description;
        public int price;
        public EquipItemType equipType;
        public int attackBonus;
        public string statBonusLabel;
        public float statBonusValue;
        public string specialEffectId;
        public float specialValue;
        public string specialDescription;
        public string iconLocation;
    }

    private static readonly EquipDef[] Equipment =
    {
        new EquipDef
        {
            id = GameItemDefs.IronPikeId,
            displayName = "Iron Pike",
            description = "A sturdy iron pike. Boosts attack power.",
            price = 120,
            equipType = EquipItemType.Spear,
            attackBonus = 2,
            statBonusLabel = "Attack +2",
            statBonusValue = 0f,
            specialEffectId = "crit_chance",
            specialValue = 0.15f,
            specialDescription = "15% chance to critically strike.",
            iconLocation = "ZB-BQ1"
        },
        new EquipDef
        {
            id = GameItemDefs.CoralLanceId,
            displayName = "Coral Lance",
            description = "Coral-forged lance with draining barbs.",
            price = 280,
            equipType = EquipItemType.Spear,
            attackBonus = 4,
            statBonusLabel = "Attack +4",
            statBonusValue = 0f,
            specialEffectId = "lifesteal",
            specialValue = 0.20f,
            specialDescription = "20% lifesteal on hit.",
            iconLocation = "ZB-BQ2"
        },
        new EquipDef
        {
            id = GameItemDefs.TideHoodId,
            displayName = "Tide Hood",
            description = "Hood soaked in tide charm. Extends attack reach.",
            price = 100,
            equipType = EquipItemType.Hood,
            attackBonus = 0,
            statBonusLabel = "Attack Range +0.2",
            statBonusValue = 0.2f,
            specialEffectId = "net_retrieve_exp",
            specialValue = 10f,
            specialDescription = "On net retrieve, gain +10 EXP.",
            iconLocation = "ZB-MZ1"
        },
        new EquipDef
        {
            id = GameItemDefs.DeepveilMaskId,
            displayName = "Deepveil Mask",
            description = "Veils your presence and stretches spear range.",
            price = 240,
            equipType = EquipItemType.Hood,
            attackBonus = 0,
            statBonusLabel = "Attack Range +0.4",
            statBonusValue = 0.4f,
            specialEffectId = "net_retrieve_double_exp",
            specialValue = 0.15f,
            specialDescription = "On net retrieve, 15% chance to double EXP.",
            iconLocation = "ZB-MZ2"
        },
        new EquipDef
        {
            id = GameItemDefs.ReefBootsId,
            displayName = "Reef Boots",
            description = "Grippy reef soles that hurry the net outward.",
            price = 110,
            equipType = EquipItemType.Shoes,
            attackBonus = 0,
            statBonusLabel = "Net Speed +10%",
            statBonusValue = 0.1f,
            specialEffectId = "net_capacity",
            specialValue = 4f,
            specialDescription = "Net capacity fixed +4.",
            iconLocation = "ZB-XZ1"
        },
        new EquipDef
        {
            id = GameItemDefs.GaleSolesId,
            displayName = "Gale Soles",
            description = "Wind-kissed soles that whip the net faster.",
            price = 260,
            equipType = EquipItemType.Shoes,
            attackBonus = 0,
            statBonusLabel = "Net Speed +20%",
            statBonusValue = 0.2f,
            specialEffectId = "net_retrieve_speed_levels",
            specialValue = 3f,
            specialDescription = "Net retrieve speed +3 levels.",
            iconLocation = "ZB-XZ2"
        },
        new EquipDef
        {
            id = GameItemDefs.ShellPlateId,
            displayName = "Shell Plate",
            description = "Layered shell plating that toughens your body.",
            price = 150,
            equipType = EquipItemType.Armor,
            attackBonus = 0,
            statBonusLabel = "HP +5",
            statBonusValue = 5f,
            specialEffectId = "hit_dmg_reduce",
            specialValue = 1f,
            specialDescription = "Each hit taken reduces damage by 1.",
            iconLocation = "ZB-YF1"
        },
        new EquipDef
        {
            id = GameItemDefs.AbyssMailId,
            displayName = "Abyss Mail",
            description = "Mail from the abyss that hardens resolve.",
            price = 300,
            equipType = EquipItemType.Armor,
            attackBonus = 0,
            statBonusLabel = "HP +10",
            statBonusValue = 10f,
            specialEffectId = "dodge_chance",
            specialValue = 0.15f,
            specialDescription = "15% chance to dodge the attack.",
            iconLocation = "ZB-YF2"
        }
    };

    static EquipmentShopSetup()
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

    [MenuItem("Tools/Template/Ensure Equipment Shop Items")]
    public static void BuildMenu()
    {
        Run();
    }

    public static void Run()
    {
        try
        {
            ConfigAssetSetup.EnsureAssets();
            ItemConfig cfg = AssetDatabase.LoadAssetAtPath<ItemConfig>(ItemConfigPath);
            if (cfg == null)
            {
                Debug.LogError(LogTag + " ItemConfig missing at " + ItemConfigPath);
                return;
            }

            SerializedObject so = new SerializedObject(cfg);
            SerializedProperty items = so.FindProperty("items");
            RemoveHealPotion(items);
            EnsureGoldPouch(items);
            for (int i = 0; i < Equipment.Length; i++)
            {
                UpsertEquipment(items, Equipment[i]);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();

            int count = items.arraySize;
            string msg = LogTag + " ItemConfig ready. entries=" + count + " (gold pouch + 8 equipment, heal removed). Icons AddressByFileName under Assets/UI.";
            Debug.Log(msg);
            try
            {
                File.WriteAllText(OkPath, msg + "\n" + DateTime.Now.ToString("o"));
            }
            catch
            {
                // ignore
            }
        }
        catch (Exception e)
        {
            Debug.LogError(LogTag + " " + e);
        }
    }

    private static void RemoveHealPotion(SerializedProperty items)
    {
        int index = FindIndex(items, GameItemDefs.HealPotionId);
        if (index < 0)
        {
            return;
        }

        items.DeleteArrayElementAtIndex(index);
    }

private static void EnsureGoldPouch(SerializedProperty items)
    {
        int index = FindIndex(items, GameItemDefs.GoldPouchId);
        if (index < 0)
        {
            index = items.arraySize;
            items.arraySize++;
        }

        SerializedProperty entry = items.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("id").stringValue = GameItemDefs.GoldPouchId;
        entry.FindPropertyRelative("displayName").stringValue = "Gold Pouch";
        entry.FindPropertyRelative("description").stringValue = "Exchange gems for gold. Grants 50 gold instantly.";
        entry.FindPropertyRelative("price").intValue = 10;
        entry.FindPropertyRelative("currency").enumValueIndex = (int)CurrencyType.Gems;
        entry.FindPropertyRelative("iconLocation").stringValue = "ZJM-JBd";
        entry.FindPropertyRelative("grantCurrency").enumValueIndex = (int)CurrencyType.Gold;
        entry.FindPropertyRelative("grantAmount").intValue = 50;
        entry.FindPropertyRelative("equipType").enumValueIndex = (int)EquipItemType.None;
        entry.FindPropertyRelative("attackBonus").intValue = 0;
        entry.FindPropertyRelative("statBonusLabel").stringValue = string.Empty;
        entry.FindPropertyRelative("statBonusValue").floatValue = 0f;
        entry.FindPropertyRelative("specialEffectId").stringValue = string.Empty;
        entry.FindPropertyRelative("specialValue").floatValue = 0f;
        entry.FindPropertyRelative("specialDescription").stringValue = string.Empty;
    }

    private static void UpsertEquipment(SerializedProperty items, EquipDef def)
    {
        int index = FindIndex(items, def.id);
        if (index < 0)
        {
            index = items.arraySize;
            items.arraySize++;
        }

        SerializedProperty entry = items.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("id").stringValue = def.id;
        entry.FindPropertyRelative("displayName").stringValue = def.displayName;
        entry.FindPropertyRelative("description").stringValue = def.description;
        entry.FindPropertyRelative("price").intValue = def.price;
        entry.FindPropertyRelative("currency").enumValueIndex = (int)CurrencyType.Gold;
        entry.FindPropertyRelative("iconLocation").stringValue = def.iconLocation;
        entry.FindPropertyRelative("grantCurrency").enumValueIndex = (int)CurrencyType.Gold;
        entry.FindPropertyRelative("grantAmount").intValue = 0;
        entry.FindPropertyRelative("equipType").enumValueIndex = (int)def.equipType;
        entry.FindPropertyRelative("attackBonus").intValue = def.attackBonus;
        entry.FindPropertyRelative("statBonusLabel").stringValue = def.statBonusLabel ?? string.Empty;
        entry.FindPropertyRelative("statBonusValue").floatValue = def.statBonusValue;
        entry.FindPropertyRelative("specialEffectId").stringValue = def.specialEffectId ?? string.Empty;
        entry.FindPropertyRelative("specialValue").floatValue = def.specialValue;
        entry.FindPropertyRelative("specialDescription").stringValue = def.specialDescription ?? string.Empty;
    }

    private static int FindIndex(SerializedProperty items, string id)
    {
        for (int i = 0; i < items.arraySize; i++)
        {
            SerializedProperty entry = items.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("id").stringValue == id)
            {
                return i;
            }
        }

        return -1;
    }
}
