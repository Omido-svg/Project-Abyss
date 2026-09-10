using System;
using UnityEngine;

/// <summary>
/// Timeline hit frame에서 이미 확정된 전투 결과를 화면 상태, 피격 반응,
/// 데미지 숫자와 VFX로 재생한다.
///
/// 전투 피해를 다시 계산하거나 적용하지 않는다. BattleVisualDamagePresenter가
/// 보유한 확정 결과/read model을 같은 hit 시점으로 진행하는 presentation 전용 객체다.
/// </summary>
internal sealed class BattleHitPresenter
{
    private readonly BattleVisualDamagePresenter damagePresenter;
    private readonly DamageNumberManager damageNumberManager;
    private readonly BattleVfxManager vfxManager;
    private readonly Func<BattleVisualPlaybackState, bool> isPlaybackActive;
    private readonly Action<BattleAction, int, int> hitFramePresented;
    private readonly bool logMissingReferences;
    private readonly bool logDebug;
    private readonly UnityEngine.Object logContext;

    public BattleHitPresenter(
        BattleVisualDamagePresenter damagePresenter,
        DamageNumberManager damageNumberManager,
        BattleVfxManager vfxManager,
        Func<BattleVisualPlaybackState, bool> isPlaybackActive,
        Action<BattleAction, int, int> hitFramePresented,
        bool logMissingReferences,
        bool logDebug,
        UnityEngine.Object logContext)
    {
        this.damagePresenter = damagePresenter;
        this.damageNumberManager = damageNumberManager;
        this.vfxManager = vfxManager;
        this.isPlaybackActive = isPlaybackActive;
        this.hitFramePresented = hitFramePresented;
        this.logMissingReferences = logMissingReferences;
        this.logDebug = logDebug;
        this.logContext = logContext;
    }

    public int GetDamageForHitIndex(
        BattleVisualPlaybackState playback,
        int hitIndex)
    {
        return damagePresenter != null
            ? damagePresenter.GetDamageForHitIndex(
                playback,
                hitIndex)
            : 0;
    }

    public void ApplyHitFrame(
        BattleVisualPlaybackState playback,
        SkillVisualDefinition visual,
        int hitIndex,
        int? damageOverride = null,
        int exchangeIndex = -1,
        bool isClash = false,
        bool isOneSided = false)
    {
        if (playback == null ||
            playback.IsCancellationRequested ||
            playback.IsCompleted ||
            playback.IsCleanedUp ||
            (isPlaybackActive != null &&
             !isPlaybackActive(playback)))
        {
            return;
        }

        BattleVisualRequest request =
            playback.Request;

        if (request == null ||
            visual == null ||
            !visual.HasHitFrameDamage ||
            damagePresenter == null)
        {
            return;
        }

        CharacterView primaryTargetView =
            playback.ActiveTargetView;

        if (primaryTargetView == null &&
            request.Target != null)
        {
            primaryTargetView =
                BattleCameraTargetResolver.GetView(
                    request.Target);
        }

        int primaryDamage =
            damageOverride ??
            damagePresenter.GetDamageForHitIndex(
                playback,
                hitIndex);

        if (logDebug)
        {
            Debug.Log(
                $"[BattleHitPresenter] HitFrame 적용 : " +
                $"{request.Attacker?.Data.CharacterName} -> {request.Target?.Data.CharacterName} / " +
                $"HitIndex={hitIndex} / PrimaryDamage={primaryDamage} / " +
                $"Impacts={request.TargetImpacts?.Count ?? 0}",
                logContext);
        }

        bool useExplicitVisualFx =
            visual.UseExplicitVisualFxTracks;

        // 위치 기반 VFX는 해당 view/target이 있을 때만 재생한다.
        // presentation state/HUD commit은 view 존재 여부와 무관하게 진행한다.
        if (!useExplicitVisualFx &&
            primaryTargetView != null)
        {
            PlaySkillVfx(
                playback,
                request,
                visual,
                BattleVfxTiming.OnHitFrame,
                hitIndex,
                primaryDamage);

            if (request.WasCritical && hitIndex == 0)
            {
                PlaySkillVfx(
                    playback,
                    request,
                    visual,
                    BattleVfxTiming.OnCritical,
                    hitIndex,
                    primaryDamage);
            }
        }

        // F04/F05: 데이터/HUD를 먼저 같은 hit 시점으로 진행한다.
        // TargetView 하나가 누락되어도 다른 대상과 read model은 정상 진행한다.
        damagePresenter.ApplyHit(
            playback,
            hitIndex,
            primaryDamage);

        bool usedImpactList =
            request.TargetImpacts != null &&
            request.TargetImpacts.Count > 0;

        if (usedImpactList)
        {
            foreach (TargetImpactPresentation impact
                     in request.TargetImpacts)
            {
                if (impact?.Target == null)
                    continue;

                int impactDamage =
                    damagePresenter.GetImpactDamageForHitIndex(
                        impact,
                        hitIndex);

                CharacterView targetView =
                    impact.IsPrimary &&
                    primaryTargetView != null
                        ? primaryTargetView
                        : BattleCameraTargetResolver.GetView(
                            impact.Target);

                if (targetView == null)
                {
                    if (logDebug)
                    {
                        Debug.LogWarning(
                            $"[BattleHitPresenter] Impact TargetView 없음. " +
                            $"HUD/read model만 진행 / " +
                            $"Target={impact.Target.name}, " +
                            $"Part={impact.TargetPart?.Type.ToString() ?? "SINGLE_HP"}",
                            logContext);
                    }

                    continue;
                }

                playback.ActiveReactionView =
                    targetView;
                playback.ReactionViews.Add(
                    targetView);

                targetView.PlayReaction(
                    ResolveTargetReaction(
                        impact,
                        visual,
                        hitIndex));

                if (impactDamage > 0)
                {
                    ShowDamageNumber(
                        targetView,
                        impact.TargetPart,
                        impactDamage,
                        impact.WasCritical
                            ? BattleDamageNumberStyle.CriticalHp
                            : BattleDamageNumberStyle.NormalHp);
                }
            }
        }
        else if (primaryTargetView != null)
        {
            // DamageContext 없는 구형/진단 request 호환.
            playback.ActiveReactionView =
                primaryTargetView;
            playback.ReactionViews.Add(
                primaryTargetView);

            primaryTargetView.PlayReaction(
                ResolveTargetReaction(
                    request,
                    visual,
                    hitIndex));

            if (primaryDamage > 0)
            {
                ShowDamageNumber(
                    primaryTargetView,
                    request.TargetPart,
                    primaryDamage,
                    request.WasCritical
                        ? BattleDamageNumberStyle.CriticalHp
                        : BattleDamageNumberStyle.NormalHp);
            }
        }

        // 흐트러짐은 현재 primary exchange 대상에만 존재하며 교환당 한 번 표시한다.
        if (hitIndex == 0 &&
            request.StaggerDamage > 0 &&
            primaryTargetView != null)
        {
            ShowDamageNumber(
                primaryTargetView,
                request.TargetPart,
                request.StaggerDamage,
                BattleDamageNumberStyle.Stagger);
        }

        if (request.SourceAction != null)
        {
            hitFramePresented?.Invoke(
                request.SourceAction,
                exchangeIndex,
                hitIndex);
        }
    }

    /// <summary>
    /// Timeline Vfx event와 자동 hit VFX가 동일한 cue 분배 규칙을 공유한다.
    /// </summary>
    public void PlaySkillVfx(
        BattleVisualPlaybackState playback,
        BattleVisualRequest request,
        SkillVisualDefinition visual,
        BattleVfxTiming timing,
        int hitIndex = -1,
        int damage = 0)
    {
        if (request == null ||
            visual == null ||
            vfxManager == null ||
            visual.VfxCues == null)
        {
            return;
        }

        BattleVfxContext context =
            BattleVfxContext.FromRequest(
                request,
                hitIndex,
                damage);

        context.AttackerView =
            playback?.ActiveActionView ??
            BattleCameraTargetResolver.GetView(
                request.Attacker);

        context.TargetView =
            playback?.ActiveTargetView ??
            BattleCameraTargetResolver.GetView(
                request.Target);

        context.BindPlayback(playback);

        for (int i = 0; i < visual.VfxCues.Count; i++)
        {
            BattleVfxCue cue = visual.VfxCues[i];

            if (cue == null || cue.Timing != timing)
                continue;

            if (!ShouldPlayAutoDistributedHitCue(
                    visual,
                    cue,
                    i,
                    hitIndex))
            {
                continue;
            }

            vfxManager.PlayCue(
                cue,
                context,
                i);
        }
    }

    private void ShowDamageNumber(
        CharacterView targetView,
        BodyPart targetPart,
        int damage,
        BattleDamageNumberStyle style)
    {
        if (damageNumberManager == null)
        {
            if (logMissingReferences)
            {
                Debug.LogWarning(
                    "[BattleHitPresenter] DamageNumberManager 없음",
                    logContext);
            }

            return;
        }

        if (targetView == null ||
            damage <= 0)
        {
            return;
        }

        Vector3 position =
            targetView.GetDamageNumberPosition(
                targetPart);

        if (style == BattleDamageNumberStyle.Stagger)
            position += Vector3.up * 0.12f;

        damageNumberManager.ShowDamage(
            position,
            damage,
            style);
    }

    private static bool ShouldPlayAutoDistributedHitCue(
        SkillVisualDefinition visual,
        BattleVfxCue cue,
        int cueIndex,
        int hitIndex)
    {
        if (visual?.VfxCues == null ||
            cue == null ||
            hitIndex < 0 ||
            cue.Timing != BattleVfxTiming.OnHitFrame ||
            cue.UseHitIndexFilter)
        {
            return true;
        }

        int matchingCueCount = 0;
        int matchingCueOrdinal = -1;

        for (int i = 0; i < visual.VfxCues.Count; i++)
        {
            BattleVfxCue candidate = visual.VfxCues[i];

            if (!cue.CanAutoDistributeByHitIndexWith(candidate))
                continue;

            if (i == cueIndex)
                matchingCueOrdinal = matchingCueCount;

            matchingCueCount++;
        }

        if (matchingCueCount <= 1 ||
            matchingCueOrdinal < 0)
        {
            return true;
        }

        return matchingCueOrdinal == hitIndex;
    }

    private static HitReactionKey ResolveTargetReaction(
        TargetImpactPresentation impact,
        SkillVisualDefinition visual,
        int hitIndex)
    {
        if (impact == null ||
            visual == null)
        {
            return HitReactionKey.HeavyHit;
        }

        bool isFinalHit =
            impact.IsFinalHit(hitIndex);

        if (isFinalHit &&
            impact.WasKilled)
        {
            return HitReactionKey.Death;
        }

        if (isFinalHit &&
            impact.BrokePart)
        {
            return HitReactionKey.PartBreak;
        }

        return visual.TargetReaction;
    }

    private static HitReactionKey ResolveTargetReaction(
        BattleVisualRequest request,
        SkillVisualDefinition visual,
        int hitIndex)
    {
        if (request?.PrimaryImpact != null)
        {
            return ResolveTargetReaction(
                request.PrimaryImpact,
                visual,
                hitIndex);
        }

        if (request == null ||
            visual == null)
        {
            return HitReactionKey.HeavyHit;
        }

        int expectedHitCount =
            request.HitDamages != null &&
            request.HitDamages.Count > 0
                ? request.HitDamages.Count
                : Mathf.Max(
                    1,
                    visual.ExpectedHitFrameCount);

        bool isFinalHit =
            hitIndex >= expectedHitCount - 1;

        if (isFinalHit && request.WasKilled)
            return HitReactionKey.Death;

        if (isFinalHit && request.BrokePart)
            return HitReactionKey.PartBreak;

        return visual.TargetReaction;
    }
}
