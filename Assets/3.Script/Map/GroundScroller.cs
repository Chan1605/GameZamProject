using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 바닥 타일 무한 반복.
/// 같은 타일 여러 장을 앞뒤로 이어 붙여 흘려보내고, 뒤로 완전히 빠진 타일을
/// 맨 앞으로 되돌려 재사용합니다. 타일 수만 충분하면 끊김 없이 이어집니다.
/// </summary>
public class GroundScroller : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("스크롤 속도·방향의 기준. 비워두면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private ScrollController scroll;

    [Header("타일")]
    [Tooltip("반복할 바닥 타일 프리팹. Plane이나 ProBuilder로 만든 판 하나면 충분합니다.")]
    [SerializeField] private GameObject tilePrefab;
    [Tooltip("동시에 깔아둘 타일 수. 화면을 덮고도 남을 만큼 넉넉히 잡으세요.")]
    [SerializeField, Min(2)] private int tileCount = 6;
    [Tooltip("타일 하나의 진행 방향 길이(m). 0이면 프리팹 크기를 재서 자동 계산합니다.")]
    [SerializeField] private float tileLength = 0f;

    [Header("위치")]
    [Tooltip("바닥 높이(y).")]
    [SerializeField] private float groundY = 0f;
    [Tooltip("첫 타일을 플레이어보다 얼마나 뒤에서 시작할지(m).")]
    [SerializeField] private float startBehind = 60f;

    private readonly List<Transform> tiles = new List<Transform>();
    private Transform container;
    private float laneLength;

    private void Awake()
    {
        if (scroll == null) scroll = FindFirstObjectByType<ScrollController>();
        if (scroll == null)
        {
            Debug.LogError("[GroundScroller] 씬에 ScrollController가 없습니다.", this);
            enabled = false;
            return;
        }

        if (tilePrefab == null)
        {
            Debug.LogError("[GroundScroller] 바닥 타일 프리팹이 비어 있습니다.", this);
            enabled = false;
            return;
        }

        container = new GameObject("Ground Tiles").transform;
        BuildTiles();
    }

    private void BuildTiles()
    {
        for (int i = 0; i < tileCount; i++)
        {
            GameObject go = Instantiate(tilePrefab, container);
            go.name = $"GroundTile_{i:00}";
            tiles.Add(go.transform);

            // 첫 타일을 만든 직후에 실제 크기를 재서 길이를 확정합니다.
            if (i == 0 && tileLength <= 0f) tileLength = MeasureLength(go);
        }

        if (tileLength <= 0f)
        {
            Debug.LogWarning("[GroundScroller] 타일 길이를 잴 수 없어 10m로 가정합니다. Tile Length를 직접 입력하세요.", this);
            tileLength = 10f;
        }

        laneLength = tileLength * tileCount;

        for (int i = 0; i < tiles.Count; i++)
        {
            PlaceAt(tiles[i], -startBehind + tileLength * i);
        }
    }

    // 렌더러 전체 크기를 구해 진행 축 방향 길이만 뽑아냅니다.
    private float MeasureLength(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 0f;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        return Mathf.Abs(Vector3.Dot(bounds.size, scroll.Forward));
    }

    private void PlaceAt(Transform tile, float distanceAlongAxis)
    {
        Vector3 position = scroll.Origin + scroll.Forward * distanceAlongAxis;
        position.y = groundY;
        tile.SetPositionAndRotation(position, scroll.transform.rotation * tilePrefab.transform.rotation);
    }

    private void Update()
    {
        float step = scroll.DeltaDistance;
        Vector3 delta = scroll.MoveDirection * step;

        for (int i = 0; i < tiles.Count; i++)
        {
            Transform tile = tiles[i];
            if (step > 0f) tile.position += delta;

            // 뒤로 완전히 빠진 타일은 편성 맨 앞으로 되돌립니다.
            float along = scroll.DistanceAlongAxis(tile.position);
            if (along < -startBehind - tileLength)
            {
                Vector3 position = tile.position + scroll.Forward * laneLength;
                position.y = groundY;
                tile.position = position;
            }
        }
    }
}
