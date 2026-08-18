using System.Collections.Generic;

public interface ICharacterVerificationCaseProvider
{
    bool Supports(
        CharacterAuthoringBundle bundle,
        Character character);

    IEnumerable<CharacterVerificationCaseDefinition>
        CreateDefaultCases(
            CharacterAuthoringBundle bundle);

    bool TryExecute(
        CharacterVerificationContext context,
        out CharacterVerificationCaseResult result);
}

public static class CharacterVerificationCaseProviderRegistry
{
    private static readonly List<ICharacterVerificationCaseProvider>
        Providers = new()
        {
            new CharacterFeatureCoverageCaseProvider(),
            new GenericCharacterVerificationCaseProvider(),
            new CoreCharacterVerificationCaseProvider(),
            new EnemyCharacterVerificationCaseProvider(),
            new OlafCharacterVerificationCaseProvider(),
            new YujinCharacterVerificationCaseProvider(),
            new HifumiCharacterVerificationCaseProvider()
        };

    public static IEnumerable<CharacterVerificationCaseDefinition>
        BuildDefaultCases(
            CharacterAuthoringBundle bundle)
    {
        HashSet<string> used =
            new HashSet<string>(
                System.StringComparer.Ordinal);

        for (int i = 0;
             i < Providers.Count;
             i++)
        {
            ICharacterVerificationCaseProvider provider =
                Providers[i];

            if (provider == null ||
                !provider.Supports(
                    bundle,
                    bundle?.CharacterPrefab))
            {
                continue;
            }

            IEnumerable<CharacterVerificationCaseDefinition>
                definitions =
                    provider.CreateDefaultCases(bundle);

            if (definitions == null)
                continue;

            foreach (CharacterVerificationCaseDefinition definition
                     in definitions)
            {
                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.CaseId) ||
                    !used.Add(definition.CaseId))
                {
                    continue;
                }

                yield return definition;
            }
        }
    }

    public static bool TryExecute(
        CharacterVerificationContext context,
        out CharacterVerificationCaseResult result)
    {
        result = null;

        for (int i = 0;
             i < Providers.Count;
             i++)
        {
            ICharacterVerificationCaseProvider provider =
                Providers[i];

            if (provider == null ||
                !provider.Supports(
                    context?.Bundle,
                    context?.Character))
            {
                continue;
            }

            if (provider.TryExecute(
                    context,
                    out result))
            {
                return true;
            }
        }

        return false;
    }
}