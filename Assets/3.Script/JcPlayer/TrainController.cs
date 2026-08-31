using UnityEngine;
using UnityEngine.InputSystem;


[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(TrainCar))] 
public class TrainController : MonoBehaviour
{

    [SerializeField] private float jumpHeight = 10f;

    [SerializeField] private TrainConsist consist;
    [Header("각도 조작 (WASD)")]
    [SerializeField] private bool controlRotation = true;
    [SerializeField] private float pitchSpeed = 70f;
    [SerializeField] private float yawSpeed = 70f;
    [SerializeField] private float maxPitchAngle = 60f;
    [SerializeField] private float maxYawAngle = 60f;
    [SerializeField] private bool invertPitchInput = false;
    [SerializeField] private bool invertYawInput = false;
    [SerializeField] private float rotationSmoothSpeed = 10f;

    private Rigidbody rb;
    private float jumpVelocity;
    private float initialYaw;
    private float initialRoll;

    private float targetPitch;
    private float targetYawOffset;

    public void AttachConsist(TrainConsist value) => consist = value;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        RecalculateJumpVelocity();

        Vector3 startEuler = transform.rotation.eulerAngles;
        initialYaw = startEuler.y;
        initialRoll = startEuler.z;

        if (controlRotation)
        {
            rb.constraints |= RigidbodyConstraints.FreezeRotation;
        }
    }

    private void RecalculateJumpVelocity()
    {
        float gravity = Mathf.Abs(Physics.gravity.y);
        jumpVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            Jump();
        }
        if (controlRotation)
        {
            ReadRotationInput(keyboard);
        }
    }

    private void ReadRotationInput(Keyboard keyboard)
    {
        float pitchInput = 0f;
        if (keyboard.wKey.isPressed) pitchInput += 1f;
        if (keyboard.sKey.isPressed) pitchInput -= 1f;
        if (invertPitchInput) pitchInput *= -1f;

        float yawInput = 0f;
        if (keyboard.dKey.isPressed) yawInput += 1f;
        if (keyboard.aKey.isPressed) yawInput -= 1f;
        if (invertYawInput) yawInput *= -1f;

        targetPitch = Mathf.Clamp(targetPitch + pitchInput * pitchSpeed * Time.deltaTime, -maxPitchAngle, maxPitchAngle);
        targetYawOffset = Mathf.Clamp(targetYawOffset + yawInput * yawSpeed * Time.deltaTime, -maxYawAngle, maxYawAngle);
    }

    private void Jump()
    {
        ApplyJumpVelocity(rb);

        if (consist == null) consist = FindFirstObjectByType<TrainConsist>();
        if (consist == null) return;


        foreach (var car in consist.Cars)
        {
            if (car == null || car.IsDetached) continue;
            if (car.Body == rb) continue;
            ApplyJumpVelocity(car.Body);
        }
    }

    private void ApplyJumpVelocity(Rigidbody body)
    {
        if (body == null || body.isKinematic) return;

        Vector3 velocity = body.linearVelocity;
        velocity.y = jumpVelocity; // 기존 y속도를 덮어써서 항상 동일한 높이로 점프
        body.linearVelocity = velocity;
    }

    private void FixedUpdate()
    {
        if (controlRotation) ApplyRotation();
    }

    private void ApplyRotation()
    {
        Quaternion targetRotation = Quaternion.Euler(targetPitch, initialYaw + targetYawOffset, initialRoll);
        Quaternion next = Quaternion.Slerp(
            rb.rotation,
            targetRotation,
            1f - Mathf.Exp(-rotationSmoothSpeed * Time.fixedDeltaTime));

        // 조인트와 충돌하지 않도록 Transform이 아닌 Rigidbody를 통해 회전
        rb.MoveRotation(next);
    }

#if UNITY_EDITOR
    // 에디터에서 jumpHeight 값을 플레이 중에 바꿔도 즉시 반영되도록 처리
    private void OnValidate()
    {
        if (Application.isPlaying && rb != null)
        {
            RecalculateJumpVelocity();
        }
    }
#endif
}
