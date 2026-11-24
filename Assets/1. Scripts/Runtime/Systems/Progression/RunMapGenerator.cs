using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 11-24 스테이지 맵 생성 — CLAUDE.md 「스테이지 · 맵 설계 규칙」 + 명세 맵 시트(스테이지 구성 · 맵 배치 규칙 · 노드 타입).
/// 해석(사용자 확인 11-24):
/// - 총 깊이 = 분기 층(1스테이지 8~10 · 2 · 3스테이지 9~11) + 고정 상점 1층 + 보스 1층
/// - 1층 폭 고정(1스테이지 4 · 이후 5), 나머지 분기 층 폭은 4~6 / 5~7
/// - 연결 = 바로 아래 인접 노드만(좌 · 중 · 우), 선이 교차하지 않고, 위 · 아래 모든 노드가 선을 하나 이상 가진다.
///   나란히 가는 경로끼리 왔다 갔다 하지 않도록 「단조 계단」으로 잇는다(한 노드 최대 3갈래)
/// - 노드 비율 일반 40 · 엘리트 15 · 턴제한 15 · 물음표 15 · 골드 상점 7.5 · 체력 상점 7.5
/// - 엘리트 · 턴제한 · 상점은 3층 이전 금지(1 · 2층은 일반 · 물음표만, 비율 재정규화)
/// - 상점 → 상점 직접 연결 금지(보스 직전 고정 상점은 별개) · 상점 개수 상한 없음
/// - 엘리트 노드는 맵 전체에 최대 2개(11-24 사용자) — 넘치면 무작위로 2개만 남기고 다시 뽑는다
/// - 같은 시드 = 같은 맵
/// </summary>
public enum RunMapNodeType
{
    NormalBattle = 0,
    EliteBattle = 1,
    TurnLimitBattle = 2,
    Mystery = 3,
    GoldShop = 4,
    HealthShop = 5,
    BossShop = 6,
    Boss = 7
}

[Serializable]
public sealed class RunMapNode
{
    public int Floor;
    public int Index;
    public RunMapNodeType Type;
    public readonly List<int> Next = new();   // 다음 층 노드 인덱스
    public readonly List<int> Prev = new();   // 이전 층 노드 인덱스
    public float X;                            // 0~1 가로 위치(표시 · 인접 판정용)
}

public sealed class RunMap
{
    public int Stage;
    public int Seed;
    public int BossVariant;                    // 0 / 1 — 스테이지 보스 2종 중 하나(시드)
    public readonly List<List<RunMapNode>> Floors = new();
    public int BranchFloorCount;
    public RunMapNode Get(int floor, int index) =>
        floor >= 0 && floor < Floors.Count && index >= 0 && index < Floors[floor].Count
            ? Floors[floor][index] : null;
    public int ShopFloor => BranchFloorCount;
    public int BossFloor => BranchFloorCount + 1;
}

public static class RunMapGenerator
{
    public const int MaxBranches = 3;

    public readonly struct StageShape
    {
        public readonly int BranchMin, BranchMax, FirstWidth, WidthMin, WidthMax;
        public StageShape(int branchMin, int branchMax, int firstWidth, int widthMin, int widthMax)
        { BranchMin = branchMin; BranchMax = branchMax; FirstWidth = firstWidth; WidthMin = widthMin; WidthMax = widthMax; }
    }

    public static StageShape ShapeFor(int stage) => stage <= 1
        ? new StageShape(8, 10, 4, 4, 6)
        : new StageShape(9, 11, 5, 5, 7);

    // 비율(%) — 일반 40 · 엘리트 15 · 턴제한 15 · 물음표 15 · 골드 상점 7.5 · 체력 상점 7.5
    private static readonly (RunMapNodeType type, float weight)[] Weights =
    {
        (RunMapNodeType.NormalBattle, 40f),
        (RunMapNodeType.EliteBattle, 15f),
        (RunMapNodeType.TurnLimitBattle, 15f),
        (RunMapNodeType.Mystery, 15f),
        (RunMapNodeType.GoldShop, 7.5f),
        (RunMapNodeType.HealthShop, 7.5f)
    };
    public const int RestrictedUntilFloor = 3; // 1-기준: 3층 이전(1 · 2층) 금지
    public const int MaxEliteNodes = 2;        // 11-24 사용자 — 맵 전체에 엘리트 노드 최대 2개

    public static bool IsShop(RunMapNodeType t) =>
        t == RunMapNodeType.GoldShop || t == RunMapNodeType.HealthShop || t == RunMapNodeType.BossShop;
    private static bool IsRestricted(RunMapNodeType t) =>
        t == RunMapNodeType.EliteBattle || t == RunMapNodeType.TurnLimitBattle || IsShop(t);

    public static RunMap Generate(int stage, int seed)
    {
        var rng = new Random(seed);
        var shape = ShapeFor(stage);
        var map = new RunMap { Stage = stage, Seed = seed, BossVariant = rng.Next(2) };
        map.BranchFloorCount = rng.Next(shape.BranchMin, shape.BranchMax + 1);

        for (int f = 0; f < map.BranchFloorCount; f++)
        {
            int width = f == 0 ? shape.FirstWidth : rng.Next(shape.WidthMin, shape.WidthMax + 1);
            var floor = new List<RunMapNode>(width);
            for (int i = 0; i < width; i++)
                floor.Add(new RunMapNode { Floor = f, Index = i, X = (i + 0.5f) / width });
            map.Floors.Add(floor);
        }
        for (int f = 0; f + 1 < map.BranchFloorCount; f++)
            Connect(map.Floors[f], map.Floors[f + 1], rng);

        // 보스 직전 고정 상점 1개로 전부 합류 → 보스
        var shop = new RunMapNode { Floor = map.ShopFloor, Index = 0, Type = RunMapNodeType.BossShop, X = 0.5f };
        var boss = new RunMapNode { Floor = map.BossFloor, Index = 0, Type = RunMapNodeType.Boss, X = 0.5f };
        foreach (var n in map.Floors[map.BranchFloorCount - 1]) { n.Next.Add(0); shop.Prev.Add(n.Index); }
        shop.Next.Add(0); boss.Prev.Add(0);
        map.Floors.Add(new List<RunMapNode> { shop });
        map.Floors.Add(new List<RunMapNode> { boss });

        AssignTypes(map, rng);
        CapEliteNodes(map, rng);
        return map;
    }

    /// <summary>
    /// 두 층을 「단조 계단」으로 잇는다 — (0,0)에서 시작해 매 걸음 위 · 아래 · 대각 중 하나로 나아가며 선을 긋는다.
    /// 교차가 생기지 않고, 위 · 아래 모든 노드가 선을 가진다. 가로 위치가 가까운 쪽으로 나아가서 인접 노드만 잇고,
    /// 가끔 같은 노드에서 한 갈래를 더 내 분기를 만든다(최대 3).
    /// </summary>
    private static void Connect(List<RunMapNode> top, List<RunMapNode> bottom, Random rng)
    {
        int i = 0, j = 0;
        Link(top[0], bottom[0]);
        while (i < top.Count - 1 || j < bottom.Count - 1)
        {
            bool canI = i < top.Count - 1, canJ = j < bottom.Count - 1;
            bool topFull = top[i].Next.Count >= MaxBranches;
            bool bottomFull = bottom[j].Prev.Count >= MaxBranches;
            int step; // 0 = 위만 전진, 1 = 아래만 전진, 2 = 둘 다
            if (!canI) step = 1;
            else if (!canJ) step = 0;
            else
            {
                float nextTop = top[i + 1].X, nextBottom = bottom[j + 1].X;
                float gap = nextBottom - nextTop;
                if (topFull) step = bottomFull || gap < -0.08f ? 2 : 0; // 위 노드가 3갈래 다 씀 → 위를 반드시 전진
                else if (gap < -0.08f) step = 1;          // 아래 다음 칸이 더 왼쪽 → 아래 전진(위 노드가 한 갈래 더)
                else if (gap > 0.08f) step = 0;           // 위 다음 칸이 더 왼쪽 → 위 전진(아래 노드로 합류)
                else step = rng.NextDouble() < 0.25 ? (rng.Next(2) == 0 ? 0 : 1) : 2; // 거의 나란하면 대개 대각, 가끔 분기 · 합류
                if (step == 1 && topFull) step = 2;
                if (step == 0 && bottomFull) step = 2;
            }
            if (step == 0 || step == 2) i++;
            if (step == 1 || step == 2) j++;
            Link(top[i], bottom[j]);
        }
    }

    private static void Link(RunMapNode a, RunMapNode b)
    {
        if (!a.Next.Contains(b.Index)) a.Next.Add(b.Index);
        if (!b.Prev.Contains(a.Index)) b.Prev.Add(a.Index);
    }

    private static void AssignTypes(RunMap map, Random rng)
    {
        for (int f = 0; f < map.BranchFloorCount; f++)
        {
            foreach (var node in map.Floors[f])
            {
                bool early = f + 1 < RestrictedUntilFloor;
                bool parentShop = f > 0 && node.Prev.Any(p => IsShop(map.Floors[f - 1][p].Type));
                var options = Weights.Where(w => !(early && IsRestricted(w.type)) &&
                                                 !(parentShop && IsShop(w.type))).ToArray();
                float total = options.Sum(o => o.weight);
                double draw = rng.NextDouble() * total;
                node.Type = options[options.Length - 1].type;
                foreach (var o in options)
                {
                    if (draw < o.weight) { node.Type = o.type; break; }
                    draw -= o.weight;
                }
            }
        }
    }

    /// <summary>
    /// 엘리트가 상한을 넘으면 무작위로 상한만큼 남기고, 나머지는 엘리트를 뺀 비율로 다시 뽑는다
    /// (앞층부터 채우지 않아 엘리트가 3 · 4층에 몰리지 않는다). 상점 연속 금지는 위 · 아래 둘 다 확인
    /// </summary>
    private static void CapEliteNodes(RunMap map, Random rng)
    {
        var elites = new List<RunMapNode>();
        for (int f = 0; f < map.BranchFloorCount; f++)
            elites.AddRange(map.Floors[f].Where(n => n.Type == RunMapNodeType.EliteBattle));
        if (elites.Count <= MaxEliteNodes) return;
        for (int k = elites.Count - 1; k > 0; k--)
        {
            int r = rng.Next(k + 1);
            (elites[k], elites[r]) = (elites[r], elites[k]);
        }
        foreach (var node in elites.Skip(MaxEliteNodes))
        {
            int f = node.Floor;
            bool nearShop = (f > 0 && node.Prev.Any(p => IsShop(map.Floors[f - 1][p].Type))) ||
                            (f + 1 < map.BranchFloorCount && node.Next.Any(x => IsShop(map.Floors[f + 1][x].Type)));
            var options = Weights.Where(w => w.type != RunMapNodeType.EliteBattle &&
                                             !(nearShop && IsShop(w.type))).ToArray();
            float total = options.Sum(o => o.weight);
            double draw = rng.NextDouble() * total;
            node.Type = options[options.Length - 1].type;
            foreach (var o in options)
            {
                if (draw < o.weight) { node.Type = o.type; break; }
                draw -= o.weight;
            }
        }
    }

    /// <summary>현재 위치에서 갈 수 있는 다음 노드들. floor = −1이면 시작(1층 전부).</summary>
    public static IReadOnlyList<RunMapNode> Reachable(RunMap map, int floor, int index)
    {
        if (map == null) return Array.Empty<RunMapNode>();
        if (floor < 0) return map.Floors[0];
        var node = map.Get(floor, index);
        if (node == null || floor + 1 >= map.Floors.Count) return Array.Empty<RunMapNode>();
        return node.Next.Select(n => map.Floors[floor + 1][n]).ToList();
    }

    /// <summary>규칙 위반 목록(검증용). 비어 있으면 통과.</summary>
    public static List<string> Validate(RunMap map)
    {
        var errors = new List<string>();
        var shape = ShapeFor(map.Stage);
        if (map.BranchFloorCount < shape.BranchMin || map.BranchFloorCount > shape.BranchMax)
            errors.Add("분기 층 수 " + map.BranchFloorCount);
        if (map.Floors[0].Count != shape.FirstWidth) errors.Add("1층 폭 " + map.Floors[0].Count);
        for (int f = 0; f < map.BranchFloorCount; f++)
        {
            var floor = map.Floors[f];
            if (f > 0 && (floor.Count < shape.WidthMin || floor.Count > shape.WidthMax)) errors.Add($"{f + 1}층 폭 {floor.Count}");
            foreach (var n in floor)
            {
                if (n.Next.Count < 1 || n.Next.Count > MaxBranches) errors.Add($"{f + 1}층 {n.Index} 갈래 {n.Next.Count}");
                if (f > 0 && n.Prev.Count < 1) errors.Add($"{f + 1}층 {n.Index} 들어오는 선 없음");
                if (f + 1 < RestrictedUntilFloor && IsRestricted(n.Type)) errors.Add($"{f + 1}층 {n.Type} 금지");
                if (IsShop(n.Type) && f + 1 < map.BranchFloorCount && n.Next.Any(x => IsShop(map.Floors[f + 1][x].Type)))
                    errors.Add($"{f + 1}층 상점 → 상점");
            }
            if (f + 1 < map.BranchFloorCount)
            {
                // 교차 검사 — (a→b), (c→d)에서 a<c인데 b>d면 교차
                var edges = floor.SelectMany(n => n.Next.Select(x => (n.Index, x))).ToList();
                foreach (var e1 in edges) foreach (var e2 in edges)
                    if (e1.Item1 < e2.Item1 && e1.Item2 > e2.Item2) { errors.Add($"{f + 1}층 선 교차"); goto NextFloor; }
            }
            NextFloor:;
        }
        int eliteCount = 0;
        for (int f = 0; f < map.BranchFloorCount; f++) eliteCount += map.Floors[f].Count(n => n.Type == RunMapNodeType.EliteBattle);
        if (eliteCount > MaxEliteNodes) errors.Add("엘리트 " + eliteCount + "개");
        if (map.Floors[map.ShopFloor].Count != 1 || map.Floors[map.ShopFloor][0].Type != RunMapNodeType.BossShop) errors.Add("보스 직전 고정 상점");
        if (map.Floors[map.BossFloor].Count != 1 || map.Floors[map.BossFloor][0].Type != RunMapNodeType.Boss) errors.Add("보스 층");
        return errors;
    }
}
