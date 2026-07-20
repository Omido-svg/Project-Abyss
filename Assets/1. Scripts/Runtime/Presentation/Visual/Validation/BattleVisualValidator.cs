using System.Collections.Generic;
using UnityEngine;

public static class BattleVisualValidator
{
    private static readonly HashSet<string> LoggedDuplicateCueWarnings = new();
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        LoggedDuplicateCueWarnings.Clear();
    }

    public static bool ValidateRequest(
        BattleVisualRequest request,
        bool logWarnings = true)
    {
        if (request == null)
            return false;

        bool valid = true;

        if (request.Attacker == null)
        {
            valid = false;

            if (logWarnings)
                Debug.LogWarning("[BATTLE VISUAL VALIDATION] Attacker가 없습니다.");
        }

        if (request.VisualDefinition == null)
        {
            valid = false;

            if (logWarnings)
            {
                Debug.LogWarning(
                    $"[BATTLE VISUAL VALIDATION] VisualDefinition이 없습니다. / ActionId={request.SourceAction?.ActionId ?? 0}");
            }
        }

        if (request.Target != null && request.Target.UsesBodyParts && request.TargetPart == null)
        {
            if (logWarnings)
            {
                Debug.LogWarning(
                    $"[BATTLE VISUAL VALIDATION] 부위형 대상인데 TargetPart가 없습니다. / Target={request.Target.Data?.CharacterName}");
            }
        }

        if (request.VisualDefinition != null)
        {
            valid &= ValidateDefinition(
                request.VisualDefinition,
                request.Attacker,
                logWarnings);
        }

        return valid;
    }

    public static bool ValidateDefinition(
        SkillVisualDefinition visual,
        Character actor = null,
        bool logWarnings = true)
    {
        if (visual == null)
            return false;

        bool valid = SkillCameraDefinitionValidator.Validate(
            visual.CameraDefinition,
            visual,
            logWarnings);

        HashSet<string> cueKeys = new();

        if (visual.VfxCues != null)
        {
            for (int i = 0; i < visual.VfxCues.Count; i++)
            {
                BattleVfxCue cue = visual.VfxCues[i];

                if (cue == null)
                    continue;

                valid &= VfxDefinitionValidator.Validate(cue.Vfx, visual, logWarnings);

                if (string.IsNullOrEmpty(cue.CueKey))
                    continue;

                string collisionIdentity =
                    BuildCollisionIdentity(cue);

                if (cueKeys.Add(collisionIdentity))
                    continue;

                if (CanAutoDistributeDuplicateHitCue(
                        visual,
                        cue,
                        i))
                {
                    continue;
                }

                valid = false;

                if (logWarnings)
                {
                    string warningKey =
                        visual.GetInstanceID() + "|" + collisionIdentity;

                    if (LoggedDuplicateCueWarnings.Add(warningKey))
                    {
                        Debug.LogWarning(
                            $"[BATTLE VISUAL VALIDATION] 실제 충돌하는 VFX Cue / " +
                            $"Visual={visual.name}, Key={cue.CueKey}, " +
                            $"Timing={cue.Timing}, HitFilter=" +
                            $"{(cue.UseHitIndexFilter ? cue.HitIndex.ToString() : "NONE")}",
                            visual);
                    }
                }
            }
        }

        if (actor != null && visual.RequiredAnimatorStates != null)
        {
            Animator animator = actor.GetComponentInChildren<Animator>(true);
            valid &= AnimatorStateValidator.Validate(
                animator,
                visual.RequiredAnimatorStates,
                visual,
                logWarnings);
        }

        return valid;
    }

    private static string BuildCollisionIdentity(
        BattleVfxCue cue)
    {
        if (cue == null)
            return string.Empty;

        return
            cue.CueKey + "|" +
            cue.Timing + "|" +
            cue.RepeatMode + "|" +
            (cue.UseHitIndexFilter
                ? "FILTER:" + cue.HitIndex
                : "NO_FILTER") + "|" +
            (cue.RequiredAttackerData != null
                ? cue.RequiredAttackerData.GetInstanceID().ToString()
                : "ANY_ATTACKER") + "|" +
            (cue.RequiredSkillNameContains ?? string.Empty) + "|" +
            cue.RequirePositiveDamage + "|" +
            cue.RequirePositiveResolvedDamage;
    }

    private static bool CanAutoDistributeDuplicateHitCue(
        SkillVisualDefinition visual,
        BattleVfxCue cue,
        int cueIndex)
    {
        if (visual?.VfxCues == null ||
            cue == null ||
            cueIndex < 0 ||
            cueIndex >= visual.VfxCues.Count)
        {
            return false;
        }

        for (int i = 0; i < cueIndex; i++)
        {
            BattleVfxCue previous = visual.VfxCues[i];

            if (cue.CanAutoDistributeByHitIndexWith(previous))
                return true;
        }

        return false;
    }

    public static bool ValidateProfile(
        SkillVisualProfile profile,
        Object context = null,
        bool logWarnings = true)
    {
        if (profile == null)
            return true;

        bool valid = true;

        foreach (SkillVisualDefinition visual in profile.EnumerateDefinitions())
        {
            if (visual == null)
                continue;

            if (!visual.AllowAsProfileFallback)
            {
                valid = false;

                if (logWarnings)
                {
                    Debug.LogWarning(
                        $"[BATTLE VISUAL VALIDATION] 캐릭터 전용 Visual이 기본 Profile에 연결됨 / Visual={visual.name}",
                        context != null ? context : profile);
                }
            }

            if (visual.VfxCues != null)
            {
                foreach (BattleVfxCue cue in visual.VfxCues)
                {
                    if (cue?.RequiredAttackerData == null)
                        continue;

                    valid = false;

                    if (logWarnings)
                    {
                        Debug.LogWarning(
                            $"[BATTLE VISUAL VALIDATION] 특정 CharacterData 필터를 가진 Visual이 기본 Profile에 연결됨 / " +
                            $"Visual={visual.name}, Character={cue.RequiredAttackerData.CharacterName}",
                            context != null ? context : profile);
                    }
                }
            }

            valid &= ValidateDefinition(visual, null, logWarnings);
        }

        return valid;
    }
}