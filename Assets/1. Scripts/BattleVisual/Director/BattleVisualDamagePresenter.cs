using System.Collections.Generic;
using UnityEngine;

internal sealed class BattleVisualDamagePresenter
{
    private BattleUIManager battleUIManager;
    private readonly bool logDebug;

    public BattleVisualDamagePresenter(
        BattleUIManager battleUIManager,
        bool logDebug)
    {
        this.battleUIManager =
            battleUIManager;

        this.logDebug =
            logDebug;
    }

    public void Prepare(
        BattleVisualPlaybackState playback,
        SkillVisualDefinition visual)
    {
        PrepareHitDamages(
            playback,
            visual);

        BeginHpOverride(
            playback);
    }

    public int GetDamageForHitIndex(
        BattleVisualPlaybackState playback,
        int hitIndex)
    {
        if (playback?.Request == null)
            return 0;

        List<int> damages =
            playback.HitDamages;

        if (damages.Count == 0)
        {
            return playback.Request
                .GetDamageForHitIndex(
                    hitIndex);
        }

        if (hitIndex < 0)
            return damages[0];

        return hitIndex < damages.Count
            ? damages[hitIndex]
            : 0;
    }

    public void ApplyHit(
        BattleVisualPlaybackState playback,
        int hitIndex,
        int damage)
    {
        ApplyHpDamage(
            playback,
            damage);

        RefreshBattleUi(
            playback?.Request,
            hitIndex,
            damage);
    }

    public void Clear(
        BattleVisualPlaybackState playback)
    {
        if (playback == null)
            return;

        BattleVisualRequest request =
            playback.Request;

        bool hadVisualHpOverride =
            playback.HasVisualHpOverride;

        ResolveBattleUiManager();

        if (hadVisualHpOverride &&
            battleUIManager != null &&
            request?.Target != null)
        {
            battleUIManager.ClearTargetHpOverride(
                request.Target,
                request.TargetPart);
        }

        playback.HasVisualHpOverride = false;
        playback.VisualHpStart = 0;
        playback.VisualHpFinal = 0;
        playback.VisualDamageAccumulated = 0;
        playback.HitDamages.Clear();

        if (hadVisualHpOverride)
        {
            RefreshBattleUi(
                request,
                -1,
                0);
        }
    }

    private void PrepareHitDamages(
        BattleVisualPlaybackState playback,
        SkillVisualDefinition visual)
    {
        if (playback == null)
            return;

        BattleVisualRequest request =
            playback.Request;

        playback.HitDamages.Clear();

        if (request?.HitDamages == null ||
            request.HitDamages.Count == 0)
        {
            return;
        }

        int totalDamage = 0;

        foreach (int damage
                 in request.HitDamages)
        {
            totalDamage +=
                Mathf.Max(0, damage);
        }

        if (totalDamage <= 0)
            return;

        if (request.HitDamages.Count > 1)
        {
            playback.HitDamages.AddRange(
                request.HitDamages);

            return;
        }

        IReadOnlyList<int> weights =
            visual != null
                ? visual.HitDamageWeights
                : null;

        playback.HitDamages.AddRange(
            DamageDistributionUtility
                .DistributeByWeights(
                    totalDamage,
                    weights));

        if (logDebug)
        {
            Debug.Log(
                "[BattleVisualDamagePresenter] HitDamage 분할 / " +
                $"Total={totalDamage}, " +
                $"Result={string.Join(",", playback.HitDamages)}");
        }
    }

    private void BeginHpOverride(
        BattleVisualPlaybackState playback)
    {
        if (playback == null)
            return;

        BattleVisualRequest request =
            playback.Request;

        if (request?.Target == null ||
            playback.HitDamages.Count == 0)
        {
            return;
        }

        int totalDamage = 0;

        foreach (int damage
                 in playback.HitDamages)
        {
            totalDamage +=
                Mathf.Max(0, damage);
        }

        if (totalDamage <= 0)
            return;

        ResolveHpRange(
            request,
            totalDamage,
            out int visualStartHp,
            out int visualFinalHp);

        playback.VisualHpStart =
            visualStartHp;

        playback.VisualHpFinal =
            visualFinalHp;

        playback.VisualDamageAccumulated =
            0;

        playback.HasVisualHpOverride =
            true;

        ResolveBattleUiManager();

        battleUIManager?.SetTargetHpOverride(
            request.Target,
            request.TargetPart,
            visualStartHp);

        RefreshBattleUi(
            request,
            -1,
            0);
    }

    private static void ResolveHpRange(
        BattleVisualRequest request,
        int totalDamage,
        out int visualStartHp,
        out int visualFinalHp)
    {
        if (request.TargetPart != null)
        {
            int maxHp =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        request.TargetPart.MaxPartHP));

            if (request.HasTargetPartHpSnapshot)
            {
                visualStartHp =
                    Mathf.Clamp(
                        request.TargetPartHpBefore,
                        0,
                        maxHp);

                visualFinalHp =
                    Mathf.Clamp(
                        request.TargetPartHpAfter,
                        0,
                        maxHp);

                return;
            }

            visualFinalHp =
                Mathf.Clamp(
                    Mathf.RoundToInt(
                        request.TargetPart.PartHP),
                    0,
                    maxHp);

            visualStartHp =
                Mathf.Clamp(
                    visualFinalHp +
                    totalDamage,
                    0,
                    maxHp);

            return;
        }

        int characterMaxHp =
            Mathf.Max(
                1,
                request.TargetCharacterMaxHp > 0
                    ? request.TargetCharacterMaxHp
                    : request.Target.MaxCombatHP);

        if (request.HasTargetCharacterHpSnapshot)
        {
            visualStartHp =
                Mathf.Clamp(
                    request.TargetCharacterHpBefore,
                    0,
                    characterMaxHp);

            visualFinalHp =
                Mathf.Clamp(
                    request.TargetCharacterHpAfter,
                    0,
                    characterMaxHp);

            return;
        }

        visualFinalHp =
            Mathf.Clamp(
                request.Target.CurrentHP,
                0,
                characterMaxHp);

        visualStartHp =
            Mathf.Clamp(
                visualFinalHp +
                totalDamage,
                0,
                characterMaxHp);
    }

    private void ApplyHpDamage(
        BattleVisualPlaybackState playback,
        int damage)
    {
        if (playback == null ||
            !playback.HasVisualHpOverride ||
            damage <= 0)
        {
            return;
        }

        BattleVisualRequest request =
            playback.Request;

        if (request?.Target == null)
            return;

        playback.VisualDamageAccumulated +=
            damage;

        int displayHp =
            Mathf.Max(
                playback.VisualHpFinal,
                playback.VisualHpStart -
                playback.VisualDamageAccumulated);

        ResolveBattleUiManager();

        battleUIManager?.SetTargetHpOverride(
            request.Target,
            request.TargetPart,
            displayHp);
    }

    private void RefreshBattleUi(
        BattleVisualRequest request,
        int hitIndex,
        int damage)
    {
        ResolveBattleUiManager();

        if (battleUIManager == null)
        {
            Debug.LogWarning(
                "[BattleVisualDamagePresenter] UI 갱신 실패: BattleUIManager 없음");

            return;
        }

        battleUIManager.RefreshAllBodyPartButtons();
        Canvas.ForceUpdateCanvases();

        if (!logDebug ||
            hitIndex < 0 ||
            request == null)
        {
            return;
        }

        string targetPoint =
            request.TargetPart == null
                ? "SINGLE_HP"
                : request.TargetPart.Type.ToString();

        Debug.Log(
            "[BattleVisualDamagePresenter] HitFrame UI 갱신 / " +
            $"Target={request.Target?.Data?.CharacterName}, " +
            $"Point={targetPoint}, " +
            $"HitIndex={hitIndex}, " +
            $"Damage={damage}");
    }

    private void ResolveBattleUiManager()
    {
        if (battleUIManager == null)
        {
            battleUIManager =
                Object.FindFirstObjectByType<
                    BattleUIManager>();
        }
    }
}
