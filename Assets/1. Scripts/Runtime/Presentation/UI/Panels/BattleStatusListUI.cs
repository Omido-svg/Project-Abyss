using System.Collections.Generic;
using UnityEngine;

public sealed class BattleStatusListUI : MonoBehaviour
{
    [SerializeField] private Character owner;
    [SerializeField] private RectTransform content;
    [SerializeField] private BattleStatusRowUI rowPrefab;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.2f;

    [Header("0922 Status Presentation")]
    [SerializeField] private bool reducedMotion;
    [SerializeField, Min(0f)] private float phaseStaggerSeconds = 0.11f;

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
    public bool ReducedMotion => reducedMotion;

    public void SetOwner(Character value)
    {
        owner = value;
        Refresh();
    }

    public void SetReducedMotion(bool value)
    {
        if (reducedMotion == value)
            return;

        reducedMotion = value;
        Refresh();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh)
            return;

        nextRefresh = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    public void Refresh()
    {
        if (content == null || rowPrefab == null)
            return;

        // Phase 11: Runtime status는 읽기만 하고 projection model에서 aggregate한다.
        List<BattleStatusChipModel> chips =
            BattleStatusPresentationProjector.Build(owner);

        while (rows.Count < chips.Count)
        {
            BattleStatusRowUI row = Instantiate(rowPrefab, content);
            row.gameObject.SetActive(true);
            rows.Add(row);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            bool active = i < chips.Count;
            rows[i].gameObject.SetActive(active);

            if (!active)
                continue;

            rows[i].ConfigurePresentation(
                reducedMotion,
                i * phaseStaggerSeconds);
            rows[i].Bind(chips[i]);
        }
    }
}
