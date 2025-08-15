using UnityEngine;
using UnityEditor;

namespace ShapeKeyTools
{
    public partial class ShapeKeyToolWindow
    {
        private void InitializeStyles()
        {
            // コードエディタ用のスタイル
            Styles.codeStyle = new GUIStyle(EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).textArea);
            Styles.codeStyle.fontSize = 12;
            Styles.codeStyle.wordWrap = true;
            Styles.codeStyle.normal.background = null;
            
            Color textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
            Styles.codeStyle.normal.textColor = textColor;
            Styles.codeStyle.hover.textColor = textColor;
            Styles.codeStyle.active.textColor = textColor;
            Styles.codeStyle.focused.textColor = textColor;
        }
    }

    /// <summary>
    /// GUIStyle のみ管理
    /// </summary>
    internal static class Styles
    {
        internal static GUIStyle codeStyle;
    }
} 