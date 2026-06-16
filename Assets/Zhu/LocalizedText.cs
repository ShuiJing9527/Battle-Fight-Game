using UnityEngine;
using TMPro;

public class LocalizedText : MonoBehaviour
{
    public string showText;
    public TMP_FontAsset targetFont;
    private TextMeshProUGUI tmp;

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
    }

    void Start()
    {
        if (tmp == null) return;
        tmp.text = showText;
        if (targetFont != null) tmp.font = targetFont;
    }
}