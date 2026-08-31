using System;
using UnityEngine;


[RequireComponent(typeof(Rigidbody))]
public class TrainCar : MonoBehaviour
{
    [Header("식별")]
    [SerializeField] private int carIndex;

    [Header("연결 지점 (비워두면 모델 크기에서 자동 계산)")]
    [SerializeField] private Transform frontCouplingPoint;
    [SerializeField] private Transform rearCouplingPoint;

    [Header("자동 설정")]
    [SerializeField] private bool autoFitCollider = true;
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

    public int CarIndex
    {
        get => carIndex;
        internal set => carIndex = value;
    }

    public bool IsDetached { get; private set; }

    public Rigidbody Body
    {
        get { Initialize(); return rb; }
    }

    public TrainCoupler FrontCoupler { get; internal set; }

    public event Action<TrainCar, Collision> Collided;

    public float Length
    {
        get { Initialize(); return localBounds.size.z; }
    }

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

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;

        rb = GetComponent<Rigidbody>();
        localBounds = CalculateLocalBounds();

        if (overrideMass) rb.mass = mass;
        if (autoFitCollider) FitCollider();

        rb.solverIterations = Mathf.Max(rb.solverIterations, 16);
        rb.solverVelocityIterations = Mathf.Max(rb.solverVelocityIterations, 8);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void Detach()
    {
        if (IsDetached) return;
        IsDetached = true;

        Initialize();

        if (FrontCoupler != null)
        {
            FrontCoupler.Break();
            FrontCoupler = null;
        }

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None; // 기관차가 회전 고정 상태였을 수 있으므로 해제

        rb.AddForce(UnityEngine.Random.onUnitSphere * detachImpulse, ForceMode.VelocityChange);
        rb.AddTorque(UnityEngine.Random.onUnitSphere * detachTorque, ForceMode.VelocityChange);

        if (destroyDelay > 0f) Destroy(gameObject, destroyDelay);
    }

    private void OnCollisionEnter(Collision collision)
    {
        Collided?.Invoke(this, collision);
    }

    private void FitCollider()
    {
        if (GetComponentInChildren<Collider>() != null) return;

        var box = gameObject.AddComponent<BoxCollider>();
        box.center = localBounds.center;
        box.size = localBounds.size;
    }

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
