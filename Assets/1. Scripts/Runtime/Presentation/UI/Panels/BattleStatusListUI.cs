using System.Collections.Generic;
using UnityEngine;

public sealed class BattleStatusListUI : MonoBehaviour
{
    [SerializeField] private Character owner;
    [SerializeField] private RectTransform content;
    [SerializeField] private BattleStatusRowUI rowPrefab;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.2f;

    private readonly List<BattleStatusRowUI> rows = new();
    private float nextRefresh;

    public void Configure(
        Character value,
        RectTransform contentRoot,
        BattleStatusRowUI template)
    {
        owner = value;
        content = contentRoot;
        rowPrefab = template;
        Refresh();
    }

    public Character Owner => owner;

    public void SetOwner(Character value)
    {
        owner = value;
        Refresh();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    public void Refresh()
    {
        if (content == null || rowPrefab == null) return;
        List<StatusEffect> effects = new();
        if (owner?.StatusEffects != null) effects.AddRange(owner.StatusEffects);
        if (owner?.BodyParts != null)
        {
            foreach (BodyPart part in owner.BodyParts)
                if (part?.StatusEffects != null) effects.AddRange(part.StatusEffects);
        }

        while (rows.Count < effects.Count)
        {
            BattleStatusRowUI row = Instantiate(rowPrefab, content);
            row.gameObject.SetActive(true);
            rows.Add(row);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            bool active = i < effects.Count;
            rows[i].gameObject.SetActive(active);
            if (active) rows[i].Bind(effects[i]);
        }
    }
}