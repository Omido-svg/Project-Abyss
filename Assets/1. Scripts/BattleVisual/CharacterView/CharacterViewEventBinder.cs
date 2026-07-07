using System.Collections;
using UnityEngine;

public class CharacterViewEventBinder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Character character;
    [SerializeField] private CharacterView characterView;

    private BattleEvent battleEvent;
    private Coroutine bindRoutine;
    private bool isBound;

    private void Reset()
    {
        character = GetComponent<Character>();
        characterView = GetComponent<CharacterView>();
    }

    private void OnEnable()
    {
        bindRoutine =
            StartCoroutine(
                BindWhenReady());
    }

    private void OnDisable()
    {
        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }

        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
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

        BattleEvent nextBattleEvent =
            character.BattleContext._battleEvent;

        if (nextBattleEvent == null)
            yield break;

        if (isBound && battleEvent == nextBattleEvent)
            yield break;

        Unbind();

        battleEvent = nextBattleEvent;

        battleEvent.OnBodyPartWeakened += OnBodyPartChanged;
        battleEvent.OnBodyPartDestroyed += OnBodyPartChanged;
        battleEvent.OnBodyPartRecovered += OnBodyPartChanged;
        battleEvent.OnCharacterDeath += OnCharacterDeath;

        isBound = true;

        if (characterView != null)
            characterView.RefreshVisualState();

        Debug.Log($"{name} CharacterViewEventBinder 연결 완료");
    }

    private void Unbind()
    {
        if (!isBound)
            return;

        if (battleEvent != null)
        {
            battleEvent.OnBodyPartWeakened -= OnBodyPartChanged;
            battleEvent.OnBodyPartDestroyed -= OnBodyPartChanged;
            battleEvent.OnBodyPartRecovered -= OnBodyPartChanged;
            battleEvent.OnCharacterDeath -= OnCharacterDeath;
        }

        battleEvent = null;
        isBound = false;
    }

    private void OnBodyPartChanged(
        Character target,
        BodyPart part)
    {
        if (target != character)
            return;

        if (part == null)
            return;

        Debug.Log(
            $"[ViewEvent] 부위 변화 수신 : {target.name} / {part.Type} / Broken={part.IsBroken}");

        if (characterView == null)
            return;

        characterView.RefreshVisualState();
    }

    private void OnCharacterDeath(
        Character target)
    {
        if (target != character)
            return;

        if (characterView == null)
            return;

        characterView.PlayDead();
    }
}