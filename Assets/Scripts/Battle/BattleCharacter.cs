using System;
using UnityEngine;

/// <summary>
/// 角色身上的射程圈。网和矛共用这一半径。
/// </summary>
public class BattleCharacter : MonoBehaviour
{
    static readonly int ThrowTrigger = Animator.StringToHash("Att");

    [SerializeField] private BattleRangeRing rangeRing;
    [SerializeField] private Animator hatAnimator;
    [SerializeField] private Transform firePoint;

    Action _onThrow;

    public Vector3 FirePosition => firePoint != null ? firePoint.position : transform.position;

    public void SetRangeRadius(float radius)
    {
        if (rangeRing != null)
        {
            rangeRing.SetRadius(radius);
        }
    }

    public void SetThrowListener(Action onThrow)
    {
        _onThrow = onThrow;
    }

    /// <summary>播放帽子上的抛射动画。真正出手等动画事件 Fire。</summary>
    public void PlayThrow()
    {
        if (hatAnimator == null)
        {
            Fire();
            return;
        }

        hatAnimator.ResetTrigger(ThrowTrigger);
        hatAnimator.SetTrigger(ThrowTrigger);
    }

    /// <summary>抛射动画事件。在帽子物体上由 BattleHatFire 转发。</summary>
    public void Fire()
    {
        if (_onThrow != null)
        {
            _onThrow();
        }
    }
}