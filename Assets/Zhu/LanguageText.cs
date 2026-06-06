using UnityEngine;
using TMPro;

public class LanguageText : MonoBehaviour
{
    public string key;
    private TextMeshProUGUI tmp;

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        TryRefresh();
    }

    void TryRefresh()
    {
        // 👇 超强防御：key为空 直接跳过，绝不报错！
        if (GameManager.Instance == null || tmp == null || string.IsNullOrEmpty(key))
            return;

        tmp.text = GameManager.Instance.GetText(key);
    }
}