using UnityEngine;
using TMPro;

public class LocalizedText : MonoBehaviour
{
    public string key;
    private TextMeshProUGUI tmp;

    void OnEnable()
    {
        tmp = GetComponent<TextMeshProUGUI>();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLanguageChanged += Refresh;
            Refresh();
        }
    }

    void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnLanguageChanged -= Refresh;
    }

    void Refresh()
    {
        if (tmp != null)
            tmp.text = GameManager.Instance.GetText(key);
    }
}