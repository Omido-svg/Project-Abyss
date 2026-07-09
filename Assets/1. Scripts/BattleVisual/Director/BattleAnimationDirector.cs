using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleAnimationDirector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleCameraDirector cameraDirector;
    [SerializeField] private BattleWorldFloatingTextManager floatingTextManager;
    [SerializeField] private DamageNumberManager damageNumberManager;
    [SerializeField] private SkillVisualProfile defaultVisualProfile;
    [SerializeField] private TargetArrowUI targetArrowUI;
    [SerializeField] private MomentumScrollbarUI momentumScrollbarUI;
    [SerializeField] private BattleVfxManager vfxManager;

    [Header("Fallback")]
    [SerializeField] private bool logMissingReferences = true;

    [Header("UI")]
    [SerializeField] private BattleUIManager battleUIManager;
    [SerializeField] private BattleActionAnnounceUI actionAnnounceUI;

    private bool isPlaying;
    private Coroutine cameraShotRoutine;

    private readonly Dictionary<BattleVisualRequest, int> visualDamageAccumulated = new();
    private readonly Dictionary<BattleVisualRequest, int> visualHpStart = new();
    private readonly Dictionary<BattleVisualRequest, List<int>> visualHitDamages = new();

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (cameraDirector == null)
            cameraDirector = FindFirstObjectByType<BattleCameraDirector>();

        if (floatingTextManager == null)
            floatingTextManager = FindFirstObjectByType<BattleWorldFloatingTextManager>();

        if (damageNumberManager == null)
            damageNumberManager = FindFirstObjectByType<DamageNumberManager>();

        if (battleUIManager == null)
            battleUIManager = FindFirstObjectByType<BattleUIManager>();

        if (actionAnnounceUI == null)
            actionAnnounceUI = FindFirstObjectByType<BattleActionAnnounceUI>();

        if (targetArrowUI == null)
            targetArrowUI = FindFirstObjectByType<TargetArrowUI>();
            
        if (momentumScrollbarUI == null)
            momentumScrollbarUI = FindFirstObjectByType<MomentumScrollbarUI>();
            
        if (vfxManager == null)
            vfxManager = FindFirstObjectByType<BattleVfxManager>();
    }

    public IEnumerator Play(BattleVisualRequest request)
    {
        if (request == null)
            yield break;

        if (isPlaying)
        {
            Debug.LogWarning("[BattleAnimationDirector] 이미 전투 연출 중입니다.");
            yield break;
        }

        isPlaying = true;

        yield return PlayInternal(request);

        isPlaying = false;
    }

    public IEnumerator PlayAction(BattleAction action)
    {
        if (action == null)
            yield break;

        SkillVisualDefinition visualDefinition =
            ResolveVisualDefinition(action);

        BattleVisualRequest request =
            BattleVisualRequest.FromAction(
                action,
                visualDefinition);

        yield return Play(request);
    }

    private IEnumerator PlayInternal(BattleVisualRequest request)
    {
        Character attacker =
            request.Attacker;

        Character target =
            request.Target;

        if (attacker == null)
            yield break;

        SkillVisualDefinition visual =
            request.VisualDefinition;

        if (visual == null)
        {
            Debug.LogWarning("[BattleAnimationDirector] VisualDefinition 없음");
            yield break;
        }

        BeginVisualRequest(
            request,
            visual);
        
        PlaySkillVfx(
            request,
            visual,
            BattleVfxTiming.OnActionStart);

        Debug.Log(
            $"[BattleAnimationDirector] PlayInternal 시작 / " +
            $"Attacker={attacker?.Data.CharacterName}, " +
            $"Target={target?.Data.CharacterName}, " +
            $"ActionType={request.ActionType}, " +
            $"HasHitFrameDamage={visual.HasHitFrameDamage}, " +
            $"ApplyDamageIfNoHitFrame={visual.ApplyDamageIfNoHitFrame}, " +
            $"ShowsClashPower={visual.ShowsClashPower}, " +
            $"ClashSteps={request.ClashSteps?.Count ?? 0}, " +
            $"HitDamages={request.HitDamages?.Count ?? 0}");

        CharacterViewSet views =
            GetViews(
                attacker,
                target);

        yield return ShowActionAnnouncement(
            request,
            visual);
            


        StartCameraShots(
            request,
            visual,
            SkillCameraShotTiming.OnActionStart);

        BeginSkillCamera(
            request,
            visual);

        if (visual.FaceEachOther &&
            target != null)
        {
            yield return FaceEachOther(
                views,
                attacker,
                target);
        }

        if (visual.MovesToTarget &&
            views.AttackerMover != null &&
            target != null)
        {
            yield return views.AttackerMover.MoveNearTarget(
                target,
                request.TargetPart);
        }

        yield return WaitSkillCameraArrive(
            visual);

        if (visual.ShowsClashPower)
        {
            yield return ShowClashPower(
                request,
                views,
                visual);
        }
        
        PlaySkillVfx(
            request,
            visual,
            BattleVfxTiming.BeforeAttackAnimation);
        


        StartCameraShots(
            request,
            visual,
            SkillCameraShotTiming.BeforeAttackAnimation);

        if (visual.BeforeActionDelay > 0f)
        {
            yield return new WaitForSeconds(
                visual.BeforeActionDelay);
        }

        int hitFrameCount = 0;

        if (views.AttackerView != null)
        {
            yield return views.AttackerView.PlayAction(
                request.ActionType,
                onHitFrame: () =>
                {
                    int hitIndex =
                        hitFrameCount;

                    hitFrameCount++;

                    ApplyHitFrame(
                        request,
                        views,
                        visual,
                        hitIndex);
                },
                onEffectFrame: null);
        }

        bool shouldFallbackHit =
            ShouldFallbackHitFrame(
                request,
                visual,
                hitFrameCount);

        if (shouldFallbackHit)
        {
            Debug.LogWarning(
                $"[BattleAnimationDirector] {request.ActionType}에 HitFrame이 없어서 fallback 피격/데미지 연출을 적용합니다. " +
                $"Attacker={request.Attacker?.Data.CharacterName}, " +
                $"Target={request.Target?.Data.CharacterName}, " +
                $"Damage={request.GetDamageForHitIndex(0)}");

            ApplyHitFrame(
                request,
                views,
                visual,
                0);
        }

        views.AttackerView?.RefreshVisualState();
        views.TargetView?.RefreshVisualState();
        
        PlaySkillVfx(
            request,
            visual,
            BattleVfxTiming.AfterAction);

        if (visual.AfterActionDelay > 0f)
        {
            yield return new WaitForSeconds(
                visual.AfterActionDelay);
        }

        if (visual.ReturnPositionAfterAction &&
            visual.MovesToTarget &&
            views.AttackerMover != null)
        {
            yield return views.AttackerMover.ReturnToDefaultPosition();
        }

        if (visual.ReturnFacingAfterAction)
        {
            yield return ReturnFacing(
                views);
        }

        StartCameraShots(
            request,
            visual,
            SkillCameraShotTiming.AfterAction);

        EndSkillCamera(
            visual);

        if (visual.AfterReturnDelay > 0f)
        {
            yield return new WaitForSeconds(
                visual.AfterReturnDelay);
        }

        yield return HideActionAnnouncement(
            visual);

        yield return PlayMomentumRefreshAtVisualEnd();

        EndVisualRequest(request);
    }
    
    private void PlaySkillVfx(
        BattleVisualRequest request,
        SkillVisualDefinition visual,
        BattleVfxTiming timing,
        int hitIndex = -1,
        int damage = 0)
    {
        if (request == null || visual == null)
            return;

        if (vfxManager == null)
            return;

        if (visual.VfxCues == null)
            return;

        BattleVfxContext context =
            new BattleVfxContext
            {
                Attacker = request.Attacker,
                Target = request.Target,
                TargetPart = request.TargetPart,
                HitIndex = hitIndex,
                Damage = damage
            };

        foreach (BattleVfxCue cue in visual.VfxCues)
        {
            if (cue == null)
                continue;

            if (cue.Timing != timing)
                continue;

            vfxManager.PlayCue(
                cue,
                context);
        }
    }
    
    private IEnumerator PlayMomentumRefreshAtVisualEnd()
    {
        if (momentumScrollbarUI == null)
            yield break;

        yield return momentumScrollbarUI.ReleaseAndAnimateToRealMomentumRoutine();
    }

    private void BeginVisualRequest(
        BattleVisualRequest request,
        SkillVisualDefinition visual)
    {
        PrepareVisualHitDamages(
            request,
            visual);

        if (targetArrowUI != null)
            targetArrowUI.SetCurrentVisualRequest(request);

        if (momentumScrollbarUI != null)
            momentumScrollbarUI.LockCurrentDisplay();

        PrepareVisualHpOverride(request);
    }
    
    private void PrepareVisualHitDamages(
        BattleVisualRequest request,
        SkillVisualDefinition visual)
    {
        if (request == null)
            return;

        visualHitDamages.Remove(request);

        if (request.HitDamages == null ||
            request.HitDamages.Count <= 0)
        {
            return;
        }

        int totalDamage = 0;

        foreach (int damage in request.HitDamages)
        {
            totalDamage += Mathf.Max(
                0,
                damage);
        }

        if (totalDamage <= 0)
            return;

        // 이미 분할되어 들어온 경우는 그대로 사용
        if (request.HitDamages.Count > 1)
        {
            visualHitDamages[request] =
                new List<int>(request.HitDamages);

            return;
        }

        // 현재 문제 상황: [4] 하나만 들어온 경우
        List<int> weights =
            visual != null
                ? visual.HitDamageWeights
                : null;

        if (weights == null ||
            weights.Count <= 0)
        {
            visualHitDamages[request] =
                new List<int> { totalDamage };

            return;
        }

        visualHitDamages[request] =
            SplitDamageByWeights(
                totalDamage,
                weights);

        Debug.Log(
            "[BattleAnimationDirector] HitDamage 분할 / " +
            "Total=" + totalDamage + ", " +
            "Result=" + string.Join(",", visualHitDamages[request]));
    }
    
    private List<int> SplitDamageByWeights(
        int totalDamage,
        List<int> weights)
    {
        List<int> result =
            new List<int>();

        if (totalDamage <= 0)
            return result;

        if (weights == null ||
            weights.Count <= 0)
        {
            result.Add(totalDamage);
            return result;
        }

        int weightSum = 0;

        foreach (int weight in weights)
        {
            weightSum += Mathf.Max(
                0,
                weight);
        }

        if (weightSum <= 0)
        {
            result.Add(totalDamage);
            return result;
        }

        int remainingDamage =
            totalDamage;

        for (int i = 0; i < weights.Count; i++)
        {
            int weight =
                Mathf.Max(
                    0,
                    weights[i]);

            int splitDamage;

            if (i == weights.Count - 1)
            {
                splitDamage = remainingDamage;
            }
            else
            {
                splitDamage =
                    Mathf.FloorToInt(
                        totalDamage * (float)weight / weightSum);

                splitDamage =
                    Mathf.Clamp(
                        splitDamage,
                        0,
                        remainingDamage);
            }

            result.Add(splitDamage);

            remainingDamage -= splitDamage;
        }

        return result;
    }
    
    private int GetVisualDamageForHitIndex(
        BattleVisualRequest request,
        int hitIndex)
    {
        if (request == null)
            return 0;

        if (visualHitDamages.TryGetValue(
                request,
                out List<int> damages))
        {
            if (damages == null ||
                damages.Count <= 0)
            {
                return 0;
            }

            if (hitIndex < 0)
                return damages[0];

            if (hitIndex >= damages.Count)
                return 0;

            return damages[hitIndex];
        }

        return request.GetDamageForHitIndex(
            hitIndex);
    }

    private void EndVisualRequest(
        BattleVisualRequest request)
    {
        if (cameraShotRoutine != null)
        {
            StopCoroutine(cameraShotRoutine);
            cameraShotRoutine = null;
        }

        ClearVisualHpOverride(request);

        if (targetArrowUI != null)
            targetArrowUI.ClearCurrentVisualRequest(request);
    }

    private void BeginSkillCamera(
        BattleVisualRequest request,
        SkillVisualDefinition visual)
    {
        if (request == null ||
            visual == null)
            return;

        if (!visual.UsesTargetCamera)
            return;

        if (visual.CameraDefinition != null)
            return;

        if (cameraDirector == null)
            return;

        if (request.Attacker == null ||
            request.Target == null)
            return;

        cameraDirector.FocusBetween(
            request.Attacker,
            request.Target);
    }

    private void StartCameraShots(
        BattleVisualRequest request,
        SkillVisualDefinition visual,
        SkillCameraShotTiming timing)
    {
        if (request == null || visual == null)
            return;

        if (cameraDirector == null)
            return;

        if (visual.CameraDefinition == null)
            return;

        if (cameraShotRoutine != null)
        {
            StopCoroutine(cameraShotRoutine);
            cameraShotRoutine = null;
        }

        cameraShotRoutine =
            StartCoroutine(
                PlayCameraShots(
                    request,
                    visual,
                    timing));
    }

    private IEnumerator PlayCameraShots(
        BattleVisualRequest request,
        SkillVisualDefinition visual,
        SkillCameraShotTiming timing)
    {
        if (request == null || visual == null)
            yield break;

        if (cameraDirector == null)
            yield break;

        if (visual.CameraDefinition == null)
            yield break;

        yield return cameraDirector.PlayShotsByTiming(
            request,
            visual.CameraDefinition,
            timing);

        cameraShotRoutine = null;
    }

    private IEnumerator WaitSkillCameraArrive(
        SkillVisualDefinition visual)
    {
        if (visual == null)
            yield break;

        if (!visual.UsesTargetCamera)
            yield break;

        if (visual.CameraDefinition != null)
            yield break;

        if (cameraDirector == null)
            yield break;

        yield return cameraDirector.WaitUntilArrived(
            visual.CameraArriveTimeout);
    }

    private void EndSkillCamera(
        SkillVisualDefinition visual)
    {
        if (visual == null)
            return;

        if (cameraDirector == null)
            return;

        if (visual.CameraDefinition != null)
        {
            if (visual.CameraDefinition.ReturnToOverviewAfterAction)
                cameraDirector.Return();

            return;
        }

        if (!visual.ReturnCameraAfterAction)
            return;

        cameraDirector.Return();
    }

    private void PlayHitCameraShake(
        SkillVisualDefinition visual)
    {
        if (visual == null)
            return;

        if (!visual.UseHitCameraShake)
            return;

        if (cameraDirector == null)
            return;

        cameraDirector.PlayShake(
            visual.HitShake);
    }

    private bool ShouldFallbackHitFrame(
        BattleVisualRequest request,
        SkillVisualDefinition visual,
        int hitFrameCount)
    {
        if (request == null ||
            visual == null)
        {
            return false;
        }

        if (!visual.HasHitFrameDamage)
            return false;

        if (hitFrameCount > 0)
            return false;

        bool hasDamageToShow =
            request.HitDamages != null &&
            request.HitDamages.Count > 0 &&
            request.GetDamageForHitIndex(0) > 0;

        return
            visual.ApplyDamageIfNoHitFrame ||
            hasDamageToShow;
    }

    private void PrepareVisualHpOverride(
        BattleVisualRequest request)
    {
        if (request == null)
            return;

        if (request.Target == null ||
            request.TargetPart == null)
            return;

        if (!visualHitDamages.TryGetValue(
                request,
                out List<int> damages))
        {
            return;
        }

        if (damages == null ||
            damages.Count <= 0)
        {
            return;
        }

        int totalDamage = 0;

        foreach (int damage in damages)
        {
            totalDamage += Mathf.Max(
                0,
                damage);
        }

        if (totalDamage <= 0)
            return;

        int realFinalHp =
            Mathf.RoundToInt(
                request.TargetPart.PartHP);

        int maxHp =
            Mathf.RoundToInt(
                request.TargetPart.MaxPartHP);

        int visualStartHp =
            Mathf.Clamp(
                realFinalHp + totalDamage,
                0,
                maxHp);

        visualHpStart[request] =
            visualStartHp;

        visualDamageAccumulated[request] =
            0;

        if (battleUIManager != null)
        {
            battleUIManager.SetBodyPartHpOverride(
                request.Target,
                request.TargetPart,
                visualStartHp);
        }

        RefreshBattleUIAtHitFrame(
            request,
            -1,
            0);
    }

    private void ApplyVisualHpDamage(
        BattleVisualRequest request,
        int damage)
    {
        if (request == null)
            return;

        if (request.Target == null ||
            request.TargetPart == null)
            return;

        if (damage <= 0)
            return;

        if (!visualHpStart.TryGetValue(
                request,
                out int startHp))
        {
            return;
        }

        if (!visualDamageAccumulated.TryGetValue(
                request,
                out int accumulatedDamage))
        {
            accumulatedDamage = 0;
        }

        accumulatedDamage += damage;

        visualDamageAccumulated[request] =
            accumulatedDamage;

        int realFinalHp =
            Mathf.RoundToInt(
                request.TargetPart.PartHP);

        int displayHp =
            Mathf.Max(
                realFinalHp,
                startHp - accumulatedDamage);

        if (battleUIManager != null)
        {
            battleUIManager.SetBodyPartHpOverride(
                request.Target,
                request.TargetPart,
                displayHp);
        }
    }

    private void ClearVisualHpOverride(
        BattleVisualRequest request)
    {
        if (request == null)
            return;

        if (battleUIManager != null &&
            request.Target != null &&
            request.TargetPart != null)
        {
            battleUIManager.ClearBodyPartHpOverride(
                request.Target,
                request.TargetPart);
        }

        visualHpStart.Remove(request);
        visualDamageAccumulated.Remove(request);
        visualHitDamages.Remove(request);

        RefreshBattleUIAtHitFrame(
            request,
            -1,
            0);
    }

    private IEnumerator ShowActionAnnouncement(
        BattleVisualRequest request,
        SkillVisualDefinition visual)
    {
        if (request == null)
            yield break;

        if (visual == null)
            yield break;

        if (!visual.ShowsActionAnnouncement)
            yield break;

        if (actionAnnounceUI == null)
            yield break;

        yield return actionAnnounceUI.ShowPersistent(
            request);
    }

    private IEnumerator HideActionAnnouncement(
        SkillVisualDefinition visual)
    {
        if (visual == null)
            yield break;

        if (!visual.ShowsActionAnnouncement)
            yield break;

        if (actionAnnounceUI == null)
            yield break;

        yield return actionAnnounceUI.Hide();
    }

    private void ApplyHitFrame(
        BattleVisualRequest request,
        CharacterViewSet views,
        SkillVisualDefinition visual,
        int hitIndex)
    {
        if (request == null ||
            visual == null)
        {
            return;
        }

        if (!visual.HasHitFrameDamage)
            return;

        CharacterView targetView =
            views.TargetView;

        if (targetView == null &&
            request.Target != null)
        {
            targetView =
                request.Target.GetComponent<CharacterView>();
        }

        if (targetView == null)
        {
            Debug.LogWarning(
                $"[BattleAnimationDirector] TargetView 없음. 피격 연출 불가 / Target={request.Target?.name}");
            return;
        }

        int damage =
            GetVisualDamageForHitIndex(
                request,
                hitIndex);
                
        Debug.Log(
            $"[BattleAnimationDirector] HitFrame 적용 : " +
            $"{request.Attacker?.Data.CharacterName} -> {request.Target?.Data.CharacterName} / " +
            $"HitIndex={hitIndex} / Damage={damage}");
            
        PlaySkillVfx(
            request,
            visual,
            BattleVfxTiming.OnHitFrame,
            hitIndex,
            damage);

        targetView.PlayHitRestart();

        if (damage > 0)
        {
            ShowDamageNumber(
                targetView,
                request.TargetPart,
                damage);
        }

        StartCameraShots(
            request,
            visual,
            SkillCameraShotTiming.OnHitFrame);

        ApplyVisualHpDamage(
            request,
            damage);

        RefreshBattleUIAtHitFrame(
            request,
            hitIndex,
            damage);

        PlayHitCameraShake(
            visual);
    }

    private void RefreshBattleUIAtHitFrame(
        BattleVisualRequest request,
        int hitIndex,
        int damage)
    {
        if (battleUIManager == null)
            battleUIManager = FindFirstObjectByType<BattleUIManager>();

        if (battleUIManager == null)
        {
            Debug.LogWarning(
                "[BattleAnimationDirector] HitFrame UI 갱신 실패 : BattleUIManager 없음");
            return;
        }

        battleUIManager.RefreshAllBodyPartButtons();

        Canvas.ForceUpdateCanvases();

        if (hitIndex < 0)
            return;

        Debug.Log(
            $"[BattleAnimationDirector] HitFrame UI 갱신 / " +
            $"Target={request.Target?.Data.CharacterName}, " +
            $"Part={request.TargetPart?.Type}, " +
            $"HitIndex={hitIndex}, " +
            $"Damage={damage}");
    }

    private IEnumerator ShowClashPower(
        BattleVisualRequest request,
        CharacterViewSet views,
        SkillVisualDefinition visual)
    {
        if (floatingTextManager == null)
        {
            if (logMissingReferences)
                Debug.LogWarning("[BattleAnimationDirector] FloatingTextManager 없음");

            yield break;
        }

        if (request.ClashSteps == null ||
            request.ClashSteps.Count == 0)
        {
            yield break;
        }

        Transform attackerAnchor =
            GetClashTextAnchor(
                request.Attacker,
                views.AttackerView);

        Transform targetAnchor =
            GetClashTextAnchor(
                request.Target,
                views.TargetView);

        yield return floatingTextManager.ShowClashPowerSequence(
            attackerAnchor,
            targetAnchor,
            request.ClashSteps,
            visual.ClashColor,
            visual.TieColor);
    }

    private void ShowDamageNumber(
        CharacterView targetView,
        BodyPart targetPart,
        int damage)
    {
        if (damageNumberManager == null)
        {
            if (logMissingReferences)
                Debug.LogWarning("[BattleAnimationDirector] DamageNumberManager 없음");

            return;
        }

        if (targetView == null)
            return;

        Vector3 position =
            targetView.GetDamageNumberPosition(
                targetPart);

        damageNumberManager.ShowDamage(
            position,
            damage);
    }

    private SkillVisualDefinition ResolveVisualDefinition(
        BattleAction action)
    {
        if (action == null ||
            action.Skill == null)
        {
            return null;
        }

        if (action.Skill is IVisualSkill visualSkill &&
            visualSkill.VisualDefinition != null)
        {
            return visualSkill.VisualDefinition;
        }

        if (defaultVisualProfile == null)
            return null;

        return defaultVisualProfile.GetDefault(
            action.Skill.ActionType);
    }

    private IEnumerator FaceEachOther(
        CharacterViewSet views,
        Character attacker,
        Character target)
    {
        Coroutine attackerRoutine = null;
        Coroutine targetRoutine = null;

        if (views.AttackerFacing != null)
        {
            attackerRoutine =
                StartCoroutine(
                    views.AttackerFacing.FaceTargetSmooth(
                        target));
        }

        if (views.TargetFacing != null)
        {
            targetRoutine =
                StartCoroutine(
                    views.TargetFacing.FaceTargetSmooth(
                        attacker));
        }

        if (attackerRoutine != null)
            yield return attackerRoutine;

        if (targetRoutine != null)
            yield return targetRoutine;
    }

    private IEnumerator ReturnFacing(
        CharacterViewSet views)
    {
        Coroutine attackerRoutine = null;
        Coroutine targetRoutine = null;

        if (views.AttackerFacing != null)
        {
            attackerRoutine =
                StartCoroutine(
                    views.AttackerFacing.ReturnToDefaultSmooth());
        }

        if (views.TargetFacing != null)
        {
            targetRoutine =
                StartCoroutine(
                    views.TargetFacing.ReturnToDefaultSmooth());
        }

        if (attackerRoutine != null)
            yield return attackerRoutine;

        if (targetRoutine != null)
            yield return targetRoutine;
    }

    private Transform GetClashTextAnchor(
        Character character,
        CharacterView view)
    {
        if (view != null)
        {
            Transform headAnchor =
                view.GetBodyPartAnchor(
                    PartType.HEAD);

            if (headAnchor != null &&
                headAnchor != view.transform)
            {
                return headAnchor;
            }

            if (view.LookAtPoint != null)
                return view.LookAtPoint;
        }

        if (character != null)
            return character.transform;

        return null;
    }

    private CharacterViewSet GetViews(
        Character attacker,
        Character target)
    {
        CharacterViewSet result =
            new CharacterViewSet();

        if (attacker != null)
        {
            result.AttackerView =
                GetCharacterView(attacker);

            result.AttackerFacing =
                attacker.GetComponent<CharacterFacingController>();

            result.AttackerMover =
                attacker.GetComponent<CharacterActionMover>();
        }

        if (target != null)
        {
            result.TargetView =
                GetCharacterView(target);

            result.TargetFacing =
                target.GetComponent<CharacterFacingController>();
        }

        return result;
    }
    
    private CharacterView GetCharacterView(Character character)
    {
        if (character == null)
            return null;

        CharacterView view =
            character.GetComponent<CharacterView>();

        if (view != null)
            return view;

        return character.GetComponentInChildren<CharacterView>(true);
    }

    private struct CharacterViewSet
    {
        public CharacterView AttackerView;
        public CharacterView TargetView;

        public CharacterFacingController AttackerFacing;
        public CharacterFacingController TargetFacing;

        public CharacterActionMover AttackerMover;
    }
}