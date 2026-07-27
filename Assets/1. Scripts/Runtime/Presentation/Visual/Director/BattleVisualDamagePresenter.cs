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
        BeginHpOverride(playback);
    }

    public int GetDamageForHitIndex(
        BattleVisualPlaybackState playback,
        int hitIndex)
    {
        if (playback?.Request == null)
            return 0;

        List<int> damages = playback.HitDamages;

        if (damages.Count == 0)
            return playback.Request.GetDamageForHitIndex(hitIndex);

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
        ApplyHpDamage(playback, damage);
        RefreshBattleUi(playback?.Request, hitIndex, damage);
    }

    public void Clear(BattleVisualPlaybackState playback)
    {
        if (playback == null)
            return;

        ResolveBattleUiManager();

        if (battleUIManager != null)
        {
            foreach (BattleVisualHpOverrideTarget target
                     in playback.HpOverrideTargets)
            {
                if (target.Character == null)
                    continue;

                battleUIManager.ClearTargetHpOverride(
                    target.Character,
                    target.Part);

                battleUIManager.RefreshTargetUI(
                    target.Character,
                    target.Part);

                ResolveWorldPlateManager();
                worldPlateManager?.ClearVisualHpOverride(target.Character);
                worldPlateManager?.RefreshCharacter(target.Character);
            }
        }
        else
        {
            ResolveWorldPlateManager();

            foreach (BattleVisualHpOverrideTarget target
                     in playback.HpOverrideTargets)
            {
                if (target.Character != null)
                    worldPlateManager?.ClearVisualHpOverride(target.Character);
            }
        }

        playback.HpOverrideTargets.Clear();
        ResetCurrentDamageState(playback);

        Canvas.ForceUpdateCanvases();
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

    private void BeginHpOverride(BattleVisualPlaybackState playback)
    {
        if (playback == null)
            return;

        BattleVisualRequest request = playback.Request;
        int totalDamage = DamageDistributionUtility.Sum(playback.HitDamages);

        if (request?.Target == null || totalDamage <= 0)
            return;

        ResolveHpRange(
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
            ResolveWorldVisualStartHp(request, totalDamage));

        RefreshBattleUi(request, -1, 0);
    }

    private static void ResolveHpRange(
        BattleVisualRequest request,
        int totalDamage,
        out int visualStartHp,
        out int visualFinalHp)
    {
        if (request.TargetPart != null)
        {
            int maxHp = Mathf.Max(1, Mathf.RoundToInt(request.TargetPart.MaxPartHP));

            if (request.HasTargetPartHpSnapshot)
            {
                visualStartHp = Mathf.Clamp(request.TargetPartHpBefore, 0, maxHp);
                visualFinalHp = Mathf.Clamp(request.TargetPartHpAfter, 0, maxHp);
                return;
            }

            visualFinalHp = Mathf.Clamp(
                Mathf.RoundToInt(request.TargetPart.PartHP),
                0,
                maxHp);

            visualStartHp = Mathf.Clamp(visualFinalHp + totalDamage, 0, maxHp);
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
            ResolveWorldVisualStartHp(
                request,
                DamageDistributionUtility.Sum(playback.HitDamages));

        int worldFinalHp = ResolveWorldVisualFinalHp(request);

        int worldDisplayHp = Mathf.Max(
            worldFinalHp,
            worldStartHp - playback.VisualDamageAccumulated);

        worldPlateManager?.SetVisualHpOverride(
            request.Target,
            worldDisplayHp);
    }

    private static int ResolveWorldVisualStartHp(
        BattleVisualRequest request,
        int totalDamage)
    {
        if (request?.Target == null)
            return 0;

        int maximum = ResolveWorldVisualMaxHp(request);

        if (request.HasTargetCharacterHpSnapshot)
        {
            return Mathf.Clamp(
                request.TargetCharacterHpBefore,
                0,
                maximum);
        }

        return Mathf.Clamp(
            ResolveWorldVisualFinalHp(request) + Mathf.Max(0, totalDamage),
            0,
            maximum);
    }

    private static int ResolveWorldVisualFinalHp(
        BattleVisualRequest request)
    {
        if (request?.Target == null)
            return 0;

        int maximum = ResolveWorldVisualMaxHp(request);

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

    private static int ResolveWorldVisualMaxHp(
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
        ResolveBattleUiManager();

        if (battleUIManager == null || request?.Target == null)
            return;

        battleUIManager.RefreshTargetUI(
            request.Target,
            request.TargetPart);

        ResolveWorldPlateManager();
        worldPlateManager?.RefreshCharacter(request.Target);

        Canvas.ForceUpdateCanvases();

        if (!logDebug || hitIndex < 0)
            return;

        Debug.Log(
            $"[BattleVisualDamagePresenter] HitFrame UI / " +
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
            battleUIManager = Object.FindFirstObjectByType<BattleUIManager>();
    }
}