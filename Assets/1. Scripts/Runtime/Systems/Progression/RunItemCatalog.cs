using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Run/Items/Run Item Catalog", fileName = "RunItemCatalog")]
public sealed class RunItemCatalog : ScriptableObject
{
    public List<RunItemDefinition> Items = new();
}
