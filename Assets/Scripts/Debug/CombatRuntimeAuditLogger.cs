using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatRuntimeAuditLogger : MonoBehaviour
{
    private const string AuditFileName = "CombatRuntimeAudit.log";
    private const int FlushLineThreshold = 32;
    private const float FlushIntervalSeconds = 2f;

    private static readonly object WriterLock = new object();
    private static readonly string[] CapturedTags =
    {
        "[Player02VisualHeightRuntime]",
        "[Player02HeightSummary]",
        "[PlayerDamagePipelineAudit]",
        "[FinalRushPressureAudit]",
        "[RuntimeEnemyStatsAudit]",
        "[BossAttackAudit]",
        "[ShieldPressure]",
        "[Player2WShieldAudit]",
        "[Player2RLifestealAudit]",
        "[TwinDebuffRuntimeAudit]",
        "[NoRuneShieldAudit]",
        "[RuneEquipResonance]",
        "[EnemySpawnWeights]",
        "[EnemyTemplateCheck]",
        "[EliteTemplateMismatch]",
        "[EnemyMeleeDamageFlow]",
        "[CombatAuditSummary]"
    };

    private static CombatRuntimeAuditLogger instance;
    private static StreamWriter writer;
    private static bool enabledForSession;
    private static bool logCallbackRegistered;
    private static bool pathLogged;
    private static bool writeFailed;
    private static string pendingWriteError;
    private static int pendingLineCount;

    private static int damageEventsTotal;
    private static int damageRejectedByInvincible;
    private static int damageRejectedByDodge;
    private static int damageFullyAbsorbedByShield;
    private static int damageAppliedToHp;
    private static int damageClamped;
    private static int damageNoHit;
    private static int bossHits;
    private static int eliteHits;
    private static int normalHits;
    private static int finalRushEnemyHits;
    private static float totalShieldConsumed;
    private static float totalHpDamage;
    private static float player2WShieldGrantedTotal;
    private static float player2RHealTotal;
    private static float finalRushShieldDamage;
    private static float finalRushHpDamage;

    private float nextFlushTime;
    private DifficultyPhase lastPhase;
    private bool phaseInitialized;

    public static bool IsEnabled => enabledForSession && !writeFailed;
    public static string LogPath => Path.Combine(Application.persistentDataPath, AuditFileName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        Application.logMessageReceived -= HandleUnityLog;
        logCallbackRegistered = false;
        CloseWriter();
        instance = null;
        enabledForSession = false;
        writeFailed = false;
        pendingWriteError = null;
        pendingLineCount = 0;
        pathLogged = false;
        ResetCounters();
    }

    public static void SetEnabled(bool shouldEnable, string source)
    {
        if (!shouldEnable || writeFailed)
        {
            return;
        }

        enabledForSession = true;
        EnsureInstance();
        EnsureWriter();
        if (writer != null && !pathLogged)
        {
            pathLogged = true;
            Debug.Log($"[CombatRuntimeAuditLogger] logPath={LogPath} enabledBy={Sanitize(source)}");
        }
    }

    public static void RecordPlayerDamageOutcome(
        GameObject player,
        GameObject monsterSource,
        string attackKind,
        string outcome,
        float shieldConsumed,
        float hpDamage,
        bool wasClamped)
    {
        if (!IsEnabled || player == null || monsterSource == null || !BattleTargetUtility.IsPlayer(player))
        {
            return;
        }

        MonsterIdentity identity = monsterSource.GetComponentInParent<MonsterIdentity>();
        if (identity == null)
        {
            return;
        }

        damageEventsTotal++;
        switch (identity.rank)
        {
            case MonsterRank.Boss:
                bossHits++;
                break;
            case MonsterRank.Elite:
                eliteHits++;
                break;
            default:
                normalHits++;
                break;
        }

        if (outcome.StartsWith("Invincible", StringComparison.Ordinal))
        {
            damageRejectedByInvincible++;
        }
        else if (string.Equals(outcome, "Dodge", StringComparison.Ordinal))
        {
            damageRejectedByDodge++;
        }
        else if (shieldConsumed > 0f && hpDamage <= 0f)
        {
            damageFullyAbsorbedByShield++;
        }

        if (hpDamage > 0f)
        {
            damageAppliedToHp++;
        }

        if (wasClamped)
        {
            damageClamped++;
        }

        totalShieldConsumed += Mathf.Max(0f, shieldConsumed);
        totalHpDamage += Mathf.Max(0f, hpDamage);

        EnemyDifficultyDirector director = EnemyDifficultyDirector.Instance;
        if (director != null && director.IsFinalRushActive)
        {
            finalRushEnemyHits++;
            finalRushShieldDamage += Mathf.Max(0f, shieldConsumed);
            finalRushHpDamage += Mathf.Max(0f, hpDamage);
        }

        Debug.Log(
            "[PlayerDamagePipelineAudit] stage=Outcome " +
            $"target={Sanitize(player.name)} enemy={Sanitize(monsterSource.name)} enemyRank={identity.rank} " +
            $"attackKind={Sanitize(attackKind)} " +
            $"noEffectiveDamageReason={Sanitize(outcome)} shieldConsumed={shieldConsumed:F2} " +
            $"hpDamage={hpDamage:F2} clamped={wasClamped} finalRush={(director != null && director.IsFinalRushActive)}",
            player);
    }

    public static void RecordNoHit(GameObject monsterSource, string attackKind, string reason)
    {
        if (!IsEnabled || monsterSource == null)
        {
            return;
        }

        MonsterIdentity identity = monsterSource.GetComponentInParent<MonsterIdentity>();
        if (identity == null)
        {
            return;
        }

        damageEventsTotal++;
        damageNoHit++;
        Debug.Log(
            "[PlayerDamagePipelineAudit] stage=NoHit " +
            $"enemy={Sanitize(monsterSource.name)} enemyRank={identity.rank} attackKind={Sanitize(attackKind)} " +
            $"noEffectiveDamageReason=NoHit detail={Sanitize(reason)}",
            monsterSource);
    }

    public static void EmitSummary(string result, string triggerSource)
    {
        if (!IsEnabled)
        {
            return;
        }

        CombatHealth playerHealth = ResolveSummaryPlayer();
        string playerName = playerHealth != null ? playerHealth.name : "None";
        float finalHp = 0f;
        float finalShield = 0f;
        int equippedRuneCount = 0;
        if (playerHealth != null)
        {
            finalHp = playerHealth.resourceBank != null
                ? playerHealth.resourceBank.currentHealth
                : playerHealth.currentHealth;
            finalShield = playerHealth.GetCurrentShield();
            RuneRuntimeState runtimeState = playerHealth.GetComponent<RuneRuntimeState>();
            equippedRuneCount = runtimeState != null ? runtimeState.GetTotalEquippedRuneCount() : 0;
        }

        EnemyDifficultyDirector director = EnemyDifficultyDirector.Instance;
        float timeSeconds = director != null ? director.ElapsedTime : Time.timeSinceLevelLoad;
        Debug.Log(
            "[CombatAuditSummary] " +
            $"result={Sanitize(result)} triggerSource={Sanitize(triggerSource)} timeSeconds={timeSeconds:F2} " +
            $"playerName={Sanitize(playerName)} equippedRuneCount={equippedRuneCount} finalHP={finalHp:F2} finalShield={finalShield:F2} " +
            $"damageEventsTotal={damageEventsTotal} damageRejectedByInvincible={damageRejectedByInvincible} " +
            $"damageRejectedByDodge={damageRejectedByDodge} damageFullyAbsorbedByShield={damageFullyAbsorbedByShield} " +
            $"damageAppliedToHP={damageAppliedToHp} damageClamped={damageClamped} damageNoHit={damageNoHit} " +
            $"bossHits={bossHits} eliteHits={eliteHits} normalHits={normalHits} " +
            $"totalShieldConsumed={totalShieldConsumed:F2} totalHpDamage={totalHpDamage:F2} " +
            $"player2WShieldGrantedTotal={player2WShieldGrantedTotal:F2} player2RHealTotal={player2RHealTotal:F2} " +
            $"finalRushEnemyHits={finalRushEnemyHits} finalRushShieldDamage={finalRushShieldDamage:F2} " +
            $"finalRushHpDamage={finalRushHpDamage:F2}",
            playerHealth);
        FlushWriter();
    }

    public static void LogPlayer02HeightSummary(
        string eventName,
        float rootY,
        float spineLocalY,
        float spineWorldY,
        bool hasColliderBottom,
        float colliderBottomY,
        bool hasGround,
        float groundY,
        string groundObject)
    {
        if (!IsEnabled || !IsHeightSummaryEvent(eventName))
        {
            return;
        }

        string summaryEvent = string.Equals(eventName, "AfterCharacterSwitch", StringComparison.Ordinal)
            ? "CharacterReactivated"
            : eventName;
        HeightBaseline baseline = HeightBaselineState.Value;
        if (string.Equals(summaryEvent, "StartApply", StringComparison.Ordinal))
        {
            baseline = new HeightBaseline(rootY, spineWorldY, true);
            HeightBaselineState.Value = baseline;
        }

        string deltaRoot = baseline.IsValid ? (rootY - baseline.RootY).ToString("F3", CultureInfo.InvariantCulture) : "n/a";
        string deltaSpine = baseline.IsValid ? (spineWorldY - baseline.SpineWorldY).ToString("F3", CultureInfo.InvariantCulture) : "n/a";
        Debug.Log(
            "[Player02HeightSummary] " +
            $"event={summaryEvent} rootY={rootY:F3} spineLocalY={spineLocalY:F3} spineWorldY={spineWorldY:F3} " +
            $"colliderBottomY={(hasColliderBottom ? colliderBottomY.ToString("F3", CultureInfo.InvariantCulture) : "n/a")} " +
            $"groundY={(hasGround ? groundY.ToString("F3", CultureInfo.InvariantCulture) : "n/a")} " +
            $"groundObject={Sanitize(groundObject)} deltaRootYFromStart={deltaRoot} deltaSpineWorldYFromStart={deltaSpine}");
    }

    public static void NotifyBossSpawned(GameObject boss)
    {
        EmitSummary("BossSpawned", boss != null ? boss.name : "null");
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        nextFlushTime = Time.realtimeSinceStartup + FlushIntervalSeconds;
    }

    private void Update()
    {
        if (!string.IsNullOrEmpty(pendingWriteError))
        {
            string error = pendingWriteError;
            pendingWriteError = null;
            Debug.LogError("[CombatRuntimeAuditLogger] fileWriteFailed=" + error);
        }

        if (Time.realtimeSinceStartup >= nextFlushTime)
        {
            nextFlushTime = Time.realtimeSinceStartup + FlushIntervalSeconds;
            FlushWriter();
        }

        EnemyDifficultyDirector director = EnemyDifficultyDirector.Instance;
        if (director == null)
        {
            return;
        }

        DifficultyPhase currentPhase = director.CurrentPhase;
        if (!phaseInitialized)
        {
            phaseInitialized = true;
            lastPhase = currentPhase;
            return;
        }

        if (currentPhase == lastPhase)
        {
            return;
        }

        DifficultyPhase previousPhase = lastPhase;
        lastPhase = currentPhase;
        if (currentPhase == DifficultyPhase.FinalRush)
        {
            EmitSummary("FinalRushEntered", previousPhase.ToString());
        }
        else if (previousPhase == DifficultyPhase.FinalRush)
        {
            EmitSummary("FinalRushExited", currentPhase.ToString());
        }
    }

    private void OnApplicationQuit()
    {
        EmitSummary("ApplicationQuit", "OnApplicationQuit");
        CloseWriter();
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        FlushWriter();
        CloseWriter();
        instance = null;
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject loggerObject = new GameObject(nameof(CombatRuntimeAuditLogger));
        instance = loggerObject.AddComponent<CombatRuntimeAuditLogger>();
    }

    private static void EnsureWriter()
    {
        if (writer != null || writeFailed)
        {
            return;
        }

        try
        {
            string path = LogPath;
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            writer = new StreamWriter(path, false, new UTF8Encoding(false), 65536)
            {
                AutoFlush = false
            };
            writer.WriteLine($"# Combat runtime audit started {DateTime.UtcNow:O}");
            writer.WriteLine($"# unity={Application.unityVersion} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            writer.Flush();
            if (!logCallbackRegistered)
            {
                Application.logMessageReceived += HandleUnityLog;
                logCallbackRegistered = true;
            }
        }
        catch (Exception exception)
        {
            HandleWriteFailure(exception);
        }
    }

    private static void HandleUnityLog(string condition, string stackTrace, LogType type)
    {
        if (!IsEnabled || string.IsNullOrEmpty(condition) || !ShouldCapture(condition))
        {
            return;
        }

        AccumulateTaggedMetrics(condition);
        lock (WriterLock)
        {
            if (writer == null)
            {
                return;
            }

            try
            {
                writer.WriteLine($"[{DateTime.UtcNow:O}] [{type}] {condition}");
                pendingLineCount++;
                if (pendingLineCount >= FlushLineThreshold)
                {
                    writer.Flush();
                    pendingLineCount = 0;
                }
            }
            catch (Exception exception)
            {
                HandleWriteFailure(exception);
            }
        }
    }

    private static bool ShouldCapture(string message)
    {
        for (int i = 0; i < CapturedTags.Length; i++)
        {
            if (message.IndexOf(CapturedTags[i], StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void AccumulateTaggedMetrics(string message)
    {
        if (message.StartsWith("[Player2WShieldAudit]", StringComparison.Ordinal) && TryReadFloat(message, "shieldGranted", out float shieldGranted))
        {
            player2WShieldGrantedTotal += Mathf.Max(0f, shieldGranted);
        }
        else if (message.StartsWith("[Player2RLifestealAudit]", StringComparison.Ordinal) && TryReadFloat(message, "healApplied", out float healApplied))
        {
            player2RHealTotal += Mathf.Max(0f, healApplied);
        }
    }

    private static bool TryReadFloat(string message, string key, out float value)
    {
        value = 0f;
        string marker = key + "=";
        int start = message.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        start += marker.Length;
        int end = message.IndexOf(' ', start);
        string raw = end >= 0 ? message.Substring(start, end - start) : message.Substring(start);
        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
               float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private static CombatHealth ResolveSummaryPlayer()
    {
        CombatHealth[] candidates = UnityEngine.Object.FindObjectsByType<CombatHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        CombatHealth fallback = null;
        for (int i = 0; i < candidates.Length; i++)
        {
            CombatHealth candidate = candidates[i];
            if (candidate == null || !BattleTargetUtility.IsPlayer(candidate.gameObject))
            {
                continue;
            }

            fallback ??= candidate;
            if (candidate.gameObject.activeInHierarchy && !candidate.IsDead)
            {
                return candidate;
            }
        }

        return fallback;
    }

    private static bool IsHeightSummaryEvent(string eventName)
    {
        return string.Equals(eventName, "StartApply", StringComparison.Ordinal) ||
               string.Equals(eventName, "BeforeKnockback", StringComparison.Ordinal) ||
               string.Equals(eventName, "AfterKnockback", StringComparison.Ordinal) ||
               string.Equals(eventName, "AfterLanding", StringComparison.Ordinal) ||
               string.Equals(eventName, "AfterRecover", StringComparison.Ordinal) ||
               string.Equals(eventName, "AfterWallHit", StringComparison.Ordinal) ||
               string.Equals(eventName, "AfterCharacterSwitch", StringComparison.Ordinal);
    }

    private static void FlushWriter()
    {
        lock (WriterLock)
        {
            if (writer == null)
            {
                return;
            }

            try
            {
                writer.Flush();
                pendingLineCount = 0;
            }
            catch (Exception exception)
            {
                HandleWriteFailure(exception);
            }
        }
    }

    private static void CloseWriter()
    {
        lock (WriterLock)
        {
            if (writer == null)
            {
                return;
            }

            try
            {
                writer.Flush();
                writer.Dispose();
            }
            catch (Exception exception)
            {
                pendingWriteError = exception.Message;
            }
            finally
            {
                writer = null;
                pendingLineCount = 0;
            }
        }
    }

    private static void HandleWriteFailure(Exception exception)
    {
        writeFailed = true;
        pendingWriteError = exception.GetType().Name + ":" + exception.Message;
        try
        {
            writer?.Dispose();
        }
        catch
        {
            // Preserve the original write failure.
        }
        writer = null;
    }

    private static void ResetCounters()
    {
        damageEventsTotal = 0;
        damageRejectedByInvincible = 0;
        damageRejectedByDodge = 0;
        damageFullyAbsorbedByShield = 0;
        damageAppliedToHp = 0;
        damageClamped = 0;
        damageNoHit = 0;
        bossHits = 0;
        eliteHits = 0;
        normalHits = 0;
        finalRushEnemyHits = 0;
        totalShieldConsumed = 0f;
        totalHpDamage = 0f;
        player2WShieldGrantedTotal = 0f;
        player2RHealTotal = 0f;
        finalRushShieldDamage = 0f;
        finalRushHpDamage = 0f;
        HeightBaselineState.Value = default;
    }

    private static string Sanitize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "None" : value.Replace(' ', '_');
    }

    private readonly struct HeightBaseline
    {
        public HeightBaseline(float rootY, float spineWorldY, bool isValid)
        {
            RootY = rootY;
            SpineWorldY = spineWorldY;
            IsValid = isValid;
        }

        public float RootY { get; }
        public float SpineWorldY { get; }
        public bool IsValid { get; }
    }

    private static class HeightBaselineState
    {
        public static HeightBaseline Value;
    }
}
