using UnityEngine;

/// <summary>
/// 角色身上的射程圈。网和矛共用这一半径。
/// </summary>
public class BattleCharacter : MonoBehaviour
{
    [SerializeField] private BattleRangeRing rangeRing;

    public void SetRangeRadius(float radius)
    {
        if (rangeRing != null)
        {
            rangeRing.SetRadius(radius);
        }
    }
}