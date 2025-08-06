using UnityEditor;
using UnityEngine;

namespace ShapeKeyTools
{
    /// <summary>
    /// 中央パネル - TreeViewを表示
    /// </summary>
    internal class CenterPane
    {
        private readonly ShapeKeyToolWindow window;

        public CenterPane(ShapeKeyToolWindow win)
        {
            window = win;
        }

        public void OnGUI(Rect rect)
        {
            GUI.BeginGroup(rect);

            // ヘッダー
            Rect headerRect = new Rect(0, 0, rect.width, 26f);
            GUILayout.BeginArea(headerRect);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("TreeView", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();

            // 検索UI
            DrawTreeViewSearchUI(rect);

            // TreeView表示エリア（検索UIの高さを考慮）
            float searchHeight = 25f;
            Rect treeViewRect = new Rect(
                0,
                26f + searchHeight,
                rect.width,
                rect.height - 26f - searchHeight
            );
            TreeViewPart.OnGUI(treeViewRect);

            GUI.EndGroup();
        }

        /// <summary>
        /// TreeView用の検索UIを描画
        /// </summary>
        private void DrawTreeViewSearchUI(Rect rect)
        {
            Rect searchRect = new Rect(0, 26f, rect.width, 25f);
            GUILayout.BeginArea(searchRect);

            EditorGUILayout.BeginVertical();

            // 検索テキストフィールド
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("検索：", GUILayout.Width(40));
            string newSearchText = EditorGUILayout.TextField(
                SearchManager.treeViewSearchText,
                GUILayout.ExpandWidth(true)
            );

            // 検索テキストが変更された場合
            if (newSearchText != SearchManager.treeViewSearchText)
            {
                SearchManager.treeViewSearchText = newSearchText;
                TreeViewPart.Reload();
                window.Repaint();
            }

            // クリアボタン
            if (GUILayout.Button("クリア", GUILayout.Width(50)))
            {
                SearchManager.treeViewSearchText = "";
                TreeViewPart.Reload();
                window.Repaint();
            }
            EditorGUILayout.EndHorizontal();



            // 正規表現が無効な場合の警告
            if (
                SearchManager.useRegex
                && !string.IsNullOrEmpty(SearchManager.treeViewSearchText)
            )
            {
                if (!SearchManager.IsValidRegex(SearchManager.treeViewSearchText))
                {
                    EditorGUILayout.HelpBox("無効な正規表現です", MessageType.Warning);
                }
            }

            EditorGUILayout.EndVertical();

            GUILayout.EndArea();
        }
    }
}
