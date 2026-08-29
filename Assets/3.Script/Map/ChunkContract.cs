using UnityEngine;

/// <summary>
/// 청크(맵 조각) 규격을 담는 단일 출처(Single Source of Truth) 에셋.
///
/// 청크 변형을 여러 개 만들어 랜덤으로 이어 붙이려면, 모든 변형이 아래 세 가지를
/// 똑같이 지켜야 어떤 순서로 붙어도 이음매가 어긋나지 않습니다.
///
///  1. <b>길이</b>가 같을 것            → Chunk Length
///  2. <b>들어오는 x</b>가 같을 것       → Seam X (첫 웨이포인트의 로컬 x)
///  3. <b>나가는 x</b>가 같을 것         → Seam X (마지막 웨이포인트의 로컬 x)
///
/// y는 맞출 필요가 없습니다. 이 게임에서 열차의 높이는 점프와 중력으로 플레이어가
/// 직접 만드는 값이고, 경로는 수평(x·z)만 담당하기 때문입니다.
///
/// 만드는 법: Project 창 우클릭 ▸ Create ▸ Zino ▸ 청크 규격(Chunk Contract)
/// 또는 Tools ▸ Zino ▸ 1) 규격 측정·에셋 갱신 이 자동으로 만들어 줍니다.
/// </summary>
[CreateAssetMenu(fileName = "ChunkContract", menuName = "Zino/청크 규격(Chunk Contract)")]
public class ChunkContract : ScriptableObject
{
    [Header("길이")]
    [Tooltip("진행축(Z) 방향 청크 길이(m). 모든 변형이 반드시 이 길이여야 합니다. " +
             "바닥 Plane의 Z 크기이자 도시 블록의 반복 주기이기도 합니다.")]
    public float chunkLength = 0f;

    [Header("이음매")]
    [Tooltip("이음매의 x 좌표(청크 로컬). 모든 변형의 첫·마지막 웨이포인트가 이 x 위에 있어야 합니다.")]
    public float seamX = 0f;

    [Tooltip("첫 웨이포인트의 로컬 z. 청크 피벗을 첫 웨이포인트에 맞추면 0입니다.")]
    public float entryZ = 0f;

    [Tooltip("마지막 웨이포인트의 로컬 z. 모든 변형이 같아야 이음매 간격이 균일합니다.")]
    public float exitZ = 0f;

    [Header("검사")]
    [Tooltip("규격 검사에서 허용할 오차(m).")]
    public float tolerance = 0.5f;

    /// <summary>마지막 웨이포인트에서 다음 청크 첫 웨이포인트까지의 거리.</summary>
    public float SeamGap => chunkLength - (exitZ - entryZ);

    /// <summary>청크 로컬 기준, 첫 웨이포인트가 있어야 할 자리(y는 자유).</summary>
    public Vector3 EntryLocal => new Vector3(seamX, 0f, entryZ);

    /// <summary>청크 로컬 기준, 마지막 웨이포인트가 있어야 할 자리(y는 자유).</summary>
    public Vector3 ExitLocal => new Vector3(seamX, 0f, exitZ);

    public bool IsValid => chunkLength > 0.01f;
}
