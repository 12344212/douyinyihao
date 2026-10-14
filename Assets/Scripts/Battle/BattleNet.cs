using TMPro;
using UnityEngine;

/// <summary>
/// 网圈预制体。右上角三枚数量只显示当前网里的木头、生铁、生肉，为 0 时隐藏。
/// </summary>
public class BattleNet : MonoBehaviour
{
    [SerializeField] private SpriteRenderer rendererSprite;
    [SerializeField] private TMP_Text woodCount;
    [SerializeField] private TMP_Text leafCount;
    [SerializeField] private TMP_Text scrapCount;
    [SerializeField] private GameObject woodFrame;
    [SerializeField] private GameObject leafFrame;
    [SerializeField] private GameObject scrapFrame;
    [Tooltip("网圈上的漂浮图，抛出和回收时绕 Z 轴转")]
    [SerializeField] private Transform floatVisual;
    [Tooltip("水面起伏高度（本地单位）")]
    [SerializeField] private float bobAmplitude = 0.045f;
    [Tooltip("起伏一周的秒数")]
    [SerializeField] private float bobPeriod = 1.15f;
    [Tooltip("静止时左右轻晃的角度")]
    [SerializeField] private float rockDegrees = 7f;
    [Tooltip("抛出、回收时每移动 1 世界单位转过的角度")]
    [SerializeField] private float spinDegreesPerUnit = 200f;
    [Tooltip("旋转角速度上限（度/秒）")]
    [SerializeField] private float maxSpinDegreesPerSecond = 720f;

    private float _bobTime;
    private float _spinAngle;
    private Vector3 _floatBaseLocal;

    private void Awake()
    {
        if (woodCount != null)
        {
            woodCount.color = BattleTuning.WoodColor;
        }

        if (leafCount != null)
        {
            leafCount.color = BattleTuning.IronColor;
        }

        if (scrapCount != null)
        {
            scrapCount.color = BattleTuning.MeatColor;
        }

        SetHeldCounts(0, 0, 0);
        if (floatVisual != null)
        {
            _floatBaseLocal = floatVisual.localPosition;
            _bobTime = Random.Range(0f, bobPeriod);
            Animator animator = floatVisual.GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = false;
            }
        }
    }

    /// <summary>水面起伏。travel 为这一帧移动的世界距离，大于 0 时绕 Z 轴旋转。</summary>
    public void TickFloat(float dt, float travel)
    {
        if (floatVisual == null || dt <= 0f)
        {
            return;
        }

        _bobTime += dt;
        float period = bobPeriod < 0.2f ? 0.2f : bobPeriod;
        float wave = Mathf.Sin(_bobTime * (Mathf.PI * 2f / period));
        Vector3 pos = _floatBaseLocal;
        pos.y += wave * bobAmplitude;
        floatVisual.localPosition = pos;

        if (travel > 0.0001f)
        {
            float spin = travel * spinDegreesPerUnit;
            float cap = maxSpinDegreesPerSecond * dt;
            if (spin > cap)
            {
                spin = cap;
            }

            _spinAngle += spin;
        }
        else
        {
            _spinAngle = Mathf.MoveTowardsAngle(_spinAngle, wave * rockDegrees, 240f * dt);
        }

        if (_spinAngle > 360f || _spinAngle < -360f)
        {
            _spinAngle %= 360f;
        }

        floatVisual.localRotation = Quaternion.Euler(0f, 0f, _spinAngle);
    }

    public void SetTint(Color color)
    {
        if (rendererSprite != null)
        {
            rendererSprite.color = color;
        }
    }

    public void SetHeldCounts(int wood, int leaf, int scrap)
    {
        ShowCount(woodFrame, woodCount, wood);
        ShowCount(leafFrame, leafCount, leaf);
        ShowCount(scrapFrame, scrapCount, scrap);
    }

    private static void ShowCount(GameObject frame, TMP_Text label, int count)
    {
        bool visible = count > 0;
        if (frame != null && frame.activeSelf != visible)
        {
            frame.SetActive(visible);
        }

        if (label == null)
        {
            return;
        }

        if (frame == null && label.gameObject.activeSelf != visible)
        {
            label.gameObject.SetActive(visible);
        }
        else if (visible && !label.gameObject.activeSelf)
        {
            label.gameObject.SetActive(true);
        }

        if (visible)
        {
            label.text = count.ToString();
        }
    }
}