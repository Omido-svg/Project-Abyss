using System.Collections.Generic;
using UnityEngine;

public static class BattleVisualValidator
{
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

                if (cueKeys.Add(cue.CueKey))
                    continue;

                valid = false;

                if (logWarnings)
                {
                    Debug.LogWarning(
                        $"[BATTLE VISUAL VALIDATION] 중복 CueKey / Visual={visual.name}, Key={cue.CueKey}",
                        visual);
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
