public interface ICharacterUniqueGaugeProvider
{
    string GaugeLabel { get; }
    float GaugeNormalized { get; }
    string GaugeValueText { get; }

    /// <summary>
    /// Allocation-free state key used by world UI to skip unchanged text/fill updates.
    /// It only needs to change when the visible gauge state changes.
    /// </summary>
    int GaugeStateVersion { get; }
}
