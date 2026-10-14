/// <summary>
/// 敌人受到的伤害来源，用于飘字颜色区分。
/// </summary>
public enum BattleDamageKind
{
    Spear = 0,
    AttackTower = 1,
    Attractor = 2,
    /// <summary>Damage taken by the player (float tint).</summary>
    Player = 3,
    /// <summary>Damage taken by a plank from enemies.</summary>
    Plank = 4
}
