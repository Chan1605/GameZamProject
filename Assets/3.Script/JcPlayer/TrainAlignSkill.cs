using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class TrainAlignSkill : MonoBehaviour
{
    [SerializeField] private TrainConsist consist;
    [SerializeField] private Key alignKey = Key.Q;
    [SerializeField] private float alignHoldDuration = 0.3f;
    [SerializeField] private PlayerSkillPoint skillPoint;

    private Coroutine routine;

    private void Awake()
    {
        if (consist == null) consist = GetComponent<TrainConsist>();
        if (consist == null) consist = FindFirstObjectByType<TrainConsist>();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[alignKey].wasPressedThisFrame)
        {
            if (skillPoint.UseSkillPoint().Equals(false)) return; // 스킬포인트 확인 후 재생

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(AlignRoutine());
        }
    }

    private IEnumerator AlignRoutine()
    {
        float t = 0f;
        while (t < alignHoldDuration)
        {
            consist?.AlignStraight();
            consist?.ForceInvincible(Time.fixedDeltaTime + 0.05f);
            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
        routine = null;
    }
}