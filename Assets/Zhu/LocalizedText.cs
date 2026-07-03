using UnityEngine;
using TMPro;

public class LocalizedText : MonoBehaviour
{
    [Tooltip("要显示的本地化文本内容")]
    public string showText;
    [Tooltip("指定TMP中文字体包")]
    public TMP_FontAsset targetFont;

    private TextMeshProUGUI tmp;

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
    }

    void Start()
    {
        if (tmp == null)
        {
            Debug.LogWarning($"物体 {gameObject.name} 未挂载 TextMeshProUGUI 组件", this);
            return;
        }

        tmp.text = showText;
        if (targetFont != null)
            tmp.font = targetFont;
    }
}