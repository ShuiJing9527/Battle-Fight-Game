using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class Player2Bootstrap : MonoBehaviour
{
    private const string TwinShiftRewardShieldSourceId = "TwinShiftReward";
    private const string CharacterSwitchEnergyNotFullKey = "CharacterSwitch_EnergyNotFull";
    private const string CharacterSwitchEnergyNotFullFallback = "能量槽未满，无法切换角色。";
    private const float CharacterSwitchEnergyNotFullHintDuration = 2f;

    [Header("Party Members")]
    [SerializeField] private GameObject player01;
    [SerializeField] private GameObject player02;
    [SerializeField] private GameObject partyLeader;

    [Header("Height Offsets")]
    [SerializeField] private float player01YOffset = 0.75f;
    [SerializeField] private float player02YOffset = 1.2f;

    [Header("Fallback Names (Optional)")]
    [SerializeField] private string player01Name = "Player01";
    [SerializeField] private string player02Name = "Player02";
    [SerializeField] private string partyLeaderName = "Player01";

    [Header("UI")]
    [SerializeField] private bool drawLegacyHud = false;
    [SerializeField] private bool showSwitchHint = true;
    [SerializeField] private bool disablePlayer2AnimatorIfSharedController = true;
    [SerializeField] private bool showHealthBar = true;
    [SerializeField] private bool showRuneHint = true;
    [SerializeField] private RuneBagUI runeBagUI;

    [Header("Player Health")]
    [SerializeField] private float playerStartHealth = 100f;

    [Header("Twin Shift")]
    [FormerlySerializedAs("twinShiftGaugeShiftAmount")]
    [SerializeField, Range(0f, 100f)] private float twinShiftSeedGaugeAmount = 15f;
    [SerializeField, Min(0f)] private float twinShiftHealMaxHpRatio = 0.1f;
    [SerializeField, Min(0f)] private float twinShiftShieldMaxHpRatio = 0.1f;
    [SerializeField, Min(0f)] private float twinShiftShieldDuration = 5f;
    [SerializeField] private TwinShiftVfxPlayer twinShiftVfxPlayer;
    [SerializeField] private bool debugTwinShiftBuff = false;
    [SerializeField] private bool debugPartyLogs = false;

    public GameObject CurrentPlayer { get; private set; }
    public Transform CurrentPlayerTransform => CurrentPlayer != null ? CurrentPlayer.transform : null;
    public GameObject PartyLeader => IsValidSceneObject(partyLeader) ? partyLeader : (IsValidSceneObject(player01) ? player01 : null);

    private PlayerCameraRig cameraRig;
    private Animator player1Animator;
    private Animator player2Animator;
    private bool initialized;

    private Texture2D healthBarBackgroundTexture;
    private Texture2D healthBarFillTexture;
    private Texture2D energyBarBackgroundTexture;
    private Texture2D energyBarFillTexture;
    private Texture2D shieldBarBackgroundTexture;
    private Texture2D shieldBarFillTexture;
    private GUIStyle switchHintStyle;
    private GUIStyle healthBarLabelStyle;
    private bool warnedMissingTwinShiftVfxPlayer;

    private void Start()
    {
        InitializePartyIfNeeded();
    }

    private void OnDisable()
    {
        ClearAllTransitionBlessings("BattleEnd");
    }

    private void OnDestroy()
    {
        ClearAllTransitionBlessings("Destroy");
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            ToggleCharacter();
        }

        if (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            if (!HasRuneUiControllerInputHandler())
            {
                ToggleRunePanel();
            }
        }
    }

    private void OnGUI()
    {
        if (!drawLegacyHud)
        {
            return;
        }

        EnsureGuiResources();

        if (showSwitchHint)
        {
            const int width = 220;
            const int height = 30;
            Rect rect = new Rect(20f, 20f, width, height);
            GUI.Label(rect, "T: Switch Player", switchHintStyle);
        }

        if (showRuneHint)
        {
            const int width = 260;
            const int height = 30;
            Rect rect = new Rect(20f, 48f, width, height);
            GUI.Label(rect, "K: Rune Panel", switchHintStyle);
        }

        if (showHealthBar)
        {
            DrawStatusBars();
            DrawShieldBar();
        }
    }

    private void ResolvePlayers()
    {
        if (!IsValidSceneObject(player01))
        {
            player01 = null;
        }

        if (!IsValidSceneObject(player02))
        {
            player02 = null;
        }

        if (!IsValidSceneObject(partyLeader))
        {
            partyLeader = null;
        }

        if (player01 == null)
        {
            player01 = FindSceneObjectByNameIncludingInactive(player01Name);
        }

        if (player02 == null)
        {
            player02 = FindSceneObjectByNameIncludingInactive(player02Name);
        }

        if (player1Animator == null && player01 != null)
        {
            player1Animator = player01.GetComponent<Animator>();
        }

        if (player2Animator == null && player02 != null)
        {
            player2Animator = player02.GetComponent<Animator>();
        }
    }

    public void SetPlayers(GameObject newPlayer01, GameObject newPlayer02, GameObject newPartyLeader = null, GameObject initialCurrentPlayer = null)
    {
        player01 = IsValidSceneObject(newPlayer01) ? newPlayer01 : null;
        player02 = IsValidSceneObject(newPlayer02) ? newPlayer02 : null;
        partyLeader = IsValidSceneObject(newPartyLeader) ? newPartyLeader : player01;

        player1Animator = player01 != null ? player01.GetComponent<Animator>() : null;
        player2Animator = player02 != null ? player02.GetComponent<Animator>() : null;

        ApplyInitialHealth(player01);
        ApplyInitialHealth(player02);

        GameObject resolvedCurrentPlayer = IsValidSceneObject(initialCurrentPlayer) ? initialCurrentPlayer : null;
        if (resolvedCurrentPlayer == null)
        {
            resolvedCurrentPlayer = partyLeader != null ? partyLeader : player01;
        }

        if (resolvedCurrentPlayer == null)
        {
            resolvedCurrentPlayer = player01 != null ? player01 : player02;
        }

        if (player01 != null)
        {
            player01.SetActive(resolvedCurrentPlayer == player01);
        }

        if (player02 != null)
        {
            player02.SetActive(resolvedCurrentPlayer == player02);
        }

        ClearAllTransitionBlessings("Initialization");
        CurrentPlayer = resolvedCurrentPlayer;

        if (cameraRig == null)
        {
            cameraRig = FindObjectOfType<PlayerCameraRig>();
        }

        if (cameraRig != null && CurrentPlayer != null)
        {
            cameraRig.playerSlot = CurrentPlayer.transform;
        }

        SafeRefreshSkillHud(CurrentPlayer);
        SyncCurrentPlayerBgm();

        if (disablePlayer2AnimatorIfSharedController)
        {
            DisablePlayer2AnimatorIfUsingPlayer01Controller();
        }

        if (DayNightGaugeRuntimeState.TryGetExistingInstance(out DayNightGaugeRuntimeState gauge))
        {
            gauge.ResetGauge();
        }

        initialized = CurrentPlayer != null;

        if (initialized)
        {
            if (debugPartyLogs)
            {
                Debug.Log($"[PARTY] Leader = {(partyLeader != null ? partyLeader.name : "null")}", this);
                Debug.Log($"[PARTY] Current Player = {(CurrentPlayer != null ? CurrentPlayer.name : "null")}", this);
            }
        }
    }

    private void ToggleCharacter()
    {
        InitializePartyIfNeeded();
        ResolvePlayers();

        if (player01 == null || player02 == null)
        {
            return;
        }

        if (CurrentPlayer != null)
        {
            Player2PrototypeController activeSkill = CurrentPlayer.GetComponent<Player2PrototypeController>();
            if (activeSkill != null && activeSkill.HasActiveRuntimeSkill)
            {
                if (debugPartyLogs)
                {
                    Debug.Log("[PLAYER SWITCH] Cannot switch while skill is active.", this);
                }
                return;
            }
        }

        try
        {
            if (TrySwitchPlayer())
            {
                RefreshOverlayPanelsForCurrentPlayer();
                if (debugPartyLogs)
                {
                    Debug.Log($"[PARTY] Switched current player = {(CurrentPlayer != null ? CurrentPlayer.name : "null")}", this);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PLAYER SWITCH] T key switch failed: {ex}", this);
        }
    }

    private bool TrySwitchPlayer()
    {
        if (player01 == null || player02 == null || CurrentPlayer == null)
        {
            return false;
        }

        GameObject previousPlayer = CurrentPlayer;
        GameObject nextPlayer = previousPlayer == player01 ? player02 : player01;
        if (nextPlayer == null || nextPlayer == previousPlayer)
        {
            return false;
        }

        if (!CanSwitchTwinPlayer(previousPlayer, nextPlayer, out string blockedReason))
        {
            DebugTwinShift($"blocked reason={blockedReason}");
            ShowEnergyNotFullSwitchHintIfNeeded(blockedReason);
            return false;
        }

        SetActivePlayer(nextPlayer);

        if (!TryFinalizeTwinShift(previousPlayer, nextPlayer, nextPlayer.transform.position))
        {
            Debug.LogError($"[TwinShift] post-switch settlement failed previous={GetObjectName(previousPlayer)} new={GetObjectName(nextPlayer)}", this);
            return false;
        }

        ApplyTransitionBlessing(nextPlayer);

        return true;
    }

    private void ToggleRunePanel()
    {
        RuneUIController runeUiController = FindObjectOfType<RuneUIController>(true);
        if (runeUiController != null && runeUiController.enabled)
        {
            return;
        }

        if (runeBagUI != null)
        {
            runeBagUI.TogglePanel();
            return;
        }

        Debug.LogWarning("[RuneUI] Missing scene references on Player2Bootstrap.", this);
    }

    private bool HasRuneUiControllerInputHandler()
    {
        RuneUIController runeUiController = FindObjectOfType<RuneUIController>(true);
        return runeUiController != null && runeUiController.enabled;
    }

    private void EnsureRunePanelReferences()
    {
        // Intentionally left blank. Rune UI should be bound explicitly in the scene.
    }

    private void SetActivePlayer(GameObject nextActive)
    {
        ResolvePlayers();

        if (player01 == null || player02 == null || nextActive == null)
        {
            return;
        }

        GameObject nextInactive = nextActive == player01 ? player02 : player01;

        if (nextInactive == null || nextInactive == nextActive)
        {
            return;
        }

        GameObject previousPlayer = CurrentPlayer;
        if (previousPlayer == nextActive)
        {
            return;
        }

        Vector3 basePosition;
        if (CurrentPlayer != null && CurrentPlayer != nextActive)
        {
            basePosition = RemoveCharacterHeightOffset(CurrentPlayer, CurrentPlayer.transform.position);
        }
        else
        {
            basePosition = RemoveCharacterHeightOffset(nextActive, nextActive.transform.position);
        }

        nextActive.transform.position = ApplyCharacterHeightOffset(nextActive, basePosition);
        if (nextInactive != null)
        {
            nextInactive.transform.position = ApplyCharacterHeightOffset(nextInactive, basePosition);
        }

        nextActive.SetActive(true);
        CurrentPlayer = nextActive;

        if (nextActive == player01)
        {
            RefreshPlayer01EyeFire();
        }

        Rigidbody nextRb = nextActive.GetComponent<Rigidbody>();
        if (nextRb != null)
        {
            Vector3 velocityBeforeWrite = nextRb.linearVelocity;
            Vector3 velocityAfterWrite = Vector3.zero;
            nextRb.linearVelocity = velocityAfterWrite;
            PlayerMovement.LogVelocityWrite(
                this,
                nameof(Player2Bootstrap),
                nameof(SetActivePlayer),
                nextRb,
                velocityBeforeWrite,
                velocityAfterWrite,
                "switch-sync-zero-velocity",
                "no-active-skill",
                "switching-active-player",
                "runtime");
            nextRb.angularVelocity = Vector3.zero;
        }

        SafeAssignCameraTarget(nextActive);
        SafeRefreshSkillHud(nextActive);
        ClearTransitionBlessing(previousPlayer, "SwitchOut");
        nextInactive.SetActive(false);
        SyncCurrentPlayerBgm();
    }

    private void SyncCurrentPlayerBgm()
    {
        if (AudioManager.Instance == null || CurrentPlayer == null)
        {
            return;
        }

        AudioManager.Instance.PlayBGMForCharacter(CurrentPlayer == player02);
    }

    private bool CanSwitchTwinPlayer(GameObject previousPlayer, GameObject newPlayer, out string blockedReason)
    {
        blockedReason = null;
        if (previousPlayer == null || newPlayer == null || previousPlayer == newPlayer)
        {
            blockedReason = "InvalidSwitch";
            return false;
        }

        DayNightGaugeRuntimeState gauge = DayNightGaugeRuntimeState.Instance;
        if (gauge == null)
        {
            blockedReason = "GaugeMissing";
            return false;
        }

        if (debugTwinShiftBuff && !gauge.DebugLogEnabled)
        {
            gauge.SetDebugLogEnabled(true);
        }

        float radiance = gauge.RadianceValue;
        float twilight = gauge.TwilightValue;
        PlayerDayNightAffinityType previousAffinity = ResolveAffinityType(previousPlayer);
        PlayerDayNightAffinityType newAffinity = ResolveAffinityType(newPlayer);

        DebugTwinShift(
            $"evaluate previous={previousPlayer.name} new={newPlayer.name} previousAffinity={previousAffinity} newAffinity={newAffinity} gauge={GetGaugeDebugLabel(gauge)} radianceBefore={radiance:F2} twilightBefore={twilight:F2}");

        if (previousAffinity == PlayerDayNightAffinityType.DayChild && newAffinity == PlayerDayNightAffinityType.NightChild)
        {
            if (!gauge.HasRadianceState())
            {
                blockedReason = "RadianceNotFull";
                return false;
            }
        }
        else if (previousAffinity == PlayerDayNightAffinityType.NightChild && newAffinity == PlayerDayNightAffinityType.DayChild)
        {
            if (!gauge.HasTwilightState())
            {
                blockedReason = "TwilightNotFull";
                return false;
            }
        }
        else
        {
            blockedReason = "UnsupportedAffinityPair";
            return false;
        }

        return true;
    }

    private void ShowEnergyNotFullSwitchHintIfNeeded(string blockedReason)
    {
        if (blockedReason != "TwilightNotFull" && blockedReason != "RadianceNotFull")
        {
            return;
        }

        CharacterSwitchWarningPopup.ShowLocalized(
            CharacterSwitchEnergyNotFullKey,
            CharacterSwitchEnergyNotFullFallback,
            CharacterSwitchEnergyNotFullHintDuration);
    }

    private bool TryFinalizeTwinShift(GameObject previousPlayer, GameObject newPlayer, Vector3 switchPosition)
    {
        DayNightGaugeRuntimeState gauge = DayNightGaugeRuntimeState.Instance;
        if (gauge == null)
        {
            return false;
        }

        float beforeRadiance = gauge.RadianceValue;
        float beforeTwilight = gauge.TwilightValue;
        PlayerDayNightAffinityType previousAffinity = ResolveAffinityType(previousPlayer);
        PlayerDayNightAffinityType newAffinity = ResolveAffinityType(newPlayer);

        if (previousAffinity == PlayerDayNightAffinityType.DayChild && newAffinity == PlayerDayNightAffinityType.NightChild)
        {
            if (!gauge.TryConsumeFullRadianceAndSeedTwilight(twinShiftSeedGaugeAmount, out float consumedRadiance))
            {
                DebugTwinShift($"settlement failed reason=RadianceSettlementRejected current={beforeRadiance:F2} twilight={beforeTwilight:F2}");
                return false;
            }

            float healAmount = ApplyTwinShiftHeal(newPlayer);
            PlayBasicSwitchVfx(switchPosition);
            PlayRadianceToTwilightRewardVfx(switchPosition);
            DebugTwinShift(
                $"trigger type=RadianceToTwilight consumedRadiance={consumedRadiance:F2} seedTwilight={twinShiftSeedGaugeAmount:F2} heal={healAmount:F2} radianceBefore={beforeRadiance:F2} twilightBefore={beforeTwilight:F2} radianceAfter={gauge.RadianceValue:F2} twilightAfter={gauge.TwilightValue:F2} emptyAfter={gauge.EmptyValue:F2}");
            return true;
        }

        if (previousAffinity == PlayerDayNightAffinityType.NightChild && newAffinity == PlayerDayNightAffinityType.DayChild)
        {
            if (!gauge.TryConsumeFullTwilightAndSeedRadiance(twinShiftSeedGaugeAmount, out float consumedTwilight))
            {
                DebugTwinShift($"settlement failed reason=TwilightSettlementRejected current={beforeTwilight:F2} radiance={beforeRadiance:F2}");
                return false;
            }

            float shieldAmount = ApplyTwinShiftShield(newPlayer);
            PlayBasicSwitchVfx(switchPosition);
            PlayTwilightToRadianceRewardVfx(switchPosition);
            DebugTwinShift(
                $"trigger type=TwilightToRadiance consumedTwilight={consumedTwilight:F2} seedRadiance={twinShiftSeedGaugeAmount:F2} shield={shieldAmount:F2} shieldDuration={twinShiftShieldDuration:F2} radianceBefore={beforeRadiance:F2} twilightBefore={beforeTwilight:F2} radianceAfter={gauge.RadianceValue:F2} twilightAfter={gauge.TwilightValue:F2} emptyAfter={gauge.EmptyValue:F2}");
            return true;
        }

        return false;
    }

    private float ApplyTwinShiftHeal(GameObject newPlayer)
    {
        float healAmount = 0f;
        CombatHealth combatHealth = newPlayer.GetComponent<CombatHealth>();
        if (combatHealth != null && !combatHealth.IsDead)
        {
            float maxHp = Mathf.Max(0f, combatHealth.MaxHealthValue);
            healAmount = maxHp * twinShiftHealMaxHpRatio;
            if (healAmount > 0f)
            {
                combatHealth.Heal(healAmount);
            }
        }

        return healAmount;
    }

    private float ApplyTwinShiftShield(GameObject newPlayer)
    {
        float shieldAmount = 0f;
        CombatHealth combatHealth = newPlayer.GetComponent<CombatHealth>();
        if (combatHealth != null && !combatHealth.IsDead)
        {
            float maxHp = Mathf.Max(0f, combatHealth.MaxHealthValue);
            shieldAmount = maxHp * twinShiftShieldMaxHpRatio;
            if (shieldAmount > 0f)
            {
                PlayerTimedShieldStatus timedShield = PlayerTimedShieldStatus.GetOrAdd(newPlayer, TwinShiftRewardShieldSourceId);
                if (timedShield != null)
                {
                    timedShield.ApplyShield(shieldAmount, twinShiftShieldDuration);
                }
            }
        }

        return shieldAmount;
    }

    private void PlayBasicSwitchVfx(Vector3 switchPosition)
    {
        TwinShiftVfxPlayer vfxPlayer = ResolveTwinShiftVfxPlayer();
        if (vfxPlayer == null)
        {
            return;
        }

        vfxPlayer.PlayBasicSwitchVfx(switchPosition);
    }

    private void PlayRadianceToTwilightRewardVfx(Vector3 switchPosition)
    {
        TwinShiftVfxPlayer vfxPlayer = ResolveTwinShiftVfxPlayer();
        if (vfxPlayer == null)
        {
            return;
        }

        vfxPlayer.PlayRadianceToTwilightRewardVfx(switchPosition);
    }

    private void PlayTwilightToRadianceRewardVfx(Vector3 switchPosition)
    {
        TwinShiftVfxPlayer vfxPlayer = ResolveTwinShiftVfxPlayer();
        if (vfxPlayer == null)
        {
            return;
        }

        vfxPlayer.PlayTwilightToRadianceRewardVfx(switchPosition);
    }

    private TwinShiftVfxPlayer ResolveTwinShiftVfxPlayer()
    {
        if (twinShiftVfxPlayer != null)
        {
            return twinShiftVfxPlayer;
        }

        twinShiftVfxPlayer = GetComponent<TwinShiftVfxPlayer>();
        if (twinShiftVfxPlayer == null)
        {
            twinShiftVfxPlayer = GetComponentInChildren<TwinShiftVfxPlayer>(true);
        }

        if (twinShiftVfxPlayer == null && debugTwinShiftBuff && !warnedMissingTwinShiftVfxPlayer)
        {
            warnedMissingTwinShiftVfxPlayer = true;
            Debug.Log("[TwinShift] TwinShiftVfxPlayer is not assigned on this bootstrap object or its children. VFX will be skipped.", this);
        }

        return twinShiftVfxPlayer;
    }

    private static PlayerDayNightAffinityType ResolveAffinityType(GameObject player)
    {
        if (player == null)
        {
            return PlayerDayNightAffinityType.None;
        }

        PlayerDayNightAffinity affinity = player.GetComponent<PlayerDayNightAffinity>();
        if (affinity == null)
        {
            affinity = player.GetComponentInParent<PlayerDayNightAffinity>();
        }

        return affinity != null ? affinity.AffinityType : PlayerDayNightAffinityType.None;
    }

    private void DebugTwinShift(string message)
    {
        if (!debugTwinShiftBuff)
        {
            return;
        }

        Debug.Log($"[TwinShift] {message}", this);
    }

    private static string GetGaugeDebugLabel(DayNightGaugeRuntimeState gauge)
    {
        return gauge != null ? gauge.DebugInstanceLabel : "null";
    }

    private static string GetObjectName(GameObject target)
    {
        return target != null ? target.name : "null";
    }

    private void SafeAssignCameraTarget(GameObject nextActive)
    {
        if (nextActive == null)
        {
            return;
        }

        try
        {
            if (cameraRig == null)
            {
                cameraRig = FindObjectOfType<PlayerCameraRig>();
            }

            if (cameraRig != null)
            {
                cameraRig.playerSlot = nextActive.transform;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PLAYER SWITCH] Failed to assign camera target during T switch: {ex}", this);
        }
    }

    private void SafeRefreshSkillHud(GameObject nextActive)
    {
        if (nextActive == null)
        {
            return;
        }

        try
        {
            PlayerSkillHUD skillHud = FindObjectOfType<PlayerSkillHUD>();
            if (skillHud == null)
            {
                return;
            }

            int playerIndex = nextActive == player01 ? 1 : (nextActive == player02 ? 2 : 0);
            if (playerIndex > 0)
            {
                skillHud.SetSkillIconSet(playerIndex);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PLAYER SWITCH] Failed to refresh skill HUD during T switch: {ex}", this);
        }
    }

    private void RefreshOverlayPanelsForCurrentPlayer()
    {
        PlayerAttributePanelUI attributePanel = FindObjectOfType<PlayerAttributePanelUI>(true);
        if (attributePanel != null && attributePanel.IsPanelOpen)
        {
            attributePanel.RefreshCurrentPlayerView();
        }

        RuneUIController runeUiController = FindObjectOfType<RuneUIController>(true);
        if (runeUiController != null && runeUiController.IsPanelOpen)
        {
            runeUiController.RefreshCurrentPlayerView();
        }

        RuneBagUI runeBag = FindObjectOfType<RuneBagUI>(true);
        if (runeBag != null && runeBag.IsPanelOpen)
        {
            runeBag.RefreshCurrentPlayerView();
        }
    }

    public void RefreshCurrentPlayerPresentation()
    {
        if (CurrentPlayer == null)
        {
            return;
        }

        SafeAssignCameraTarget(CurrentPlayer);
        SafeRefreshSkillHud(CurrentPlayer);
        RefreshOverlayPanelsForCurrentPlayer();
    }

    public void EnsureInitializedForSpawn()
    {
        InitializePartyIfNeeded();
    }

    private void InitializePartyIfNeeded()
    {
        if (initialized)
        {
            return;
        }

        ResolvePlayers();
        if (player01 == null || player02 == null)
        {
            return;
        }

        ApplyInitialHealth(player01);
        ApplyInitialHealth(player02);
        cameraRig = FindObjectOfType<PlayerCameraRig>();

        if (partyLeader == null && !string.IsNullOrEmpty(partyLeaderName))
        {
            if (player01 != null && player01.name == partyLeaderName)
            {
                partyLeader = player01;
            }
            else if (player02 != null && player02.name == partyLeaderName)
            {
                partyLeader = player02;
            }
        }

        if (partyLeader == null)
        {
            partyLeader = player01;
        }

        GameObject startPlayer = player01 != null ? player01 : (partyLeader == player02 ? player02 : partyLeader);
        SetActivePlayer(startPlayer);

        if (disablePlayer2AnimatorIfSharedController)
        {
            DisablePlayer2AnimatorIfUsingPlayer01Controller();
        }

        Debug.Log($"[PARTY] Leader = {(partyLeader != null ? partyLeader.name : "null")}", this);
        Debug.Log($"[PARTY] Current Player = {(CurrentPlayer != null ? CurrentPlayer.name : "null")}", this);

        initialized = true;
    }

    private void RefreshPlayer01EyeFire()
    {
        if (player01 == null)
        {
            return;
        }

        EyeFireHorizontalRotationController[] controllers = player01.GetComponentsInChildren<EyeFireHorizontalRotationController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
            {
                controllers[i].Reinitialize();
            }
        }
    }

    private void ApplyTransitionBlessing(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        TwinTransitionBlessingStatus status = ResolveTransitionBlessingStatus(target);
        if (status == null)
        {
            return;
        }

        status.ApplyBlessingForSwitchIn(ResolveAffinityType(target));
        RefreshCurrentPlayerPresentation();
    }

    private void ClearTransitionBlessing(GameObject target, string reason)
    {
        if (target == null)
        {
            return;
        }

        TwinTransitionBlessingStatus status = target.GetComponent<TwinTransitionBlessingStatus>();
        if (status == null)
        {
            return;
        }

        status.ClearBlessing(reason);
    }

    private void ClearAllTransitionBlessings(string reason)
    {
        ClearTransitionBlessing(player01, reason);
        ClearTransitionBlessing(player02, reason);
    }

    private static TwinTransitionBlessingStatus ResolveTransitionBlessingStatus(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        TwinTransitionBlessingStatus status = target.GetComponent<TwinTransitionBlessingStatus>();
        if (status != null)
        {
            return status;
        }

        return target.AddComponent<TwinTransitionBlessingStatus>();
    }

    public float GetCharacterHeightOffset(GameObject character)
    {
        if (character == null)
        {
            return 0f;
        }

        if (character == player02 || character.GetComponent<Player2PrototypeController>() != null || (!string.IsNullOrEmpty(player02Name) && character.name.Contains(player02Name)))
        {
            return player02YOffset;
        }

        return player01YOffset;
    }

    public Vector3 ApplyCharacterHeightOffset(GameObject character, Vector3 basePosition)
    {
        return basePosition + Vector3.up * GetCharacterHeightOffset(character);
    }

    public Vector3 RemoveCharacterHeightOffset(GameObject character, Vector3 worldPosition)
    {
        return worldPosition - Vector3.up * GetCharacterHeightOffset(character);
    }

    private void EnsureGuiResources()
    {
        if (healthBarBackgroundTexture == null)
        {
            healthBarBackgroundTexture = CreateColorTexture(new Color(0f, 0f, 0f, 0.75f));
        }

        if (healthBarFillTexture == null)
        {
            healthBarFillTexture = CreateColorTexture(new Color(0.85f, 0.15f, 0.15f, 1f));
        }

        if (energyBarBackgroundTexture == null)
        {
            energyBarBackgroundTexture = CreateColorTexture(new Color(0.04f, 0.12f, 0.22f, 0.78f));
        }

        if (energyBarFillTexture == null)
        {
            energyBarFillTexture = CreateColorTexture(new Color(0.22f, 0.55f, 1f, 1f));
        }

        if (shieldBarBackgroundTexture == null)
        {
            shieldBarBackgroundTexture = CreateColorTexture(new Color(0f, 0.1f, 0.2f, 0.75f));
        }

        if (shieldBarFillTexture == null)
        {
            shieldBarFillTexture = CreateColorTexture(new Color(0.25f, 0.8f, 1f, 1f));
        }

        if (switchHintStyle == null)
        {
            switchHintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft
            };
            switchHintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
        }

        if (healthBarLabelStyle == null)
        {
            healthBarLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            healthBarLabelStyle.normal.textColor = Color.white;
        }
    }

    private void DrawStatusBars()
    {
        if (CurrentPlayer == null)
        {
            return;
        }

        float maxHealth = ResolveMaxHealth(CurrentPlayer);
        float currentHealth = Mathf.Clamp(ResolveCurrentHealth(CurrentPlayer), 0f, maxHealth);
        float maxEnergy = ResolveMaxEnergy(CurrentPlayer);
        float currentEnergy = Mathf.Clamp(ResolveCurrentEnergy(CurrentPlayer), 0f, maxEnergy);

        if (maxHealth <= 0f && maxEnergy <= 0f)
        {
            return;
        }

        const float x = 20f;
        const float y = 60f;
        const float width = 220f;
        const float height = 20f;
        const float gap = 8f;
        const float border = 2f;

        if (maxHealth > 0f)
        {
            DrawBar(new Rect(x, y, width, height), healthBarBackgroundTexture, healthBarFillTexture, currentHealth, maxHealth, "HP", Color.red);
        }

        if (maxEnergy > 0f)
        {
            DrawBar(new Rect(x, y + height + gap, width, height), energyBarBackgroundTexture, energyBarFillTexture, currentEnergy, maxEnergy, "MP", new Color(0.25f, 0.65f, 1f, 1f));
        }
    }

    private void DrawShieldBar()
    {
        if (CurrentPlayer == null)
        {
            return;
        }

        float currentShield = ResolveCurrentShield(CurrentPlayer);
        float maxShield = ResolveMaxShield(CurrentPlayer);
        if (currentShield <= 0f || maxShield <= 0f)
        {
            return;
        }

        const float width = 260f;
        const float height = 22f;
        const float border = 2f;
        float x = Mathf.Max(20f, (Screen.width - width) * 0.5f);
        float y = Mathf.Max(24f, Screen.height - 54f);

        GUI.DrawTexture(new Rect(x, y, width, height), shieldBarBackgroundTexture);

        float fillWidth = (width - border * 2f) * Mathf.Clamp01(currentShield / maxShield);
        if (fillWidth > 0f)
        {
            GUI.DrawTexture(new Rect(x + border, y + border, fillWidth, height - border * 2f), shieldBarFillTexture);
        }

        Color previousColor = healthBarLabelStyle.normal.textColor;
        healthBarLabelStyle.normal.textColor = new Color(0.5f, 0.9f, 1f, 1f);
        GUI.Label(new Rect(x, y, width, height), $"SHIELD  {Mathf.CeilToInt(currentShield)}/{Mathf.CeilToInt(maxShield)}", healthBarLabelStyle);
        healthBarLabelStyle.normal.textColor = previousColor;
    }

    private void DrawBar(Rect rect, Texture2D background, Texture2D fill, float currentValue, float maxValue, string label, Color labelColor)
    {
        if (background == null || fill == null || maxValue <= 0f)
        {
            return;
        }

        const float border = 2f;
        GUI.DrawTexture(rect, background);

        float fillWidth = (rect.width - border * 2f) * Mathf.Clamp01(currentValue / maxValue);
        if (fillWidth > 0f)
        {
            GUI.DrawTexture(new Rect(rect.x + border, rect.y + border, fillWidth, rect.height - border * 2f), fill);
        }

        Color previousColor = healthBarLabelStyle.normal.textColor;
        healthBarLabelStyle.normal.textColor = labelColor;
        GUI.Label(rect, $"{label}  {Mathf.CeilToInt(currentValue)}/{Mathf.CeilToInt(maxValue)}", healthBarLabelStyle);
        healthBarLabelStyle.normal.textColor = previousColor;
    }

    private void ApplyInitialHealth(GameObject player)
    {
        if (player == null)
        {
            return;
        }

        CombatStats stats = player.GetComponent<CombatStats>();
        float resolvedMaxHealth = stats != null && stats.maxHealth > 0f ? stats.maxHealth : playerStartHealth;

        BattleResourceBank resourceBank = player.GetComponent<BattleResourceBank>();
        if (resourceBank != null)
        {
            resourceBank.maxHealth = resolvedMaxHealth;
            resourceBank.currentHealth = resolvedMaxHealth;
        }

        CombatHealth combatHealth = player.GetComponent<CombatHealth>();
        if (combatHealth != null)
        {
            combatHealth.stats = stats;
            combatHealth.resourceBank = resourceBank;
            combatHealth.currentHealth = resolvedMaxHealth;
        }

        if (resourceBank != null)
        {
            resourceBank.SyncHealthFromCombatStats(refillCurrentHealth: true);
        }

        if (combatHealth != null)
        {
            combatHealth.SyncHealthFromStats(refillCurrentHealth: true);
        }
    }

    private float ResolveCurrentHealth(GameObject player)
    {
        if (player == null)
        {
            return 0f;
        }

        BattleResourceBank resourceBank = player.GetComponent<BattleResourceBank>();
        if (resourceBank != null)
        {
            return resourceBank.currentHealth;
        }

        CombatHealth combatHealth = player.GetComponent<CombatHealth>();
        if (combatHealth != null)
        {
            return combatHealth.currentHealth;
        }

        return 0f;
    }

    private float ResolveMaxHealth(GameObject player)
    {
        if (player == null)
        {
            return 0f;
        }

        BattleResourceBank resourceBank = player.GetComponent<BattleResourceBank>();
        if (resourceBank != null)
        {
            return resourceBank.maxHealth;
        }

        CombatStats stats = player.GetComponent<CombatStats>();
        if (stats != null)
        {
            return stats.maxHealth;
        }

        CombatHealth combatHealth = player.GetComponent<CombatHealth>();
        if (combatHealth != null)
        {
            return Mathf.Max(combatHealth.currentHealth, playerStartHealth);
        }

        return playerStartHealth;
    }

    private float ResolveCurrentEnergy(GameObject player)
    {
        if (player == null)
        {
            return 0f;
        }

        BattleResourceBank resourceBank = player.GetComponent<BattleResourceBank>();
        if (resourceBank != null)
        {
            return resourceBank.currentEnergy;
        }

        return 0f;
    }

    private float ResolveMaxEnergy(GameObject player)
    {
        if (player == null)
        {
            return 0f;
        }

        BattleResourceBank resourceBank = player.GetComponent<BattleResourceBank>();
        if (resourceBank != null)
        {
            return resourceBank.maxEnergy;
        }

        return 0f;
    }

    private float ResolveCurrentShield(GameObject player)
    {
        if (player == null)
        {
            return 0f;
        }

        CombatHealth combatHealth = player.GetComponent<CombatHealth>();
        if (combatHealth != null)
        {
            return combatHealth.CurrentShield;
        }

        BattleResourceBank resourceBank = player.GetComponent<BattleResourceBank>();
        if (resourceBank != null)
        {
            return resourceBank.CurrentShield;
        }

        return 0f;
    }

    private float ResolveMaxShield(GameObject player)
    {
        if (player == null)
        {
            return 0f;
        }

        CombatHealth combatHealth = player.GetComponent<CombatHealth>();
        if (combatHealth != null)
        {
            return combatHealth.MaxShield;
        }

        BattleResourceBank resourceBank = player.GetComponent<BattleResourceBank>();
        if (resourceBank != null)
        {
            return resourceBank.MaxShield;
        }

        return 0f;
    }

    private static Texture2D CreateColorTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private void DisablePlayer2AnimatorIfUsingPlayer01Controller()
    {
        if (player02 == null)
        {
            return;
        }

        if (player1Animator == null)
        {
            player1Animator = player01 != null ? player01.GetComponent<Animator>() : null;
        }

        if (player2Animator == null)
        {
            player2Animator = player02.GetComponent<Animator>();
        }

        if (player2Animator == null)
        {
            return;
        }

        RuntimeAnimatorController player2ControllerAsset = player2Animator.runtimeAnimatorController;
        RuntimeAnimatorController player1ControllerAsset = player1Animator != null ? player1Animator.runtimeAnimatorController : null;

        bool sharedWithPlayer1 =
            player2ControllerAsset != null &&
            player1ControllerAsset != null &&
            player2ControllerAsset == player1ControllerAsset;

        bool likelyPlayer01Controller =
            player2ControllerAsset != null &&
            player2ControllerAsset.name == "Player";

        if (sharedWithPlayer1 || likelyPlayer01Controller)
        {
            player2Animator.enabled = false;
            Debug.LogWarning("[PLAYER SWITCH] Disabled shared animator on switch/default player to preserve sprite.", player02);
        }
    }

    private static GameObject FindSceneObjectByNameIncludingInactive(string targetName)
    {
        if (string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();

        for (int i = 0; i < all.Length; i++)
        {
            GameObject go = all[i];

            if (go == null)
            {
                continue;
            }

            if (!go.scene.IsValid())
            {
                continue;
            }

            if (go.name == targetName)
            {
                return go;
            }
        }

        return null;
    }

private static bool IsValidSceneObject(GameObject go)
    {
        return go != null && go.scene.IsValid();
    }
}

[DisallowMultipleComponent]
public sealed class TwinTransitionBlessingStatus : MonoBehaviour
{
    private const string RadianceBlessingName = "Radiance Blessing";
    private const string NightfallFavorName = "Nightfall Favor";
    private const string NightfallFavorShieldSourceId = "TwinTransition.NightfallFavor";

    private enum BlessingKind
    {
        None,
        RadianceBlessing,
        NightfallFavor
    }

    [SerializeField] private bool debugLifecycle;

    private BlessingKind activeBlessing;
    private CombatStats combatStats;
    private CombatHealth combatHealth;
    private PlayerSkillCooldownManager cooldownManager;
    private PlayerTimedShieldStatus nightfallShieldStatus;
    private float appliedPhysicalAttackBonus;
    private float appliedSpecialAttackBonus;
    private float appliedPhysicalDefenseBonus;
    private float appliedSpecialDefenseBonus;
    private bool deathSubscribed;

    public bool HasActiveBlessing => activeBlessing != BlessingKind.None;

    private void Awake()
    {
        ResolveReferences();
        SubscribeDeathEventIfNeeded();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeDeathEventIfNeeded();
    }

    private void Update()
    {
        if (activeBlessing == BlessingKind.None)
        {
            return;
        }

        ResolveReferences();

        if (combatHealth != null && combatHealth.IsDead)
        {
            ClearBlessing("Death");
            return;
        }

        if (activeBlessing == BlessingKind.RadianceBlessing)
        {
            if (combatStats != null)
            {
                SyncStatMultiplier(ref combatStats.physicalDefense, ref appliedPhysicalDefenseBonus, 1.5f);
                SyncStatMultiplier(ref combatStats.specialDefense, ref appliedSpecialDefenseBonus, 1.5f);
            }

            cooldownManager?.SetManaRegenMultiplier(this, 1.5f);

            if (DayNightAffinityDamageModifier.HasNightChildState(gameObject))
            {
                ClearBlessing("NightChildActivated");
            }

            return;
        }

        if (combatStats != null)
        {
            SyncStatMultiplier(ref combatStats.physicalAttack, ref appliedPhysicalAttackBonus, 1.5f);
            SyncStatMultiplier(ref combatStats.specialAttack, ref appliedSpecialAttackBonus, 1.5f);
        }

        if (DayNightAffinityDamageModifier.HasDayChildState(gameObject))
        {
            ClearBlessing("DayChildActivated");
        }
    }

    private void OnDisable()
    {
        UnsubscribeDeathEventIfNeeded();
        if (activeBlessing != BlessingKind.None)
        {
            ClearBlessing("BattleEnd");
        }
    }

    private void OnDestroy()
    {
        UnsubscribeDeathEventIfNeeded();
        if (activeBlessing != BlessingKind.None)
        {
            ClearBlessing("Destroy");
        }
    }

    public void ApplyBlessingForSwitchIn(PlayerDayNightAffinityType affinityType)
    {
        if (affinityType == PlayerDayNightAffinityType.NightChild)
        {
            ApplyRadianceBlessing();
            return;
        }

        if (affinityType == PlayerDayNightAffinityType.DayChild)
        {
            ApplyNightfallFavor();
        }
    }

    public void ClearBlessing(string reason)
    {
        if (activeBlessing == BlessingKind.None)
        {
            return;
        }

        if (activeBlessing == BlessingKind.RadianceBlessing)
        {
            cooldownManager?.RemoveManaRegenMultiplier(this);
        }
        else if (activeBlessing == BlessingKind.NightfallFavor)
        {
            ResolveNightfallShieldStatus(createIfMissing: false)?.ClearShield();
        }

        RevertAllAppliedBonuses();

        string blessingName = GetBlessingName(activeBlessing);
        activeBlessing = BlessingKind.None;

        if (combatHealth != null)
        {
            combatHealth.NotifyShieldStateChanged();
        }

        DebugLifecycle($"Removed {blessingName}, reason={reason}");
        RefreshCurrentPlayerPresentationIfNeeded();
    }

    private void ApplyRadianceBlessing()
    {
        ResolveReferences();
        SubscribeDeathEventIfNeeded();

        if (combatHealth != null && combatHealth.IsDead)
        {
            return;
        }

        if (DayNightAffinityDamageModifier.HasNightChildState(gameObject))
        {
            ClearBlessing("NightChildActivated");
            return;
        }

        PrepareForBlessing(BlessingKind.RadianceBlessing);
        activeBlessing = BlessingKind.RadianceBlessing;

        if (combatStats != null)
        {
            SyncStatMultiplier(ref combatStats.physicalDefense, ref appliedPhysicalDefenseBonus, 1.5f);
            SyncStatMultiplier(ref combatStats.specialDefense, ref appliedSpecialDefenseBonus, 1.5f);
        }

        cooldownManager?.SetManaRegenMultiplier(this, 1.5f);

        DebugLifecycle($"Applied Radiance Blessing to {ResolvePlayerLabel()}");
        RefreshCurrentPlayerPresentationIfNeeded();
    }

    private void ApplyNightfallFavor()
    {
        ResolveReferences();
        SubscribeDeathEventIfNeeded();

        if (combatHealth != null && combatHealth.IsDead)
        {
            return;
        }

        if (DayNightAffinityDamageModifier.HasDayChildState(gameObject))
        {
            ClearBlessing("DayChildActivated");
            return;
        }

        PrepareForBlessing(BlessingKind.NightfallFavor);
        activeBlessing = BlessingKind.NightfallFavor;

        if (combatStats != null)
        {
            SyncStatMultiplier(ref combatStats.physicalAttack, ref appliedPhysicalAttackBonus, 1.5f);
            SyncStatMultiplier(ref combatStats.specialAttack, ref appliedSpecialAttackBonus, 1.5f);
        }

        float shieldAmount = combatHealth != null ? Mathf.Max(0f, combatHealth.MaxHealthValue) : 0f;
        if (shieldAmount > 0f)
        {
            ResolveNightfallShieldStatus()?.ApplyPersistentShield(shieldAmount);
            combatHealth?.NotifyShieldStateChanged();
        }

        DebugLifecycle($"Applied Nightfall Favor to {ResolvePlayerLabel()}, shield={shieldAmount:F2}");
        RefreshCurrentPlayerPresentationIfNeeded();
    }

    private void PrepareForBlessing(BlessingKind newBlessing)
    {
        if (activeBlessing != BlessingKind.None && activeBlessing != newBlessing)
        {
            ClearBlessing("Replaced");
        }

        if (newBlessing != BlessingKind.RadianceBlessing)
        {
            cooldownManager?.RemoveManaRegenMultiplier(this);
        }
    }

    private void ResolveReferences()
    {
        if (combatStats == null)
        {
            combatStats = GetComponent<CombatStats>();
        }

        if (combatHealth == null)
        {
            combatHealth = GetComponent<CombatHealth>();
        }

        if (cooldownManager == null)
        {
            cooldownManager = GetComponent<PlayerSkillCooldownManager>();
        }
    }

    private void SubscribeDeathEventIfNeeded()
    {
        if (deathSubscribed || combatHealth == null)
        {
            return;
        }

        combatHealth.Died += HandleOwnerDied;
        deathSubscribed = true;
    }

    private void UnsubscribeDeathEventIfNeeded()
    {
        if (!deathSubscribed || combatHealth == null)
        {
            return;
        }

        combatHealth.Died -= HandleOwnerDied;
        deathSubscribed = false;
    }

    private void HandleOwnerDied(GameObject _)
    {
        ClearBlessing("Death");
    }

    private PlayerTimedShieldStatus ResolveNightfallShieldStatus(bool createIfMissing = true)
    {
        if (nightfallShieldStatus != null)
        {
            return nightfallShieldStatus;
        }

        nightfallShieldStatus = createIfMissing
            ? PlayerTimedShieldStatus.GetOrAdd(gameObject, NightfallFavorShieldSourceId)
            : PlayerTimedShieldStatus.Find(gameObject, NightfallFavorShieldSourceId);
        return nightfallShieldStatus;
    }

    private void RevertAllAppliedBonuses()
    {
        if (combatStats == null)
        {
            appliedPhysicalAttackBonus = 0f;
            appliedSpecialAttackBonus = 0f;
            appliedPhysicalDefenseBonus = 0f;
            appliedSpecialDefenseBonus = 0f;
            return;
        }

        RemoveAppliedBonus(ref combatStats.physicalAttack, ref appliedPhysicalAttackBonus);
        RemoveAppliedBonus(ref combatStats.specialAttack, ref appliedSpecialAttackBonus);
        RemoveAppliedBonus(ref combatStats.physicalDefense, ref appliedPhysicalDefenseBonus);
        RemoveAppliedBonus(ref combatStats.specialDefense, ref appliedSpecialDefenseBonus);
    }

    private void SyncStatMultiplier(ref float statValue, ref float appliedBonus, float multiplier)
    {
        float rawWithoutBlessing = Mathf.Max(0f, statValue - appliedBonus);
        float desiredBonus = Mathf.Max(0f, rawWithoutBlessing * Mathf.Max(0f, multiplier - 1f));
        statValue = rawWithoutBlessing + desiredBonus;
        appliedBonus = desiredBonus;
    }

    private void RemoveAppliedBonus(ref float statValue, ref float appliedBonus)
    {
        if (appliedBonus <= 0f)
        {
            return;
        }

        statValue = Mathf.Max(0f, statValue - appliedBonus);
        appliedBonus = 0f;
    }

    private void RefreshCurrentPlayerPresentationIfNeeded()
    {
        Player2Bootstrap bootstrap = FindObjectOfType<Player2Bootstrap>();
        if (bootstrap != null && bootstrap.CurrentPlayer == gameObject)
        {
            bootstrap.RefreshCurrentPlayerPresentation();
        }
    }

    private string ResolvePlayerLabel()
    {
        PlayerDayNightAffinity affinity = GetComponent<PlayerDayNightAffinity>();
        if (affinity != null)
        {
            if (affinity.AffinityType == PlayerDayNightAffinityType.NightChild)
            {
                return "Player01";
            }

            if (affinity.AffinityType == PlayerDayNightAffinityType.DayChild)
            {
                return "Player02";
            }
        }

        return string.IsNullOrWhiteSpace(name) ? "Player" : name;
    }

    private string GetBlessingName(BlessingKind blessing)
    {
        switch (blessing)
        {
            case BlessingKind.RadianceBlessing:
                return RadianceBlessingName;
            case BlessingKind.NightfallFavor:
                return NightfallFavorName;
            default:
                return "None";
        }
    }

    private void DebugLifecycle(string message)
    {
        if (!debugLifecycle)
        {
            return;
        }

        Debug.Log($"[TwinTransitionBuff] {message}", this);
        Debug.Log($"[TwinTransitionBuff.Debug] owner={name} state={activeBlessing} {message}", this);
    }
}
