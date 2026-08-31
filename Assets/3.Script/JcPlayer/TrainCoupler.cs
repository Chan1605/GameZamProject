using System;
using UnityEngine;

/// <summary>
/// 연결부 물리 설정. TrainConsist의 Inspector에서 한 번에 조절합니다.
/// </summary>
[Serializable]
public class CouplerSettings
{
    [Tooltip("이 힘을 넘으면 연결부가 저절로 끊어집니다. 0 이하면 끊어지지 않음(무한).")]
    public float breakForce = 0f;
    [Tooltip("이 회전력을 넘으면 연결부가 저절로 끊어집니다. 0 이하면 끊어지지 않음(무한).")]
    public float breakTorque = 0f;

    [Tooltip("연결부에서 위아래로 꺾일 수 있는 각도(도). 점프할 때 뒤 차량이 따라 들리는 정도를 결정합니다.")]
    public float pitchSwingLimit = 12f;
    [Tooltip("연결부에서 좌우로 꺾일 수 있는 각도(도).")]
    public float yawSwingLimit = 12f;

    [Tooltip("연결부의 여유(느슨함, 단위 m). 0이면 완전히 고정되고, 값이 크면 로프처럼 헐렁하게 당겨집니다.")]
    public float slack = 0f;
}

/// <summary>
/// 열차 연결부. <b>연결부 인덱스 i는 car[i-1](앞)과 car[i](뒤)를 잇습니다.</b>
/// 즉 CouplerIndex는 "이 연결부가 끊어졌을 때 가장 먼저 떨어지는 차량 번호"와 같습니다.
/// 예: BreakCouplerAt(2) → 2번 차량부터 뒤로 전부 추락.
///
/// ConfigurableJoint는 Rigidbody와 같은 GameObject에만 붙을 수 있으므로,
/// 이 컴포넌트와 조인트는 항상 "뒤쪽 차량"에 올라갑니다.
/// </summary>
public class TrainCoupler : MonoBehaviour
{
    [SerializeField] private int couplerIndex;
    [SerializeField] private TrainCar frontCar;
    [SerializeField] private TrainCar rearCar;

    private ConfigurableJoint joint;

    /// <summary>이 연결부가 끊기면 가장 먼저 떨어지는 차량의 인덱스.</summary>
    public int CouplerIndex => couplerIndex;
    public TrainCar FrontCar => frontCar;
    public TrainCar RearCar => rearCar;
    public bool IsBroken => joint == null;

    /// <summary>연결부가 끊어졌을 때 발생 (수동 파손 + 힘에 의한 자연 파손 모두).</summary>
    public event Action<TrainCoupler> Broken;

    /// <summary>앞 차량과 뒤 차량을 ConfigurableJoint로 연결하고 연결부 컴포넌트를 만듭니다.</summary>
    public static TrainCoupler Connect(TrainCar front, TrainCar rear, int index, CouplerSettings settings)
    {
        if (front == null || rear == null) return null;

        front.Initialize();
        rear.Initialize();

        var coupler = rear.gameObject.AddComponent<TrainCoupler>();
        coupler.couplerIndex = index;
        coupler.frontCar = front;
        coupler.rearCar = rear;
        coupler.joint = coupler.BuildJoint(settings);

        rear.FrontCoupler = coupler;
        return coupler;
    }

    private ConfigurableJoint BuildJoint(CouplerSettings s)
    {
        var j = gameObject.AddComponent<ConfigurableJoint>();
        j.connectedBody = frontCar.Body;

        // 두 차량의 실제 연결 지점(앞 차량의 꼬리 ↔ 뒤 차량의 머리)을 앵커로 사용
        j.autoConfigureConnectedAnchor = false;
        j.anchor = rearCar.FrontAnchorLocal;
        j.connectedAnchor = frontCar.RearAnchorLocal;

        // 위치: 기본은 완전 고정. slack을 주면 그만큼 헐렁하게 당겨집니다.
        if (s.slack > 0f)
        {
            var limit = j.linearLimit;
            limit.limit = s.slack;
            j.linearLimit = limit;
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Limited;
        }
        else
        {
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
        }

        // 회전: 위아래(pitch)/좌우(yaw)로는 제한적으로 꺾이고, 옆으로 구르는(roll) 건 막습니다.
        j.angularXMotion = ConfigurableJointMotion.Limited;
        j.angularYMotion = ConfigurableJointMotion.Limited;
        j.angularZMotion = ConfigurableJointMotion.Locked;

        var low = j.lowAngularXLimit; low.limit = -Mathf.Abs(s.pitchSwingLimit); j.lowAngularXLimit = low;
        var high = j.highAngularXLimit; high.limit = Mathf.Abs(s.pitchSwingLimit); j.highAngularXLimit = high;
        var yaw = j.angularYLimit; yaw.limit = Mathf.Abs(s.yawSwingLimit); j.angularYLimit = yaw;

        // 조인트가 늘어나거나 떨리는 것을 억제
        j.enablePreprocessing = false;
        j.projectionMode = JointProjectionMode.PositionAndRotation;
        j.projectionDistance = 0.05f;
        j.projectionAngle = 3f;
        j.enableCollision = false;

        j.breakForce = s.breakForce > 0f ? s.breakForce : Mathf.Infinity;
        j.breakTorque = s.breakTorque > 0f ? s.breakTorque : Mathf.Infinity;

        return j;
    }

    /// <summary>연결부를 강제로 끊습니다.</summary>
    public void Break()
    {
        if (joint != null)
        {
            Destroy(joint);
            joint = null;
        }

        if (rearCar != null) rearCar.FrontCoupler = null;
        Broken?.Invoke(this);
    }

    // breakForce/breakTorque를 넘겨 물리엔진이 조인트를 끊었을 때 Unity가 호출합니다.
    private void OnJointBreak(float breakForce)
    {
        joint = null;
        if (rearCar != null) rearCar.FrontCoupler = null;
        Broken?.Invoke(this);
    }

    // 씬 뷰에서 어느 차량끼리 몇 번으로 이어져 있는지 선으로 표시합니다.
    private void OnDrawGizmos()
    {
        if (frontCar == null || rearCar == null) return;

        Gizmos.color = IsBroken ? Color.red : Color.green;
        Vector3 a = frontCar.transform.TransformPoint(frontCar.RearAnchorLocal);
        Vector3 b = rearCar.transform.TransformPoint(rearCar.FrontAnchorLocal);
        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireCube((a + b) * 0.5f, Vector3.one * 0.6f);
    }
}
