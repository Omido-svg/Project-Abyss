using System.Collections;
using UnityEngine;

public class BasicBattleAnimationDirector : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float beforeActionDelay = 0.15f;
    [SerializeField] private float afterActionDelay = 0.25f;
    [SerializeField] private bool returnFacingAfterAction = true;

    public IEnumerator PlayAction(BattleAction action)
    {
        if (action == null)
            yield break;

        Character attacker = action.Owner;
        Character target = action.Target;

        if (attacker == null)
            yield break;

        CharacterView attackerView =
            attacker.GetComponent<CharacterView>();

        CharacterView targetView =
            target == null
                ? null
                : target.GetComponent<CharacterView>();

        CharacterFacingController attackerFacing =
            attacker.GetComponent<CharacterFacingController>();

        CharacterFacingController targetFacing =
            target == null
                ? null
                : target.GetComponent<CharacterFacingController>();

        yield return FaceEachOther(
            attacker,
            target,
            attackerFacing,
            targetFacing);

        yield return new WaitForSeconds(beforeActionDelay);

        if (attackerView != null &&
            action.Skill != null)
        {
            yield return attackerView.PlayAction(
                action.Skill.ActionType,
                onHitFrame: null,
                onEffectFrame: null);
        }

        if (targetView != null)
        {
            targetView.PlayHit();
            targetView.RefreshVisualState();
        }

        if (attackerView != null)
            attackerView.RefreshVisualState();

        yield return new WaitForSeconds(afterActionDelay);

        if (returnFacingAfterAction)
        {
            if (attackerFacing != null)
                yield return attackerFacing.ReturnToDefaultSmooth();

            if (targetFacing != null)
                yield return targetFacing.ReturnToDefaultSmooth();
        }
    }

    private IEnumerator FaceEachOther(
        Character attacker,
        Character target,
        CharacterFacingController attackerFacing,
        CharacterFacingController targetFacing)
    {
        if (attacker == null || target == null)
            yield break;

        Coroutine attackerRoutine = null;
        Coroutine targetRoutine = null;

        if (attackerFacing != null)
        {
            attackerRoutine =
                StartCoroutine(
                    attackerFacing.FaceTargetSmooth(target));
        }

        if (targetFacing != null)
        {
            targetRoutine =
                StartCoroutine(
                    targetFacing.FaceTargetSmooth(attacker));
        }

        if (attackerRoutine != null)
            yield return attackerRoutine;

        if (targetRoutine != null)
            yield return targetRoutine;
    }
}