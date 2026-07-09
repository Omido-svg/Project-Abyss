using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleStatusVisualDirector : MonoBehaviour
{
    [SerializeField] private StatusEffectVisualDatabase visualDatabase;
    [SerializeField] private BattleVfxManager vfxManager;
    [SerializeField] private DamageNumberManager damageNumberManager;

    [Header("Timing")]
    [SerializeField] private float delayBetweenTicks = 0.15f;

    private Coroutine queueRoutine;
    private readonly Queue<StatusDamageVisualRequest> queue = new();

    private void Awake()
    {
        if (vfxManager == null)
            vfxManager = FindFirstObjectByType<BattleVfxManager>();

        if (damageNumberManager == null)
            damageNumberManager = FindFirstObjectByType<DamageNumberManager>();
    }

    public void ShowStatusDamage(
        StatusDamageVisualRequest request)
    {
        if (request == null)
        {
            Debug.LogWarning("[BattleStatusVisualDirector] request null");
            return;
        }

        Debug.Log(
            $"[BattleStatusVisualDirector] 상태 데미지 표시 요청 받음 / " +
            $"StatusKey={request.StatusKey}, " +
            $"Target={request.Target?.Data.CharacterName}, " +
            $"Part={request.TargetPart?.Type}, " +
            $"Damage={request.Damage}");

        queue.Enqueue(request);

        if (queueRoutine == null)
            queueRoutine = StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        while (queue.Count > 0)
        {
            StatusDamageVisualRequest request =
                queue.Dequeue();

            PlayOne(request);

            if (delayBetweenTicks > 0f)
                yield return new WaitForSeconds(delayBetweenTicks);
        }

        queueRoutine = null;
    }

    private void PlayOne(
        StatusDamageVisualRequest request)
    {
        if (request.Target == null)
        {
            Debug.LogWarning("[BattleStatusVisualDirector] Target null");
            return;
        }

        if (visualDatabase == null)
        {
            Debug.LogWarning("[BattleStatusVisualDirector] visualDatabase null");
            return;
        }

        StatusEffectVisualDefinition visual =
            visualDatabase.GetVisual(request.StatusKey);

        if (visual == null)
        {
            Debug.LogWarning(
                $"[BattleStatusVisualDirector] StatusKey에 맞는 Visual 없음 / StatusKey={request.StatusKey}");
            return;
        }

        Color damageColor =
            visual.DamageNumberColor;

        CharacterView targetView =
            request.Target.GetComponentInChildren<CharacterView>(true);

        if (targetView != null &&
            damageNumberManager != null)
        {
            Vector3 position =
                targetView.GetDamageNumberPosition(
                    request.TargetPart);

            damageNumberManager.ShowDamage(
                position,
                request.Damage,
                damageColor);

            Debug.Log(
                $"[BattleStatusVisualDirector] 상태 데미지 숫자 표시 / " +
                $"Damage={request.Damage}, Color={damageColor}");
        }
        else
        {
            Debug.LogWarning(
                $"[BattleStatusVisualDirector] 데미지 숫자 표시 실패 / " +
                $"TargetView={targetView}, DamageNumberManager={damageNumberManager}");
        }

        if (visual.TickDamageVfx == null)
        {
            Debug.LogWarning(
                $"[BattleStatusVisualDirector] TickDamageVfx null / StatusKey={request.StatusKey}");
            return;
        }

        if (vfxManager == null)
        {
            Debug.LogWarning("[BattleStatusVisualDirector] vfxManager null");
            return;
        }

        BattleVfxContext context =
            new BattleVfxContext
            {
                Target = request.Target,
                TargetPart = request.TargetPart,
                Damage = request.Damage
            };

        Debug.Log(
            $"[BattleStatusVisualDirector] 상태 VFX 실행 / " +
            $"StatusKey={request.StatusKey}, VFX={visual.TickDamageVfx.name}");

        vfxManager.PlayVfx(
            visual.TickDamageVfx,
            request.TargetPart != null
                ? BattleVfxAnchorType.TargetBodyPart
                : BattleVfxAnchorType.TargetRoot,
            context);
    }
}