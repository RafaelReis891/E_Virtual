#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Menu: Tools > Emuseu > Criar Prefab Anfitrian V4
// Configura o import do Assets/Games/Models/Anfitrian_V4/Anfitrian_V4.fbx (Humanoid, escala 1.1 como a V2/V3),
// cria o material M_Anfitria_V4 (mesmo shader do material da V2 + textura V4) e gera o prefab
// Assets/Games/Models/Anfitrian_V4/Anfitrian_V4.prefab (variante do FBX, igual ao Anfitrian-Prefab da V2:
// Animator com IdlesAnfitrian.controller, SphereCollider trigger, CharacterAnimationTrigger e OlharParaCamera).
// NAO altera a V2, cenas ou prefabs existentes. Pode ser rodado de novo (pergunta antes de sobrescrever).
public static class AnfitrianV4PrefabMenu
{
    const string Pasta = "Assets/Games/Models/Anfitrian_V4";
    const string FbxPath = Pasta + "/Anfitrian_V4.fbx";
    const string TexPath = Pasta + "/T_Anfitrian_V4_BaseColor.png";
    const string MatPath = Pasta + "/M_Anfitria_V4.mat";
    const string PrefabPath = Pasta + "/Anfitrian_V4.prefab";
    const string MatV2Path = "Assets/Games/Models/Anfitrian_V2/M_Anfitria_BaseMap.mat";
    const string ControllerPath = "Assets/Anfitrian/Animations/IdlesAnfitrian.controller";
    const string FbxMaterialName = "tripo_mat_14cacc52"; // nome do material dentro do FBX V4

    [MenuItem("Tools/Emuseu/Criar Prefab Anfitrian V4")]
    static void Criar()
    {
        AssetDatabase.Refresh();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath) == null)
        {
            Debug.LogError("[AnfitrianV4] FBX nao encontrado/importado: " + FbxPath);
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            !EditorUtility.DisplayDialog("Anfitrian V4", "Anfitrian_V4.prefab ja existe. Sobrescrever?", "Sobrescrever", "Cancelar"))
            return;

        // 1) Material V4 (shader do material da V2, ou URP/Lit)
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            var matV2 = AssetDatabase.LoadAssetAtPath<Material>(MatV2Path);
            Shader sh = matV2 != null ? matV2.shader : Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            mat = new Material(sh) { name = "M_Anfitria_V4" };
            if (matV2 != null) mat.CopyPropertiesFromMaterial(matV2);
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        if (tex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
        }

        // 2) Import settings do FBX (Humanoid, escala 1.1 como a V2/V3) + remap do material
        var imp = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (imp == null) { Debug.LogError("[AnfitrianV4] ModelImporter nulo: " + FbxPath); return; }
        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.globalScale = 1.1f;
        imp.useFileScale = true;
        imp.meshCompression = ModelImporterMeshCompression.Medium;
        imp.isReadable = false;
        imp.optimizeGameObjects = false;
        imp.importCameras = false;
        imp.importLights = false;
        imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), FbxMaterialName), mat);
        imp.SaveAndReimport();
        AssetDatabase.SaveAssets();

        // 3) Prefab (variante do FBX)
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        go.name = "Anfitrian_V4";
        try
        {
            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
            else Debug.LogWarning("[AnfitrianV4] Controller nao encontrado: " + ControllerPath);
            if (anim.avatar == null || !anim.avatar.isHuman)
                Debug.LogWarning("[AnfitrianV4] Avatar nao e Humanoid valido - abra o FBX > Rig > Configure.");

            var col = go.GetComponent<SphereCollider>();
            if (col == null) col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.5f;
            col.center = Vector3.zero;

            var t1 = AddPorNome(go, "CharacterAnimationTrigger");
            if (t1 != null)
            {
                SetCampo(t1, "activateOnTrigger", true);
                SetCampo(t1, "playerTag", "Player");
                SetCampo(t1, "playSequence", true);
                SetCampo(t1, "selectedAnimation", "Talking02");
                SetCampo(t1, "animationSequence", new System.Collections.Generic.List<string> { "Talking01", "Talking03" });
                SetCampo(t1, "delayBetweenAnimations", 0.5f);
            }
            var t2 = AddPorNome(go, "OlharParaCamera");
            if (t2 != null) SetCampo(t2, "tipo", 1);

            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        var criado = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Selection.activeObject = criado;
        EditorGUIUtility.PingObject(criado);
        Debug.Log("[AnfitrianV4] Prefab criado: " + PrefabPath + " (Humanoid, escala 1.1). Arraste para uma cena de teste; nenhuma cena foi alterada.");
    }

    static Component AddPorNome(GameObject go, string nomeTipo)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = null;
            try { t = asm.GetType(nomeTipo); } catch { }
            if (t != null && typeof(Component).IsAssignableFrom(t))
                return go.GetComponent(t) ?? go.AddComponent(t);
        }
        Debug.LogWarning("[AnfitrianV4] Script nao encontrado (pulado): " + nomeTipo);
        return null;
    }

    static void SetCampo(Component c, string campo, object valor)
    {
        var f = c.GetType().GetField(campo, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f == null) return;
        try
        {
            if (valor is System.Collections.Generic.List<string> lista && f.FieldType == typeof(string[])) valor = lista.ToArray();
            if (f.FieldType.IsEnum && valor is int) valor = Enum.ToObject(f.FieldType, (int)valor);
            f.SetValue(c, valor);
        }
        catch (Exception e) { Debug.LogWarning("[AnfitrianV4] campo " + campo + ": " + e.Message); }
    }
}
#endif