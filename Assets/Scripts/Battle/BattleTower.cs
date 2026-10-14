using UnityEngine;

/// <summary>
/// 塔。捕获塔和攻击塔是两个预制体，种类用序列化枚举区分。
/// </summary>
public class BattleTower : MonoBehaviour
{
    [SerializeField] private SpriteRenderer rendererSprite;
    [SerializeField] private BattleRangeRing rangeRing;
    [SerializeField] private Transform firePoint;
    [Tooltip("炮口转到目标方向的角速度（度/秒）")]
    [SerializeField] private float aimDegreesPerSecond = 240f;
    [SerializeField] private BattleBuildingKind kind = BattleBuildingKind.Capture;

    public BattleBuildingKind Kind => kind;

    public Vector3 FirePosition => firePoint != null ? firePoint.position : transform.position;

    /// <summary>
    /// 绕塔中心逐步旋转，使 Fire 点落到朝向目标的方向上。已经对准时返回 true。
    /// </summary>
    public bool AimToward(Vector3 worldPoint, float dt)
    {
        if (firePoint == null || dt <= 0f)
        {
            return false;
        }

        Vector3 toTarget = worldPoint - transform.position;
        toTarget.z = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        Vector3 muzzle = firePoint.localPosition;
        muzzle.z = 0f;
        float aim = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        float rest = muzzle.sqrMagnitude > 0.0001f ? Mathf.Atan2(muzzle.y, muzzle.x) * Mathf.Rad2Deg : 0f;
        float desired = aim - rest;
        float current = transform.eulerAngles.z;
        float next = Mathf.MoveTowardsAngle(current, desired, aimDegreesPerSecond * dt);
        transform.rotation = Quaternion.Euler(0f, 0f, next);
        return Mathf.Abs(Mathf.DeltaAngle(next, desired)) <= 0.5f;
    }

    public void ApplyColor(Color color)
    {
        if (rendererSprite != null)
        {
            rendererSprite.color = color;
        }
    }

    public void SetRangeRadius(float radius)
    {
        if (rangeRing != null)
        {
            rangeRing.SetQuietRadius(radius);
        }
    }

    public void ShowRangeBriefly()
    {
        if (rangeRing != null)
        {
            rangeRing.ShowBriefly();
        }
    }
}