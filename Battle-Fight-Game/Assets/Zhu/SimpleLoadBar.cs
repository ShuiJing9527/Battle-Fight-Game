using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SimpleLoadBar : MonoBehaviour
{
    public Image progressBar;
    public TextMeshProUGUI loadText;
    public GameObject startBtn;

    public float loadTime = 3f;
    private float timer;
    private bool isLoad;

    void Start()
    {
        isLoad = true;
        timer = 0;
        progressBar.fillAmount = 0;
        startBtn.SetActive(false);
    }

    void Update()
    {
        if (!isLoad) return;

        timer += Time.deltaTime;
        float p = Mathf.Clamp01(timer / loadTime);
        progressBar.fillAmount = p;

        loadText.text = GameManager.Instance.GetText("loadbar_loading") + " " + Mathf.Round(p * 100) + "%";

        if (p >= 1f)
        {
            isLoad = false;
            loadText.text = GameManager.Instance.GetText("loadbar_complete");
            startBtn.SetActive(true);
        }
    }
}