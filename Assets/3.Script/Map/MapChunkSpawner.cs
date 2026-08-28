using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 맵(Group) 프리팹을 진행 방향으로 이어 붙여 무한한 트랙을 만듭니다.
/// GroundScroller와 달리 조각들은 스스로 흐르지 않고 제자리에 고정되며,
/// 대신 기차(TrainPathFollower)가 그 조각들이 품고 있는 MapPath 좌표를 따라
/// 앞으로 나아갑니다.
///
/// <b>준비물</b>
///  - chunkPrefab(Group) 루트에 MapPath 컴포넌트가 있어야 합니다.
///  - Chunk Length를 비워두면(0) 첫 조각을 만들 때 렌더러 크기로 자동 측정합니다.
///    Tools ▸ 맵 크기 측정 (Group)으로 미리 정확한 값을 재서 직접 입력해도 됩니다
///    (자동 측정과 결과가 같아야 정상입니다 — 다르면 프리팹에 숨은 렌더러가 있는지 확인하세요).
/// </summary>
public class MapChunkSpawner : MonoBehaviour
{
    [Header("맵 조각")]
    [Tooltip("반복 배치할 맵(Group) 프리팹. 루트에 MapPath가 있어야 합니다.")]
    [SerializeField] private GameObject chunkPrefab;
    [Tooltip("동시에 유지할 조각 수. 최소 3장 이상 권장(현재 조각 + 앞뒤 여유).")]
    [SerializeField, Min(2)] private int chunkCount = 4;
    [Tooltip("조각 하나의 진행 축 길이(m). 0이면 첫 조각을 만들 때 렌더러 크기로 자동 측정합니다.")]
    [SerializeField] private float chunkLength = 0f;

    [Header("진행 축")]
    [Tooltip("조각을 이어 붙일 기준. 비워두면 이 오브젝트의 forward를 사용합니다.")]
    [SerializeField] private Transform axisReference;

    [Header("정리")]
    [Tooltip("기차보다 이만큼 뒤로 빠지면 조각을 회수해 맨 앞으로 재배치합니다.")]
    [SerializeField] private float despawnBehind = 40f;
    [Tooltip("경로를 따라가는 기차. 비워두면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private TrainPathFollower train;

    private readonly List<Transform> chunks = new List<Transform>();
    private readonly List<Vector3> pathPoints = new List<Vector3>();
    private Transform container;
    private Vector3 axisForward;
    private Vector3 axisOrigin;
    private Quaternion axisRotation;

    /// <summary>현재 이어붙여진 조각들의 전체 경로 좌표 (월드 스페이스, 진행 순서).</summary>
    public IReadOnlyList<Vector3> PathPoints => pathPoints;

    private void Awake()
    {
        if (chunkPrefab == null)
        {
            Debug.LogError("[MapChunkSpawner] 맵 조각 프리팹이 비어 있습니다.", this);
            enabled = false;
            return;
        }

        if (chunkPrefab.GetComponent<MapPath>() == null)
        {
            Debug.LogError("[MapChunkSpawner] 맵 조각 프리팹 루트에 MapPath 컴포넌트가 없습니다.", this);
            enabled = false;
            return;
        }

        if (train == null) train = FindFirstObjectByType<TrainPathFollower>();

        Transform axis = axisReference != null ? axisReference : transform;
        axisForward = axis.forward;
        axisOrigin = axis.position;
        axisRotation = Quaternion.LookRotation(axisForward, Vector3.up);

        container = new GameObject("Map Chunks").transform;
        BuildInitialChunks();
        RebuildPath();

        if (train != null) train.SetPath(pathPoints);
    }

    private void BuildInitialChunks()
    {
        for (int i = 0; i < chunkCount; i++)
        {
            SpawnChunkAt(i);
        }
    }

    private void SpawnChunkAt(int index)
    {
        GameObject go = Instantiate(chunkPrefab, container);
        go.name = $"{chunkPrefab.name}_{index:00}";

        if (index == 0 && chunkLength <= 0f) chunkLength = MeasureLength(go);

        Vector3 position = axisOrigin + axisForward * (chunkLength * index);
        go.transform.SetPositionAndRotation(position, axisRotation * chunkPrefab.transform.rotation);

        chunks.Add(go.transform);
    }

    // GroundScroller.MeasureLength와 같은 방식: 렌더러 전체를 감싸는 바운드를 구해
    // 진행 축 방향 길이만 뽑아냅니다.
    private float MeasureLength(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return FallbackLength();

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        float measured = Mathf.Abs(Vector3.Dot(bounds.size, axisForward));
        return measured > 0f ? measured : FallbackLength();
    }

    private float FallbackLength()
    {
        Debug.LogWarning("[MapChunkSpawner] 조각 길이를 잴 수 없어 100m로 가정합니다. Chunk Length를 직접 입력하세요.", this);
        return 100f;
    }

    private void Update()
    {
        if (train == null)
        {
            train = FindFirstObjectByType<TrainPathFollower>();
            if (train == null) return;
        }

        float trainDistance = Vector3.Dot(train.transform.position - axisOrigin, axisForward);

        bool changed = false;
        for (int i = 0; i < chunks.Count; i++)
        {
            float along = Vector3.Dot(chunks[i].position - axisOrigin, axisForward);
            if (along < trainDistance - despawnBehind - chunkLength)
            {
                float frontMost = FrontMostDistance();
                Vector3 position = axisOrigin + axisForward * (frontMost + chunkLength);
                chunks[i].SetPositionAndRotation(position, axisRotation * chunkPrefab.transform.rotation);
                changed = true;
            }
        }

        if (changed)
        {
            RebuildPath();
            if (train != null) train.SetPath(pathPoints);
        }
    }

    private float FrontMostDistance()
    {
        float max = float.NegativeInfinity;
        for (int i = 0; i < chunks.Count; i++)
        {
            float along = Vector3.Dot(chunks[i].position - axisOrigin, axisForward);
            if (along > max) max = along;
        }
        return max;
    }

    // 조각들을 진행 축 기준으로 정렬한 뒤, 각 조각의 MapPath 좌표를 순서대로 이어 붙입니다.
    private void RebuildPath()
    {
        pathPoints.Clear();

        var ordered = new List<Transform>(chunks);
        ordered.Sort((a, b) =>
        {
            float da = Vector3.Dot(a.position - axisOrigin, axisForward);
            float db = Vector3.Dot(b.position - axisOrigin, axisForward);
            return da.CompareTo(db);
        });

        for (int i = 0; i < ordered.Count; i++)
        {
            var path = ordered[i].GetComponent<MapPath>();
            if (path != null) path.AppendWorldPoints(pathPoints);
        }
    }
}
