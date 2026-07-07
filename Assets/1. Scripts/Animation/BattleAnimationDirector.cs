using System.Collections;
using UnityEngine;

public class BattleAnimationDirector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleCameraDirector cameraDirector;
    [SerializeField] private BattleWorldFloatingTextManager floatingTextManager;
    [SerializeField] private DamageNumberManager damageNumberManager;
    [SerializeField] private SkillVisualProfile defaultVisualProfile;

    [Header("Fallback")]
    [SerializeField] private bool logMissingReferences = true;

    private bool isPlaying;

    private void Awake()
    {
        if (cameraDirector == null)
            cameraDirector = FindFirstObjectByType<BattleCameraDirector>();

        if (floatingTextManager == null)
            floatingTextManager = FindFirstObjectByType<BattleWorldFloatingTextManager>();

        if (damageNumberManager == null)
            damageNumberManager = FindFirstObjectByType<DamageNumberManager>();
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

        CharacterViewSet views =
            GetViews(attacker, target);

        if (visual.UsesTargetCamera &&
            cameraDirector != null &&
            target != null)
        {
            cameraDirector.FocusBetween(
                attacker,
                target);
        }

        if (visual.FaceEachOther && target != null)
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

        if (visual.UsesTargetCamera &&
            cameraDirector != null)
        {
            yield return cameraDirector.WaitUntilArrived(
                visual.CameraArriveTimeout);
        }

        if (visual.ShowsClashPower)
        {
            yield return ShowClashPower(
                request,
                views,
                visual);
        }

        yield return new WaitForSeconds(
            visual.BeforeActionDelay);

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

        if (visual.HasHitFrameDamage &&
            visual.ApplyDamageIfNoHitFrame &&
            hitFrameCount <= 0)
        {
            Debug.LogWarning(
                $"[BattleAnimationDirector] {request.ActionType}에 HitFrame이 없어 fallback damage를 적용합니다.");

            ApplyHitFrame(
                request,
                views,
                visual,
                0);
        }

        views.AttackerView?.RefreshVisualState();
        views.TargetView?.RefreshVisualState();

        yield return new WaitForSeconds(
            visual.AfterActionDelay);

        if (visual.ReturnPositionAfterAction &&
            visual.MovesToTarget &&
            views.AttackerMover != null)
        {
            yield return views.AttackerMover.ReturnToDefaultPosition();
        }

        if (visual.ReturnFacingAfterAction)
        {
            yield return ReturnFacing(views);
        }

        if (visual.ReturnCameraAfterAction &&
            cameraDirector != null)
        {
            cameraDirector.Return();
        }
    }

    private void ApplyHitFrame(
        BattleVisualRequest request,
        CharacterViewSet views,
        SkillVisualDefinition visual,
        int hitIndex)
    {
        if (!visual.HasHitFrameDamage)
            return;

        if (views.TargetView == null)
            return;

        views.TargetView.PlayHitRestart();

        int damage =
            request.GetDamageForHitIndex(hitIndex);

        if (damage > 0)
        {
            ShowDamageNumber(
                views.TargetView,
                request.TargetPart,
                damage);
        }

        if (visual.UseHitCameraShake &&
            cameraDirector != null)
        {
            cameraDirector.PlayShake(
                visual.HitShake);
        }
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
            targetView.GetDamageNumberPosition(targetPart);

        damageNumberManager.ShowDamage(
            position,
            damage);
    }

    private SkillVisualDefinition ResolveVisualDefinition(
        BattleAction action)
    {
        if (action == null || action.Skill == null)
            return null;

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
                    views.AttackerFacing.FaceTargetSmooth(target));
        }

        if (views.TargetFacing != null)
        {
            targetRoutine =
                StartCoroutine(
                    views.TargetFacing.FaceTargetSmooth(attacker));
        }

        if (attackerRoutine != null)
            yield return attackerRoutine;

        if (targetRoutine != null)
            yield return targetRoutine;
    }

    private IEnumerator ReturnFacing(CharacterViewSet views)
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
                view.GetBodyPartAnchor(PartType.HEAD);

            if (headAnchor != null && headAnchor != view.transform)
                return headAnchor;

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
                attacker.GetComponent<CharacterView>();

            result.AttackerFacing =
                attacker.GetComponent<CharacterFacingController>();

            result.AttackerMover =
                attacker.GetComponent<CharacterActionMover>();
        }

        if (target != null)
        {
            result.TargetView =
                target.GetComponent<CharacterView>();

            result.TargetFacing =
                target.GetComponent<CharacterFacingController>();
        }

        return result;
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