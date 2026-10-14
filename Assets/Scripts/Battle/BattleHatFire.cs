using UnityEngine;

/// <summary>
/// 挂在带 Animator 的帽子上。抛射动画事件调用 Fire，再转给角色。
/// </summary>
public class BattleHatFire : MonoBehaviour
{
    [SerializeField] private BattleCharacter character;

    public void Fire()
    {
        if (character != null)
        {
            character.Fire();
        }
    }
}
