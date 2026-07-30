using UnityEngine;

/// <summary>
/// Character Prefab에 CharacterAuthoringBundle을 적용한다.
/// BattleManager보다 먼저 CharacterData, CombatLoadout, Passive/Item,
/// Legacy Adapter, Animator를 연결한다.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class CharacterAuthoringLink : MonoBehaviour
{
    [SerializeField] private CharacterAuthoringBundle bundle;
    [SerializeField] private Character targetCharacter;
    [SerializeField] private Animator targetAnimator;
    [SerializeField] private bool applyOnAwake = true;

    public CharacterAuthoringBundle Bundle => bundle;
    public Character TargetCharacter => targetCharacter;
    public bool ApplyOnAwake => applyOnAwake;

    private void Awake()
    {
        if (applyOnAwake)
            ApplyNow();
    }

    public void Configure(CharacterAuthoringBundle newBundle)
    {
        bundle = newBundle;
        ResolveReferences();
    }

    [ContextMenu("Apply Character Authoring Bundle")]
    private void ApplyNowFromContextMenu() => ApplyNow();

    public bool ApplyNow()
    {
        ResolveReferences();

        if (bundle == null || targetCharacter == null)
            return false;

        if (!bundle.IsCompatibleWith(targetCharacter, out string reason))
        {
            Debug.LogError(
                "[CharacterAuthoringLink] Bundle 적용 실패 / " + reason,
                this);
            return false;
        }

        targetCharacter.ConfigureAuthoringCore(
            bundle.CharacterData,
            bundle.CombatLoadout,
            bundle.OverrideLoadout ? bundle.EquippedItems : null,
            bundle.OverrideLoadout ? bundle.EquippedAugments : null);

        if (targetCharacter is ICharacterAuthoringTarget target &&
            !target.ApplyCharacterAuthoring(bundle))
        {
            Debug.LogError(
                "[CharacterAuthoringLink] 종류별 Authoring 적용 실패 / " +
                $"Character={targetCharacter.GetType().Name}, Bundle={bundle.name}",
                this);
            return false;
        }

        ApplyAnimator();
        ApplyPresentationProfile();
        return true;
    }

    private void ApplyAnimator()
    {
        if (targetAnimator == null)
            return;

        if (bundle.OverrideAnimatorController &&
            bundle.AnimatorController != null)
        {
            targetAnimator.runtimeAnimatorController = bundle.AnimatorController;
        }

        if (bundle.OverrideAvatar && bundle.Avatar != null)
            targetAnimator.avatar = bundle.Avatar;
    }


    private void ApplyPresentationProfile()
    {
        CharacterView view =
            targetCharacter != null
                ? targetCharacter.GetComponentInChildren<
                    CharacterView>(true)
                : null;

        view?.ConfigurePresentationProfile(
            bundle?.PresentationProfile);
    }

    private void ResolveReferences()
    {
        targetCharacter ??= GetComponent<Character>();
        targetCharacter ??= GetComponentInChildren<Character>(true);
        targetAnimator ??= targetCharacter != null
            ? targetCharacter.GetComponentInChildren<Animator>(true)
            : GetComponentInChildren<Animator>(true);
    }
}