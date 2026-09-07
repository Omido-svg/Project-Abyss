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
        BeginStaggerOverride(playback);
    }

    public void PrepareSequence(
        BattleVisualPlaybackState playback)
    {
        BattleVisualRequest root =
            playback?.RootRequest;

        if (root?.HasClashSequence != true)
            return;

        ResolveBattleUiManager();
        ResolveWorldPlateManager();

        List<BattleVisualHpOverrideTarget> initializedTargets =
            new List<BattleVisualHpOverrideTarget>();

        HashSet<Character> initializedWorldCharacters =
            new HashSet<Character>();

        HashSet<Character> initializedStaggerCharacters =
            new HashSet<Character>();

        foreach (BattleClashVisualExchange exchange
                 in root.ClashExchanges)
        {
            BattleVisualRequest request =
                exchange?.AttackRequest;

            if (request?.Target == null)
                continue;

            int totalDamage =
                DamageDistributionUtility.Sum(
                    request.HitDamages);

            if (totalDamage > 0)
            {
                ResolveHpRange(
                    request,
                    totalDamage,
                    out int visualStartHp,
                    out _);

                if (!ContainsOverrideTarget(
                        initializedTargets,
                        request.Target,
                        request.TargetPart))
                {
                    BattleVisualHpOverrideTarget target =
                        new BattleVisualHpOverrideTarget(
                            request.Target,
                            request.TargetPart);

                    initializedTargets.Add(target);
                    playback.TrackHpOverride(
                        request.Target,
                        request.TargetPart);

                    battleUIManager?.SetTargetHpOverride(
                        request.Target,
                        request.TargetPart,
                        visualStartHp);
                }

                if (initializedWorldCharacters.Add(
                        request.Target))
                {
                    worldPlateManager?.SetVisualHpOverride(
                        request.Target,
                        ResolveWorldVisualStartHp(
                            request,
                            totalDamage),
                        forceImmediate: true);
                }
            }

            // 흐트러짐 로직도 HP와 마찬가지로 이미 최종값까지 계산된 뒤
            // Timeline이 재생된다. 첫 Hit Event 전에는 첫 교환의 before 값을
            // 화면에 고정하여 실제 타격보다 게이지가 먼저 줄어들지 않게 한다.
            if (request.StaggerDamage > 0 &&
                initializedStaggerCharacters.Add(
                    request.Target))
            {
                playback.TrackStaggerOverride(
                    request.Target);

                worldPlateManager?.SetVisualStaggerOverride(
                    request.Target,
                    request.StaggerGaugeBefore,
                    vulnerable:
                        request.StaggerGaugeBefore <= 0);
            }
        }

        // 합 결과는 로직 단계에서 이미 계산되지만,
        // 화면은 첫 Hit Event 전까지 각 대상의 합 시작 HP를 유지한다.
        // 이 잠금을 합 안내 UI와 진입 모션보다 먼저 끝내
        // 실제 HP가 잠깐 노출됐다가 되돌아오는 현상을 막는다.
        Canvas.ForceUpdateCanvases();
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
        // Hit Event가 화면 상태 변경의 유일한 presentation commit point다.
        // HP / 흐트러짐 / 각종 HUD refresh를 이 한 호출에서 함께 처리한다.
        ApplyHpDamage(playback, damage);

        if (hitIndex == 0)
            ApplyStaggerDamage(playback);

        RefreshBattleUi(
            playback?.Request,
            hitIndex,
            damage);
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

        ResolveWorldPlateManager();

        foreach (Character character
                 in playback.StaggerOverrideTargets)
        {
            if (character == null)
                continue;

            worldPlateManager?.ClearVisualStaggerOverride(
                character);

            worldPlateManager?.RefreshCharacter(
                character);
        }

        playback.StaggerOverrideTargets.Clear();
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
            ResolveWorldVisualStartHp(request, totalDamage),
            forceImmediate: true);

        RefreshBattleUi(request, -1, 0);
    }

    private void BeginStaggerOverride(
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

        playback.TrackStaggerOverride(
            request.Target);

        worldPlateManager?.SetVisualStaggerOverride(
            request.Target,
            request.StaggerGaugeBefore,
            vulnerable:
                request.StaggerGaugeBefore <= 0);
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

        worldPlateManager?.SetVisualHpOverrideAtHit(
            request.Target,
            worldDisplayHp);
    }

    private void ApplyStaggerDamage(
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

        playback.TrackStaggerOverride(
            request.Target);

        worldPlateManager?.SetVisualStaggerOverrideAtHit(
            request.Target,
            request.StaggerGaugeAfter,
            vulnerable:
                request.StaggerGaugeAfter <= 0);
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
        if (request?.Target == null)
            return;

        ResolveBattleUiManager();

        battleUIManager?.RefreshTargetUI(
            request.Target,
            request.TargetPart);

        ResolveWorldPlateManager();
        worldPlateManager?.RefreshCharacter(
            request.Target);

        // 같은 Hit Event 안에서 HP 숫자/HP bar/흐트러짐 bar의
        // RectTransform과 TMP 상태를 즉시 확정한다.
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
    
    private static bool ContainsOverrideTarget(
        List<BattleVisualHpOverrideTarget> targets,
        Character character,
        BodyPart part)
    {
        if (targets == null ||
            character == null)
        {
            return false;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            BattleVisualHpOverrideTarget target =
                targets[i];

            if (ReferenceEquals(
                    target.Character,
                    character) &&
                ReferenceEquals(
                    target.Part,
                    part))
            {
                return true;
            }
        }

        return false;
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