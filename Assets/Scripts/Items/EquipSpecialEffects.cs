using System;
using UnityEngine;

/// <summary>
/// Battle-time equip special apply point. Combat systems call this later;
/// current stub never applies real effects.
/// </summary>
public interface IEquipSpecialEffect
{
    string EffectId { get; }
    bool TryApply(EquipSpecialContext context);
}

/// <summary>
/// Minimal context for future battle hooks. Fields are placeholders.
/// </summary>
public sealed class EquipSpecialContext
{
    public string stageId;
    public bool isEliteTarget;
    public float currentHpRatio = 1f;
}

/// <summary>
/// Looks up equip special data from ItemConfig and exposes a stub apply path.
/// Does not invent combat systems — TryApply always returns false until battle wires in.
/// </summary>
public static class EquipSpecialEffects
{
    private sealed class StubEffect : IEquipSpecialEffect
    {
        public string EffectId { get; }
        public StubEffect(string effectId) { EffectId = effectId ?? string.Empty; }
        public bool TryApply(EquipSpecialContext context)
        {
            // Stub: no combat integration yet.
            return false;
        }
    }

    public static bool IsEquipment(string itemId)
    {
        GameItemDefs.Def def = GameItemDefs.Get(itemId);
        return def != null && def.equipType != EquipItemType.None;
    }

    public static IEquipSpecialEffect GetEffect(string itemId)
    {
        GameItemDefs.Def def = GameItemDefs.Get(itemId);
        if (def == null || def.equipType == EquipItemType.None || string.IsNullOrEmpty(def.specialEffectId))
        {
            return null;
        }

        return new StubEffect(def.specialEffectId);
    }

    /// <summary>Apply equip special. Stub returns false until battle implements effects.</summary>
    public static bool TryApply(string itemId, EquipSpecialContext context = null)
    {
        IEquipSpecialEffect effect = GetEffect(itemId);
        if (effect == null)
        {
            return false;
        }

        return effect.TryApply(context ?? new EquipSpecialContext());
    }

    public static int GetAttackBonus(string itemId)
    {
        GameItemDefs.Def def = GameItemDefs.Get(itemId);
        return def != null ? Mathf.Max(0, def.attackBonus) : 0;
    }
}