using UnityEngine;

public class BattleEffectResolver
{
    private readonly BattleContext context;
    private BattleStatusVisualDirector statusVisualDirector;

    public BattleEffectResolver(BattleContext context)
    {
        this.context = context;

        statusVisualDirector =
            Object.FindFirstObjectByType<BattleStatusVisualDirector>();
    }

    public bool ApplyPartDamage(EffectRequest request)
    {
        if (!IsValidCommonRequest(request))
            return false;

        if (request.TargetPart == null)
            return false;

        if (request.TargetCharacter.IsDead)
            return false;

        request.TargetCharacter.TakeDamage(
            request.TargetPart,
            request.Value,
            request.CanBreakPart);

        return true;
    }

    public bool ApplyStatusPartDamage(EffectRequest request)
    {
        if (!IsValidCommonRequest(request))
            return false;

        if (request.TargetPart == null)
            return false;

        if (request.TargetCharacter.IsDead)
            return false;

        if (request.TargetPart.IsBroken)
            return false;

        request.TargetCharacter.TakeStatusPartDamage(
            request.TargetPart,
            request.Value,
            request.SourceStatusEffect);

        Debug.Log(
            $"[BattleEffectResolver] 상태이상 피해 시각화 요청 시도 / " +
            $"Target={request.TargetCharacter?.Data.CharacterName}, " +
            $"Part={request.TargetPart?.Type}, " +
            $"Damage={request.Value}, " +
            $"Status={request.SourceStatusEffect?.GetType().Name}");

        ShowStatusDamageVisual(request);

        return true;
    }

    public bool ApplyTrueDamage(EffectRequest request)
    {
        if (!IsValidCommonRequest(request))
            return false;

        if (request.TargetCharacter.IsDead)
            return false;

        request.TargetCharacter.TakeTrueDamage(
            request.Value,
            request.SourceStatusEffect);

        ShowStatusDamageVisual(request);

        return true;
    }

    private void ShowStatusDamageVisual(
        EffectRequest request)
    {
        if (request == null)
        {
            Debug.LogWarning("[BattleEffectResolver] 상태 시각화 실패 : request null");
            return;
        }

        if (request.Value <= 0)
        {
            Debug.LogWarning("[BattleEffectResolver] 상태 시각화 실패 : damage <= 0");
            return;
        }

        if (request.SourceStatusEffect == null)
        {
            Debug.LogWarning("[BattleEffectResolver] 상태 시각화 실패 : SourceStatusEffect null");
            return;
        }

        if (request.TargetCharacter == null)
        {
            Debug.LogWarning("[BattleEffectResolver] 상태 시각화 실패 : TargetCharacter null");
            return;
        }

        if (statusVisualDirector == null)
        {
            statusVisualDirector =
                Object.FindFirstObjectByType<BattleStatusVisualDirector>();
        }

        if (statusVisualDirector == null)
        {
            Debug.LogWarning("[BattleEffectResolver] 상태 시각화 실패 : BattleStatusVisualDirector 없음");
            return;
        }

        string statusKey =
            request.SourceStatusEffect.GetType().Name;

        string partName =
            request.TargetPart != null
                ? request.TargetPart.Type.ToString()
                : "NONE";

        Debug.Log(
            $"[BattleEffectResolver] 상태 시각화 요청 전달 / " +
            $"StatusKey={statusKey}, " +
            $"Target={request.TargetCharacter.Data.CharacterName}, " +
            $"Part={partName}, " +
            $"Damage={request.Value}");

        statusVisualDirector.ShowStatusDamage(
            new StatusDamageVisualRequest
            {
                Target = request.TargetCharacter,
                TargetPart = request.TargetPart,
                Damage = request.Value,
                StatusKey = statusKey
            });
    }

    public bool ApplyCharacterStatus(EffectRequest request)
    {
        if (!IsValidCommonRequest(request))
            return false;

        if (request.StatusEffect == null)
            return false;

        if (request.TargetCharacter.IsDead)
            return false;

        request.TargetCharacter.AddStatus(
            request.StatusEffect,
            request.SourceCharacter);

        return true;
    }

    public bool ApplyBodyPartStatus(EffectRequest request)
    {
        if (!IsValidCommonRequest(request))
            return false;

        if (request.TargetPart == null)
            return false;

        if (request.StatusEffect == null)
            return false;

        if (request.TargetCharacter.IsDead)
            return false;

        if (request.TargetPart.IsBroken)
        {
            Debug.Log(
                $"[EFFECT BLOCKED] 파괴된 부위에는 부위 상태이상을 부여할 수 없습니다. " +
                $"{request.TargetCharacter.Data.CharacterName} / {request.TargetPart.Type} / {request.StatusEffect.EffectName}");

            return false;
        }

        request.TargetCharacter.AddPartStatus(
            request.TargetPart,
            request.StatusEffect,
            request.SourceCharacter);

        return true;
    }

    public bool ForceBreakPart(EffectRequest request)
    {
        if (!IsValidCommonRequest(request))
            return false;

        if (request.TargetPart == null)
            return false;

        if (request.TargetCharacter.IsDead)
            return false;

        if (request.TargetPart.IsBroken)
            return false;

        request.TargetCharacter.ForceBreakPart(
            request.TargetPart);

        return true;
    }

    public bool BreakWeakenedPart(EffectRequest request)
    {
        if (!IsValidCommonRequest(request))
            return false;

        if (request.TargetPart == null)
            return false;

        if (request.TargetCharacter.IsDead)
            return false;

        if (request.TargetPart.IsBroken)
            return false;

        if (!request.TargetPart.IsWeakened)
            return false;

        return request.TargetCharacter.TryBreakWeakenedPart(
            request.TargetPart);
    }

    public bool AddPrestige(EffectRequest request)
    {
        if (!IsValidCommonRequest(request))
            return false;

        if (request.TargetCharacter.IsDead)
            return false;

        request.TargetCharacter.AddPrestige(
            request.Value);

        return true;
    }

    public bool RemoveBodyPartStatus(EffectRequest request)
    {
        if (!IsValidCommonRequest(request))
            return false;

        if (request.TargetPart == null)
            return false;

        if (request.StatusEffect == null)
            return false;

        if (request.TargetCharacter.IsDead)
            return false;

        request.TargetCharacter.RemovePartStatus(
            request.TargetPart,
            request.StatusEffect);

        return true;
    }

    public bool RecoverPart(EffectRequest request)
    {
        if (!IsValidCommonRequest(request))
            return false;

        if (request.TargetPart == null)
            return false;

        if (request.TargetCharacter.IsDead)
            return false;

        if (!request.TargetPart.IsBroken &&
            !request.TargetPart.IsWeakened)
        {
            return false;
        }

        request.TargetCharacter.RecoverPart(
            request.TargetPart);

        return true;
    }

    public bool ForceKill(EffectRequest request)
    {
        if (!IsValidCommonRequest(request))
            return false;

        if (request.TargetCharacter.IsDead)
            return false;

        request.TargetCharacter.Die();

        return true;
    }

    public bool SetPrestigeToMax(EffectRequest request)
    {
        if (!IsValidCommonRequest(request))
            return false;

        if (request.TargetCharacter.IsDead)
            return false;

        if (request.TargetCharacter.RuntimeStatus == null)
            return false;

        if (request.TargetCharacter.CurrentStatus == null)
            return false;

        request.TargetCharacter.RuntimeStatus.currentPrestige =
            request.TargetCharacter.CurrentStatus.maxPrestige;

        return true;
    }

    private bool IsValidCommonRequest(EffectRequest request)
    {
        if (request == null)
            return false;

        if (request.TargetCharacter == null)
            return false;

        if (request.Value < 0)
            return false;

        return true;
    }
}