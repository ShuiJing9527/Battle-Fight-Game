using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillSlotUI : MonoBehaviour
{
    [Header("UI控件引用")]
    public Image icon;
    public Image cdMask;
    public TextMeshProUGUI cdText;
    public TextMeshProUGUI keyText;

    [Header("显示配置")]
    public KeyCode bindKey = KeyCode.Q;
    public Color notReadyColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    private Color _defaultIconColor;
    private float _lastShowCD = -1f;

    void Awake()
    {
        if (icon != null) _defaultIconColor = icon.color;
        if (keyText != null) keyText.text = bindKey.ToString();
    }

    // 外部调用刷新，自身不做任何更新循环
    public void Refresh(SkillStatus status)
    {
        // 冷却遮罩
        if (cdMask != null && status.maxCD > 0)
        {
            float ratio = Mathf.Clamp01(status.currentCD / status.maxCD);
            cdMask.fillAmount = ratio;
        }

        // 冷却数字（只有变化超过0.1秒才更新，减少GC）
        if (cdText != null && Mathf.Abs(status.currentCD - _lastShowCD) > 0.1f)
        {
            _lastShowCD = status.currentCD;
            cdText.text = status.currentCD > 0.1f
                ? status.currentCD.ToString("F1")
                : string.Empty;
        }

        // 可用状态变色
        if (icon != null)
        {
            icon.color = status.isReady ? _defaultIconColor : notReadyColor;
        }
    }
}