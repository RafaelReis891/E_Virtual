using UnityEditor;

// Configura automaticamente as animacoes do Mixamo desta pasta:
// rig Humanoid e loop nos idles. Nao altera nenhum outro asset.
public class MixamoImportConfig : AssetPostprocessor
{
    const string Pasta = "Assets/Anfitrian/Animations/Mixamo/";

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Pasta)) return;
        var imp = (ModelImporter)assetImporter;
        if (!imp.importSettingsMissing) return; // so na primeira importacao
        if (imp.animationType != ModelImporterAnimationType.Human)
            imp.animationType = ModelImporterAnimationType.Human;
        imp.materialImportMode = ModelImporterMaterialImportMode.None;
    }

    void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith(Pasta)) return;
        var imp = (ModelImporter)assetImporter;
        if (imp.clipAnimations.Length > 0) return; // so na primeira importacao
        bool loop = assetPath.Contains("Idle") || assetPath.Contains("Soccer") || assetPath.Contains("Talking");
        var clips = imp.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.loopTime = loop;
            c.lockRootPositionXZ = true;
            c.lockRootHeightY = true;
            c.lockRootRotation = true;
            c.keepOriginalPositionXZ = true;
            c.keepOriginalPositionY = true;
            c.keepOriginalOrientation = true;
        }
        imp.clipAnimations = clips;
    }
}
