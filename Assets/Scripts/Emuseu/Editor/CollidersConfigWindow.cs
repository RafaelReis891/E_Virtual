// Parametros do gerador de colliders na selecao (Tools > Emuseu > Colliders Config).
// Salvos em EditorPrefs (por maquina/usuario do Editor).
#if UNITY_EDITOR
namespace Emuseu.EditorTools
{
    using UnityEditor;
    using UnityEngine;

    public static class CollidersSettings
    {
        const string K = "Emuseu.Colliders.";
        public const double DefTolerance = 0.30, DefThickness = 0.40, DefMinHeight = 3.0, DefCloseRadius = 0.40;
        public const int DefMaxSegments = 100;
        public const bool DefIncludeChildren = false;

        public static double Tolerance   { get { return EditorPrefs.GetFloat(K + "Tolerance", (float)DefTolerance); }     set { EditorPrefs.SetFloat(K + "Tolerance", (float)value); } }
        public static double Thickness   { get { return EditorPrefs.GetFloat(K + "Thickness", (float)DefThickness); }     set { EditorPrefs.SetFloat(K + "Thickness", (float)value); } }
        public static double MinHeight   { get { return EditorPrefs.GetFloat(K + "MinHeight", (float)DefMinHeight); }     set { EditorPrefs.SetFloat(K + "MinHeight", (float)value); } }
        public static double CloseRadius { get { return EditorPrefs.GetFloat(K + "CloseRadius", (float)DefCloseRadius); } set { EditorPrefs.SetFloat(K + "CloseRadius", (float)value); } }
        public static int MaxSegments    { get { return EditorPrefs.GetInt(K + "MaxSegments", DefMaxSegments); }          set { EditorPrefs.SetInt(K + "MaxSegments", value); } }
        public static bool IncludeChildren { get { return EditorPrefs.GetBool(K + "IncludeChildren", DefIncludeChildren); } set { EditorPrefs.SetBool(K + "IncludeChildren", value); } }

        public static RailOutlineParams ToParams()
        {
            return new RailOutlineParams
            {
                Tolerance = System.Math.Max(0.02, Tolerance),
                Thickness = System.Math.Max(0.02, Thickness),
                MinHeight = System.Math.Max(0.1, MinHeight),
                CloseRadius = System.Math.Max(0.05, CloseRadius),
                MaxSegments = System.Math.Max(1, MaxSegments)
            };
        }

        public static void Reset()
        {
            Tolerance = DefTolerance; Thickness = DefThickness; MinHeight = DefMinHeight;
            CloseRadius = DefCloseRadius; MaxSegments = DefMaxSegments; IncludeChildren = DefIncludeChildren;
        }
    }

    public class CollidersConfigWindow : EditorWindow
    {
        public static void Abrir()
        {
            var w = GetWindow<CollidersConfigWindow>(true, "Colliders Config", true);
            w.minSize = new Vector2(380, 250);
            w.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            float tol = EditorGUILayout.Slider(new GUIContent("Tolerance (m)", "Douglas-Peucker: menor = mais segmentos, mais fiel ao contorno."), (float)CollidersSettings.Tolerance, 0.02f, 2f);
            float thick = EditorGUILayout.Slider(new GUIContent("Thickness (m)", "Espessura de cada BoxCollider."), (float)CollidersSettings.Thickness, 0.02f, 3f);
            float minH = EditorGUILayout.Slider(new GUIContent("Min Height (m)", "Altura minima dos boxes."), (float)CollidersSettings.MinHeight, 0.1f, 20f);
            float close = EditorGUILayout.Slider(new GUIContent("Close Radius (m)", "Fecha vaos menores que 2x este valor (ex.: 0.4 fecha vaos < 0.8 m)."), (float)CollidersSettings.CloseRadius, 0.05f, 2f);
            int maxSeg = EditorGUILayout.IntSlider(new GUIContent("Max Segments", "Se passar, a tolerancia sobe automaticamente ate caber."), CollidersSettings.MaxSegments, 1, 2000);
            bool kids = EditorGUILayout.Toggle(new GUIContent("Incluir filhos", "Combina tambem os MeshFilters dos filhos ativos do objeto selecionado."), CollidersSettings.IncludeChildren);
            if (EditorGUI.EndChangeCheck())
            {
                CollidersSettings.Tolerance = tol; CollidersSettings.Thickness = thick; CollidersSettings.MinHeight = minH;
                CollidersSettings.CloseRadius = close; CollidersSettings.MaxSegments = maxSeg; CollidersSettings.IncludeChildren = kids;
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Restaurar padrao")) { CollidersSettings.Reset(); GUI.FocusControl(null); Repaint(); }
                using (new EditorGUI.DisabledScope(!SelectionCollidersGenerator.PodeGerar()))
                    if (GUILayout.Button("Gerar na selecao")) SelectionCollidersGenerator.GerarNaSelecao();
                using (new EditorGUI.DisabledScope(!SelectionCollidersGenerator.PodeRemover()))
                    if (GUILayout.Button("Remover da selecao")) SelectionCollidersGenerator.RemoverDaSelecao();
            }
            EditorGUILayout.HelpBox("Selecione GameObjects com MeshFilter na Hierarchy e use Tools > Emuseu > Gerar Colliders na Selecao " +
                "(ou botao direito > Emuseu). Cria '<Nome>_Colliders' como irmao e desabilita o MeshCollider.", MessageType.Info);
        }

        void OnSelectionChange() { Repaint(); }
    }
}
#endif