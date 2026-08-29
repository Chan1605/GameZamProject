using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 청크(맵 조각) 파이프라인 도구.
///
/// <b>처음 한 번</b>
///   1) 규격 측정 · 에셋 만들기   → 도시 주기를 실측해 ChunkContract 에셋을 만듭니다.
///   2) 씬을 청크 프리팹으로 변환 → Ground·Obstacle·Group을 한 프리팹으로 묶고 스포너를 세팅합니다.
///
/// <b>변형을 추가할 때 (반복 작업)</b>
///   A) 새 청크 변형 만들기(템플릿) → 규격에 맞는 빈 청크가 생깁니다. 안을 꾸미세요.
///   B) 변형 폴더를 스포너에 등록    → 폴더 안 프리팹을 전부 스포너에 넣어줍니다.
///   C) 청크 규격 검사               → 이음매가 어긋난 변형이 있으면 잡아냅니다.
/// </summary>
public static class ZinoChunkTool
{
    private const string ContractPath = "Assets/2.Model/Chunks/ChunkContract.asset";
    private const string ChunkFolder = "Assets/2.Model/Chunks";
    private const float DefaultSeamX = 0f;   // 이음매 x 기본값. 에셋에서 나중에 바꿔도 됩니다.

    // ────────────────────────────────────────────────────────────────────
    // 1) 규격 측정 · 에셋 만들기
    // ────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Zino/1) 규격 측정 · 에셋 만들기", priority = 0)]
    private static void MeasureAndCreateContract()
    {
        if (!CollectSceneParts(out var ground, out var groups, out var obstacles, out string error))
        {
            EditorUtility.DisplayDialog("규격 측정", error, "확인");
            return;
        }

        Bounds city = CombinedBounds(groups);
        float chunkLength = city.size.z;

        Transform first = obstacles.First();
        Transform last = obstacles.Last();

        var contract = LoadOrCreateContract();
        contract.chunkLength = chunkLength;
        contract.seamX = DefaultSeamX;
        contract.entryZ = 0f;                                   // 청크 피벗을 첫 웨이포인트에 맞춥니다.
        contract.exitZ = last.position.z - first.position.z;
        EditorUtility.SetDirty(contract);
        AssetDatabase.SaveAssets();

        Vector3 groundSize = new Vector3(ground.localScale.x, 0f, ground.localScale.z) * 10f; // 기본 Plane = 10×10

        var report = new System.Text.StringBuilder();
        report.AppendLine("[Zino 규격 측정]");
        report.AppendLine($"  현재 Ground(기본 Plane) 크기 = {groundSize.x:F4} (X) × {groundSize.z:F4} (Z)");
        report.AppendLine($"  도시 블록 {groups.Count}개 전체 바운드 = {city.size.x:F4} (X) × {city.size.z:F4} (Z), 중심 {city.center}");
        report.AppendLine($"  → 청크 길이(Chunk Length) = 도시 Z 주기 = {chunkLength:F4}");
        report.AppendLine($"  → Ground의 Z 스케일을 {chunkLength / 10f:F6} 으로 맞추면 빈 띠 없이 딱 이어집니다.");
        report.AppendLine();
        report.AppendLine($"  웨이포인트(Obstacle) {obstacles.Count}개, z {first.position.z:F1} → {last.position.z:F1} (길이 {contract.exitZ:F1})");
        report.AppendLine($"  이음매 x: 첫 {first.position.x:F2} / 마지막 {last.position.x:F2} → 둘 다 {contract.seamX:F2} 로 맞출 예정");
        report.AppendLine($"  이음매 간격(마지막 → 다음 청크 첫 점) = {contract.SeamGap:F2}");
        report.AppendLine();
        report.Append("  구간별 z 간격 = ");
        for (int i = 1; i < obstacles.Count; i++)
            report.Append($"{obstacles[i].position.z - obstacles[i - 1].position.z:F1}  ");
        report.AppendLine();
        report.AppendLine($"  규격 에셋: {ContractPath}");

        Debug.Log(report.ToString(), contract);
        Selection.activeObject = contract;
        EditorGUIUtility.PingObject(contract);
    }

    // ────────────────────────────────────────────────────────────────────
    // 2) 씬을 청크 프리팹으로 변환
    // ────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Zino/2) 씬을 청크 프리팹으로 변환", priority = 1)]
    private static void ConvertSceneToChunk()
    {
        var contract = AssetDatabase.LoadAssetAtPath<ChunkContract>(ContractPath);
        if (contract == null || !contract.IsValid)
        {
            EditorUtility.DisplayDialog("청크 변환", "먼저 '1) 규격 측정 · 에셋 만들기'를 실행하세요.", "확인");
            return;
        }

        if (!CollectSceneParts(out var ground, out var groups, out var obstacles, out string error))
        {
            EditorUtility.DisplayDialog("청크 변환", error, "확인");
            return;
        }

        if (!EditorUtility.DisplayDialog("청크 변환",
                $"씬의 Ground · 도시 블록 {groups.Count}개 · Obstacle {obstacles.Count}개를 하나의 청크 프리팹으로 묶습니다.\n\n" +
                $"· Ground Z 스케일 → {contract.chunkLength / 10f:F4} (길이 {contract.chunkLength:F2})\n" +
                $"· 첫·마지막 Obstacle의 x → {contract.seamX:F2}\n" +
                $"· 열차: Freeze Position X 해제 + TrainPathFollower 연결\n" +
                $"· 기존 GroundScroller / ObstacleSpawner 비활성화\n\n" +
                "되돌리려면 Ctrl+Z 또는 _Backup 폴더의 씬 백업을 쓰세요.",
                "진행", "취소"))
            return;

        Bounds city = CombinedBounds(groups);
        Transform first = obstacles.First();
        Transform last = obstacles.Last();

        Undo.SetCurrentGroupName("Zino 청크 변환");
        int undoGroup = Undo.GetCurrentGroup();

        // ── 청크 루트: 피벗을 첫 웨이포인트의 z에 맞춰 로컬 entryZ = 0 이 되게 합니다.
        var root = new GameObject("GroundChunk");
        Undo.RegisterCreatedObjectUndo(root, "청크 루트 생성");
        root.transform.SetPositionAndRotation(new Vector3(0f, 0f, first.position.z), Quaternion.identity);

        // ── 이음매 x 맞추기 (첫·마지막 Obstacle만)
        MoveX(first, contract.seamX);
        MoveX(last, contract.seamX);

        // ── Ground 규격 맞추기: Z는 도시 주기, X는 도시를 덮을 만큼
        Undo.RecordObject(ground, "Ground 규격");
        float groundScaleX = Mathf.Max(ground.localScale.x, city.size.x / 10f);
        ground.localScale = new Vector3(groundScaleX, ground.localScale.y, contract.chunkLength / 10f);
        ground.position = new Vector3(city.center.x, ground.position.y, city.center.z);

        // ── 한 지붕 아래로
        foreach (var t in new[] { ground }.Concat(groups).Concat(obstacles))
            Undo.SetTransformParent(t, root.transform, "청크에 편입");

        // ── 경로: 각 Obstacle의 Point(득점 트리거)를 그대로 웨이포인트로 씁니다.
        var waypoints = obstacles.Select(o => o.Find("Point") != null ? o.Find("Point") : o).ToArray();

        var mapPath = Undo.AddComponent<MapPath>(root);
        var pathSo = new SerializedObject(mapPath);
        var pointsProp = pathSo.FindProperty("points");
        pointsProp.arraySize = waypoints.Length;
        for (int i = 0; i < waypoints.Length; i++)
            pointsProp.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
        pathSo.FindProperty("contract").objectReferenceValue = contract;
        pathSo.ApplyModifiedProperties();

        // ── 프리팹으로 저장
        EnsureFolder(ChunkFolder);
        string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{ChunkFolder}/GroundChunk_01.prefab");
        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.UserAction);

        // ── 스포너 세팅
        var spawnerGo = GameObject.Find("MapChunkSpawner");
        if (spawnerGo == null)
        {
            spawnerGo = new GameObject("MapChunkSpawner");
            Undo.RegisterCreatedObjectUndo(spawnerGo, "스포너 생성");
        }
        spawnerGo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        var spawner = spawnerGo.GetComponent<MapChunkSpawner>();
        if (spawner == null) spawner = Undo.AddComponent<MapChunkSpawner>(spawnerGo);

        var spawnerSo = new SerializedObject(spawner);
        spawnerSo.FindProperty("contract").objectReferenceValue = contract;
        var variants = spawnerSo.FindProperty("chunkVariants");
        variants.arraySize = 1;
        variants.GetArrayElementAtIndex(0).FindPropertyRelative("prefab").objectReferenceValue = prefab;
        variants.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 1f;
        spawnerSo.FindProperty("chunkCount").intValue = 3;
        spawnerSo.FindProperty("editorPreviewChunk").objectReferenceValue = root;
        spawnerSo.ApplyModifiedProperties();

        SetupTrain(spawner);
        DisableLegacyScrollers();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneMarkDirty();

        Debug.Log($"[Zino] 청크 변환 완료\n" +
                  $"  프리팹: {prefabPath}\n" +
                  $"  길이 {contract.chunkLength:F2} · 이음매 x {contract.seamX:F2} · 이음 간격 {contract.SeamGap:F2}\n" +
                  $"  씬에 남은 'GroundChunk'는 눈으로 보라고 둔 편집용 원본입니다(플레이 시작 시 자동 제거).\n" +
                  $"  변형을 더 만들려면 Tools ▸ Zino ▸ A) 새 청크 변형 만들기", prefab);

        Selection.activeGameObject = spawnerGo;
    }

    private static void MoveX(Transform t, float x)
    {
        Undo.RecordObject(t, "이음매 x 정렬");
        Vector3 p = t.position;
        p.x = x;
        t.position = p;
    }

    private static void SetupTrain(MapChunkSpawner spawner)
    {
        var trainGo = GameObject.Find("train");
        if (trainGo == null)
        {
            Debug.LogWarning("[Zino] 씬에서 'train'을 찾지 못해 열차 연결은 건너뜁니다.");
            return;
        }

        // Freeze Position X만 해제합니다. 회전 고정은 TrainController가 직접 관리하므로 그대로 둡니다.
        var rb = trainGo.GetComponent<Rigidbody>();
        if (rb != null && (rb.constraints & RigidbodyConstraints.FreezePositionX) != 0)
        {
            Undo.RecordObject(rb, "Freeze Position X 해제");
            rb.constraints &= ~RigidbodyConstraints.FreezePositionX;
        }

        var follower = trainGo.GetComponent<TrainPathFollower>();
        if (follower == null) follower = Undo.AddComponent<TrainPathFollower>(trainGo);

        var scroll = Object.FindFirstObjectByType<ScrollController>();
        var followerSo = new SerializedObject(follower);
        if (scroll != null) followerSo.FindProperty("scroll").objectReferenceValue = scroll;
        followerSo.ApplyModifiedProperties();

        // TrainController.cs는 수정하지 않고, 이미 있는 Path Follower 슬롯만 채웁니다.
        var controller = trainGo.GetComponent<TrainController>();
        if (controller != null)
        {
            var controllerSo = new SerializedObject(controller);
            var slot = controllerSo.FindProperty("pathFollower");
            if (slot != null)
            {
                slot.objectReferenceValue = follower;
                controllerSo.ApplyModifiedProperties();
            }
        }

        var spawnerSo = new SerializedObject(spawner);
        spawnerSo.FindProperty("train").objectReferenceValue = follower;
        spawnerSo.ApplyModifiedProperties();
    }

    // 옛 방식(바닥이 흘러오는 스크롤)과 새 방식(열차가 달림)이 겹치면 속도가 두 배가 되고
    // 장애물이 이중으로 생기므로 꺼둡니다.
    // 스크립트 파일 자체를 지워도 이 도구가 깨지지 않도록 이름으로만 찾습니다.
    private static readonly string[] LegacyComponents = { "GroundScroller", "ObstacleSpawner" };

    private static void DisableLegacyScrollers()
    {
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null || !LegacyComponents.Contains(mb.GetType().Name)) continue;

            Undo.RecordObject(mb, "옛 스크롤 컴포넌트 비활성화");
            mb.enabled = false;
            Debug.Log($"[Zino] '{mb.gameObject.name}'의 {mb.GetType().Name}를 껐습니다. " +
                      $"새 청크 방식과 겹치므로 Remove Component로 지우셔도 됩니다.", mb);
        }
    }

    // ────────────────────────────────────────────────────────────────────
    // A) 새 청크 변형 만들기 (템플릿)
    // ────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Zino/A) 새 청크 변형 만들기 (템플릿)", priority = 20)]
    private static void CreateVariantTemplate()
    {
        var contract = AssetDatabase.LoadAssetAtPath<ChunkContract>(ContractPath);
        if (contract == null || !contract.IsValid)
        {
            EditorUtility.DisplayDialog("새 청크 변형", "먼저 '1) 규격 측정 · 에셋 만들기'를 실행하세요.", "확인");
            return;
        }

        EnsureFolder(ChunkFolder);

        // 기존 변형에서 바닥의 크기·머티리얼을 그대로 물려받습니다.
        Transform sample = FindSampleGround(out Material groundMaterial, out float groundScaleX, out float groundLocalZ);
        if (sample == null)
        {
            groundScaleX = 89.347855f;
            groundLocalZ = contract.entryZ + contract.chunkLength * 0.5f;
        }

        var root = new GameObject("GroundChunk");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(root.transform, false);
        ground.transform.localPosition = new Vector3(0f, 0f, groundLocalZ);
        ground.transform.localScale = new Vector3(groundScaleX, 1f, contract.chunkLength / 10f);
        if (groundMaterial != null) ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

        // 규격이 요구하는 입구·출구 웨이포인트를 미리 정확한 자리에 놓아 둡니다.
        var path = new GameObject("Path");
        path.transform.SetParent(root.transform, false);

        var entry = new GameObject("WP_IN");
        entry.transform.SetParent(path.transform, false);
        entry.transform.localPosition = new Vector3(contract.seamX, 50f, contract.entryZ);

        var exit = new GameObject("WP_OUT");
        exit.transform.SetParent(path.transform, false);
        exit.transform.localPosition = new Vector3(contract.seamX, 50f, contract.exitZ);

        var mapPath = root.AddComponent<MapPath>();
        var so = new SerializedObject(mapPath);
        so.FindProperty("pointsContainer").objectReferenceValue = path.transform;
        so.FindProperty("contract").objectReferenceValue = contract;
        so.ApplyModifiedProperties();

        EnsureFolder(ChunkFolder);
        string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{ChunkFolder}/GroundChunk_new.prefab");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"[Zino] 새 청크 변형 생성: {prefabPath}\n" +
                  $"  길이 {contract.chunkLength:F2} · 입구/출구 x {contract.seamX:F2} (z {contract.entryZ:F1} → {contract.exitZ:F1})\n" +
                  $"  Path 아래에 WP_IN과 WP_OUT 사이로 웨이포인트를 추가하고, Obstacle과 건물을 배치하세요.\n" +
                  $"  WP_IN · WP_OUT의 x와 z는 건드리지 마세요(이음매가 어긋납니다). y는 자유입니다.\n" +
                  $"  다 만들었으면 Tools ▸ Zino ▸ B) 변형 폴더를 스포너에 등록", prefab);

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
    }

    private static Transform FindSampleGround(out Material material, out float scaleX, out float localZ)
    {
        material = null; scaleX = 89.347855f; localZ = 0f;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ChunkFolder }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var ground = prefab != null ? prefab.transform.Find("Ground") : null;
            if (ground == null) continue;

            var renderer = ground.GetComponent<Renderer>();
            material = renderer != null ? renderer.sharedMaterial : null;
            scaleX = ground.localScale.x;
            localZ = ground.localPosition.z;
            return ground;
        }
        return null;
    }

    // ────────────────────────────────────────────────────────────────────
    // B) 변형 폴더를 스포너에 등록
    // ────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Zino/B) 변형 폴더를 스포너에 등록", priority = 21)]
    private static void RegisterVariants()
    {
        var spawner = Object.FindFirstObjectByType<MapChunkSpawner>();
        if (spawner == null)
        {
            EditorUtility.DisplayDialog("변형 등록", "씬에 MapChunkSpawner가 없습니다. 먼저 '2) 씬을 청크 프리팹으로 변환'을 실행하세요.", "확인");
            return;
        }

        EnsureFolder(ChunkFolder);
        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { ChunkFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(p => p != null && p.GetComponent<MapPath>() != null)
            .ToList();

        if (prefabs.Count == 0)
        {
            EditorUtility.DisplayDialog("변형 등록", $"{ChunkFolder} 안에 MapPath를 가진 프리팹이 없습니다.", "확인");
            return;
        }

        // 이미 등록된 변형의 가중치는 유지합니다.
        var so = new SerializedObject(spawner);
        var variants = so.FindProperty("chunkVariants");
        var weights = new Dictionary<Object, float>();
        for (int i = 0; i < variants.arraySize; i++)
        {
            var e = variants.GetArrayElementAtIndex(i);
            var p = e.FindPropertyRelative("prefab").objectReferenceValue;
            if (p != null) weights[p] = e.FindPropertyRelative("weight").floatValue;
        }

        variants.arraySize = prefabs.Count;
        for (int i = 0; i < prefabs.Count; i++)
        {
            var e = variants.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("prefab").objectReferenceValue = prefabs[i];
            e.FindPropertyRelative("weight").floatValue = weights.TryGetValue(prefabs[i], out float w) ? w : 1f;
        }
        so.ApplyModifiedProperties();
        EditorSceneMarkDirty();

        Debug.Log($"[Zino] 변형 {prefabs.Count}개 등록: {string.Join(", ", prefabs.Select(p => p.name))}", spawner);
        Selection.activeGameObject = spawner.gameObject;
    }

    // ────────────────────────────────────────────────────────────────────
    // C) 청크 규격 검사
    // ────────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Zino/C) 청크 규격 검사", priority = 22)]
    private static void ValidateAll()
    {
        var contract = AssetDatabase.LoadAssetAtPath<ChunkContract>(ContractPath);
        if (contract == null || !contract.IsValid)
        {
            EditorUtility.DisplayDialog("규격 검사", "먼저 '1) 규격 측정 · 에셋 만들기'를 실행하세요.", "확인");
            return;
        }

        EnsureFolder(ChunkFolder);
        var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { ChunkFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(p => p != null && p.GetComponent<MapPath>() != null)
            .ToList();

        int ok = 0;
        var report = new System.Text.StringBuilder();
        report.AppendLine($"[Zino 규격 검사] 길이 {contract.chunkLength:F2} · 이음매 x {contract.seamX:F2} · " +
                          $"z {contract.entryZ:F1} → {contract.exitZ:F1} · 이음 간격 {contract.SeamGap:F2}");

        foreach (var prefab in prefabs)
        {
            // 프리팹 에셋의 Transform은 로컬 좌표가 곧 청크 로컬이라 그대로 검사할 수 있습니다.
            var path = prefab.GetComponent<MapPath>();
            bool pass = path.Validate(contract, out string message);
            if (pass) ok++;
            report.AppendLine($"  {(pass ? "[통과]" : "[불합격]")} {prefab.name}\n    · {message}");

            // 바닥 길이도 규격과 맞는지 확인합니다(기본 Plane = 10단위).
            var ground = prefab.transform.Find("Ground");
            if (ground != null)
            {
                float len = ground.localScale.z * 10f;
                if (Mathf.Abs(len - contract.chunkLength) > contract.tolerance)
                    report.AppendLine($"    · 바닥 Z 길이 {len:F2} ≠ 규격 {contract.chunkLength:F2} " +
                                      $"(Z 스케일을 {contract.chunkLength / 10f:F6} 으로)");
            }
        }

        report.AppendLine($"  → {ok}/{prefabs.Count} 통과");
        if (ok == prefabs.Count) Debug.Log(report.ToString());
        else Debug.LogWarning(report.ToString());
    }

    // ────────────────────────────────────────────────────────────────────
    // 공통
    // ────────────────────────────────────────────────────────────────────
    private static bool CollectSceneParts(out Transform ground, out List<Transform> groups,
                                          out List<Transform> obstacles, out string error)
    {
        ground = null;
        groups = new List<Transform>();
        obstacles = new List<Transform>();
        error = null;

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var stack = new Stack<Transform>();
        foreach (var go in scene.GetRootGameObjects()) stack.Push(go.transform);

        while (stack.Count > 0)
        {
            var t = stack.Pop();
            if (t.name == "Ground" && ground == null && t.GetComponent<MeshFilter>() != null) ground = t;
            else if (t.name.StartsWith("Group")) { groups.Add(t); continue; }
            else if (t.name.StartsWith("Obstacle") && t.GetComponent<MeshFilter>() != null) { obstacles.Add(t); continue; }

            for (int i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));
        }

        obstacles = obstacles.OrderBy(o => o.position.z).ToList();

        if (ground == null) { error = "씬에서 MeshFilter를 가진 'Ground'를 찾지 못했습니다."; return false; }
        if (groups.Count == 0) { error = "씬에서 이름이 'Group'으로 시작하는 도시 블록을 찾지 못했습니다."; return false; }
        if (obstacles.Count < 2) { error = "Obstacle이 2개 미만입니다."; return false; }
        return true;
    }

    private static Bounds CombinedBounds(IEnumerable<Transform> targets)
    {
        Bounds bounds = default;
        bool started = false;

        foreach (var t in targets)
        {
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                if (!started) { bounds = r.bounds; started = true; }
                else bounds.Encapsulate(r.bounds);
            }
        }
        return bounds;
    }

    private static ChunkContract LoadOrCreateContract()
    {
        var contract = AssetDatabase.LoadAssetAtPath<ChunkContract>(ContractPath);
        if (contract != null) return contract;

        EnsureFolder(ChunkFolder);
        contract = ScriptableObject.CreateInstance<ChunkContract>();
        AssetDatabase.CreateAsset(contract, ContractPath);
        return contract;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void EditorSceneMarkDirty()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
