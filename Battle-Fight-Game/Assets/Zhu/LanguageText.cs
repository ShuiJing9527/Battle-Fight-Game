using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
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
        if (GameManager.Instance == null) return;

        UpdateText();
        GameManager.Instance.OnLanguageChanged += UpdateText;
    }

    void OnDisable()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnLanguageChanged -= UpdateText;
    }

    void UpdateText()
    {
        if (GameManager.Instance == null || tmp == null)
            return;

        tmp.text = GameManager.Instance.GetText(key);
    }
}