using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BodyPartButtonRegistry : MonoBehaviour
{
    [SerializeField]
    private bool rebuildFromSceneOnAwake = true;

    private readonly List<BodyPartButton> buttons = new();
    private readonly Dictionary<Character, List<BodyPartButton>> byCharacter = new();

    public event Action RegistryChanged;

    public IReadOnlyList<BodyPartButton> Buttons => buttons;

    private void Awake()
    {
        if (rebuildFromSceneOnAwake)
            RebuildFromScene();
    }

    public void RebuildFromScene()
    {
        buttons.Clear();
        byCharacter.Clear();

        BodyPartButton[] found =
            FindObjectsByType<BodyPartButton>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (BodyPartButton button in found)
        {
            RegisterInternal(button);
        }

        RegistryChanged?.Invoke();
    }

    public void Register(BodyPartButton button)
    {
        if (!RegisterInternal(button))
            return;

        RegistryChanged?.Invoke();
    }

    public void Unregister(BodyPartButton button)
    {
        if (button == null)
            return;

        bool changed = buttons.Remove(button);

        foreach (List<BodyPartButton> list in byCharacter.Values)
        {
            changed |= list.Remove(button);
        }

        RemoveEmptyCharacterLists();

        if (changed)
            RegistryChanged?.Invoke();
    }

    public void NotifyBindingChanged(BodyPartButton button)
    {
        if (button == null)
            return;

        buttons.Remove(button);

        foreach (List<BodyPartButton> list in byCharacter.Values)
        {
            list.Remove(button);
        }

        RemoveEmptyCharacterLists();
        RegisterInternal(button);

        RegistryChanged?.Invoke();
    }

    public BodyPartButton Find(
        Character character,
        BodyPart part,
        bool requireActive = false)
    {
        if (character == null)
            return null;

        if (!byCharacter.TryGetValue(
                character,
                out List<BodyPartButton> candidates))
        {
            return null;
        }

        foreach (BodyPartButton button in candidates)
        {
            if (!IsUsable(button, requireActive))
                continue;

            if (button.BodyPart == part)
                return button;

            if (part != null &&
                button.BodyPart != null &&
                button.BodyPart.Type == part.Type)
            {
                return button;
            }

            if (part == null &&
                button.BodyPart == null)
            {
                return button;
            }
        }

        return null;
    }

    public void CopyAllTo(
        List<BodyPartButton> destination,
        bool requireActive = false)
    {
        if (destination == null)
            return;

        destination.Clear();

        foreach (BodyPartButton button in buttons)
        {
            if (IsUsable(button, requireActive))
                destination.Add(button);
        }
    }

    public void CopyCharacterButtonsTo(
        Character character,
        List<BodyPartButton> destination,
        bool requireActive = false)
    {
        if (destination == null)
            return;

        destination.Clear();

        if (character == null ||
            !byCharacter.TryGetValue(
                character,
                out List<BodyPartButton> candidates))
        {
            return;
        }

        foreach (BodyPartButton button in candidates)
        {
            if (IsUsable(button, requireActive))
                destination.Add(button);
        }
    }

    public void CopyPartButtonsTo(
        BodyPart part,
        List<BodyPartButton> destination,
        bool requireActive = false)
    {
        if (destination == null)
            return;

        destination.Clear();

        foreach (BodyPartButton button in buttons)
        {
            if (!IsUsable(button, requireActive))
                continue;

            if (button.BodyPart == part)
            {
                destination.Add(button);
                continue;
            }

            if (part != null &&
                button.BodyPart != null &&
                button.BodyPart.Type == part.Type &&
                button.Owner == part.Owner)
            {
                destination.Add(button);
            }
        }
    }

    private bool RegisterInternal(BodyPartButton button)
    {
        if (button == null ||
            button.IsTemplate)
        {
            return false;
        }

        bool changed = false;

        if (!buttons.Contains(button))
        {
            buttons.Add(button);
            changed = true;
        }

        Character owner = button.Owner;

        if (owner == null)
            return changed;

        if (!byCharacter.TryGetValue(
                owner,
                out List<BodyPartButton> list))
        {
            list = new List<BodyPartButton>();
            byCharacter.Add(owner, list);
        }

        if (!list.Contains(button))
        {
            list.Add(button);
            changed = true;
        }

        return changed;
    }

    private bool IsUsable(
        BodyPartButton button,
        bool requireActive)
    {
        if (button == null ||
            button.IsTemplate)
        {
            return false;
        }

        if (!requireActive)
            return true;

        return button.gameObject.activeInHierarchy;
    }

    private void RemoveEmptyCharacterLists()
    {
        List<Character> emptyKeys = null;

        foreach (KeyValuePair<Character, List<BodyPartButton>> pair
                 in byCharacter)
        {
            pair.Value.RemoveAll(button => button == null);

            if (pair.Value.Count > 0)
                continue;

            emptyKeys ??= new List<Character>();
            emptyKeys.Add(pair.Key);
        }

        if (emptyKeys == null)
            return;

        foreach (Character key in emptyKeys)
        {
            byCharacter.Remove(key);
        }
    }
}