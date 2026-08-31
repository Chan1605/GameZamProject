using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장애물 무한 스크롤 스포너.
/// 프리팹 목록에서 무작위로 골라 앞쪽 멀리서 생성하고, 항상 같은 간격을 유지한 채
/// 플레이어 쪽으로 흘려보냅니다. 지나간 장애물은 파괴하지 않고 풀에 넣어 재사용합니다.
/// </summary>
public class ObstacleSpawner : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("스크롤 속도·방향의 기준. 비워두면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private ScrollController scroll;

    [Header("장애물")]
    [Tooltip("생성할 장애물 프리팹들. 여러 개를 넣으면 무작위로 골라 배치합니다.")]
    [SerializeField] private GameObject[] obstaclePrefabs;

    [Header("배치")]
    [Tooltip("장애물 사이의 간격(m). 속도와 무관하게 항상 이 간격이 유지됩니다.")]
    [SerializeField] private float spacing = 40f;
    [Tooltip("플레이어보다 얼마나 앞에서 생성할지(m). 카메라에 갑자기 튀어나오지 않을 만큼 넉넉히.")]
    [SerializeField] private float spawnDistance = 200f;
    [Tooltip("플레이어를 얼마나 지나치면 회수할지(m).")]
    [SerializeField] private float despawnDistance = 60f;
    [Tooltip("시작할 때 미리 레인을 장애물로 채웁니다. 끄면 첫 장애물이 올 때까지 앞이 비어 있습니다.")]
    [SerializeField] private bool prewarm = true;

    [Header("높이 랜덤 범위")]
    [SerializeField] private float minY = 2f;
    [SerializeField] private float maxY = 40f;

    [Header("회전")]
    [Tooltip("진행 축을 중심으로 무작위로 굴립니다. 링 게이트를 다양하게 보이게 할 때 사용하세요.")]
    [SerializeField] private bool randomRoll = false;

    private readonly Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();
    private readonly List<Spawned> active = new List<Spawned>();
    private Transform container;
    private float distanceAccum;

    private struct Spawned
    {
        public GameObject instance;
        public GameObject prefab;
    }

    /// <summary>현재 흘러가고 있는 장애물 수.</summary>
    public int ActiveCount => active.Count;

    private void Awake()
    {
        if (scroll == null) scroll = FindFirstObjectByType<ScrollController>();
        if (scroll == null)
        {
            Debug.LogError("[ObstacleSpawner] 씬에 ScrollController가 없습니다.", this);
            enabled = false;
            return;
        }

        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0)
        {
            Debug.LogError("[ObstacleSpawner] 장애물 프리팹이 하나도 등록되지 않았습니다.", this);
            enabled = false;
            return;
        }

        container = new GameObject("Obstacles (Pooled)").transform;
    }

    private void Start()
    {
        if (prewarm) Prewarm();
    }

    private void Update()
    {
        float step = scroll.DeltaDistance;
        if (step > 0f)
        {
            MoveActive(step);
            AdvanceSpawning(step);
        }

        RecyclePassed();
    }

    // 레인 전체를 미리 장애물로 채워, 시작하자마자 앞이 비어 보이지 않게 합니다.
    private void Prewarm()
    {
        float lane = spawnDistance + despawnDistance;
        for (float traveled = 0f; traveled < lane; traveled += spacing)
        {
            SpawnOne(traveled);
        }
    }

    private void MoveActive(float step)
    {
        Vector3 delta = scroll.MoveDirection * step;
        for (int i = 0; i < active.Count; i++)
        {
            active[i].instance.transform.position += delta;
        }
    }

    // 이동 거리를 누적해 정확히 spacing 간격마다 하나씩 생성합니다.
    // 남은 거리(distanceAccum)만큼 이미 진행한 위치에 놓으므로, 속도가 변해도 간격이 흐트러지지 않습니다.
    private void AdvanceSpawning(float step)
    {
        distanceAccum += step;
        while (distanceAccum >= spacing)
        {
            distanceAccum -= spacing;
            SpawnOne(distanceAccum);
        }
    }

    // 플레이어를 충분히 지나친 장애물을 풀로 돌려보냅니다.
    private void RecyclePassed()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            float along = scroll.DistanceAlongAxis(active[i].instance.transform.position);
            if (along < -despawnDistance)
            {
                Release(active[i]);
                active.RemoveAt(i);
            }
        }
    }

    /// <summary>지정한 거리만큼 이미 진행한 상태로 장애물 하나를 배치합니다.</summary>
    private void SpawnOne(float alreadyTraveled)
    {
        GameObject prefab = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];
        if (prefab == null) return;

        // 생성 위치: 원점 앞쪽 spawnDistance 지점에서 alreadyTraveled 만큼 되돌아온 곳
        Vector3 position = scroll.Origin + scroll.Forward * (spawnDistance - alreadyTraveled);
        position.y = Random.Range(minY, maxY);

        // 레인 회전 × 프리팹 자체 회전 → 레인을 돌려놔도 장애물 방향이 함께 따라옵니다.
        Quaternion rotation = scroll.transform.rotation * prefab.transform.rotation;
        if (randomRoll)
        {
            rotation = Quaternion.AngleAxis(Random.Range(0f, 360f), scroll.Forward) * rotation;
        }

        GameObject instance = Take(prefab);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);

        active.Add(new Spawned { instance = instance, prefab = prefab });
    }

    // ─── 오브젝트 풀링 ───────────────────────────────

    private GameObject Take(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out var queue) && queue.Count > 0)
        {
            return queue.Dequeue();
        }

        GameObject created = Instantiate(prefab, container);

        // 콜라이더를 매 프레임 Transform으로 움직일 때, 키네마틱 Rigidbody가 있어야
        // 물리엔진이 정적 콜라이더 구조를 통째로 다시 만들지 않아 훨씬 가볍습니다.
        if (!created.TryGetComponent(out Rigidbody rb)) rb = created.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        return created;
    }

    private void Release(Spawned spawned)
    {
        spawned.instance.SetActive(false);

        if (!pools.TryGetValue(spawned.prefab, out var queue))
        {
            queue = new Queue<GameObject>();
            pools[spawned.prefab] = queue;
        }
        queue.Enqueue(spawned.instance);
    }

    /// <summary>모든 장애물을 회수하고 처음부터 다시 시작합니다. (재시작용)</summary>
    public void ResetSpawner()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Release(active[i]);
        }
        active.Clear();
        distanceAccum = 0f;
        if (prewarm) Prewarm();
    }

    private void OnDrawGizmosSelected()
    {
        if (scroll == null) return;

        // 생성 지점(초록)과 회수 지점(빨강)을 씬 뷰에 표시합니다.
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(scroll.Origin + scroll.Forward * spawnDistance, 3f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(scroll.Origin - scroll.Forward * despawnDistance, 3f);
    }
}
