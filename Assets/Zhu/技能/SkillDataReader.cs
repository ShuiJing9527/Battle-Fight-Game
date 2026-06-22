using UnityEngine;

public struct SkillStatus
{
    public float currentCD;
    public float maxCD;
    public float manaCost;
    public bool isReady;
}

public class SkillDataReader : MonoBehaviour
{
    [Header("Runtime source")]
    public PlayerSkillCooldownManager cooldownManager;
    public MonoBehaviour playerSkillScript;
    public bool useMockData = false;

    private readonly float[] mockMaxCD = { 2f, 6f, 4f, 12f };
    private readonly float[] mockManaCost = { 10f, 30f, 20f, 60f };
    private readonly KeyCode[] skillKeys = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R };
    private float[] mockCurrentCD;

    private void Awake()
    {
        mockCurrentCD = new float[4];
        ResolveCooldownManager();
    }

    private void Update()
    {
        if (!useMockData)
        {
            ResolveCooldownManager();
            return;
        }

        for (int i = 0; i < mockCurrentCD.Length; i++)
        {
            mockCurrentCD[i] = Mathf.Max(0f, mockCurrentCD[i] - Time.deltaTime);
            if (Input.GetKeyDown(skillKeys[i]) && mockCurrentCD[i] <= 0.01f)
            {
                mockCurrentCD[i] = mockMaxCD[i];
            }
        }
    }

    public SkillStatus GetSkillStatus(int index)
    {
        SkillStatus status = new SkillStatus();
        if (index < 0 || index >= 4)
        {
            return status;
        }

        ResolveCooldownManager();
        if (!useMockData && cooldownManager != null)
        {
            status.maxCD = cooldownManager.GetSkillMaxCD(index);
            status.manaCost = cooldownManager.GetSkillManaCost(index);
            status.currentCD = cooldownManager.GetCurrentSkillCD(index);
            status.isReady = cooldownManager.IsSkillReady(index);
            return status;
        }

        status.maxCD = mockMaxCD[index];
        status.manaCost = mockManaCost[index];
        status.currentCD = mockCurrentCD[index];
        status.isReady = status.currentCD <= 0.01f;
        return status;
    }

    private void ResolveCooldownManager()
    {
        if (cooldownManager != null)
        {
            return;
        }

        if (playerSkillScript != null)
        {
            cooldownManager = playerSkillScript.GetComponent<PlayerSkillCooldownManager>();
        }

        if (cooldownManager == null)
        {
            cooldownManager = FindObjectOfType<PlayerSkillCooldownManager>();
        }
    }
}
