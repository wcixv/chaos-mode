// IMGUI in this game never receives mouse clicks (the game routes all input through the new Input System),
// so we draw with IMGUI but read the mouse/keyboard ourselves and hit-test the button rects.
using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ModShared
{
    public class ClickGui
    {
        private Vector2? pendingClick;      // screen position (top-left origin) of an unhandled click
        private Vector2 windowOffset;       // top-left of the window currently being drawn
        private string focusedField;        // id of the text field that has keyboard focus
        private readonly StringBuilder typed = new StringBuilder();
        private bool backspace;
        private bool hooked;

        // Call from Update() every frame.
        public void Poll(bool menuOpen)
        {
            if (!hooked && Keyboard.current != null)
            {
                Keyboard.current.onTextInput += c => { if (focusedField != null && !char.IsControl(c)) typed.Append(c); };
                hooked = true;
            }
            var mouse = Mouse.current;
            if (menuOpen && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Vector2 p = mouse.position.ReadValue();
                pendingClick = new Vector2(p.x, Screen.height - p.y);
            }
            if (focusedField != null && Keyboard.current != null && Keyboard.current.backspaceKey.wasPressedThisFrame) backspace = true;
            if (!menuOpen) { pendingClick = null; focusedField = null; }
        }

        public bool Typing => focusedField != null;

        // Call at the start of the window function.
        public void BeginWindow(Rect windowRect) { windowOffset = windowRect.position; }

        // Call at the end of OnGUI (after all windows): a click that hit nothing unfocuses text fields.
        public void EndFrame()
        {
            if (Event.current.type == EventType.Repaint && pendingClick.HasValue) { pendingClick = null; focusedField = null; }
        }

        private bool Hit(Rect local)
        {
            if (Event.current.type != EventType.Repaint || !pendingClick.HasValue) return false;
            var screen = new Rect(local.position + windowOffset, local.size);
            if (!screen.Contains(pendingClick.Value)) return false;
            pendingClick = null;
            return true;
        }

        public bool Button(string label, params GUILayoutOption[] opts)
        {
            Rect r = GUILayoutUtility.GetRect(new GUIContent(label), GUI.skin.button, opts);
            GUI.Button(r, label);
            return Hit(r);
        }

        public bool Toggle(bool value, string label)
        {
            return Button((value ? "[ON]  " : "[off]  ") + label) ? !value : value;
        }

        public string TextField(string id, string value, int maxLength)
        {
            value = value ?? "";
            bool focused = focusedField == id;
            string shown = value + (focused && (int)(Time.unscaledTime * 2) % 2 == 0 ? "|" : "");
            Rect r = GUILayoutUtility.GetRect(new GUIContent(shown + " "), GUI.skin.textField);
            GUI.Box(r, new GUIContent(focused ? shown : (value.Length == 0 ? "(click to type)" : value)), GUI.skin.textField);
            if (Hit(r)) focusedField = id;
            if (focused && Event.current.type == EventType.Repaint)
            {
                if (backspace && value.Length > 0) value = value.Substring(0, value.Length - 1);
                backspace = false;
                if (typed.Length > 0) { value += typed.ToString(); typed.Length = 0; }
                if (value.Length > maxLength) value = value.Substring(0, maxLength);
            }
            return value;
        }
    }
}
