#if UNITY_EDITOR
using System;

/// <summary>
/// 여러 Project Abyss Exporter를 한 번에 실행할 때 성공 Dialog/Reveal을 억제합니다.
/// 개별 Export 메뉴 실행에는 영향을 주지 않습니다.
/// </summary>
public static class ProjectAbyssExportSession
{
    public static bool IsBatch { get; private set; }

    public static IDisposable BeginBatch()
    {
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private readonly bool previous;

        public Scope()
        {
            previous = IsBatch;
            IsBatch = true;
        }

        public void Dispose()
        {
            IsBatch = previous;
        }
    }
}
#endif
