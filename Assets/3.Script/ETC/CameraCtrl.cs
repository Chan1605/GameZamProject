using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraCtrl : MonoBehaviour
{
    public Transform target;
    public float targetHeight = 1.2f;
    public float targetSide = -0.15f;
    public float distance = 4.0f;
    public float maxDistance = 6;
    public float minDistance = 1.0f;
    public float xSpeed = 250.0f;
    public float ySpeed = 120.0f;
    public float yMinLimit = -10;
    public float yMaxLimit = 70;
    public float zoomRate = 80;

    private float x = 20.0f;
    private float y = 0.0f;
    private Vector3 shakeOffset;

    private void Awake()
    {
        if (!target)
            target = GameObject.FindGameObjectWithTag("Player")?.transform;
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }



    void LateUpdate()
    {
        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;

        x += mouseDelta.x * xSpeed * 0.002f;
        y -= mouseDelta.y * ySpeed * 0.002f;
        distance -= scroll * Time.deltaTime * zoomRate * 0.01f; // 스크롤 감도는 테스트하면서 조절 필요
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
        y = ClampAngle(y, yMinLimit, yMaxLimit);

        Quaternion rotation = Quaternion.Euler(y, x, 0);
        transform.rotation = rotation;

        Vector3 position = target.position - (rotation * new Vector3(targetSide, 0, 1) * distance + new Vector3(0, -targetHeight, 0));
        transform.position = position + shakeOffset;
    }

    public void Shake(float duration = 0.12f, float magnitude = 0.15f)
    {
        StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            shakeOffset = Random.insideUnitSphere * magnitude;
            elapsed += Time.deltaTime;
            yield return null;
        }
        shakeOffset = Vector3.zero;
    }

    static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360) angle += 360;
        if (angle > 360) angle -= 360;
        return Mathf.Clamp(angle, min, max);
    }

    public void SetTarget(Transform newTarget)
    {
        if (newTarget != null)
        {
            target = newTarget;
        }
    }
}