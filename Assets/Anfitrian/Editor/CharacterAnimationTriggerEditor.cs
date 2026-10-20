using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(CharacterAnimationTrigger))]
public class CharacterAnimationTriggerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CharacterAnimationTrigger script = (CharacterAnimationTrigger)target;
        Animator animator = script.GetComponent<Animator>();

        // Desenha os campos normais
        DrawDefaultInspector();

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            EditorGUILayout.HelpBox("Adicione um Animator com Controller para ver as animações.", MessageType.Warning);
            return;
        }

        // Pega todos os nomes dos estados/clips
        List<string> animNames = new List<string>();
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
        {
            if (!animNames.Contains(clip.name))
                animNames.Add(clip.name);
        }

        if (animNames.Count == 0) return;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Seleção de Animações", EditorStyles.boldLabel);

        // Dropdown para animação única
        if (!script.playSequence)
        {
            int currentIndex = Mathf.Max(0, animNames.IndexOf(script.selectedAnimation));
            int newIndex = EditorGUILayout.Popup("Animação", currentIndex, animNames.ToArray());
            script.selectedAnimation = animNames[newIndex];
        }
        else
        {
            // Lista para sequência
            EditorGUILayout.LabelField("Sequência de Animações");
            for (int i = 0; i < script.animationSequence.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                int idx = Mathf.Max(0, animNames.IndexOf(script.animationSequence[i]));
                int newIdx = EditorGUILayout.Popup(idx, animNames.ToArray());
                script.animationSequence[i] = animNames[newIdx];

                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    script.animationSequence.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Adicionar Animação"))
                script.animationSequence.Add(animNames[0]);
        }

        if (GUI.changed)
            EditorUtility.SetDirty(script);
    }
}