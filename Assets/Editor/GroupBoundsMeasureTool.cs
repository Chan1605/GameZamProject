using UnityEditor;
using UnityEngine;

/// <summary>
/// 프리팹의 실제 렌더러 크기(Bounds)를 측정하는 에디터 도구.
/// 무한 스크롤/청크 이어붙이기에서 "정확히 얼마나 이격시켜야 딱 맞는지"를 구할 때 사용합니다.
///
/// 사용법
///  - Tools ▸ 맵 크기 측정 (Group)  → Assets/2.Model/Prefabs/Group.prefab을 바로 측정
///  - Tools ▸ 프리팹 크기 측정 (선택한 오브젝트)  → 프로젝트 창의 다른 프리팹이나
///    씬에 배치된 오브젝트를 선택한 뒤 실행하면 그것을 측정
///  - 결과는 Console 창에 출력됩니다.
/// </summary>
public static class GroupBoundsMeasureTool
{
    private const string GroupPrefabPath = "Assets/2.Model/Prefabs/Group.prefab";

    [MenuItem("Tools/맵 크기 측정 (Group)")]
    private static void MeasureGroup()
    {
        MeasurePrefabAt(GroupPrefabPath);
    }

    [MenuItem("Tools/프리팹 크기 측정 (선택한 오브젝트)")]
    private static void MeasureSelected()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("[크기 측정] 하이러키나 프로젝트 창에서 오브젝트를 먼저 선택하세요.");
            return;
        }

        // 프로젝트 창에서 프리팹 에셋 자체를 선택한 경우 (씬에 배치된 게 아님)
        string assetPath = AssetDatabase.GetAssetPath(selected);
        if (!string.IsNullOrEmpty(assetPath) && !selected.scene.IsValid())
        {
            MeasurePrefabAt(assetPath);
            return;
        }

        // 씬에 이미 배치된 오브젝트를 선택한 경우 - 그대로(현재 스케일 기준) 측정
        Report(selected.name, selected.transform, selected.gameObject, destroyAfter: false);
    }

    private static void MeasurePrefabAt(string assetPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            Debug.LogError($"[크기 측정] 프리팹을 찾을 수 없습니다: {assetPath}");
            return;
        }

        // 원본 프리팹 크기(스케일 1, 회전 없음) 기준으로 재기 위해 임시로 인스턴스화합니다.
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        instance.transform.localScale = Vector3.one;

        Report(prefab.name, instance.transform, instance, destroyAfter: true);
    }

    private static void Report(string label, Transform pivot, GameObject go, bool destroyAfter)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"[크기 측정] '{label}'에 Renderer가 하나도 없습니다 (메시가 없거나 비활성 상태).");
            if (destroyAfter) Object.DestroyImmediate(go);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        Vector3 pivotToMin = bounds.min - pivot.position;
        Vector3 pivotToMax = bounds.max - pivot.position;

        Debug.Log(
            $"[크기 측정] {label}\n" +
            $"  size (x, y, z)      = ({bounds.size.x:F4}, {bounds.size.y:F4}, {bounds.size.z:F4})\n" +
            $"  center(월드)         = {bounds.center}\n" +
            $"  pivot → bounds.min  = {pivotToMin}\n" +
            $"  pivot → bounds.max  = {pivotToMax}\n" +
            $"  ※ Z축(앞) 방향으로 이어붙인다면 이격 거리 = size.z = {bounds.size.z:F4}\n" +
            $"  ※ X축(옆) 방향으로 이어붙인다면 이격 거리 = size.x = {bounds.size.x:F4}\n" +
            $"  ※ 피벗이 바운드 중앙에 있지 않아도, 다음 조각을 '이 값만큼' 평행이동시키면 항상 딱 맞물립니다."
        );

        if (destroyAfter) Object.DestroyImmediate(go);
    }
}
