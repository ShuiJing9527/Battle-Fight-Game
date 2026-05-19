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
        if (GameManager.Instance == null || tmp == null)
            return;

        tmp.text = GameManager.Instance.GetText(key);
    }
}