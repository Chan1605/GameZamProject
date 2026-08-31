using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 코스 안내 화살표를 링(Obstacle) 중앙마다 띄웁니다.
///
/// MapChunkSpawner가 청크들을 이어 붙여 만든 경로(PathPoints)를 그대로 읽습니다.
/// 그 좌표들이 곧 각 Obstacle의 Point(링 정중앙)라서, 화살표는 링 한가운데에 놓이고
/// <b>다음 링 방향</b>을 바라봅니다. 청크가 재활용돼 경로가 바뀌면 자동으로 따라갑니다.
///
/// <b>배치 방법</b>
///  1. 씬에 빈 GameObject를 만들고 이 컴포넌트를 붙입니다. (이름 예: CourseArrows)
///  2. Arrow Prefab에 Assets/2.Model/Prefabs/Arrow.prefab 을 넣습니다.
///  3. Spawner는 비워두면 씬에서 MapChunkSpawner를 알아서 찾습니다.
///
/// 화살표는 풀링해서 재사용하므로 매 프레임 Instantiate하지 않습니다.
/// </summary>
public class CourseArrowSpawner : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("경로를 만들어 주는 스포너. 비워두면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private MapChunkSpawner spawner;

    [Tooltip("띄울 화살표 프리팹. 루트에 CoursArrowPath가 있으면 그 Init()을 씁니다.")]
    [SerializeField] private GameObject arrowPrefab;

    [Header("위치")]
    [Tooltip("링 중앙을 기준으로 한 오프셋(m). 화살표가 바라보는 방향 기준입니다. " +
             "x=오른쪽, y=위, z=앞. (0,0,0)이면 링 정중앙입니다.")]
    [SerializeField] private Vector3 offset = Vector3.zero;

    [Tooltip("화살표 크기 배율. 1이면 프리팹 원본 크기 그대로입니다.")]
    [SerializeField, Min(0.01f)] private float scale = 1f;

    [Header("방향")]
    [Tooltip("켜면 화살표가 수평을 유지합니다(위아래 기울지 않음). " +
             "끄면 다음 링의 높이까지 반영해 위/아래로 기웁니다.")]
    [SerializeField] private bool keepLevel = false;

    [Header("표시 범위")]
    [Tooltip("마지막 지점에는 '다음'이 없어 화살표를 놓지 않습니다. " +
             "경로 맨 뒤 몇 개를 더 비울지 정합니다. 0이면 마지막 하나만 비웁니다.")]
    [SerializeField, Min(0)] private int trimTail = 0;

    private readonly List<GameObject> arrows = new List<GameObject>();
    private Transform container;

    // 경로가 바뀌었는지 알아보려고 마지막으로 반영한 상태를 기억해 둡니다.
    private int lastCount = -1;
    private Vector3 lastFirst, lastLast;

    private void Start()
    {
        if (spawner == null) spawner = FindFirstObjectByType<MapChunkSpawner>();

        if (spawner == null)
        {
            Debug.LogError("[CourseArrowSpawner] 씬에서 MapChunkSpawner를 찾지 못했습니다.", this);
            enabled = false;
            return;
        }

        if (arrowPrefab == null)
        {
            Debug.LogError("[CourseArrowSpawner] Arrow Prefab이 비어 있습니다. " +
                           "Assets/2.Model/Prefabs/Arrow.prefab 을 넣어주세요.", this);
            enabled = false;
            return;
        }

        container = new GameObject("Course Arrows").transform;
        Refresh();
    }

    private void LateUpdate()
    {
        // 청크가 재활용되면 MapChunkSpawner가 경로를 새로 만듭니다. 그때만 다시 배치합니다.
        if (HasPathChanged()) Refresh();
    }

    private bool HasPathChanged()
    {
        var points = spawner.PathPoints;
        if (points == null) return lastCount != 0;
        if (points.Count != lastCount) return true;
        if (points.Count == 0) return false;

        return points[0] != lastFirst || points[points.Count - 1] != lastLast;
    }

    private void Refresh()
    {
        var points = spawner.PathPoints;

        lastCount = points != null ? points.Count : 0;
        if (lastCount > 0)
        {
            lastFirst = points[0];
            lastLast = points[lastCount - 1];
        }

        // 마지막 지점은 '다음'이 없어 방향을 정할 수 없으므로 화살표를 놓지 않습니다.
        int wanted = Mathf.Max(0, lastCount - 1 - trimTail);

        EnsureCount(wanted);

        for (int i = 0; i < wanted; i++)
        {
            Place(arrows[i], points[i], points[i + 1]);
        }

        // 남는 화살표는 지우지 않고 꺼둡니다(다음 번에 다시 씁니다).
        for (int i = wanted; i < arrows.Count; i++)
        {
            if (arrows[i] != null) arrows[i].SetActive(false);
        }
    }

    private void EnsureCount(int wanted)
    {
        while (arrows.Count < wanted)
        {
            GameObject arrow = Instantiate(arrowPrefab, container);
            arrow.name = $"Arrow_{arrows.Count}";
            arrows.Add(arrow);
        }
    }

    private void Place(GameObject arrow, Vector3 point, Vector3 nextPoint)
    {
        if (arrow == null) return;

        Vector3 direction = nextPoint - point;
        if (keepLevel) direction.y = 0f;

        // 두 지점이 겹치면 LookRotation이 경고를 뱉으므로 이 화살표는 건너뜁니다.
        if (direction.sqrMagnitude < 0.0001f)
        {
            arrow.SetActive(false);
            return;
        }

        arrow.SetActive(true);
        arrow.transform.localScale = Vector3.one * scale;

        // 프리팹에 CoursArrowPath가 있으면 그 Init()으로 위치·회전을 맡깁니다.
        var path = arrow.GetComponent<CoursArrowPath>();
        if (path != null)
        {
            path.Init(point, keepLevel ? point + direction : nextPoint);
        }
        else
        {
            arrow.transform.SetPositionAndRotation(point, Quaternion.LookRotation(direction));
        }

        // 오프셋은 화살표가 바라보는 방향 기준으로 밀어줍니다(x=오른쪽, y=위, z=앞).
        if (offset != Vector3.zero)
            arrow.transform.position += arrow.transform.TransformDirection(offset);
    }

    private void OnDrawGizmosSelected()
    {
        if (spawner == null) return;

        var points = spawner.PathPoints;
        if (points == null) return;

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        for (int i = 1; i < points.Count; i++) Gizmos.DrawLine(points[i - 1], points[i]);
    }
}
