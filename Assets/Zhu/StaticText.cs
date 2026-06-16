using UnityEngine;
using TMPro;

public class StaticText : MonoBehaviour
{
    // 直接在Inspector输入要显示的文字
    public string showText;
    // 拖入你生成好的TMP字体asset（白F/蓝F都可以）
    public TMP_FontAsset targetFont;

    private TextMeshProUGUI tmp;

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
    }

    void Start()
    {
        if (tmp == null) return;

        // 设置文字内容
        tmp.text = showText;
        // 设置字体，解决方框
        if (targetFont != null)
            tmp.font = targetFont;
    }
}