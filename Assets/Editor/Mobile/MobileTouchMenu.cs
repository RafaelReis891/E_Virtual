// MobileTouchMenu.cs (Editor)
// Menu: Mobile > Tools > Simular controles touch no Play
// Liga/desliga os controles de toque no Play do Editor (o mouse faz o papel do dedo).
// Pode alternar durante o Play. Fica salvo so nesta maquina (EditorPrefs); nao entra na build.
using UnityEditor;
using UnityEngine;

public static class MobileTouchMenu
{
    const string Caminho = "Mobile/Tools/Simular controles touch no Play";

    [MenuItem(Caminho, false, 300)]
    static void Alternar()
    {
        bool novo = !EditorPrefs.GetBool(MobileInput.PrefSimularEditor, false);
        EditorPrefs.SetBool(MobileInput.PrefSimularEditor, novo);
        if (Application.isPlaying) MobileInput.Reavaliar();
        Debug.Log("[MobileTouch] Simular controles touch no Play: " + (novo ? "LIGADO" : "DESLIGADO")
            + (Application.isPlaying ? "" : " (vale no proximo Play)"));
    }

    [MenuItem(Caminho, true)]
    static bool Validar()
    {
        Menu.SetChecked(Caminho, EditorPrefs.GetBool(MobileInput.PrefSimularEditor, false));
        return true;
    }
}
