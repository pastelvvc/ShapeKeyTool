using UnityEngine;
using UnityEditor;

namespace ShapeKeyTools
{
    /// <summary>
    /// 読み込みオプション選択ウィンドウ
    /// </summary>
    public class LoadOptionsWindow : EditorWindow
    {
        private ShapeKeyToolWindow mainWindow;
        
        // 詳細な読み込みオプション
        private bool loadGroupStructure = true;
        private bool loadShapeKeyValues = true;
        private bool loadLockedStates = true;
        private bool loadExtendedInfo = true;
        private bool loadFoldouts = true;
        private bool loadTestSliders = true;
        private bool loadLockedStatesFromGroups = true;
        
        // 新しい詳細オプション
        private bool loadGroupNames = true;
        private bool loadShapeKeyNames = true;
        private bool loadShapeKeyOrder = true;
        private bool validateMeshCompatibility = true;

        // 簡易モード用
        private bool useSimpleMode = false;

        public static void ShowWindow(ShapeKeyToolWindow window)
        {
            LoadOptionsWindow wnd = GetWindow<LoadOptionsWindow>("読み込みオプション");
            wnd.mainWindow = window;
            wnd.minSize = new Vector2(450, 500);
            wnd.maxSize = new Vector2(600, 800);
            wnd.autoRepaintOnSceneChange = true;
        }

        private void OnGUI()
        {
            if (mainWindow == null)
            {
                EditorGUILayout.HelpBox("メインウィンドウが見つかりません。", MessageType.Error);
                return;
            }

            // メインコンテンツエリア（スクロール可能）
            var contentRect = new Rect(0, 0, position.width, position.height - 60); // ボタン用のスペースを確保
            GUILayout.BeginArea(contentRect);
            
            // スクロールビューを使用してコンテンツを表示
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("読み込みするデータを選択してください", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            // モード切り替え
            EditorGUILayout.BeginHorizontal();
            useSimpleMode = EditorGUILayout.ToggleLeft("簡易モード", useSimpleMode, GUILayout.Width(100));
            EditorGUILayout.LabelField("（簡易モードでは基本的なオプションのみ表示）", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (useSimpleMode)
            {
                DrawSimpleMode();
            }
            else
            {
                DrawDetailedMode();
            }

            DrawInfoAndWarnings();

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();

            // ボタンエリア（固定表示）
            var buttonRect = new Rect(0, position.height - 60, position.width, 60);
            GUILayout.BeginArea(buttonRect);
            
            // 余白を削除
            GUILayout.Space(5);
            
            // ボタン
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("読み込み", GUILayout.Height(30)))
            {
                if (!loadGroupStructure && !loadShapeKeyValues && !loadLockedStates && 
                    !loadExtendedInfo && !loadFoldouts && !loadTestSliders && !loadLockedStatesFromGroups &&
                    !loadGroupNames && !loadShapeKeyNames && !loadShapeKeyOrder && !validateMeshCompatibility)
                {
                    EditorUtility.DisplayDialog(
                        "エラー",
                        "少なくとも1つの項目を選択してください。",
                        "OK"
                    );
                    return;
                }

                ShapeKeyPersistenceManager.ManualLoadWithOptions(
                    mainWindow, loadGroupStructure, loadShapeKeyValues, loadLockedStates, loadExtendedInfo,
                    loadFoldouts, loadTestSliders, loadLockedStatesFromGroups,
                    loadGroupNames, loadShapeKeyNames, loadShapeKeyOrder, validateMeshCompatibility
                );
                Close();
            }
            
            if (GUILayout.Button("キャンセル", GUILayout.Height(30)))
            {
                Close();
            }
            
            EditorGUILayout.EndHorizontal();
            
            GUILayout.EndArea();

            // ウィンドウサイズを自動調整
            AutoResizeWindow();
        }

        private Vector2 scrollPosition = Vector2.zero;

        private void AutoResizeWindow()
        {
            // 現在のウィンドウサイズを取得
            var currentSize = position.size;
            
            // 推奨サイズを計算
            float recommendedHeight = 500f; // 基本サイズ
            
            if (!useSimpleMode)
            {
                recommendedHeight += 100f; // 詳細モードの場合は追加
            }
            
            if (loadGroupStructure)
            {
                recommendedHeight += 80f; // 警告メッセージの分を追加
            }
            
            // 最小サイズを確保
            recommendedHeight = Mathf.Max(recommendedHeight, 500f);
            
            // ウィンドウサイズを調整（必要に応じて）
            if (currentSize.y < recommendedHeight)
            {
                var newSize = new Vector2(currentSize.x, recommendedHeight);
                position = new Rect(position.x, position.y, newSize.x, newSize.y);
            }
        }

        private void DrawSimpleMode()
        {
            // 全選択/全解除ボタン
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全選択", GUILayout.Width(80)))
            {
                loadGroupStructure = true;
                loadShapeKeyValues = true;
                loadLockedStates = true;
                loadExtendedInfo = true;
                loadFoldouts = true;
                loadTestSliders = true;
                loadLockedStatesFromGroups = true;
                loadGroupNames = true;
                loadShapeKeyNames = true;
                loadShapeKeyOrder = true;
                validateMeshCompatibility = true;
            }
            if (GUILayout.Button("全解除", GUILayout.Width(80)))
            {
                loadGroupStructure = false;
                loadShapeKeyValues = false;
                loadLockedStates = false;
                loadExtendedInfo = false;
                loadFoldouts = false;
                loadTestSliders = false;
                loadLockedStatesFromGroups = false;
                loadGroupNames = false;
                loadShapeKeyNames = false;
                loadShapeKeyOrder = false;
                validateMeshCompatibility = false;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // 簡易版のチェックボックス
            EditorGUILayout.BeginVertical("box");
            
            loadGroupStructure = EditorGUILayout.ToggleLeft(
                new GUIContent("グループ構成", "グループ名とシェイプキーのリストを読み込みます"), 
                loadGroupStructure
            );
            
            loadShapeKeyValues = EditorGUILayout.ToggleLeft(
                new GUIContent("シェイプキー値", "各シェイプキーのweight値を読み込みます"), 
                loadShapeKeyValues
            );
            
            loadLockedStates = EditorGUILayout.ToggleLeft(
                new GUIContent("ロック状態", "シェイプキーのロック情報を読み込みます"), 
                loadLockedStates
            );
            
            loadExtendedInfo = EditorGUILayout.ToggleLeft(
                new GUIContent("拡張シェイプキー情報", "拡張シェイプキーの詳細情報を読み込みます"), 
                loadExtendedInfo
            );
            
            loadFoldouts = EditorGUILayout.ToggleLeft(
                new GUIContent("展開状態", "グループの開閉状態を読み込みます"), 
                loadFoldouts
            );
            
            loadTestSliders = EditorGUILayout.ToggleLeft(
                new GUIContent("テストスライダー", "グループテストのスライダー値を読み込みます"), 
                loadTestSliders
            );
            
            EditorGUILayout.EndVertical();
        }

        private void DrawDetailedMode()
        {
            // 全選択/全解除ボタン
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全選択", GUILayout.Width(80)))
            {
                loadGroupStructure = true;
                loadShapeKeyValues = true;
                loadLockedStates = true;
                loadExtendedInfo = true;
                loadFoldouts = true;
                loadTestSliders = true;
                loadLockedStatesFromGroups = true;
                loadGroupNames = true;
                loadShapeKeyNames = true;
                loadShapeKeyOrder = true;
                validateMeshCompatibility = true;
            }
            if (GUILayout.Button("全解除", GUILayout.Width(80)))
            {
                loadGroupStructure = false;
                loadShapeKeyValues = false;
                loadLockedStates = false;
                loadExtendedInfo = false;
                loadFoldouts = false;
                loadTestSliders = false;
                loadLockedStatesFromGroups = false;
                loadGroupNames = false;
                loadShapeKeyNames = false;
                loadShapeKeyOrder = false;
                validateMeshCompatibility = false;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // 詳細版のチェックボックス
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.LabelField("グループ構成", EditorStyles.boldLabel);
            loadGroupStructure = EditorGUILayout.ToggleLeft(
                new GUIContent("グループ構成", "グループ名とシェイプキーのリストを読み込みます"), 
                loadGroupStructure
            );
            
            loadGroupNames = EditorGUILayout.ToggleLeft(
                new GUIContent("グループ名", "グループ名を読み込みます"), 
                loadGroupNames
            );
            
            loadShapeKeyNames = EditorGUILayout.ToggleLeft(
                new GUIContent("シェイプキー名", "シェイプキー名を読み込みます"), 
                loadShapeKeyNames
            );
            
            loadShapeKeyOrder = EditorGUILayout.ToggleLeft(
                new GUIContent("シェイプキー順序", "シェイプキーの順序を読み込みます"), 
                loadShapeKeyOrder
            );
            
            validateMeshCompatibility = EditorGUILayout.ToggleLeft(
                new GUIContent("メッシュ整合性チェック", "現在のメッシュとの整合性をチェックします"), 
                validateMeshCompatibility
            );
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("シェイプキー情報", EditorStyles.boldLabel);
            loadShapeKeyValues = EditorGUILayout.ToggleLeft(
                new GUIContent("シェイプキー値", "各シェイプキーのweight値を読み込みます"), 
                loadShapeKeyValues
            );
            
            loadExtendedInfo = EditorGUILayout.ToggleLeft(
                new GUIContent("拡張シェイプキー情報", "拡張シェイプキーの詳細情報（originalName, minValue, maxValue）を読み込みます"), 
                loadExtendedInfo
            );
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("ロック状態", EditorStyles.boldLabel);
            loadLockedStatesFromGroups = EditorGUILayout.ToggleLeft(
                new GUIContent("ロック状態（グループデータから）", "グループデータに含まれるロック状態を読み込みます"), 
                loadLockedStatesFromGroups
            );
            
            loadLockedStates = EditorGUILayout.ToggleLeft(
                new GUIContent("ロック状態（独立データから）", "独立したロック状態データを読み込みます"), 
                loadLockedStates
            );
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("UI状態", EditorStyles.boldLabel);
            loadFoldouts = EditorGUILayout.ToggleLeft(
                new GUIContent("展開状態", "グループの開閉状態を読み込みます"), 
                loadFoldouts
            );
            
            loadTestSliders = EditorGUILayout.ToggleLeft(
                new GUIContent("テストスライダー", "グループテストのスライダー値を読み込みます"), 
                loadTestSliders
            );
            
            EditorGUILayout.EndVertical();
        }

        private void DrawInfoAndWarnings()
        {
            EditorGUILayout.Space(10);

            // 説明
            EditorGUILayout.HelpBox(
                "グループ構成: グループ名とシェイプキーのリスト\n" +
                "シェイプキー値: 各シェイプキーのweight値\n" +
                "拡張シェイプキー情報: originalName, minValue, maxValue\n" +
                "ロック状態: シェイプキーのロック情報\n" +
                "展開状態: グループの開閉状態\n" +
                "テストスライダー: グループテストのスライダー値",
                MessageType.Info
            );

            // グループ構成の読み込みに関する警告
            if (loadGroupStructure)
            {
                EditorGUILayout.HelpBox(
                    "⚠️ グループ構成を読み込む場合：\n" +
                    "• 現在のメッシュに存在しないシェイプキーは自動的にスキップされます\n" +
                    "• シェイプキー順序を読み込む場合、グループ化処理が実行されます\n" +
                    "• 問題が発生した場合は、シェイプキー順序の読み込みを無効にしてください\n" +
                    "• メッシュ整合性チェックを有効にすると、存在しないシェイプキーを事前に確認できます",
                    MessageType.Warning
                );
            }
            
            // シェイプキー順序の読み込みに関する警告
            if (loadShapeKeyOrder)
            {
                EditorGUILayout.HelpBox(
                    "⚠️ シェイプキー順序を読み込む場合：\n" +
                    "• グループ化処理が実行されるため、現在のグループ構成が変更される可能性があります\n" +
                    "• 読み込んだデータと現在のメッシュの状態がマージされます\n" +
                    "• 問題が発生した場合は、このオプションを無効にしてください",
                    MessageType.Warning
                );
            }
            
            // 新しいオプションに関する説明
            if (validateMeshCompatibility)
            {
                EditorGUILayout.HelpBox(
                    "ℹ️ メッシュ整合性チェック：\n" +
                    "• 保存されたデータと現在のメッシュの整合性を事前にチェックします\n" +
                    "• 存在しないシェイプキーがある場合は警告を表示します\n" +
                    "• エラーを防ぐために推奨されます",
                    MessageType.Info
                );
            }
        }
    }
} 