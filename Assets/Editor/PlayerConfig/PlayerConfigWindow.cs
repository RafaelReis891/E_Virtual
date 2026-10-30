using UnityEditor;
using UnityEngine;

public class PlayerConfigWindow : EditorWindow
{
    PlayerConfig cfg;
    FinalCardAlbum album;
    Vector2 scroll;
    string filtro = "";
    const string Caminho = "Assets/Resources/PlayerConfig.asset";

    [MenuItem("Tools/Player Config")]
    static void Abrir() => GetWindow<PlayerConfigWindow>("Player Config");

    void OnEnable()
    {
        cfg = AssetDatabase.LoadAssetAtPath<PlayerConfig>(Caminho);
        var json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Scripts/FinalCard/cards.json");
        if (json != null) album = JsonUtility.FromJson<FinalCardAlbum>(json.text);
    }

    void OnGUI()
    {
        if (cfg == null)
        {
            if (GUILayout.Button("Criar PlayerConfig em " + Caminho))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                cfg = CreateInstance<PlayerConfig>(); cfg.Garantir();
                AssetDatabase.CreateAsset(cfg, Caminho); AssetDatabase.SaveAssets();
            }
            return;
        }
        cfg.Garantir();
        EditorGUI.BeginChangeCheck();
        scroll = EditorGUILayout.BeginScrollView(scroll);

        cfg.idioma = (IdiomaJogador)EditorGUILayout.EnumPopup("Idioma", cfg.idioma);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Estados (E1 a E5)", EditorStyles.boldLabel);
        foreach (var e in cfg.estados)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(e.estado, GUILayout.Width(40));
            for (int i = 0; i < 5; i++) e.etapas[i] = GUILayout.Toggle(e.etapas[i], "E" + (i + 1), GUILayout.Width(40));
            EditorGUILayout.LabelField(e.Concluidas() + "/5", GUILayout.Width(30));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Cartas (" + cfg.cartas.Count + ")", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Marcar todas") && album != null) foreach (var c in album.cards) cfg.AdicionarCarta(c.id);
        if (GUILayout.Button("Limpar")) cfg.cartas.Clear();
        EditorGUILayout.EndHorizontal();
        filtro = EditorGUILayout.TextField("Filtro", filtro);
        if (album != null)
            foreach (var c in album.cards)
            {
                string rot = c.id + " - " + c.title;
                if (!string.IsNullOrEmpty(filtro) && rot.IndexOf(filtro, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool tem = cfg.TemCarta(c.id);
                bool novo = EditorGUILayout.ToggleLeft(rot, tem);
                if (novo != tem) { if (novo) cfg.AdicionarCarta(c.id); else cfg.cartas.Remove(c.id); }
            }
        else EditorGUILayout.HelpBox("cards.json nao encontrado.", MessageType.Warning);

        EditorGUILayout.Space();
        if (GUILayout.Button("Copiar JSON (exemplo de payload)")) EditorGUIUtility.systemCopyBuffer = cfg.ParaJson();

        EditorGUILayout.EndScrollView();
        if (EditorGUI.EndChangeCheck()) { EditorUtility.SetDirty(cfg); }
    }
}