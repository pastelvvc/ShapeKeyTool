using UnityEditor;
using UnityEngine;

namespace ShapeKeyTools
{
    /// <summary>
    /// 拡張シェイプキー設定用のポップアップウィンドウ
    /// </summary>
    public class ShapeKeyExtensionWindow : EditorWindow
    {
        private string originalShapeKeyName = "";
        private string baseShapeKeyName = "";
        private int minValue = -100;
        private int maxValue = 200;
        private System.Action<string, int, int> onApply;
        
        private Vector2 minMaxRange = new Vector2(-100, 200);
        private Vector2 scrollPosition = Vector2.zero;
        
        public static void ShowWindow(string shapeKeyName, System.Action<string, int, int> callback)
        {
            var window = GetWindow<ShapeKeyExtensionWindow>("シェイプキー拡張設定");
            window.originalShapeKeyName = shapeKeyName;
            window.baseShapeKeyName = shapeKeyName;
            window.onApply = callback;
            window.minValue = -100;
            window.maxValue = 200;
            window.minMaxRange = new Vector2(-100, 200);
            
            // ウィンドウサイズを設定（高さを増加）
            window.minSize = new Vector2(400, 300);
            window.maxSize = new Vector2(500, 350);
            
            // 画面中央に表示
            var rect = window.position;
            rect.center = new Rect(0, 0, Screen.currentResolution.width, Screen.currentResolution.height).center;
            window.position = rect;
        }
        
        private void OnGUI()
        {
            // スクロールビューを使用してレイアウトを改善
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            EditorGUILayout.Space(10);
            
            // タイトル
            EditorGUILayout.LabelField($"シェイプキー拡張設定", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"対象: {originalShapeKeyName}", EditorStyles.miniLabel);
            
            EditorGUILayout.Space(10);
            
            // 基本シェイプキー名の入力
            EditorGUILayout.LabelField("基本シェイプキー名:", EditorStyles.boldLabel);
            baseShapeKeyName = EditorGUILayout.TextField("", baseShapeKeyName);
            
            EditorGUILayout.Space(10);
            
            // 説明
            EditorGUILayout.HelpBox(
                "この機能により、シェイプキーの値を0-100の範囲を超えて設定できるようになります。\n" +
                "拡張されたシェイプキーは「元の名前_min:最小値_max:最大値」の形式で作成されます。",
                MessageType.Info
            );
            
            EditorGUILayout.Space(10);
            
            // 値の範囲設定
            EditorGUILayout.LabelField("値の範囲設定", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("範囲 (最小値 〜 最大値):", GUILayout.Width(150));
            minMaxRange = EditorGUILayout.Vector2Field("", minMaxRange);
            EditorGUILayout.EndHorizontal();
            
            // Vector2Fieldの値を整数に変換
            minValue = Mathf.RoundToInt(minMaxRange.x);
            maxValue = Mathf.RoundToInt(minMaxRange.y);
            
            // バリデーション
            bool hasError = false;
            
            if (string.IsNullOrEmpty(baseShapeKeyName))
            {
                EditorGUILayout.HelpBox("基本シェイプキー名を入力してください。", MessageType.Error);
                hasError = true;
            }
            
            if (minValue >= maxValue)
            {
                EditorGUILayout.HelpBox("最小値は最大値より小さくしてください。", MessageType.Error);
                hasError = true;
            }
            
            if (hasError)
            {
                GUI.enabled = false;
            }
            
            EditorGUILayout.Space(10);
            
            // プレビュー
            EditorGUILayout.LabelField("作成されるシェイプキー名:", EditorStyles.boldLabel);
            string previewName = $"{baseShapeKeyName}_min:{minValue}_max:{maxValue}";
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(previewName, EditorStyles.textField);
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space(20);
            
            EditorGUILayout.EndScrollView();
            
            // ボタンをスクロールビューの外に配置
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("キャンセル", GUILayout.Height(30)))
            {
                Close();
            }
            
            if (GUILayout.Button("拡張シェイプキーを作成", GUILayout.Height(30)))
            {
                if (minValue < maxValue && !string.IsNullOrEmpty(baseShapeKeyName))
                {
                    onApply?.Invoke(baseShapeKeyName, minValue, maxValue);
                    Close();
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            GUI.enabled = true;
        }
    }
} 