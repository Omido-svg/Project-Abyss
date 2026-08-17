public interface ICharacterUniqueGaugeProvider
{
    string GaugeLabel { get; }
    float GaugeNormalized { get; }
    string GaugeValueText { get; }
}
