using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerStatusHUD : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Bars")]
    [SerializeField] private Image hpFill;
    [SerializeField] private Image mpFill;
    [SerializeField] private Image shieldFill;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI mpText;
    [SerializeField] private TextMeshProUGUI shieldText;
    [SerializeField] private TextMeshProUGUI switchHintText;
    [SerializeField] private TextMeshProUGUI runeHintText;

    private Player2Bootstrap cachedBootstrap;
    private GameObject cachedPlayer;
    private BattleResourceBank cachedResourceBank;
    private CombatHealth cachedCombatHealth;
    private CombatStats cachedCombatStats;
    private float nextBootstrapLookupTime;

    private void Awake()
    {
        EnsureAttributePanelController();
        ApplyStaticTexts();
        RefreshPlayerCache(force: true);
        RefreshHud();
    }

    private void OnEnable()
    {
        EnsureAttributePanelController();
        ApplyStaticTexts();
        RefreshPlayerCache(force: true);
        RefreshHud();
    }

    private void Update()
    {
        RefreshPlayerCache(force: false);
        RefreshHud();
    }

    private void ApplyStaticTexts()
    {
        if (switchHintText != null)
        {
            switchHintText.text = "T: Switch Player";
        }

        if (runeHintText != null)
        {
            runeHintText.text = "K: Rune Panel";
        }
    }

    private void RefreshPlayerCache(bool force)
    {
        if ((force || cachedBootstrap == null) && Time.unscaledTime >= nextBootstrapLookupTime)
        {
            cachedBootstrap = FindObjectOfType<Player2Bootstrap>();
            nextBootstrapLookupTime = Time.unscaledTime + 1f;
        }

        GameObject currentPlayer = cachedBootstrap != null ? cachedBootstrap.CurrentPlayer : null;
        if (!force && currentPlayer == cachedPlayer)
        {
            return;
        }

        cachedPlayer = currentPlayer;
        cachedResourceBank = cachedPlayer != null ? cachedPlayer.GetComponent<BattleResourceBank>() : null;
        cachedCombatHealth = cachedPlayer != null ? cachedPlayer.GetComponent<CombatHealth>() : null;
        cachedCombatStats = cachedPlayer != null ? cachedPlayer.GetComponent<CombatStats>() : null;
    }

    private void RefreshHud()
    {
        if (root != null && !root.activeSelf)
        {
            return;
        }

        float hpCurrent;
        float hpMax;
        ResolveHealth(out hpCurrent, out hpMax);

        float mpCurrent;
        float mpMax;
        ResolveMana(out mpCurrent, out mpMax);

        float shieldCurrent;
        float shieldMax;
        ResolveShield(out shieldCurrent, out shieldMax);

        SetFill(hpFill, hpMax > 0f ? hpCurrent / hpMax : 0f);
        SetFill(mpFill, mpMax > 0f ? mpCurrent / mpMax : 0f);
        SetFill(shieldFill, ResolveShieldRatio(shieldCurrent, shieldMax));

        SetText(hpText, $"HP {Mathf.CeilToInt(hpCurrent)}/{Mathf.CeilToInt(hpMax)}");
        SetText(mpText, $"MP {Mathf.CeilToInt(mpCurrent)}/{Mathf.CeilToInt(mpMax)}");
        SetText(shieldText, $"Shield {Mathf.CeilToInt(shieldCurrent)}");
    }

    private void ResolveHealth(out float current, out float max)
    {
        if (cachedResourceBank != null)
        {
            current = Mathf.Max(0f, cachedResourceBank.currentHealth);
            max = Mathf.Max(1f, cachedResourceBank.maxHealth);
            return;
        }

        if (cachedCombatHealth != null)
        {
            current = Mathf.Max(0f, cachedCombatHealth.currentHealth);
            max = cachedCombatStats != null ? Mathf.Max(1f, cachedCombatStats.maxHealth) : Mathf.Max(1f, current);
            return;
        }

        current = 0f;
        max = 1f;
    }

    private void ResolveMana(out float current, out float max)
    {
        if (cachedResourceBank != null)
        {
            current = Mathf.Max(0f, cachedResourceBank.currentEnergy);
            max = Mathf.Max(1f, cachedResourceBank.maxEnergy);
            return;
        }

        current = 0f;
        max = 1f;
    }

    private void ResolveShield(out float current, out float max)
    {
        if (cachedCombatHealth != null)
        {
            current = Mathf.Max(0f, cachedCombatHealth.CurrentShield);
            max = Mathf.Max(0f, cachedCombatHealth.MaxShield);
            return;
        }

        if (cachedResourceBank != null)
        {
            current = Mathf.Max(0f, cachedResourceBank.CurrentShield);
            max = Mathf.Max(0f, cachedResourceBank.MaxShield);
            return;
        }

        current = 0f;
        max = 0f;
    }

    private static float ResolveShieldRatio(float current, float max)
    {
        if (current <= 0f)
        {
            return 0f;
        }

        if (max > 0f)
        {
            return current / max;
        }

        return 1f;
    }

    private static void SetFill(Image fill, float ratio)
    {
        if (fill == null)
        {
            return;
        }

        ratio = Mathf.Clamp01(ratio);

        RectTransform rect = fill.rectTransform;
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(ratio, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label == null)
        {
            return;
        }

        label.text = value;
    }

    private void EnsureAttributePanelController()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        Transform existingController = canvas.transform.Find("PlayerAttributePanelController");
        GameObject controllerObject;
        if (existingController != null)
        {
            controllerObject = existingController.gameObject;
        }
        else
        {
            controllerObject = new GameObject("PlayerAttributePanelController", typeof(RectTransform));
            controllerObject.transform.SetParent(canvas.transform, false);
        }

        if (controllerObject.GetComponent<PlayerAttributePanelUI>() == null)
        {
            controllerObject.AddComponent<PlayerAttributePanelUI>();
        }
    }
}
