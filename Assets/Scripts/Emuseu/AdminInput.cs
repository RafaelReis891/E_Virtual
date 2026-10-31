using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Leitura de teclas das ferramentas de admin (AdminToolsPanel / AdminPlayerControl).
// Funciona com Input System novo, Input Manager antigo ou os dois (activeInputHandler = Both neste projeto).
public static class AdminInput
{
    // N1..N9 precisam ficar contiguos (o painel usa N1 + indice)
    public enum Tecla { F2, Esc, W, A, S, D, Space, Ctrl, Q, Shift, G, N1, N2, N3, N4, N5, N6, N7, N8, N9 }

    public static bool Apertou(Tecla t)
    {
#if ENABLE_INPUT_SYSTEM
        try
        {
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                switch (t)
                {
                    case Tecla.F2: if (k.f2Key.wasPressedThisFrame) return true; break;
                    case Tecla.Esc: if (k.escapeKey.wasPressedThisFrame) return true; break;
                    case Tecla.G: if (k.gKey.wasPressedThisFrame) return true; break;
                    default:
                        if (t >= Tecla.N1 && t <= Tecla.N9)
                        {
                            int n = (int)t - (int)Tecla.N1;
                            if (k[(Key)((int)Key.Digit1 + n)].wasPressedThisFrame || k[(Key)((int)Key.Numpad1 + n)].wasPressedThisFrame) return true;
                        }
                        break;
                }
            }
        }
        catch (Exception) { }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            switch (t)
            {
                case Tecla.F2: if (Input.GetKeyDown(KeyCode.F2)) return true; break;
                case Tecla.Esc: if (Input.GetKeyDown(KeyCode.Escape)) return true; break;
                case Tecla.G: if (Input.GetKeyDown(KeyCode.G)) return true; break;
                default:
                    if (t >= Tecla.N1 && t <= Tecla.N9)
                    {
                        int n = (int)t - (int)Tecla.N1;
                        if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + n)) || Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + n))) return true;
                    }
                    break;
            }
        }
        catch (Exception) { }
#endif
        return false;
    }

    public static bool Segurando(Tecla t)
    {
#if ENABLE_INPUT_SYSTEM
        try
        {
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                switch (t)
                {
                    case Tecla.W: if (k.wKey.isPressed) return true; break;
                    case Tecla.A: if (k.aKey.isPressed) return true; break;
                    case Tecla.S: if (k.sKey.isPressed) return true; break;
                    case Tecla.D: if (k.dKey.isPressed) return true; break;
                    case Tecla.Q: if (k.qKey.isPressed) return true; break;
                    case Tecla.Space: if (k.spaceKey.isPressed) return true; break;
                    case Tecla.Ctrl: if (k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed) return true; break;
                    case Tecla.Shift: if (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed) return true; break;
                    default: break;
                }
            }
        }
        catch (Exception) { }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            switch (t)
            {
                case Tecla.W: if (Input.GetKey(KeyCode.W)) return true; break;
                case Tecla.A: if (Input.GetKey(KeyCode.A)) return true; break;
                case Tecla.S: if (Input.GetKey(KeyCode.S)) return true; break;
                case Tecla.D: if (Input.GetKey(KeyCode.D)) return true; break;
                case Tecla.Q: if (Input.GetKey(KeyCode.Q)) return true; break;
                case Tecla.Space: if (Input.GetKey(KeyCode.Space)) return true; break;
                case Tecla.Ctrl: if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return true; break;
                case Tecla.Shift: if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return true; break;
                default: break;
            }
        }
        catch (Exception) { }
#endif
        return false;
    }

    // ---- mouse (polling; o painel de admin nao depende dos eventos de mouse do IMGUI) ----

    // posicao em pixels da tela, origem no canto inferior esquerdo (igual Input.mousePosition)
    public static Vector2 PosicaoMouse()
    {
#if ENABLE_INPUT_SYSTEM
        try
        {
            Mouse m = Mouse.current;
            if (m != null) return m.position.ReadValue();
        }
        catch (Exception) { }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        try { Vector3 p = Input.mousePosition; return new Vector2(p.x, p.y); }
        catch (Exception) { }
#endif
        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    public static bool CliquePressionado()
    {
#if ENABLE_INPUT_SYSTEM
        try
        {
            Mouse m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame) return true;
        }
        catch (Exception) { }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        try { if (Input.GetMouseButtonDown(0)) return true; }
        catch (Exception) { }
#endif
        return false;
    }

    public static bool CliqueSegurando()
    {
#if ENABLE_INPUT_SYSTEM
        try
        {
            Mouse m = Mouse.current;
            if (m != null && m.leftButton.isPressed) return true;
        }
        catch (Exception) { }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        try { if (Input.GetMouseButton(0)) return true; }
        catch (Exception) { }
#endif
        return false;
    }

    // roda do mouse: >0 para cima, <0 para baixo (so o sinal importa)
    public static float Roda()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        try { float d = Input.mouseScrollDelta.y; if (d != 0f) return d; }
        catch (Exception) { }
#endif
#if ENABLE_INPUT_SYSTEM
        try
        {
            Mouse m = Mouse.current;
            if (m != null) return m.scroll.ReadValue().y;
        }
        catch (Exception) { }
#endif
        return 0f;
    }}
