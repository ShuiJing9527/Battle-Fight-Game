using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class LocalizedText : MonoBehaviour
{
    public string key;
    private TextMeshProUGUI text;

    void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        UpdateText();
        if (GameManager.Instance != null)
            GameManager.Instance.OnLanguageChanged += UpdateText;
    }

    void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnLanguageChanged -= UpdateText;
    }

    void UpdateText()
    {
        if (GameManager.Instance == null) return;
        text.text = GameManager.Instance.GetText(key);
    }
}