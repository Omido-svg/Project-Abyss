using System.Collections;
using UnityEngine;

public class BattleAnimationTestDriver : MonoBehaviour
{
    [Header("Characters")]
    [SerializeField] private Character attacker;
    [SerializeField] private Character target;

    [Header("Test Action")]
    [SerializeField] private ActionType testActionType = ActionType.NormalAttack;

    [Header("Input")]
    [SerializeField] private KeyCode faceOnlyKey = KeyCode.F1;
    [SerializeField] private KeyCode attackKey = KeyCode.F2;
    [SerializeField] private KeyCode returnKey = KeyCode.F3;

    [Header("Camera")]
    [SerializeField] private CameraController cameraController;
    [SerializeField] private bool returnCameraAfterAction = true;
    [SerializeField] private float cameraArriveTimeout = 1.5f;

    [Header("Move To Target")]
    [SerializeField] private bool returnPositionAfterAction = true;

    [Header("Facing")]
    [SerializeField] private bool returnFacingAfterAction = true;

    [Header("Timing")]
    [SerializeField] private float beforeActionDelay = 0.15f;
    [SerializeField] private float afterActionDelay = 0.35f;

    [Header("Clash Power")]
    [SerializeField] private BattleWorldFloatingTextManager  floatingTextManager;
    [SerializeField] private bool showClashPower = true;
    [SerializeField] private float clashRollDuration = 0.45f;
    [SerializeField] private float clashResultHoldDuration = 0.45f;
    [SerializeField] private int testAttackerClashPower = 12;
    [SerializeField] private int testTargetClashPower = 9;

    [Header("Damage Test")]
    [SerializeField] private DamageNumberManager damageNumberManager;
    [SerializeField] private int testDamage = 4;
    [SerializeField] private PartType testTargetPartType = PartType.HEAD;

    [Header("Preparation Option")]
    [SerializeField] private bool preparationUsesCamera = false;
    [SerializeField] private bool preparationMovesToTarget = false;
    [SerializeField] private bool preparationHasHitDamage = false;
    [SerializeField] private bool preparationShowsClashPower = false;

    [Header("Debug")]
    [SerializeField] private bool ignoreInputWhilePlaying = true;

    private bool isPlaying;

    private void Awake()
    {
        if (cameraController == null)
            cameraController = FindFirstObjectByType<CameraController>();

        if (floatingTextManager == null)
            floatingTextManager = FindFirstObjectByType<BattleWorldFloatingTextManager>();

        if (damageNumberManager == null)
            damageNumberManager = FindFirstObjectByType<DamageNumberManager>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(faceOnlyKey))
            TryStartSequence(TestFaceOnly());

        if (Input.GetKeyDown(attackKey))
            TryStartSequence(TestAction());

        if (Input.GetKeyDown(returnKey))
            TryStartSequence(TestReturnFacing());
    }

    private void TryStartSequence(IEnumerator sequence)
    {
        if (isPlaying && ignoreInputWhilePlaying)
        {
            Debug.Log("[BattleAnimationTestDriver] 이미 애니메이션 테스트 중입니다.");
            return;
        }

        StartCoroutine(RunSequence(sequence));
    }

    private IEnumerator RunSequence(IEnumerator sequence)
    {
        isPlaying = true;

        yield return sequence;

        isPlaying = false;
    }

    private IEnumerator TestFaceOnly()
    {
        CharacterViewSet views =
            GetViews();

        if (!views.IsValid)
            yield break;

        yield return FaceEachOther(views);
    }

    private IEnumerator TestAction()
    {
        CharacterViewSet views =
            GetViews();

        if (!views.IsValid)
            yield break;

        ActionType actionType =
            testActionType;

        BodyPart targetPart =
            GetTargetPart(
                target,
                testTargetPartType);

        bool useCamera =
            ShouldUseTargetCamera(actionType);

        bool moveToTarget =
            ShouldMoveToTarget(actionType);

        bool showPower =
            ShouldShowClashPower(actionType);

        bool playHitDamage =
            ShouldPlayHitAndDamage(actionType);

        // 1. 카메라 이동 시작
        if (useCamera && cameraController != null)
        {
            cameraController.FocusBetween(
                attacker,
                target);
        }

        // 2. 서로 바라보기
        yield return FaceEachOther(views);

        // 3. 공격자가 상대 앞으로 이동
        if (moveToTarget && views.AttackerMover != null)
        {
            yield return views.AttackerMover.MoveNearTarget(
                target,
                targetPart);
        }

        // 4. 카메라 도착 대기
        if (useCamera && cameraController != null)
        {
            yield return cameraController.WaitUntilArrived(
                cameraArriveTimeout);
        }

        // 5. 합 표시
        // NormalAttack과 Duel 모두 여기서 표시됨
        if (showPower)
        {
            yield return ShowClashPowerNumbers(
                views.AttackerView,
                views.TargetView);
        }

        yield return new WaitForSeconds(beforeActionDelay);

        // 6. 공격 애니메이션 실행
        if (views.AttackerView != null)
        {
            int hitFrameCount = 0;

            yield return views.AttackerView.PlayAction(
                actionType,
                onHitFrame: () =>
                {
                    hitFrameCount++;

                    Debug.Log(
                        $"[BattleAnimationTestDriver] HitFrame {hitFrameCount} 발생");

                    if (!playHitDamage)
                        return;

                    if (views.TargetView == null)
                        return;

                    views.TargetView.PlayHitRestart();

                    ShowDamageNumber(
                        views.TargetView,
                        targetPart,
                        testDamage);
                },
                onEffectFrame: null);
        }

        // 7. 상태 갱신
        views.AttackerView?.RefreshVisualState();
        views.TargetView?.RefreshVisualState();

        yield return new WaitForSeconds(afterActionDelay);

        // 8. 공격자 위치 복귀
        if (returnPositionAfterAction &&
            moveToTarget &&
            views.AttackerMover != null)
        {
            yield return views.AttackerMover.ReturnToDefaultPosition();
        }

        // 9. 방향 복귀
        if (returnFacingAfterAction)
        {
            yield return ReturnFacing(views);
        }

        // 10. 카메라 복귀
        if (returnCameraAfterAction && cameraController != null)
        {
            cameraController.Return();
        }
    }

    private IEnumerator FaceEachOther(CharacterViewSet views)
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

    private IEnumerator TestReturnFacing()
    {
        CharacterViewSet views =
            GetViews();

        if (!views.IsValid)
            yield break;

        yield return ReturnFacing(views);

        if (cameraController != null)
            cameraController.Return();
    }

    private IEnumerator ShowClashPowerNumbers(
        CharacterView attackerView,
        CharacterView targetView)
    {
        if (!showClashPower)
            yield break;

        if (floatingTextManager == null)
        {
            Debug.LogWarning("[BattleAnimationTestDriver] BattleWorldFloatingTextManager 없음");
            yield break;
        }

        Transform attackerTextAnchor =
            GetClashTextAnchor(
                attacker,
                attackerView);

        Transform targetTextAnchor =
            GetClashTextAnchor(
                target,
                targetView);

        Debug.Log(
            $"[BattleAnimationTestDriver] 합 표시 시도 / " +
            $"AttackerAnchor={(attackerTextAnchor == null ? "NULL" : attackerTextAnchor.name)} / " +
            $"TargetAnchor={(targetTextAnchor == null ? "NULL" : targetTextAnchor.name)}");

        Color clashColor =
            Color.yellow;

        Color tieColor =
            new Color(1f, 0.45f, 0.1f);

        yield return floatingTextManager.ShowClashPowerUntilResolved(
            attackerTextAnchor,
            targetTextAnchor,
            testAttackerClashPower,
            testTargetClashPower,
            clashColor,
            tieColor);
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
    private void ShowDamageNumber(
        CharacterView targetView,
        BodyPart targetPart,
        int damage)
    {
        if (damageNumberManager == null)
            return;

        if (targetView == null)
            return;

        if (damage <= 0)
            return;

        Vector3 position =
            targetView.GetDamageNumberPosition(targetPart);

        damageNumberManager.ShowDamage(
            position, damage);
    }

    private BodyPart GetTargetPart(
        Character character,
        PartType partType)
    {
        if (character == null)
            return null;

        if (character.BodyParts == null)
            return null;

        foreach (BodyPart part in character.BodyParts)
        {
            if (part == null)
                continue;

            if (part.Type == partType)
                return part;
        }

        return null;
    }

    private bool ShouldUseTargetCamera(ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.NormalAttack:
            case ActionType.Duel:
            case ActionType.Prestige:
                return true;

            case ActionType.Preparation:
                return preparationUsesCamera;

            default:
                return false;
        }
    }

    private bool ShouldMoveToTarget(ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.NormalAttack:
            case ActionType.Duel:
            case ActionType.Prestige:
                return true;

            case ActionType.Preparation:
                return preparationMovesToTarget;

            default:
                return false;
        }
    }

    private bool ShouldShowClashPower(ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.NormalAttack:
            case ActionType.Duel:
                return true;

            case ActionType.Preparation:
                return preparationShowsClashPower;

            default:
                return false;
        }
    }

    private bool ShouldPlayHitAndDamage(ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.NormalAttack:
            case ActionType.Duel:
            case ActionType.Prestige:
                return true;

            case ActionType.Preparation:
                return preparationHasHitDamage;

            default:
                return false;
        }
    }

    private CharacterViewSet GetViews()
    {
        CharacterViewSet result = new CharacterViewSet();

        result.Attacker = attacker;
        result.Target = target;

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
        public Character Attacker;
        public Character Target;

        public CharacterView AttackerView;
        public CharacterView TargetView;

        public CharacterFacingController AttackerFacing;
        public CharacterFacingController TargetFacing;

        public CharacterActionMover AttackerMover;

        public bool IsValid =>
            Attacker != null &&
            Target != null;
    }
}