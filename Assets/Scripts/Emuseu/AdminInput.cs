using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Leitura de teclas das ferramentas de admin (AdminToolsPanel / AdminPlayerControl).
// Funciona com Input System novo, Input Manager antigo ou os dois (activeInputHandler = Both neste projeto).
public static class AdminInput
{
    public enum Tecla { F2, Esc, W, A, S, D, Space, Ctrl, Q, Shift }

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
                case Tecla.F2: if (Input.GetKeyDown(KeyCode.F2)) return true; break;
                case Tecla.Esc: if (Input.GetKeyDown(KeyCode.Escape)) return true; break;
                default: break;
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
}