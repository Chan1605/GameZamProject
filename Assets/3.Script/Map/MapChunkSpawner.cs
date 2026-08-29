using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 청크(맵 조각) 프리팹을 진행 방향으로 이어 붙여 무한한 트랙을 만듭니다.
/// 조각들은 스스로 흐르지 않고 제자리에 고정되며, 대신 기차(TrainPathFollower)가
/// 조각들이 품고 있는 MapPath 좌표를 따라 앞으로 나아갑니다.
/// 뒤로 완전히 빠진 조각은 회수해 맨 앞에 <b>다시 뽑은 변형</b>으로 다시 놓습니다.
///
/// <b>변형(Variant)</b> — Chunk Variants에 프리팹을 여러 개 넣어두면 매번 랜덤으로
/// 골라 붙이므로 같은 풍경이 반복되지 않습니다. 변형끼리 자연스럽게 이어지려면
/// 모두 같은 ChunkContract 규격(길이·이음매 x)을 지켜야 하며, 지키지 않은 변형은
/// 플레이 시작 시 콘솔에 경고로 알려줍니다.
///
/// <b>준비물</b>
///  - 각 변형 프리팹 루트에 MapPath 컴포넌트
///  - Contract 슬롯에 청크 규격 에셋 (Tools ▸ Zino ▸ 1) 규격 측정·에셋 갱신)
/// </summary>
public class MapChunkSpawner : MonoBehaviour
{
    [System.Serializable]
    public class ChunkVariant
    {
        [Tooltip("이어 붙일 청크 프리팹. 루트에 MapPath가 있어야 합니다.")]
        public GameObject prefab;

        [Tooltip("뽑힐 확률의 가중치. 2면 1짜리보다 두 배 자주 나옵니다. 0이면 뽑지 않습니다.")]
        [Min(0f)] public float weight = 1f;
    }

    [Header("규격")]
    [Tooltip("모든 변형이 공유하는 청크 규격. 길이는 여기 값을 씁니다.")]
    [SerializeField] private ChunkContract contract;

    [Tooltip("규격 에셋이 없을 때만 쓰는 길이(m). 0이면 첫 조각의 렌더러 크기로 자동 측정합니다.")]
    [SerializeField] private float fallbackChunkLength = 0f;

    [Header("청크 변형")]
    [Tooltip("랜덤으로 골라 이어 붙일 청크 프리팹들. 하나만 넣으면 그것만 반복됩니다.")]
    [SerializeField] private List<ChunkVariant> chunkVariants = new List<ChunkVariant>();

    [Tooltip("동시에 유지할 조각 수. 최소 3장 이상 권장(현재 조각 + 앞뒤 여유).")]
    [SerializeField, Min(2)] private int chunkCount = 3;

    [Tooltip("첫 청크를 이만큼 앞쪽에 놓습니다(m). 열차가 첫 게이트에 겹쳐서 출발하지 않고 " +
             "달려와서 통과하도록 활주 구간을 만들어 줍니다. 0이면 열차가 첫 게이트 안에서 시작합니다.")]
    [SerializeField] private float startOffset = 150f;

    [Header("랜덤")]
    [Tooltip("같은 변형이 연속으로 두 번 나오지 않게 합니다(변형이 2개 이상일 때).")]
    [SerializeField] private bool avoidImmediateRepeat = true;

    [Tooltip("게임 시작 시 항상 이 변형부터 시작합니다. -1이면 첫 조각도 랜덤입니다.")]
    [SerializeField] private int startVariantIndex = 0;

    [Tooltip("0이 아니면 매번 같은 순서로 나옵니다(테스트용 고정 시드).")]
    [SerializeField] private int randomSeed = 0;

    [Header("진행 축")]
    [Tooltip("조각을 이어 붙일 기준. 비워두면 이 오브젝트의 forward를 사용합니다.")]
    [SerializeField] private Transform axisReference;

    [Header("정리")]
    [Tooltip("기차보다 이만큼 뒤로 빠지면 조각을 회수해 맨 앞으로 재배치합니다.")]
    [SerializeField] private float despawnBehind = 40f;

    [Tooltip("경로를 따라가는 기차. 비워두면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private TrainPathFollower train;

    [Header("에디터 편집용")]
    [Tooltip("씬에 눈으로 보라고 놔둔 청크 인스턴스. 플레이가 시작되면 자동으로 지워지고 " +
             "그 자리에 스포너가 만든 조각이 들어갑니다. 비워둬도 됩니다.")]
    [SerializeField] private GameObject editorPreviewChunk;

    private class Slot
    {
        public Transform transform;
        public GameObject go;
        public int variantIndex;
    }

    private readonly List<Slot> slots = new List<Slot>();
    private readonly List<Vector3> pathPoints = new List<Vector3>();
    private List<Stack<GameObject>> pools;
    private Transform container;
    private Vector3 axisForward;
    private Vector3 axisOrigin;
    private Quaternion axisRotation;
    private float chunkLength;
    private int lastVariantIndex = -1;
    private System.Random rng;

    /// <summary>현재 이어붙여진 조각들의 전체 경로 좌표 (월드 스페이스, 진행 순서).</summary>
    public IReadOnlyList<Vector3> PathPoints => pathPoints;

    /// <summary>실제로 사용 중인 청크 길이(m).</summary>
    public float ChunkLength => chunkLength;

    private void Awake()
    {
        if (CountUsableVariants() == 0)
        {
            Debug.LogError("[MapChunkSpawner] Chunk Variants가 비어 있습니다. 청크 프리팹을 최소 1개 넣어주세요.", this);
            enabled = false;
            return;
        }

        rng = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();

        if (train == null) train = FindFirstObjectByType<TrainPathFollower>();

        Transform axis = axisReference != null ? axisReference : transform;
        axisForward = axis.forward;
        axisOrigin = axis.position;
        axisRotation = Quaternion.LookRotation(axisForward, Vector3.up);

        // 에디터에서 눈으로 보려고 놔둔 원본은 플레이 시작과 동시에 치웁니다.
        if (editorPreviewChunk != null) Destroy(editorPreviewChunk);

        ResolveChunkLength();
        ValidateVariants();

        pools = new List<Stack<GameObject>>(chunkVariants.Count);
        for (int i = 0; i < chunkVariants.Count; i++) pools.Add(new Stack<GameObject>());

        container = new GameObject("Map Chunks").transform;
        BuildInitialChunks();
        RebuildPath();

        if (train != null) train.SetPath(pathPoints);
    }

    private int CountUsableVariants()
    {
        int n = 0;
        for (int i = 0; i < chunkVariants.Count; i++)
            if (chunkVariants[i] != null && chunkVariants[i].prefab != null) n++;
        return n;
    }

    private void ResolveChunkLength()
    {
        if (contract != null && contract.IsValid)
        {
            chunkLength = contract.chunkLength;
            return;
        }

        if (fallbackChunkLength > 0f)
        {
            chunkLength = fallbackChunkLength;
            return;
        }

        // 규격 에셋도 직접 입력값도 없으면 첫 변형의 렌더러 크기로 잽니다.
        GameObject probe = Instantiate(FirstPrefab());
        probe.transform.SetPositionAndRotation(axisOrigin, axisRotation);
        chunkLength = MeasureLength(probe);
        DestroyImmediate(probe); // 한 프레임이라도 화면에 비치지 않게 즉시 정리합니다.

        if (chunkLength <= 0f)
        {
            chunkLength = 100f;
            Debug.LogWarning("[MapChunkSpawner] 청크 길이를 잴 수 없어 100m로 가정합니다. " +
                             "Contract 에셋을 지정하거나 Fallback Chunk Length를 직접 입력하세요.", this);
        }
    }

    private GameObject FirstPrefab()
    {
        for (int i = 0; i < chunkVariants.Count; i++)
            if (chunkVariants[i] != null && chunkVariants[i].prefab != null) return chunkVariants[i].prefab;
        return null;
    }

    private float MeasureLength(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 0f;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        return Mathf.Abs(Vector3.Dot(bounds.size, axisForward));
    }

    // 변형들이 같은 규격을 지키는지 확인하고, 어긋나면 콘솔에 알려줍니다.
    private void ValidateVariants()
    {
        for (int i = 0; i < chunkVariants.Count; i++)
        {
            var variant = chunkVariants[i];
            if (variant == null || variant.prefab == null) continue;

            var path = variant.prefab.GetComponent<MapPath>();
            if (path == null)
            {
                Debug.LogError($"[MapChunkSpawner] '{variant.prefab.name}' 루트에 MapPath가 없습니다.", this);
                continue;
            }

            if (contract == null || !contract.IsValid) continue;
            if (!path.Validate(contract, out string message))
            {
                Debug.LogWarning($"[MapChunkSpawner] 규격에 어긋난 청크 '{variant.prefab.name}'\n    · {message}", this);
            }
        }
    }

    private void BuildInitialChunks()
    {
        for (int i = 0; i < chunkCount; i++)
        {
            int variantIndex = (i == 0 && IsValidVariantIndex(startVariantIndex))
                ? startVariantIndex
                : PickVariant();

            lastVariantIndex = variantIndex;
            slots.Add(Spawn(variantIndex, startOffset + chunkLength * i));
        }
    }

    private bool IsValidVariantIndex(int index)
    {
        return index >= 0 && index < chunkVariants.Count
            && chunkVariants[index] != null && chunkVariants[index].prefab != null;
    }

    // 가중치를 반영해 변형을 하나 고릅니다. 직전과 같은 것은 (설정에 따라) 피합니다.
    private int PickVariant()
    {
        float total = 0f;
        for (int i = 0; i < chunkVariants.Count; i++)
        {
            var v = chunkVariants[i];
            if (v == null || v.prefab == null || v.weight <= 0f) continue;
            if (avoidImmediateRepeat && i == lastVariantIndex && CountUsableVariants() > 1) continue;
            total += v.weight;
        }

        if (total <= 0f) return lastVariantIndex >= 0 ? lastVariantIndex : FirstVariantIndex();

        float roll = (float)rng.NextDouble() * total;
        for (int i = 0; i < chunkVariants.Count; i++)
        {
            var v = chunkVariants[i];
            if (v == null || v.prefab == null || v.weight <= 0f) continue;
            if (avoidImmediateRepeat && i == lastVariantIndex && CountUsableVariants() > 1) continue;

            roll -= v.weight;
            if (roll <= 0f) return i;
        }
        return FirstVariantIndex();
    }

    private int FirstVariantIndex()
    {
        for (int i = 0; i < chunkVariants.Count; i++)
            if (chunkVariants[i] != null && chunkVariants[i].prefab != null) return i;
        return 0;
    }

    private Slot Spawn(int variantIndex, float alongAxis)
    {
        GameObject prefab = chunkVariants[variantIndex].prefab;

        GameObject go = pools[variantIndex].Count > 0 ? pools[variantIndex].Pop() : Instantiate(prefab, container);
        go.name = $"{prefab.name}_{alongAxis:F0}";
        go.SetActive(true);
        go.transform.SetPositionAndRotation(
            axisOrigin + axisForward * alongAxis,
            axisRotation * prefab.transform.rotation);

        return new Slot { transform = go.transform, go = go, variantIndex = variantIndex };
    }

    private void Update()
    {
        if (train == null)
        {
            train = FindFirstObjectByType<TrainPathFollower>();
            if (train == null) return;
        }

        float trainDistance = Vector3.Dot(train.transform.position - axisOrigin, axisForward);
        float recycleLine = trainDistance - despawnBehind - chunkLength;

        bool changed = false;
        for (int i = 0; i < slots.Count; i++)
        {
            float along = Vector3.Dot(slots[i].transform.position - axisOrigin, axisForward);
            if (along >= recycleLine) continue;

            // 뒤로 빠진 조각은 풀에 넣어두고, 맨 앞에는 새로 뽑은 변형을 놓습니다.
            float frontMost = FrontMostDistance();
            Recycle(slots[i]);

            int variantIndex = PickVariant();
            lastVariantIndex = variantIndex;
            slots[i] = Spawn(variantIndex, frontMost + chunkLength);
            changed = true;
        }

        if (changed)
        {
            RebuildPath();
            train.SetPath(pathPoints);
        }
    }

    private void Recycle(Slot slot)
    {
        slot.go.SetActive(false);
        slot.go.transform.SetParent(container, false);
        pools[slot.variantIndex].Push(slot.go);
    }

    private float FrontMostDistance()
    {
        float max = float.NegativeInfinity;
        for (int i = 0; i < slots.Count; i++)
        {
            float along = Vector3.Dot(slots[i].transform.position - axisOrigin, axisForward);
            if (along > max) max = along;
        }
        return max;
    }

    // 조각들을 진행 축 기준으로 정렬한 뒤, 각 조각의 MapPath 좌표를 순서대로 이어 붙입니다.
    private void RebuildPath()
    {
        pathPoints.Clear();

        var ordered = new List<Slot>(slots);
        ordered.Sort((a, b) =>
        {
            float da = Vector3.Dot(a.transform.position - axisOrigin, axisForward);
            float db = Vector3.Dot(b.transform.position - axisOrigin, axisForward);
            return da.CompareTo(db);
        });

        for (int i = 0; i < ordered.Count; i++)
        {
            var path = ordered[i].transform.GetComponent<MapPath>();
            if (path != null) path.AppendWorldPoints(pathPoints);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // 플레이 전에도 청크가 어디서 끊기는지 씬 뷰에서 보이도록 경계면을 그립니다.
        float length = contract != null && contract.IsValid ? contract.chunkLength : fallbackChunkLength;
        if (length <= 0f) return;

        Transform axis = axisReference != null ? axisReference : transform;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.7f);

        for (int i = 0; i <= chunkCount; i++)
        {
            Vector3 p = axis.position + axis.forward * (startOffset + length * i);
            Vector3 side = axis.right * 250f;
            Gizmos.DrawLine(p - side, p + side);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(p + axis.right * 255f, $"{i} ({length * i:F0}m)");
#endif
        }
    }
}
