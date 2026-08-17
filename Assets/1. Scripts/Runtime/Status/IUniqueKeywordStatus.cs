/// <summary>
/// 캐릭터 전용 자원/상태가 공용 상태이상과 섞이지 않도록 구분하는 마커 계약.
/// </summary>
public interface IUniqueKeywordStatus
{
    string UniqueKeywordId { get; }
}
