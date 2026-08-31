using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 장애물(ProBuilder 토러스)을 반투명하게 만드는 도구.
///
/// Obstacle에는 SpriteRenderer가 없습니다. ProBuilder로 만든 3D 메시라
/// 투명도는 <b>머티리얼</b>이 담당합니다. 그런데 지금 장애물들이 쓰는 머티리얼은
/// ProBuilder 패키지 안에 있는 기본 머티리얼이라 직접 고칠 수 없고,
/// 고쳤다간 그 머티리얼을 쓰는 다른 오브젝트까지 같이 투명해집니다.
///
/// 그래서 이 도구는
///   1) 장애물 전용 반투명 머티리얼을 Assets/6.Materials 에 새로 만들고
///   2) 프리팹·씬에 있는 Obstacle의 MeshRenderer에만 그 머티리얼을 끼웁니다.
///
/// 사용법: Tools ▸ Zino ▸ 장애물 반투명 설정  →  색·투명도 정하고 [적용]
/// </summary>
public class ObstacleTransparencyTool : EditorWindow
{
    private const string MaterialFolder = "Assets/6.Materials";
    private const string MaterialPath = MaterialFolder + "/Obstacle_Transparent.mat";

    // 이름이 이걸로 시작하는 오브젝트의 렌더러만 갈아끼웁니다.
    private const string TargetNamePrefix = "Obstacle";

    // 뒤져볼 프리팹 폴더. 여기 있는 프리팹 안의 Obstacle까지 전부 처리합니다.
    private static readonly string[] SearchFolders = { "Assets/2.Model" };

    private Color color = new Color(1f, 0.35f, 0.35f, 0.35f);
    private bool writeDepth = false;
    private bool includeOpenScene = true;

    [MenuItem("Tools/Zino/장애물 반투명 설정", priority = 40)]
    private static void Open()
    {
        var window = GetWindow<ObstacleTransparencyTool>(true, "장애물 반투명 설정");
        window.minSize = new Vector2(380f, 250f);

        // 이미 만들어 둔 머티리얼이 있으면 그 값을 그대로 불러옵니다.
        var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null && existing.HasProperty("_BaseColor")) window.color = existing.GetColor("_BaseColor");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Obstacle은 ProBuilder 3D 메시라 SpriteRenderer가 없습니다.\n" +
            "전용 반투명 머티리얼을 만들어 Obstacle의 MeshRenderer에만 끼웁니다.",
            MessageType.Info);

        EditorGUILayout.Space();
        color = EditorGUILayout.ColorField(new GUIContent("색 (A = 투명도)"), color, true, true, false);
        color.a = EditorGUILayout.Slider("투명도 (Alpha)", color.a, 0f, 1f);

        EditorGUILayout.Space();
        writeDepth = EditorGUILayout.ToggleLeft(
            new GUIContent("Depth Write 켜기",
                "도넛처럼 자기 자신이 겹치는 모양은 이걸 켜면 앞뒤가 덜 지저분해집니다. " +
                "대신 뒤쪽 면이 안 비쳐서 조금 덜 '유리' 같아집니다."),
            writeDepth);

        includeOpenScene = EditorGUILayout.ToggleLeft("지금 열려 있는 씬의 Obstacle도 함께 적용", includeOpenScene);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("머티리얼", MaterialPath, EditorStyles.miniLabel);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("적용", GUILayout.Height(30f))) Apply();
            if (GUILayout.Button("되돌리기 (머티리얼 삭제)", GUILayout.Height(30f))) Revert();
        }
    }

    private void Apply()
    {
        Material mat = CreateOrUpdateMaterial();
        if (mat == null) return;

        int prefabHits = 0, prefabFiles = 0, sceneHits = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", SearchFolders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            int hits = AssignTo(contents.transform, mat);

            if (hits > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                prefabHits += hits;
                prefabFiles++;
            }
            PrefabUtility.UnloadPrefabContents(contents);
        }

        if (includeOpenScene)
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                sceneHits += AssignTo(root.transform, mat, recordUndo: true);

            if (sceneHits > 0)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"[Zino] 장애물 반투명 적용 완료 (alpha {color.a:F2})\n" +
                  $"  프리팹 {prefabFiles}개 안의 렌더러 {prefabHits}개\n" +
                  $"  씬 안의 렌더러 {sceneHits}개\n" +
                  $"  머티리얼: {MaterialPath}", mat);

        EditorUtility.DisplayDialog("장애물 반투명",
            $"렌더러 {prefabHits + sceneHits}개에 적용했습니다.\n투명도 {color.a:F2}", "확인");
    }

    // 이름이 Obstacle로 시작하는 오브젝트의 MeshRenderer에만 머티리얼을 끼웁니다.
    private static int AssignTo(Transform root, Material mat, bool recordUndo = false)
    {
        int count = 0;
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!renderer.gameObject.name.StartsWith(TargetNamePrefix)) continue;
            if (renderer.sharedMaterials.Length == 1 && renderer.sharedMaterial == mat) continue;

            if (recordUndo) Undo.RecordObject(renderer, "장애물 반투명");
            var slots = new Material[renderer.sharedMaterials.Length == 0 ? 1 : renderer.sharedMaterials.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = mat;
            renderer.sharedMaterials = slots;

            EditorUtility.SetDirty(renderer);
            count++;
        }
        return count;
    }

    private Material CreateOrUpdateMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            EditorUtility.DisplayDialog("장애물 반투명",
                "URP Lit 셰이더를 찾지 못했습니다. 이 프로젝트가 URP인지 확인해 주세요.", "확인");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets", MaterialFolder.Substring("Assets/".Length));

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }
        mat.shader = shader;

        // ── URP Lit을 Transparent로 세팅. 인스펙터에서 Surface Type을 Transparent로
        //    바꾸는 것과 같은 일을 코드로 합니다(설정값 하나만 빠져도 불투명하게 보입니다).
        mat.SetFloat("_Surface", 1f);                                   // 0=Opaque, 1=Transparent
        mat.SetFloat("_Blend", 0f);                                     // 0=Alpha
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWriteControl")) mat.SetFloat("_ZWriteControl", writeDepth ? 1f : 0f);
        mat.SetFloat("_ZWrite", writeDepth ? 1f : 0f);
        if (mat.HasProperty("_QueueOffset")) mat.SetFloat("_QueueOffset", 0f);

        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;

        // 반투명한 물체가 그림자를 진하게 떨구면 이상해 보여서 꺼둡니다.
        mat.SetShaderPassEnabled("ShadowCaster", false);

        mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

        // 매끈한 유리 느낌이 나도록 살짝만 조정합니다. 취향대로 인스펙터에서 바꾸셔도 됩니다.
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private void Revert()
    {
        if (!EditorUtility.DisplayDialog("되돌리기",
                $"{MaterialPath} 를 삭제합니다.\n" +
                "장애물의 머티리얼 슬롯이 비어(분홍색) 보이게 되므로, " +
                "원래 머티리얼을 다시 지정하거나 이 창에서 다시 [적용]하세요.",
                "삭제", "취소"))
            return;

        AssetDatabase.DeleteAsset(MaterialPath);
        AssetDatabase.Refresh();
    }
}
