using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 맵(Group) 프리팹 안에 좌표를 찍어 기차가 따라갈 경로를 정의합니다.
///
/// <b>사용법</b>
///  1. Group 프리팹 루트에 이 컴포넌트를 붙입니다.
///  2. 빈 오브젝트를 진행 방향 순서대로 자식으로 만들어 경로 좌표로 씁니다.
///     (씬 뷰에서 보라색 구슬과 번호로 순서가 보입니다.)
///  3. Points 배열에 순서대로 등록하거나, Points Container를 지정하면
///     그 자식들을 하이러키 순서 그대로 자동 수집합니다.
///
/// <b>이음매 요령</b> — 이 조각의 첫 좌표는 로컬 Z ≈ 0 부근, 마지막 좌표는
/// 로컬 Z ≈ (이 프리팹의 진행축 길이) 부근에 두세요. MapChunkSpawner가 조각을
/// 정확히 그 길이만큼 이격해 배치하므로, 그렇게 해두면 이전 조각의 마지막 좌표와
/// 다음 조각의 첫 좌표가 자연스럽게 이어집니다.
/// </summary>
public class MapPath : MonoBehaviour
{
    [Header("경로 포인트")]
    [Tooltip("경로를 이루는 좌표들(진행 방향 순서). 비워두면 Points Container의 자식들을 순서대로 자동 수집합니다.")]
    [SerializeField] private Transform[] points;

    [Tooltip("points가 비어 있을 때 자동 수집 대상이 되는 컨테이너. 비워두면 이 오브젝트 자신의 자식들을 사용합니다.")]
    [SerializeField] private Transform pointsContainer;

    private Transform[] resolved;

    /// <summary>이 맵 조각에 등록된 경로 포인트 목록(순서대로).</summary>
    public IReadOnlyList<Transform> Points
    {
        get
        {
            if (resolved == null || resolved.Length == 0) Resolve();
            return resolved;
        }
    }

    private void Resolve()
    {
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
    /// 이 맵 조각이 씬(월드)에 배치된 상태 그대로, 경로 포인트들의 월드 좌표를
    /// 순서대로 target 리스트 뒤에 이어 붙입니다. (여러 조각을 이어 붙일 때 사용)
    /// </summary>
    public void AppendWorldPoints(List<Vector3> target)
    {
        var pts = Points;
        for (int i = 0; i < pts.Count; i++)
        {
            if (pts[i] != null) target.Add(pts[i].position);
        }
    }

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
    }
}
