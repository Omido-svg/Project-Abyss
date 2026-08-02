using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "Project Abyss/Verification/Character Verification Profile",
    fileName = "NewCharacterVerificationProfile")]
public sealed class CharacterVerificationProfile :
    ScriptableObject
{
    [SerializeField]
    private string profileId = "character.verification";

    [SerializeField]
    private CharacterAuthoringBundle bundle;

    [SerializeField]
    private bool strictPresentationValidation = true;

    [SerializeField]
    private List<CharacterVerificationCaseDefinition> cases = new();

    public string ProfileId =>
        string.IsNullOrWhiteSpace(profileId)
            ? name
            : profileId;

    public CharacterAuthoringBundle Bundle =>
        bundle;

    public bool StrictPresentationValidation =>
        strictPresentationValidation;

    public IReadOnlyList<CharacterVerificationCaseDefinition> Cases =>
        cases;

    public void Configure(
        string newProfileId,
        CharacterAuthoringBundle newBundle,
        IEnumerable<CharacterVerificationCaseDefinition> definitions,
        bool strictPresentation = true)
    {
        profileId =
            string.IsNullOrWhiteSpace(newProfileId)
                ? name
                : newProfileId.Trim();

        bundle = newBundle;
        strictPresentationValidation = strictPresentation;

        cases ??=
            new List<CharacterVerificationCaseDefinition>();

        cases.Clear();

        if (definitions == null)
            return;

        foreach (CharacterVerificationCaseDefinition definition
                 in definitions)
        {
            if (definition != null)
                cases.Add(definition);
        }
    }

    public CharacterVerificationCaseDefinition FindCase(
        string caseId)
    {
        if (string.IsNullOrWhiteSpace(caseId) ||
            cases == null)
        {
            return null;
        }

        for (int i = 0; i < cases.Count; i++)
        {
            CharacterVerificationCaseDefinition definition =
                cases[i];

            if (definition != null &&
                string.Equals(
                    definition.CaseId,
                    caseId,
                    System.StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        cases ??=
            new List<CharacterVerificationCaseDefinition>();

        HashSet<string> used =
            new HashSet<string>(
                System.StringComparer.Ordinal);

        for (int i = cases.Count - 1;
             i >= 0;
             i--)
        {
            CharacterVerificationCaseDefinition definition =
                cases[i];

            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.CaseId) ||
                !used.Add(definition.CaseId))
            {
                cases.RemoveAt(i);
            }
        }
    }
#endif
}
