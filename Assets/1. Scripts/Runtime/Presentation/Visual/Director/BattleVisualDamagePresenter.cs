using System.Collections.Generic;
using UnityEngine;

internal sealed class BattleVisualDamagePresenter
{
    private BattleUIManager battleUIManager;
    private BattleWorldCharacterPlateManager worldPlateManager;
    private readonly bool logDebug;

    public BattleVisualDamagePresenter(
        BattleUIManager battleUIManager,
        bool logDebug)
    {
        this.battleUIManager = battleUIManager;
        this.logDebug = logDebug;
    }

    public void Prepare(
        BattleVisualPlaybackState playback,
        SkillVisualDefinition visual)
    {
        ResetCurrentDamageState(playback);
        PrepareHitDamages(playback, visual);

        bool created =
            EnsurePresentationState(playback);

        if (created)
        {
            SyncAllPresentationState(
                playback,
                forceImmediate: true);
            return;
        }

        // Clash sequence에서는 root 시작 시 전체 대상의 before 상태를 이미 잠갔다.
        // 개별 exchange 진입에서 같은 값을 다시 쓰면 이전 hit 진행 상태가 되감기므로
        // 여기서는 현재 request의 hit 분배만 준비한다.
        if (playback?.Request?.HasTargetImpacts != true)
        {
            BeginLegacyHpOverride(playback);
            BeginLegacyStaggerOverride(playback);
        }
    }

    public void PrepareSequence(
        BattleVisualPlaybackState playback)
    {
        BattleVisualRequest root =
            playback?.RootRequest;

        if (root?.HasClashSequence != true)
            return;

        EnsurePresentationState(playback);

        // 합 결과는 로직 단계에서 마지막 교환까지 이미 계산되어 있다.
        // Timeline 진입 전에 모든 영향 대상의 최초 before snapshot을 화면에 고정한다.
        SyncAllPresentationState(
            playback,
            forceImmediate: true);
    }

    public int GetDamageForHitIndex(
        BattleVisualPlaybackState playback,
        int hitIndex)
    {
        if (playback?.Request == null)
            return 0;

        TargetImpactPresentation primary =
            playback.Request.PrimaryImpact;

        if (primary != null)
            return primary.GetDamageForHitIndex(hitIndex);

        List<int> damages = playback.HitDamages;

        if (damages.Count == 0)
            return playback.Request.GetDamageForHitIndex(hitIndex);

        if (hitIndex < 0)
            return damages[0];

        return hitIndex < damages.Count
            ? damages[hitIndex]
            : 0;
    }

    public int GetImpactDamageForHitIndex(
        TargetImpactPresentation impact,
        int hitIndex)
    {
        return impact?.GetDamageForHitIndex(hitIndex) ?? 0;
    }

    public void ApplyHit(
        BattleVisualPlaybackState playback,
        int hitIndex,
        int legacyPrimaryDamage)
    {
        if (playback?.Request == null)
            return;

        BattlePresentationState state =
            playback.PresentationState;

        if (state != null)
        {
            state.ApplyHit(
                playback.Request,
                hitIndex);

            if (!playback.Request.HasTargetImpacts)
            {
                ApplyLegacyHpDamage(
                    playback,
                    legacyPrimaryDamage);
            }

            SyncRequestPresentationState(
                playback,
                hitIndex);

            if (!playback.Request.HasTargetImpacts)
            {
                RefreshBattleUi(
                    playback.Request,
                    hitIndex,
                    legacyPrimaryDamage);
            }

            return;
        }

        ApplyLegacyHpDamage(
            playback,
            legacyPrimaryDamage);

        if (hitIndex == 0)
            ApplyLegacyStaggerDamage(playback);

        RefreshBattleUi(
            playback.Request,
            hitIndex,
            legacyPrimaryDamage);
    }

    public void Clear(BattleVisualPlaybackState playback)
    {
        if (playback == null)
            return;

        ResolveBattleUiManager();
        ResolveWorldPlateManager();

        BattlePresentationState state =
            playback.PresentationState;

        // 먼저 read model을 해제해야 이후 Refresh가 실제 최종 Character/BodyPart 상태를 읽는다.
        BattlePresentationStateRegistry.Clear(state);

        HashSet<Character> worldCharacters =
            new HashSet<Character>();

        foreach (BattleVisualHpOverrideTarget target
                 in playback.HpOverrideTargets)
        {
            if (target.Character == null)
                continue;

            battleUIManager?.ClearTargetHpOverride(
                target.Character,
                target.Part);

            worldCharacters.Add(
                target.Character);
        }

        foreach (Character character
                 in worldCharacters)
        {
            worldPlateManager?.ClearVisualHpOverride(
                character);
        }

        foreach (Character character
                 in playback.StaggerOverrideTargets)
        {
            if (character == null)
                continue;

            worldPlateManager?.ClearVisualStaggerOverride(
                character);
        }

        if (state != null)
        {
            foreach (BodyPartDisplayState partState
                     in state.Parts)
            {
                if (partState?.Character == null)
                    continue;

                battleUIManager?.RefreshTargetUI(
                    partState.Character,
                    partState.Part);
            }

            foreach (CharacterDisplayState characterState
                     in state.Characters)
            {
                Character character =
                    characterState?.Character;

                if (character == null)
                    continue;

                if (characterState.HasCharacterLevelImpact)
                {
                    battleUIManager?.RefreshTargetUI(
                        character,
                        null);
                }

                worldPlateManager?.RefreshCharacter(
                    character);

                CharacterView view =
                    BattleCameraTargetResolver.GetView(
                        character);

                view?.RefreshVisualState();
            }
        }
        else
        {
            foreach (BattleVisualHpOverrideTarget target
                     in playback.HpOverrideTargets)
            {
                if (target.Character == null)
                    continue;

                battleUIManager?.RefreshTargetUI(
                    target.Character,
                    target.Part);

                worldPlateManager?.RefreshCharacter(
                    target.Character);
            }
        }

        playback.HpOverrideTargets.Clear();
        playback.StaggerOverrideTargets.Clear();
        playback.PresentationState = null;
        ResetCurrentDamageState(playback);

        Canvas.ForceUpdateCanvases();
    }

    private bool EnsurePresentationState(
        BattleVisualPlaybackState playback)
    {
        if (playback == null)
            return false;

        if (playback.PresentationState != null)
        {
            BattlePresentationStateRegistry.Bind(
                playback.PresentationState);
            return false;
        }

        BattleVisualRequest root =
            playback.RootRequest ??
            playback.Request;

        BattlePresentationState state =
            BattlePresentationState.Create(root);

        playback.PresentationState = state;
        BattlePresentationStateRegistry.Bind(state);
        return true;
    }

    private void SyncAllPresentationState(
        BattleVisualPlaybackState playback,
        bool forceImmediate)
    {
        BattlePresentationState state =
            playback?.PresentationState;

        if (state == null)
            return;

        ResolveBattleUiManager();
        ResolveWorldPlateManager();

        foreach (BodyPartDisplayState partState
                 in state.Parts)
        {
            if (partState?.Character == null ||
                partState.Part == null)
            {
                continue;
            }

            playback.TrackHpOverride(
                partState.Character,
                partState.Part);

            battleUIManager?.SetTargetHpOverride(
                partState.Character,
                partState.Part,
                partState.CurrentHp);

            battleUIManager?.RefreshTargetUI(
                partState.Character,
                partState.Part);
        }

        foreach (CharacterDisplayState characterState
                 in state.Characters)
        {
            Character character =
                characterState?.Character;

            if (character == null)
                continue;

            if (characterState.HasCharacterLevelImpact)
            {
                playback.TrackHpOverride(
                    character,
                    null);

                battleUIManager?.SetTargetHpOverride(
                    character,
                    null,
                    characterState.CurrentHp);

                battleUIManager?.RefreshTargetUI(
                    character,
                    null);
            }

            worldPlateManager?.SetVisualHpOverride(
                character,
                characterState.CurrentHp,
                forceImmediate);
        }

        foreach (StaggerDisplayState staggerState
                 in state.StaggerStates)
        {
            Character character =
                staggerState?.Character;

            if (character == null)
                continue;

            playback.TrackStaggerOverride(
                character);

            worldPlateManager?.SetVisualStaggerOverride(
                character,
                staggerState.CurrentGauge,
                staggerState.IsVulnerable);
        }

        Canvas.ForceUpdateCanvases();
    }

    private void SyncRequestPresentationState(
        BattleVisualPlaybackState playback,
        int hitIndex)
    {
        BattleVisualRequest request =
            playback?.Request;

        BattlePresentationState state =
            playback?.PresentationState;

        if (request == null || state == null)
            return;

        ResolveBattleUiManager();
        ResolveWorldPlateManager();

        HashSet<Character> refreshedCharacters =
            new HashSet<Character>();

        HashSet<BodyPart> refreshedParts =
            new HashSet<BodyPart>();

        if (request.TargetImpacts != null)
        {
            foreach (TargetImpactPresentation impact
                     in request.TargetImpacts)
            {
                if (impact?.Target == null)
                    continue;

                if (impact.TargetPart != null &&
                    refreshedParts.Add(impact.TargetPart) &&
                    state.TryGetPart(
                        impact.TargetPart,
                        out BodyPartDisplayState partState))
                {
                    playback.TrackHpOverride(
                        impact.Target,
                        impact.TargetPart);

                    battleUIManager?.SetTargetHpOverride(
                        impact.Target,
                        impact.TargetPart,
                        partState.CurrentHp);

                    battleUIManager?.RefreshTargetUI(
                        impact.Target,
                        impact.TargetPart);
                }
                else if (impact.TargetPart == null &&
                         state.TryGetCharacter(
                             impact.Target,
                             out CharacterDisplayState directState) &&
                         directState.HasCharacterLevelImpact)
                {
                    playback.TrackHpOverride(
                        impact.Target,
                        null);

                    battleUIManager?.SetTargetHpOverride(
                        impact.Target,
                        null,
                        directState.CurrentHp);

                    battleUIManager?.RefreshTargetUI(
                        impact.Target,
                        null);
                }

                if (refreshedCharacters.Add(impact.Target) &&
                    state.TryGetCharacter(
                        impact.Target,
                        out CharacterDisplayState characterState))
                {
                    worldPlateManager?.SetVisualHpOverrideAtHit(
                        impact.Target,
                        characterState.CurrentHp);
                }

                if (impact.IsFinalHit(hitIndex))
                {
                    CharacterView view =
                        BattleCameraTargetResolver.GetView(
                            impact.Target);

                    view?.RefreshVisualState();
                }
            }
        }

        if (request.Target != null &&
            request.StaggerDamage > 0 &&
            state.TryGetStagger(
                request.Target,
                out StaggerDisplayState staggerState))
        {
            playback.TrackStaggerOverride(
                request.Target);

            worldPlateManager?.SetVisualStaggerOverrideAtHit(
                request.Target,
                staggerState.CurrentGauge,
                staggerState.IsVulnerable);
        }

        Canvas.ForceUpdateCanvases();

        if (logDebug)
        {
            Debug.Log(
                $"[BattleVisualDamagePresenter] HitFrame presentation commit / " +
                $"Request={request.RequestId}, Hit={hitIndex}, " +
                $"Impacts={request.TargetImpacts?.Count ?? 0}");
        }
    }

    private static void ResetCurrentDamageState(
        BattleVisualPlaybackState playback)
    {
        if (playback == null)
            return;

        playback.HasVisualHpOverride = false;
        playback.VisualHpStart = 0;
        playback.VisualHpFinal = 0;
        playback.VisualDamageAccumulated = 0;
        playback.HitDamages.Clear();
    }

    private static void PrepareHitDamages(
        BattleVisualPlaybackState playback,
        SkillVisualDefinition visual)
    {
        if (playback == null)
            return;

        BattleVisualRequest request = playback.Request;
        playback.HitDamages.Clear();

        if (request?.HitDamages == null || request.HitDamages.Count == 0)
            return;

        int totalDamage = DamageDistributionUtility.Sum(request.HitDamages);

        if (request.HitDamages.Count > 1)
        {
            playback.HitDamages.AddRange(request.HitDamages);
            return;
        }

        int expectedCount = visual != null
            ? Mathf.Max(1, visual.ExpectedHitFrameCount)
            : 1;

        playback.HitDamages.AddRange(
            DamageDistributionUtility.DistributeByWeights(
                totalDamage,
                visual?.HitDamageWeights,
                expectedCount));
    }

    // DamageContext가 없는 구형/진단 요청의 HP 표시만 기존 방식으로 보존한다.
    private void BeginLegacyHpOverride(
        BattleVisualPlaybackState playback)
    {
        if (playback == null)
            return;

        BattleVisualRequest request = playback.Request;
        int totalDamage = DamageDistributionUtility.Sum(playback.HitDamages);

        if (request?.Target == null || totalDamage <= 0)
            return;

        ResolveLegacyHpRange(
            request,
            totalDamage,
            out int visualStartHp,
            out int visualFinalHp);

        playback.VisualHpStart = visualStartHp;
        playback.VisualHpFinal = visualFinalHp;
        playback.VisualDamageAccumulated = 0;
        playback.HasVisualHpOverride = true;
        playback.TrackHpOverride(
            request.Target,
            request.TargetPart);

        ResolveBattleUiManager();
        battleUIManager?.SetTargetHpOverride(
            request.Target,
            request.TargetPart,
            visualStartHp);

        ResolveWorldPlateManager();
        worldPlateManager?.SetVisualHpOverride(
            request.Target,
            ResolveLegacyWorldStartHp(
                request,
                totalDamage),
            forceImmediate: true);

        RefreshBattleUi(request, -1, 0);
    }

    private void BeginLegacyStaggerOverride(
        BattleVisualPlaybackState playback)
    {
        BattleVisualRequest request =
            playback?.Request;

        if (request?.Target == null ||
            request.StaggerDamage <= 0)
        {
            return;
        }

        ResolveWorldPlateManager();
        playback.TrackStaggerOverride(request.Target);

        worldPlateManager?.SetVisualStaggerOverride(
            request.Target,
            request.StaggerGaugeBefore,
            vulnerable:
                request.StaggerGaugeBefore <= 0);
    }

    private void ApplyLegacyHpDamage(
        BattleVisualPlaybackState playback,
        int damage)
    {
        if (playback == null ||
            !playback.HasVisualHpOverride ||
            damage <= 0)
        {
            return;
        }

        BattleVisualRequest request = playback.Request;

        if (request?.Target == null)
            return;

        playback.VisualDamageAccumulated += damage;

        int displayHp = Mathf.Max(
            playback.VisualHpFinal,
            playback.VisualHpStart - playback.VisualDamageAccumulated);

        ResolveBattleUiManager();
        battleUIManager?.SetTargetHpOverride(
            request.Target,
            request.TargetPart,
            displayHp);

        ResolveWorldPlateManager();

        int worldStartHp =
            ResolveLegacyWorldStartHp(
                request,
                DamageDistributionUtility.Sum(playback.HitDamages));

        int worldFinalHp =
            ResolveLegacyWorldFinalHp(request);

        int worldDisplayHp = Mathf.Max(
            worldFinalHp,
            worldStartHp - playback.VisualDamageAccumulated);

        worldPlateManager?.SetVisualHpOverrideAtHit(
            request.Target,
            worldDisplayHp);
    }

    private void ApplyLegacyStaggerDamage(
        BattleVisualPlaybackState playback)
    {
        BattleVisualRequest request =
            playback?.Request;

        if (request?.Target == null ||
            request.StaggerDamage <= 0)
        {
            return;
        }

        ResolveWorldPlateManager();
        playback.TrackStaggerOverride(request.Target);

        worldPlateManager?.SetVisualStaggerOverrideAtHit(
            request.Target,
            request.StaggerGaugeAfter,
            vulnerable:
                request.StaggerGaugeAfter <= 0);
    }

    private static void ResolveLegacyHpRange(
        BattleVisualRequest request,
        int totalDamage,
        out int visualStartHp,
        out int visualFinalHp)
    {
        if (request.TargetPart != null)
        {
            int maxHp = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    request.TargetPart.MaxPartHP));

            if (request.HasTargetPartHpSnapshot)
            {
                visualStartHp = Mathf.Clamp(
                    request.TargetPartHpBefore,
                    0,
                    maxHp);

                visualFinalHp = Mathf.Clamp(
                    request.TargetPartHpAfter,
                    0,
                    maxHp);
                return;
            }

            visualFinalHp = Mathf.Clamp(
                Mathf.RoundToInt(
                    request.TargetPart.PartHP),
                0,
                maxHp);

            visualStartHp = Mathf.Clamp(
                visualFinalHp + totalDamage,
                0,
                maxHp);
            return;
        }

        int characterMaxHp = Mathf.Max(
            1,
            request.TargetCharacterMaxHp > 0
                ? request.TargetCharacterMaxHp
                : request.Target.MaxCombatHP);

        if (request.HasTargetCharacterHpSnapshot)
        {
            visualStartHp = Mathf.Clamp(
                request.TargetCharacterHpBefore,
                0,
                characterMaxHp);

            visualFinalHp = Mathf.Clamp(
                request.TargetCharacterHpAfter,
                0,
                characterMaxHp);
            return;
        }

        visualFinalHp = Mathf.Clamp(
            request.Target.CurrentHP,
            0,
            characterMaxHp);

        visualStartHp = Mathf.Clamp(
            visualFinalHp + totalDamage,
            0,
            characterMaxHp);
    }

    private static int ResolveLegacyWorldStartHp(
        BattleVisualRequest request,
        int totalDamage)
    {
        if (request?.Target == null)
            return 0;

        int maximum = ResolveLegacyWorldMaxHp(request);

        if (request.HasTargetCharacterHpSnapshot)
        {
            return Mathf.Clamp(
                request.TargetCharacterHpBefore,
                0,
                maximum);
        }

        return Mathf.Clamp(
            ResolveLegacyWorldFinalHp(request) +
            Mathf.Max(0, totalDamage),
            0,
            maximum);
    }

    private static int ResolveLegacyWorldFinalHp(
        BattleVisualRequest request)
    {
        if (request?.Target == null)
            return 0;

        int maximum = ResolveLegacyWorldMaxHp(request);

        if (request.HasTargetCharacterHpSnapshot)
        {
            return Mathf.Clamp(
                request.TargetCharacterHpAfter,
                0,
                maximum);
        }

        return Mathf.Clamp(
            request.Target.CurrentHP,
            0,
            maximum);
    }

    private static int ResolveLegacyWorldMaxHp(
        BattleVisualRequest request)
    {
        if (request?.Target == null)
            return 1;

        return Mathf.Max(
            1,
            request.TargetCharacterMaxHp > 0
                ? request.TargetCharacterMaxHp
                : request.Target.MaxCombatHP);
    }

    private void RefreshBattleUi(
        BattleVisualRequest request,
        int hitIndex,
        int damage)
    {
        if (request?.Target == null)
            return;

        ResolveBattleUiManager();
        battleUIManager?.RefreshTargetUI(
            request.Target,
            request.TargetPart);

        ResolveWorldPlateManager();
        worldPlateManager?.RefreshCharacter(
            request.Target);

        Canvas.ForceUpdateCanvases();

        if (!logDebug || hitIndex < 0)
            return;

        Debug.Log(
            $"[BattleVisualDamagePresenter] Legacy HitFrame UI / " +
            $"Request={request.RequestId}, " +
            $"Target={request.Target.Data?.CharacterName}, " +
            $"Point={(request.TargetPart == null ? "SINGLE_HP" : request.TargetPart.Type.ToString())}, " +
            $"Hit={hitIndex}, Damage={damage}");
    }

    private void ResolveWorldPlateManager()
    {
        if (worldPlateManager != null)
            return;

        worldPlateManager =
            Object.FindFirstObjectByType<BattleWorldCharacterPlateManager>(
                FindObjectsInactive.Include);
    }

    private void ResolveBattleUiManager()
    {
        if (battleUIManager == null)
            battleUIManager =
                Object.FindFirstObjectByType<BattleUIManager>();
    }
}
