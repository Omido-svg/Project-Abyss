using System.Collections;
using UnityEngine;

public class CharacterViewEventBinder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Character character;
    [SerializeField] private CharacterView characterView;

    private BattleEvent battleEvent;

    private void Reset()
    {
        character = GetComponent<Character>();
        characterView = GetComponent<CharacterView>();
    }

    private void Start()
    {
        StartCoroutine(BindWhenReady());
    }

    private IEnumerator BindWhenReady()
    {
        if (character == null)
            character = GetComponent<Character>();

        if (characterView == null)
            characterView = GetComponent<CharacterView>();

        while (character != null &&
               character.BattleContext == null)
        {
            yield return null;
        }

        if (character == null)
            yield break;

        if (character.BattleContext == null)
            yield break;

        battleEvent = character.BattleContext._battleEvent;

        if (battleEvent == null)
            yield break;

        battleEvent.OnBodyPartWeakened += OnBodyPartChanged;
        battleEvent.OnBodyPartDestroyed += OnBodyPartChanged;
        battleEvent.OnBodyPartRecovered += OnBodyPartChanged;
        battleEvent.OnCharacterDeath += OnCharacterDeath;

        characterView.RefreshVisualState();

        Debug.Log($"{name} CharacterViewEventBinder 연결 완료");
    }

    private void OnDestroy()
    {
        if (battleEvent == null)
            return;

        battleEvent.OnBodyPartWeakened -= OnBodyPartChanged;
        battleEvent.OnBodyPartDestroyed -= OnBodyPartChanged;
        battleEvent.OnBodyPartRecovered -= OnBodyPartChanged;
        battleEvent.OnCharacterDeath -= OnCharacterDeath;
    }

    private void OnBodyPartChanged(
        Character target,
        BodyPart part)
    {
        Debug.Log(
            $"[ViewEvent] 부위 변화 수신 : {target.name} / {part.Type} / Broken={part.IsBroken}");

        if (target != character)
            return;

        if (characterView == null)
            return;

        characterView.RefreshVisualState();
    }

    private void OnCharacterDeath(Character target)
    {
        if (target != character)
            return;

        if (characterView == null)
            return;

        characterView.PlayDead();
    }
}