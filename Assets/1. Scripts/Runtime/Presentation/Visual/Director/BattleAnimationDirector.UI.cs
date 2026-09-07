using System;
using System.Collections;
using UnityEngine;

public partial class BattleAnimationDirector : MonoBehaviour
{
    private IEnumerator ShowActionAnnouncement(
        BattleVisualPlaybackState playback,
        SkillVisualDefinition visual)
    {
        BattleVisualRequest request =
            playback != null
                ? playback.Request
                : null;

        if (request == null)
            yield break;

        if (visual == null)
            yield break;

        if (!visual.ShowsActionAnnouncement)
            yield break;

        if (actionAnnounceUI == null)
            yield break;

        playback.IsAnnouncementVisible = true;

        yield return actionAnnounceUI.ShowPersistent(
            request);
    }

    private IEnumerator HideActionAnnouncement(
        BattleVisualPlaybackState playback,
        SkillVisualDefinition visual)
    {
        if (visual == null)
            yield break;

        if (!visual.ShowsActionAnnouncement)
            yield break;

        if (actionAnnounceUI == null)
            yield break;

        yield return actionAnnounceUI.Hide();

        if (playback != null)
            playback.IsAnnouncementVisible = false;
    }

    private void ShowDamageNumber(
        CharacterView targetView,
        BodyPart targetPart,
        int damage,
        BattleDamageNumberStyle style)
    {
        if (damageNumberManager == null)
        {
            if (logMissingReferences)
                Debug.LogWarning("[BattleAnimationDirector] DamageNumberManager 없음");

            return;
        }

        if (targetView == null ||
            damage <= 0)
        {
            return;
        }

        Vector3 position =
            targetView.GetDamageNumberPosition(
                targetPart);

        // HP와 흐트러짐 숫자가 같은 프레임에 동시에 발생할 때
        // 완전히 겹치지 않도록 흐트러짐을 살짝 위로 띄운다.
        if (style == BattleDamageNumberStyle.Stagger)
            position += Vector3.up * 0.12f;

        damageNumberManager.ShowDamage(
            position,
            damage,
            style);
    }

    private static string GetCharacterDisplayName(
        Character character,
        string fallback)
    {
        if (character == null)
            return fallback;

        string dataName =
            character.Data?.CharacterName;

        if (!string.IsNullOrWhiteSpace(dataName))
            return dataName;

        if (!string.IsNullOrWhiteSpace(character.name))
            return character.name;

        return fallback;
    }

}
