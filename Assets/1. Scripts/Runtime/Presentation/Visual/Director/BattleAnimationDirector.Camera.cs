using System;
using System.Collections;
using UnityEngine;

public partial class BattleAnimationDirector : MonoBehaviour
{
    private void TriggerCameraImpactPulse(
        BattleVisualPlaybackState playback,
        BattleVisualRequest request,
        SkillVisualDefinition visual,
        SkillCameraImpactTiming timing,
        int hitIndex = -1,
        int exchangeIndex = -1,
        int damage = 0,
        bool isCritical = false,
        bool brokePart = false,
        bool wasKilled = false,
        bool isClash = false,
        bool isOneSided = false)
    {
        if (request == null ||
            visual == null ||
            cameraDirector == null)
        {
            return;
        }

        SkillCameraImpactPulse pulse =
            visual.FindImpactPulse(
                timing,
                hitIndex,
                exchangeIndex,
                damage,
                isCritical,
                brokePart,
                wasKilled,
                isClash,
                isOneSided);

        if (pulse == null)
            return;

        Coroutine routine =
            cameraDirector.StartImpactPulse(
                pulse);

        if (routine != null &&
            playback != null)
        {
            playback.HasCameraActivity = true;
        }
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

}
