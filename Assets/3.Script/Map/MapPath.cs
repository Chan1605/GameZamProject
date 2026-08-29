using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 청크(맵 조각) 프리팹 안에 좌표를 찍어 기차가 따라갈 경로를 정의합니다.
///
/// <b>사용법</b>
///  1. 청크 프리팹 루트에 이 컴포넌트를 붙입니다.
///  2. 경로로 쓸 Transform(보통 각 Obstacle의 Point)들을 진행 방향 순서대로
///     Points 배열에 등록합니다. 비워두면 Points Container의 자식들을 순서대로 씁니다.
///     (씬 뷰에서 보라색 구슬과 번호로 순서가 보입니다.)
///
/// <b>이음매 규칙</b> — 변형을 여러 개 만들어 랜덤으로 붙이려면 모든 변형이
/// ChunkContract가 정한 규격을 지켜야 합니다.
///  · 첫 웨이포인트 = 로컬 (seamX, 자유, entryZ)
///  · 마지막 웨이포인트 = 로컬 (seamX, 자유, exitZ)
///  · 청크 길이 = chunkLength
/// y는 맞출 필요 없습니다(점프·중력이 담당하는 축이라 경로가 관여하지 않습니다).
/// Contract 슬롯을 채워두면 씬 뷰 기즈모와 Tools ▸ Zino ▸ 청크 규격 검사가
/// 어긋난 부분을 바로 알려줍니다.
/// </summary>
public class MapPath : MonoBehaviour
{
    [Header("경로 포인트")]
    [Tooltip("경로를 이루는 좌표들(진행 방향 순서). 비워두면 Points Container의 자식들을 순서대로 자동 수집합니다.")]
    [SerializeField] private Transform[] points;

    [Tooltip("points가 비어 있을 때 자동 수집 대상이 되는 컨테이너. 비워두면 이 오브젝트 자신의 자식들을 사용합니다.")]
    [SerializeField] private Transform pointsContainer;

    [Header("규격")]
    [Tooltip("이 청크가 지켜야 할 규격 에셋. 비워두면 검사와 기즈모 표시를 건너뜁니다.")]
    [SerializeField] private ChunkContract contract;

    private Transform[] resolved;
    private bool resolvedOnce;

    /// <summary>이 청크에 등록된 경로 포인트 목록(순서대로).</summary>
    public IReadOnlyList<Transform> Points
    {
        get
        {
            if (!resolvedOnce || resolved == null) Resolve();
            return resolved;
        }
    }

    public ChunkContract Contract => contract;

    /// <summary>첫 웨이포인트의 청크 로컬 좌표.</summary>
    public Vector3 EntryLocal => LocalOf(0);

    /// <summary>마지막 웨이포인트의 청크 로컬 좌표.</summary>
    public Vector3 ExitLocal => LocalOf(Points.Count - 1);

    private Vector3 LocalOf(int index)
    {
        var pts = Points;
        if (pts == null || index < 0 || index >= pts.Count || pts[index] == null) return Vector3.zero;
        return transform.InverseTransformPoint(pts[index].position);
    }

    private void Resolve()
    {
        resolvedOnce = true;

        if (points != null && points.Length > 0)
        {
            resolved = points;
            return;
        }

        Transform root = pointsContainer != null ? pointsContainer : transform;
        int count = root.childCount;
        resolved = new Transform[count];
        for (int i = 0; i < count; i++) resolved[i] = root.GetChild(i);
    }

    /// <summary>
    /// 이 청크가 씬(월드)에 배치된 상태 그대로, 경로 포인트들의 월드 좌표를
    /// 순서대로 target 리스트 뒤에 이어 붙입니다. (여러 청크를 이어 붙일 때 사용)
    /// </summary>
    public void AppendWorldPoints(List<Vector3> target)
    {
        var pts = Points;
        for (int i = 0; i < pts.Count; i++)
        {
            if (pts[i] != null) target.Add(pts[i].position);
        }
    }

    /// <summary>
    /// 규격을 지키고 있는지 검사합니다. 어긋난 항목이 있으면 false와 사유를 돌려줍니다.
    /// </summary>
    public bool Validate(ChunkContract rule, out string message)
    {
        if (rule == null || !rule.IsValid)
        {
            message = "규격 에셋이 없거나 Chunk Length가 0입니다.";
            return false;
        }

        var pts = Points;
        if (pts == null || pts.Count < 2)
        {
            message = "웨이포인트가 2개 미만입니다.";
            return false;
        }

        var problems = new List<string>();
        Vector3 entry = EntryLocal;
        Vector3 exit = ExitLocal;
        float tol = Mathf.Max(0.001f, rule.tolerance);

        if (Mathf.Abs(entry.x - rule.seamX) > tol)
            problems.Add($"첫 웨이포인트 x = {entry.x:F2} (규격 {rule.seamX:F2}, 차이 {entry.x - rule.seamX:+0.00;-0.00})");
        if (Mathf.Abs(exit.x - rule.seamX) > tol)
            problems.Add($"마지막 웨이포인트 x = {exit.x:F2} (규격 {rule.seamX:F2}, 차이 {exit.x - rule.seamX:+0.00;-0.00})");
        if (Mathf.Abs(entry.z - rule.entryZ) > tol)
            problems.Add($"첫 웨이포인트 z = {entry.z:F2} (규격 {rule.entryZ:F2})");
        if (Mathf.Abs(exit.z - rule.exitZ) > tol)
            problems.Add($"마지막 웨이포인트 z = {exit.z:F2} (규격 {rule.exitZ:F2})");

        // 진행 방향(z)이 뒤로 가는 구간이 있으면 경로가 꼬입니다.
        for (int i = 1; i < pts.Count; i++)
        {
            if (pts[i] == null || pts[i - 1] == null) continue;
            float dz = transform.InverseTransformPoint(pts[i].position).z
                     - transform.InverseTransformPoint(pts[i - 1].position).z;
            if (dz <= 0f) problems.Add($"{i - 1}번 → {i}번 웨이포인트가 z로 전진하지 않습니다 (Δz = {dz:F2}).");
        }

        message = problems.Count == 0
            ? $"OK — 길이 {rule.chunkLength:F2}, 이음매 x {rule.seamX:F2}, 이음 간격 {rule.SeamGap:F2}"
            : string.Join("\n    · ", problems);
        return problems.Count == 0;
    }

#if UNITY_EDITOR
    // 인스펙터에서 Points를 고치면 즉시 다시 읽어들이도록 캐시를 비웁니다.
    private void OnValidate()
    {
        resolvedOnce = false;
        resolved = null;
    }
#endif

    private void OnDrawGizmos()
    {
        var pts = Points;
        if (pts == null || pts.Count == 0) return;

        Gizmos.color = new Color(1f, 0f, 1f, 0.9f);
        for (int i = 0; i < pts.Count; i++)
        {
            if (pts[i] == null) continue;
            Gizmos.DrawSphere(pts[i].position, 0.5f);
            if (i > 0 && pts[i - 1] != null) Gizmos.DrawLine(pts[i - 1].position, pts[i].position);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(pts[i].position + Vector3.up * 0.7f, i.ToString());
#endif
        }

        if (contract == null || !contract.IsValid) return;

        // 규격이 요구하는 이음매 위치를 초록(맞음)/빨강(어긋남)으로 표시합니다.
        DrawSeamMarker(contract.EntryLocal, EntryLocal, "IN");
        DrawSeamMarker(contract.ExitLocal, ExitLocal, "OUT");

        // 청크 경계(다음 청크가 시작되는 면)
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Vector3 near = transform.TransformPoint(new Vector3(contract.seamX, 0f, contract.entryZ));
        Vector3 far = transform.TransformPoint(new Vector3(contract.seamX, 0f, contract.entryZ + contract.chunkLength));
        Gizmos.DrawLine(near + Vector3.left * 200f, near + Vector3.right * 200f);
        Gizmos.DrawLine(far + Vector3.left * 200f, far + Vector3.right * 200f);
    }

    private void DrawSeamMarker(Vector3 wantedLocal, Vector3 actualLocal, string label)
    {
        float tol = Mathf.Max(0.001f, contract.tolerance);
        bool ok = Mathf.Abs(actualLocal.x - wantedLocal.x) <= tol
               && Mathf.Abs(actualLocal.z - wantedLocal.z) <= tol;

        Vector3 wanted = transform.TransformPoint(new Vector3(wantedLocal.x, actualLocal.y, wantedLocal.z));
        Gizmos.color = ok ? new Color(0.2f, 1f, 0.4f, 0.9f) : new Color(1f, 0.25f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(wanted, new Vector3(6f, 6f, 6f));
#if UNITY_EDITOR
        UnityEditor.Handles.Label(wanted + Vector3.up * 5f, ok ? label : label + " ✗");
#endif
    }
}
