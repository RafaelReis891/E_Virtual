using UnityEditor;
using UnityEngine;

// Inspector do RemoteTexto: mostra a key efetiva (nome do objeto ou keyOverride) e o texto encontrado.
[CustomEditor(typeof(RemoteTexto))]
[CanEditMultipleObjects]
public sealed class RemoteTextoEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (targets.Length != 1) return;

        var rt = (RemoteTexto)target;
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField(new GUIContent("Key efetiva", "Key do setting no site. Nome do objeto, ou keyOverride se preenchido."), rt.Key);
            Component alvo = rt.TextoAlvo;
            EditorGUILayout.ObjectField(new GUIContent("Texto encontrado", "TMP_Text / Text / TextMesh neste objeto ou nos filhos."), alvo, typeof(Component), true);
        }
        if (rt.TextoAlvo == null)
            EditorGUILayout.HelpBox("Nenhum TMP_Text / Text / TextMesh neste objeto nem nos filhos.", MessageType.Warning);
        string k = rt.Key;
        if (k == "Text" || k == "Text (TMP)" || k == "Dica" || k == "Titulo" || k == "Descricao" || k.StartsWith("GameObject"))
            EditorGUILayout.HelpBox("Nome generico: outros objetos com o mesmo nome vao mostrar o mesmo texto. Use keyOverride.", MessageType.Info);
    }
}