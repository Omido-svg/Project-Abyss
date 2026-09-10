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
