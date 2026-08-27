using System;
using UnityEngine;

/// <summary>
/// 열차 한 량(車輛). 편성 내 위치를 나타내는 CarIndex를 가집니다. (0 = 기관차, 뒤로 갈수록 1, 2, 3...)
/// 앞 차량과는 TrainCoupler(연결부)로 이어지며, Detach()를 호출하면 연결이 끊기고 물리적으로 추락합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TrainCar : MonoBehaviour
{
    [Header("식별")]
    [Tooltip("편성 내 순번. 0 = 기관차(맨 앞). TrainConsist가 자동으로 부여합니다.")]
    [SerializeField] private int carIndex;

    [Header("연결 지점 (비워두면 모델 크기에서 자동 계산)")]
    [Tooltip("앞 차량과 이어지는 지점. 비워두면 모델의 앞쪽 끝(+Z)이 사용됩니다.")]
    [SerializeField] private Transform frontCouplingPoint;
    [Tooltip("뒤 차량과 이어지는 지점. 비워두면 모델의 뒤쪽 끝(-Z)이 사용됩니다.")]
    [SerializeField] private Transform rearCouplingPoint;

    [Header("자동 설정")]
    [Tooltip("콜라이더가 하나도 없으면 모델 크기에 맞는 BoxCollider를 자동으로 붙입니다.")]
    [SerializeField] private bool autoFitCollider = true;
    [Tooltip("모든 차량의 질량을 같게 맞추면 조인트가 훨씬 안정적입니다.")]
    [SerializeField] private bool overrideMass = true;
    [SerializeField] private float mass = 1f;

    [Header("분리 연출")]
    [Tooltip("연결이 끊길 때 튕겨나가는 힘")]
    [SerializeField] private float detachImpulse = 1.5f;
    [Tooltip("연결이 끊길 때 회전하며 구르는 정도")]
    [SerializeField] private float detachTorque = 4f;
    [Tooltip("분리 후 이 시간(초)이 지나면 오브젝트를 제거합니다. 0이면 제거하지 않습니다.")]
    [SerializeField] private float destroyDelay = 6f;

    private Rigidbody rb;
    private Bounds localBounds;
    private bool initialized;

    /// <summary>편성 내 순번. 0 = 기관차.</summary>
    public int CarIndex
    {
        get => carIndex;
        internal set => carIndex = value;
    }

    /// <summary>이미 분리되어 떨어진 차량인지 여부.</summary>
    public bool IsDetached { get; private set; }

    public Rigidbody Body
    {
        get { Initialize(); return rb; }
    }

    /// <summary>이 차량을 앞 차량에 매달고 있는 연결부. 기관차는 null.</summary>
    public TrainCoupler FrontCoupler { get; internal set; }

    /// <summary>이 차량이 무언가와 부딪혔을 때 발생. TrainConsist가 구독해 뒤쪽을 분리합니다.</summary>
    public event Action<TrainCar, Collision> Collided;

    /// <summary>모델의 앞뒤 길이(로컬 Z 기준). 차량 간격 계산에 사용합니다.</summary>
    public float Length
    {
        get { Initialize(); return localBounds.size.z; }
    }

    /// <summary>앞 차량과 이어질 지점 (이 차량의 로컬 좌표).</summary>
    public Vector3 FrontAnchorLocal
    {
        get
        {
            Initialize();
            return frontCouplingPoint != null
                ? transform.InverseTransformPoint(frontCouplingPoint.position)
                : new Vector3(localBounds.center.x, localBounds.center.y, localBounds.max.z);
        }
    }

    /// <summary>뒤 차량과 이어질 지점 (이 차량의 로컬 좌표).</summary>
    public Vector3 RearAnchorLocal
    {
        get
        {
            Initialize();
            return rearCouplingPoint != null
                ? transform.InverseTransformPoint(rearCouplingPoint.position)
                : new Vector3(localBounds.center.x, localBounds.center.y, localBounds.min.z);
        }
    }

    private void Awake() => Initialize();

    /// <summary>
    /// Awake 실행 순서에 의존하지 않도록, TrainConsist에서도 명시적으로 호출할 수 있는 초기화.
    /// 여러 번 불러도 안전합니다.
    /// </summary>
    public void Initialize()
    {
        if (initialized) return;
        initialized = true;

        rb = GetComponent<Rigidbody>();
        localBounds = CalculateLocalBounds();

        if (overrideMass) rb.mass = mass;
        if (autoFitCollider) FitCollider();

        // 조인트로 이어진 물체는 솔버 반복 횟수를 올려야 늘어짐/떨림이 줄어듭니다.
        rb.solverIterations = Mathf.Max(rb.solverIterations, 16);
        rb.solverVelocityIterations = Mathf.Max(rb.solverVelocityIterations, 8);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    /// <summary>
    /// 앞 연결부를 끊고 이 차량을 물리적으로 추락시킵니다.
    /// 뒤에 달린 차량까지 한꺼번에 떨어뜨리려면 TrainConsist.BreakCouplerAt()을 사용하세요.
    /// </summary>
    public void Detach()
    {
        if (IsDetached) return;
        IsDetached = true; // 연결부 파손 이벤트가 되돌아와도 무한 재귀에 빠지지 않도록 먼저 세팅

        Initialize();

        if (FrontCoupler != null)
        {
            FrontCoupler.Break();
            FrontCoupler = null;
        }

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None; // 기관차가 회전 고정 상태였을 수 있으므로 해제

        // 그냥 수직 낙하하면 밋밋하므로 약간 튕기고 구르게 만듭니다.
        rb.AddForce(UnityEngine.Random.onUnitSphere * detachImpulse, ForceMode.VelocityChange);
        rb.AddTorque(UnityEngine.Random.onUnitSphere * detachTorque, ForceMode.VelocityChange);

        if (destroyDelay > 0f) Destroy(gameObject, destroyDelay);
    }

    private void OnCollisionEnter(Collision collision)
    {
        Collided?.Invoke(this, collision);
    }

    // 콜라이더가 하나도 없으면 모델 전체를 감싸는 BoxCollider를 붙입니다.
    private void FitCollider()
    {
        if (GetComponentInChildren<Collider>() != null) return;

        var box = gameObject.AddComponent<BoxCollider>();
        box.center = localBounds.center;
        box.size = localBounds.size;
    }

    // 자식 메시들의 실제 크기를 이 오브젝트의 로컬 좌표계로 모아 계산합니다.
    private Bounds CalculateLocalBounds()
    {
        Matrix4x4 toLocal = transform.worldToLocalMatrix;
        Bounds acc = default;
        bool has = false;

        foreach (var mf in GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Encapsulate(ref acc, ref has, mf.sharedMesh.bounds, toLocal * mf.transform.localToWorldMatrix);
        }

        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (smr.sharedMesh == null) continue;
            Encapsulate(ref acc, ref has, smr.sharedMesh.bounds, toLocal * smr.transform.localToWorldMatrix);
        }

        return has ? acc : new Bounds(Vector3.zero, Vector3.one);
    }

    private static void Encapsulate(ref Bounds acc, ref bool has, Bounds meshBounds, Matrix4x4 m)
    {
        Vector3 c = meshBounds.center;
        Vector3 e = meshBounds.extents;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);

            Vector3 p = m.MultiplyPoint3x4(corner);
            if (!has) { acc = new Bounds(p, Vector3.zero); has = true; }
            else acc.Encapsulate(p);
        }
    }

    // 씬 뷰에서 연결 지점을 눈으로 확인할 수 있게 표시합니다.
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) localBounds = CalculateLocalBounds();

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(new Vector3(localBounds.center.x, localBounds.center.y, localBounds.max.z), 0.5f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(new Vector3(localBounds.center.x, localBounds.center.y, localBounds.min.z), 0.5f);
    }
}
