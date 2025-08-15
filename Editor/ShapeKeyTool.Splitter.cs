using UnityEditor;
using UnityEngine;

namespace ShapeKeyTools
{
    /// <summary>
    /// 2画面スプリッタ（中央 + 右）を管理
    /// </summary>
    internal static class Splitter
    {
        private const string CenterPanelRatioKey = "ShapeKeyTool_CenterPanelRatio";

        internal static float centerPanelRatio = 0.6f;  // 中央パネル 60%
        private static bool centerResize;
        private const float splitterWidth = 4f;

        internal static void InitializePrefs(ShapeKeyToolWindow w)
        {
            centerPanelRatio = EditorPrefs.GetFloat(CenterPanelRatioKey, 0.6f);
        }

        internal static void SavePrefs()
        {
            EditorPrefs.SetFloat(CenterPanelRatioKey, centerPanelRatio);
        }

        internal static void DrawLayout(
            ShapeKeyToolWindow win, Rect root,
            System.Action<Rect> drawCenter,
            System.Action<Rect> drawRight)
        {
            // 2画面の幅を計算
            float centerW = Mathf.Clamp(root.width * centerPanelRatio, 200f, root.width - 150f);
            float rightW = root.width - centerW - splitterWidth;

            // 各パネルの位置を計算
            var centerR = new Rect(0, 0, centerW, root.height);
            var centerSplit = new Rect(centerW, 0, splitterWidth, root.height);
            var rightR = new Rect(centerW + splitterWidth, 0, rightW, root.height);

            // 描画
            drawCenter(centerR);
            EditorGUI.DrawRect(centerSplit, new Color(.2f, .2f, .2f));
            EditorGUIUtility.AddCursorRect(centerSplit, MouseCursor.ResizeHorizontal);
            HandleCenterSplit(centerSplit, win);
            
            drawRight(rightR);
        }



        private static void HandleCenterSplit(Rect r, EditorWindow w)
        {
            Event e = Event.current;
            switch (e.rawType)
            {
                case EventType.MouseDown:
                    if (r.Contains(e.mousePosition) && e.button == 0) { centerResize = true; e.Use(); }
                    break;
                case EventType.MouseDrag:
                    if (centerResize)
                    {
                        centerPanelRatio = Mathf.Clamp(e.mousePosition.x / w.position.width, .3f, .8f);
                        w.Repaint(); e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (centerResize) { centerResize = false; e.Use(); }
                    break;
            }
        }


    }
} 