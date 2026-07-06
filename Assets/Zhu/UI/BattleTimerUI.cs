using TMPro;
using UnityEngine;

public class BattleTimerUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText;

    private EnemyDifficultyDirector director;
    private bool warnedMissingTimerText;

    public static BattleTimerUI EnsureInstance()
    {
        return FindObjectOfType<BattleTimerUI>(true);
    }

    private void Awake()
    {
        director = EnemyDifficultyDirector.Instance;
    }

    private void OnEnable()
    {
        director = EnemyDifficultyDirector.Instance;
        RefreshText();
    }

    private void Update()
    {
        if (director == null)
        {
            director = EnemyDifficultyDirector.Instance;
        }

        RefreshText();
    }

    private void RefreshText()
    {
        if (timerText == null)
        {
            if (!warnedMissingTimerText)
            {
                warnedMissingTimerText = true;
                Debug.LogWarning("[BattleTimerUI] timerText is not assigned.", this);
            }
            return;
        }

        warnedMissingTimerText = false;

        if (director == null)
        {
            timerText.text = "00:00";
            return;
        }

        timerText.text = director.BuildTimerText();
    }
}
