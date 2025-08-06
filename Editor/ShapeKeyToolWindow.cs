using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ShapeKeyTools
{
    public partial class ShapeKeyToolWindow : EditorWindow
    {
        // 共通定数/設定
        private static readonly string[] HeaderPatterns = { "==", "!!", "◇◇", "★★", "◆◆", "!!!" };
        private const float LockSkipThreshold = 0.01f;
        private const string WindowTitle = "ShapeKey Tool";

        internal bool debugVerbose = false; // 過剰ログ抑制フラグ

        // スクリプト全体で共有していた変数
        internal SkinnedMeshRenderer selectedRenderer;
        private Mesh sharedMesh;
        internal readonly List<BlendShape> blendShapes = new List<BlendShape>();
        internal Vector2 scrollPosition;

        // シンタックスハイライト用の変数（UIメモ用途）
        private string pythonCode =
            "# Python形式でシェイプキーのグループ化ルールを定義\n# 階層構造に対応したグループ化ルール\n\n# グループヘッダーかどうかを判定\ndef is_group_header(name):\n    header_patterns = ['==', '!!', '◇◇', '★★', '◆◆', '!!!']\n    for pattern in header_patterns:\n        if name.startswith(pattern):\n            return True\n    return False\n\n# グループヘッダーからグループ名を抽出\ndef extract_group_name(header_name):\n    header_patterns = ['==', '!!', '◇◇', '★★', '◆◆', '!!!']\n    \n    for pattern in header_patterns:\n        if header_name.startswith(pattern):\n            # パターンを除去\n            group_name = header_name[len(pattern):]\n            \n            # 末尾の記号を除去\n            while group_name and group_name[-1] in '=!◇★◆':\n                group_name = group_name[:-1]\n            \n            return group_name.strip()\n    \n    return \"その他\"\n\n# メイン処理\ndef process_shape_keys(shape_keys):\n    current_group = \"その他\"\n    grouped_keys = {}\n    \n    for key in shape_keys:\n        if is_group_header(key):\n            current_group = extract_group_name(key)\n            if current_group not in grouped_keys:\n                grouped_keys[current_group] = []\n        else:\n            if current_group not in grouped_keys:\n                grouped_keys[current_group] = []\n            grouped_keys[current_group].append(key)\n    \n    return grouped_keys\n";

        // 他ファイル側と共有したいものは internal/protected に
        internal Dictionary<string, List<BlendShape>> groupedShapes = new();
        internal Dictionary<string, bool> groupFoldouts = new();
        internal Dictionary<string, float> groupTestSliders = new();
        internal Dictionary<int, bool> lockedShapeKeys = new();

        // グループごとに「初期 weight」を保持
        internal Dictionary<string, Dictionary<int, float>> originalWeights = new();

        // 高速探査で値が入っているものをスキップするか
        internal bool skipNonZeroValues = true;

        // 拡張シェイプキーの適用設定


        // Maxボタンのプレビュー用
        internal Dictionary<int, float> originalWeightsForMaxPreview = new Dictionary<int, float>();

        // 現在のグループ名を追跡
        internal string currentGroup = "その他";

        // メニュー用の変数
        internal int fileMenuIndex = 0;
        internal int displayMenuIndex = 0;
        internal int operationMenuIndex = 0;
        internal int shapeKeyMenuIndex = 0;
        internal int optionMenuIndex = 0;

        internal GUIContent[] fileMenuOptions = new GUIContent[]
        {
            new GUIContent("ファイル"),
            new GUIContent("JSON エクスポート"),
            new GUIContent("JSON インポート"),
            new GUIContent("永続化データの読み込み"),
            new GUIContent("コンポーネントの削除"),
        };
        internal GUIContent[] displayMenuOptions = new GUIContent[]
        {
            new GUIContent("表示"),
            new GUIContent("すべて開く"),
            new GUIContent("すべて閉じる"),
        };
        internal GUIContent[] operationMenuOptions = new GUIContent[]
        {
            new GUIContent("操作"),
            new GUIContent("ランダム設定"),
            new GUIContent("すべてロック"),
            new GUIContent("すべてアンロック"),
            new GUIContent("リセット: 0に上書き"),
            new GUIContent("初期化:TreeViewの操作"),
        };
        internal GUIContent[] shapeKeyMenuOptions = new GUIContent[]
        {
            new GUIContent("シェイプキー"),
            new GUIContent("すべてロック"),
            new GUIContent("すべて解除"),
            new GUIContent("値が入っているものをロックする"),
            new GUIContent("拡張シェイプキーを一括削除"),
            new GUIContent("TreeViewをリセットする"),
        };
        internal GUIContent[] optionMenuOptions = new GUIContent[]
        {
            new GUIContent("オプション"),
            new GUIContent("値が入っている物はスキップする"),
            new GUIContent("自動でコンポーネントに保存する"),
        };

        // ジャンプ機能用の変数
        internal string jumpToGroup = null; // ジャンプ先のグループ名
        internal bool needScrollToGroup = false; // スクロールが必要かどうかのフラグ

        // 現在のグループ表示用の変数
        internal string currentGroupDisplay = "";

        // Foldoutの状態を保持するフィールド
        internal bool treeViewFoldout = true;

        // オプション用のチェックボックス
        internal bool option1Enabled = false;
        internal bool option2Enabled = false;
        internal bool option3Enabled = false;

        // 3画面構成用のパネル
        internal CenterPane centerPane;
        private ShapeKeyToolGUI gui;

        [MenuItem("OpenTool/ShapeKey")]
        private static void ShowWindow()
        {
            GetWindow<ShapeKeyToolWindow>(WindowTitle);
        }

        private void OnEnable()
        {
            try
            {
                // 基本的な初期化を最初に実行
                InitializeStyles(); // Styles.cs
                Splitter.InitializePrefs(this); // Splitter.cs
                
                // 共通設定を初期化
                ShapeKeyToolSettings.Initialize();
                skipNonZeroValues = ShapeKeyToolSettings.SkipNonZeroValues;
                
                // 3画面構成用のパネルを初期化
                centerPane = new CenterPane(this);
                gui = new ShapeKeyToolGUI(this);
                
                // TreeViewを初期化（selectedRendererがnullでも動作するように）
                TreeViewPart.Init(this); // TreeView.cs
                
                // イベントハンドラーを登録
                Selection.selectionChanged += UpdateSelectedObject;
                
                // 選択されたオブジェクトを更新（nullチェック付き）
                UpdateSelectedObject();
                
                // 永続化データの自動読み込みを有効化
                ShapeKeyPersistenceManager.LoadData(this);
                
                // 高速探査スライダーを初期化（エラー防止のため）
                ResetTestSliders();
                
                // 初期化完了後にTreeViewを再読み込み
                TreeViewPart.Reload();
                
                // 右側のパネルを更新
                Repaint();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ShapeKeyTool初期化エラー: {ex.Message}\n{ex.StackTrace}");
                
                // エラーが発生した場合でも基本的な機能は動作するように
                try
                {
                    if (centerPane == null) centerPane = new CenterPane(this);
                    if (gui == null) gui = new ShapeKeyToolGUI(this);
                    if (TreeViewPart.GetTreeViewState() == null) TreeViewPart.Init(this);
                }
                catch (System.Exception fallbackEx)
                {
                    Debug.LogError($"ShapeKeyToolフォールバック初期化エラー: {fallbackEx.Message}");
                }
            }
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= UpdateSelectedObject;
            Splitter.SavePrefs(); // Splitter.cs
        }

        private void OnGUI()
        {
            gui.OnGUI();
        }

        #region --- 2画面パネル ---

        private void DrawCenterPanel(Rect r)
        {
            centerPane.OnGUI(r);
        }

        private void DrawRightPanel(Rect r)
        {
            // GUI描画はShapeKeyToolGUIクラスに委譲
            // このメソッドはSplitter.DrawLayoutで使用されるため残す
        }
        #endregion

        private void UpdateSelectedObject()
        {
            try
            {
                GameObject selectedObject = Selection.activeGameObject;
                if (selectedObject != null)
                {
                    selectedRenderer = selectedObject.GetComponent<SkinnedMeshRenderer>();
                    if (selectedRenderer != null && selectedRenderer.sharedMesh != null)
                    {
                        sharedMesh = selectedRenderer.sharedMesh;
                        UpdateBlendShapes();
                        
                        // 永続化データの自動読み込みを有効化
                        ShapeKeyPersistenceManager.LoadData(this);
                        
                        // TreeViewを自動更新
                        TreeViewPart.Reload();
                        
                        // 右側のパネルを更新
                        Repaint();
                    }
                    else
                    {
                        selectedRenderer = null;
                        sharedMesh = null;
                        blendShapes.Clear();
                        // groupedShapes.Clear(); // 拡張シェイプキーの情報を保持するため、クリアしない
                        
                        // TreeViewを自動更新
                        TreeViewPart.Reload();
                        
                        // 右側のパネルを更新
                        Repaint();
                    }
                }
                else
                {
                    selectedRenderer = null;
                    sharedMesh = null;
                    blendShapes.Clear();
                    // groupedShapes.Clear(); // 拡張シェイプキーの情報を保持するため、クリアしない
                    
                    // TreeViewを自動更新
                    TreeViewPart.Reload();
                    
                    // 右側のパネルを更新
                    Repaint();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"UpdateSelectedObjectエラー: {ex.Message}");
                
                // エラーが発生した場合は安全な状態にリセット
                selectedRenderer = null;
                sharedMesh = null;
                blendShapes.Clear();
                
                try
                {
                    TreeViewPart.Reload();
                    Repaint();
                }
                catch (System.Exception reloadEx)
                {
                    Debug.LogError($"TreeView再読み込みエラー: {reloadEx.Message}");
                }
            }
        }

        internal void UpdateBlendShapes()
        {
            try
            {
                blendShapes.Clear();
                if (sharedMesh != null && selectedRenderer != null)
                {
                    // まず、実際のメッシュのBlendShapeを追加
                    for (int i = 0; i < sharedMesh.blendShapeCount; i++)
                    {
                        string shapeKeyName = sharedMesh.GetBlendShapeName(i);
                        float weight = selectedRenderer.GetBlendShapeWeight(i);
                        bool isLocked = lockedShapeKeys.ContainsKey(i) ? lockedShapeKeys[i] : false;
                        
                        // 拡張パラメータをチェック
                        bool isExtended = false;
                        int minValue = -100;
                        int maxValue = 200;
                        string originalName = shapeKeyName;
                        
                        // まず、名前から拡張シェイプキーかどうかを判定
                        var extendedInfo = ExtendedShapeKeyInfo.TryParseFromName(shapeKeyName, out var info);
                        if (extendedInfo)
                        {
                            isExtended = true;
                            minValue = info.minValue;
                            maxValue = info.maxValue;
                            originalName = info.originalName;
                            
                            // 永続化マネージャーに登録
                            ExtendedShapeKeyManager.RegisterExtendedShapeKey(shapeKeyName, info);
                        }
                        else
                        {
                            // 名前から判定できない場合、永続化マネージャーから情報を取得
                            if (ExtendedShapeKeyManager.TryGetExtendedShapeKeyInfo(shapeKeyName, out var storedInfo))
                            {
                                isExtended = true;
                                minValue = storedInfo.minValue;
                                maxValue = storedInfo.maxValue;
                                originalName = storedInfo.originalName;
                            }
                        }
                        
                        var blendShape = new BlendShape
                        {
                            name = shapeKeyName,
                            weight = weight,
                            isLocked = isLocked,
                            isExtended = isExtended,
                            originalName = originalName,
                            minValue = minValue,
                            maxValue = maxValue,
                            index = i // 正しいインデックスを設定
                        };
                        
                        blendShapes.Add(blendShape);
                    }

                    // 次に、拡張シェイプキーを追加（メッシュに存在しないもの）
                    var extendedShapeKeys = ExtendedShapeKeyManager.GetAllExtendedShapeKeys();
                    foreach (var kvp in extendedShapeKeys)
                    {
                        string extendedName = kvp.Key;
                        var extendedInfo = kvp.Value;
                        
                        // メッシュに存在しない拡張シェイプキーのみ追加
                        bool existsInMesh = false;
                        for (int i = 0; i < sharedMesh.blendShapeCount; i++)
                        {
                            if (sharedMesh.GetBlendShapeName(i) == extendedName)
                            {
                                existsInMesh = true;
                                break;
                            }
                        }
                        
                        if (!existsInMesh)
                        {
                            // 元のシェイプキーのインデックスを取得
                            int originalIndex = -1;
                            for (int i = 0; i < sharedMesh.blendShapeCount; i++)
                            {
                                if (sharedMesh.GetBlendShapeName(i) == extendedInfo.originalName)
                                {
                                    originalIndex = i;
                                    break;
                                }
                            }
                            
                            var extendedBlendShape = new BlendShape
                            {
                                name = extendedName,
                                weight = 0f,
                                isLocked = false,
                                isExtended = true,
                                originalName = extendedInfo.originalName,
                                minValue = extendedInfo.minValue,
                                maxValue = extendedInfo.maxValue,
                                index = originalIndex // 元のシェイプキーのインデックスを使用
                            };
                            
                            blendShapes.Add(extendedBlendShape);
                        }
                    }

                    // グループ化を実行
                    GroupShapes();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"UpdateBlendShapesエラー: {ex.Message}");
                
                // エラーが発生した場合は安全な状態にリセット
                blendShapes.Clear();
                GroupShapes();
            }
        }

        internal void UpdateCurrentGroupDisplay()
        {
            if (groupedShapes.Count == 0)
            {
                currentGroupDisplay = "";
                return;
            }

            float currentY = scrollPosition.y;
            float headerHeight = EditorGUIUtility.singleLineHeight + 4f;
            float shapeHeight = EditorGUIUtility.singleLineHeight + 2f;
            float testSliderHeight = EditorGUIUtility.singleLineHeight + 2f;

            float accumulatedY = 0f;
            string foundGroup = "";

            foreach (var group in groupedShapes)
            {
                string groupName = group.Key;
                List<BlendShape> shapes = group.Value;

                if (currentY >= accumulatedY && currentY < accumulatedY + headerHeight)
                {
                    foundGroup = groupName;
                    break;
                }

                accumulatedY += headerHeight;

                if (groupFoldouts[groupName])
                {
                    accumulatedY += testSliderHeight;
                    accumulatedY += shapes.Count * shapeHeight;
                }

                if (
                    currentY
                        >= accumulatedY
                            - (
                                groupFoldouts[groupName]
                                    ? (testSliderHeight + shapes.Count * shapeHeight)
                                    : 0
                            )
                    && currentY < accumulatedY
                )
                {
                    foundGroup = groupName;
                    break;
                }
            }

            if (string.IsNullOrEmpty(foundGroup))
            {
                accumulatedY = 0f;
                foreach (var group in groupedShapes)
                {
                    string groupName = group.Key;
                    List<BlendShape> shapes = group.Value;

                    float groupHeight = headerHeight;
                    if (groupFoldouts[groupName])
                    {
                        groupHeight += testSliderHeight + shapes.Count * shapeHeight;
                    }

                    if (currentY < accumulatedY + groupHeight)
                    {
                        foundGroup = groupName;
                        break;
                    }

                    accumulatedY += groupHeight;
                }
            }

            currentGroupDisplay = foundGroup;
        }

        /// <summary>
        /// 拡張シェイプキーの重みを適用する
        /// </summary>
        internal void ApplyExtendedShapeKeyWeight(BlendShape blendShape)
        {
            if (selectedRenderer == null)
                return;

            var mesh = selectedRenderer.sharedMesh;
            if (mesh == null)
                return;

            try
            {
                // 元のシェイプキーのインデックスを取得
                int originalIndex = -1;
                
                // まず、blendShapeのindexをチェック
                if (blendShape.index >= 0 && blendShape.index < mesh.blendShapeCount)
                {
                    string shapeName = mesh.GetBlendShapeName(blendShape.index);
                    if (shapeName == blendShape.originalName)
                    {
                        originalIndex = blendShape.index;
                    }
                }
                
                // indexが無効な場合は、名前で検索
                if (originalIndex == -1)
                {
                    for (int i = 0; i < mesh.blendShapeCount; i++)
                    {
                        string shapeName = mesh.GetBlendShapeName(i);
                        if (shapeName == blendShape.originalName)
                        {
                            originalIndex = i;
                            break;
                        }
                    }
                }

                if (originalIndex == -1)
                {
                    Debug.LogWarning($"元のシェイプキー '{blendShape.originalName}' が見つかりません。");
                    return;
                }

                // 拡張シェイプキーの重みを元のシェイプキーに適用
                // 拡張範囲を考慮して正規化された値を計算
                float normalizedWeight = blendShape.weight;
                
                // 拡張範囲を0-100に正規化
                float range = blendShape.maxValue - blendShape.minValue;
                if (range > 0)
                {
                    normalizedWeight = (blendShape.weight - blendShape.minValue) / range * 100f;
                }
                
                // 元のシェイプキーに適用
                selectedRenderer.SetBlendShapeWeight(originalIndex, normalizedWeight);
                
                // メッシュをダーティにする
                Utility.MarkRendererDirty(selectedRenderer);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ApplyExtendedShapeKeyWeightエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 拡張シェイプキーを一括削除する
        /// </summary>
        internal void DeleteAllExtendedShapeKeys()
        {
            if (selectedRenderer == null || sharedMesh == null)
            {
                // 削除対象のオブジェクトが選択されていません
                return;
            }

            // 確認ダイアログを表示
            bool confirmed = EditorUtility.DisplayDialog(
                "拡張シェイプキーの一括削除",
                "すべての拡張シェイプキーを削除しますか？\n\nこの操作は元に戻せません。",
                "削除",
                "キャンセル"
            );

            if (!confirmed)
                return;

            try
            {
                int deletedCount = 0;
                var shapesToRemove = new List<BlendShape>();

                // 拡張シェイプキーを特定
                foreach (var group in groupedShapes)
                {
                    foreach (var shape in group.Value)
                    {
                        // isExtendedフラグまたは名前に拡張パラメータが含まれている場合
                        if (shape.isExtended || (shape.name.Contains("_min:") && shape.name.Contains("_max:")))
                        {
                            shapesToRemove.Add(shape);
                            deletedCount++;
                        }
                    }
                }

                                        // 拡張シェイプキーを削除
                        foreach (var shape in shapesToRemove)
                        {
                            // 永続化マネージャーから削除
                            ExtendedShapeKeyManager.RemoveExtendedShapeKey(shape.name);
                            
                            // groupedShapesから削除
                            foreach (var group in groupedShapes)
                            {
                                group.Value.Remove(shape);
                            }

                            // blendShapesからも削除
                            blendShapes.Remove(shape);
                        }

                // 空のグループを削除
                var emptyGroups = groupedShapes.Where(g => g.Value.Count == 0).ToList();
                foreach (var group in emptyGroups)
                {
                    groupedShapes.Remove(group.Key);
                    groupFoldouts.Remove(group.Key);
                    groupTestSliders.Remove(group.Key);
                }

                // UIを更新
                EditorUtility.SetDirty(selectedRenderer);
                TreeViewPart.Reload();

                // 削除完了
                EditorUtility.DisplayDialog(
                    "削除完了",
                    $"{deletedCount}個の拡張シェイプキーを削除しました。",
                    "OK"
                );
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "エラー",
                    $"拡張シェイプキーの削除に失敗しました:\n{ex.Message}",
                    "OK"
                );
            }
        }

        internal void ShowOptionMenu(Rect buttonRect)
        {
            GenericMenu menu = new GenericMenu();

            // チェックボックス付きのメニュー項目を追加
            menu.AddItem(
                new GUIContent("値が入っている物はスキップする"),
                skipNonZeroValues,
                () =>
                {
                    skipNonZeroValues = !skipNonZeroValues;
                    ShapeKeyToolSettings.SkipNonZeroValues = skipNonZeroValues;
                }
            );

            menu.AddItem(
                new GUIContent("検索で正規表現を使う"),
                ShapeKeyToolSettings.UseRegex,
                () =>
                {
                    ShapeKeyToolSettings.UseRegex = !ShapeKeyToolSettings.UseRegex;
                    TreeViewPart.Reload();
                    Repaint();
                }
            );

            menu.AddItem(
                new GUIContent("検索で大文字小文字を区別する"),
                ShapeKeyToolSettings.CaseSensitive,
                () =>
                {
                    ShapeKeyToolSettings.CaseSensitive = !ShapeKeyToolSettings.CaseSensitive;
                    TreeViewPart.Reload();
                    Repaint();
                }
            );

            menu.AddItem(
                new GUIContent("自動でコンポーネントに保存する"),
                ShapeKeyPersistenceManager.AutoSaveEnabled,
                () =>
                {
                    ToggleAutoSave();
                }
            );

            // ボタンの位置にメニューを表示
            menu.DropDown(buttonRect);
        }

        /// <summary>
        /// TreeViewの状態をリセットする
        /// </summary>
        internal void ResetTreeView()
        {
            if (selectedRenderer == null || sharedMesh == null)
            {
                EditorUtility.DisplayDialog(
                    "エラー",
                    "リセット対象のオブジェクトが選択されていません。",
                    "OK"
                );
                return;
            }

            // 確認ダイアログを表示
            bool confirmed = EditorUtility.DisplayDialog(
                "TreeViewリセットの確認",
                "TreeViewの状態をリセットしますか？\n\n" +
                "以下の変更が元に戻されます：\n" +
                "• グループの展開/折りたたみ状態\n" +
                "• カスタムグループの削除\n" +
                "• シェイプキーの並び順\n" +
                "• 拡張シェイプキーの削除\n" +
                "• シェイプキーの名前変更\n\n" +
                "この操作は元に戻せません。",
                "リセット",
                "キャンセル"
            );

            if (!confirmed)
                return;

            try
            {
                // 拡張シェイプキーを削除
                DeleteAllExtendedShapeKeys();

                // グループの展開状態をリセット
                groupFoldouts.Clear();
                groupTestSliders.Clear();

                // シェイプキーの名前を元に戻す
                ResetShapeKeyNames();

                // カスタムグループを削除（「その他」グループは保持）
                var groupsToRemove = new List<string>();
                foreach (var group in groupedShapes)
                {
                    if (group.Key != "その他")
                    {
                        groupsToRemove.Add(group.Key);
                    }
                }

                foreach (var groupName in groupsToRemove)
                {
                    groupedShapes.Remove(groupName);
                }

                // シェイプキーのデータを再構築
                UpdateBlendShapes();

                // TreeViewを更新
                TreeViewPart.Reload();

                EditorUtility.DisplayDialog(
                    "リセット完了",
                    "TreeViewの状態をリセットしました。",
                    "OK"
                );
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "エラー",
                    $"TreeViewのリセットに失敗しました:\n{ex.Message}",
                    "OK"
                );
            }
        }

        /// <summary>
        /// コンポーネントを削除する
        /// </summary>
        internal void RemovePersistenceComponent()
        {
            if (selectedRenderer == null)
            {
                EditorUtility.DisplayDialog(
                    "エラー",
                    "削除対象のオブジェクトが選択されていません。",
                    "OK"
                );
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "コンポーネント削除の確認",
                "シェイプキー永続化コンポーネントを削除しますか？\n\n" +
                "この操作により、保存されたシェイプキー情報が失われます。\n" +
                "この操作は元に戻せません。",
                "削除",
                "キャンセル"
            );

            if (!confirmed)
                return;

            try
            {
                ShapeKeyPersistenceManager.RemoveComponent(selectedRenderer.gameObject);
                EditorUtility.DisplayDialog(
                    "削除完了",
                    "シェイプキー永続化コンポーネントを削除しました。",
                    "OK"
                );
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "エラー",
                    $"コンポーネントの削除に失敗しました:\n{ex.Message}",
                    "OK"
                );
            }
        }

        /// <summary>
        /// 自動保存の設定を切り替え
        /// </summary>
        internal void ToggleAutoSave()
        {
            bool currentState = ShapeKeyPersistenceManager.AutoSaveEnabled;
            ShapeKeyPersistenceManager.AutoSaveEnabled = !currentState;

            // 自動保存を有効にした場合、現在のデータをコンポーネントに保存
            if (ShapeKeyPersistenceManager.AutoSaveEnabled && selectedRenderer != null)
            {
                var target = selectedRenderer.gameObject;
                var persistence = ShapeKeyPersistenceManager.GetOrCreateComponent(target);
                if (persistence != null)
                {
                    ShapeKeyPersistenceEditor.SaveData(persistence, this);
                    EditorUtility.SetDirty(target);
                }
            }

            EditorUtility.DisplayDialog(
                "自動保存設定",
                $"自動保存を{(ShapeKeyPersistenceManager.AutoSaveEnabled ? "有効" : "無効")}にしました。\n" +
                $"{(ShapeKeyPersistenceManager.AutoSaveEnabled ? "シェイプキーの値を変更すると自動でコンポーネントが追加されます。" : "")}",
                "OK"
            );
        }

        /// <summary>
        /// TreeViewの操作を初期化する（より包括的なリセット）
        /// </summary>
        internal void InitializeTreeViewOperations()
        {
            if (selectedRenderer == null || sharedMesh == null)
            {
                EditorUtility.DisplayDialog(
                    "エラー",
                    "初期化対象のオブジェクトが選択されていません。",
                    "OK"
                );
                return;
            }

            // 確認ダイアログを表示
            bool confirmed = EditorUtility.DisplayDialog(
                "TreeView操作初期化の確認",
                "TreeViewのすべての操作を初期化しますか？\n\n" +
                "以下の変更が元に戻されます：\n" +
                "• グループの展開/折りたたみ状態\n" +
                "• カスタムグループの削除\n" +
                "• シェイプキーの並び順\n" +
                "• 拡張シェイプキーの削除\n" +
                "• シェイプキーの名前変更\n" +
                "• グループ名の変更\n" +
                "• ロック状態のリセット\n" +
                "• テストスライダーの値\n" +
                "• スクロール位置\n\n" +
                "この操作は元に戻せません。",
                "初期化",
                "キャンセル"
            );

            if (!confirmed)
                return;

            try
            {
                // 拡張シェイプキーを削除
                DeleteAllExtendedShapeKeys();

                // グループの展開状態をリセット
                groupFoldouts.Clear();
                groupTestSliders.Clear();
                
                // すべてのグループを折りたたんだ状態に設定
                foreach (var group in groupedShapes)
                {
                    groupFoldouts[group.Key] = false;
                }

                // ロック状態をリセット
                lockedShapeKeys.Clear();

                // スクロール位置をリセット
                scrollPosition = Vector2.zero;

                // TreeViewStateの展開状態をクリア
                if (TreeViewPart.GetTreeViewState() != null)
                {
                    TreeViewPart.GetTreeViewState().expandedIDs.Clear();
                }

                // メニューインデックスをリセット
                fileMenuIndex = 0;
                displayMenuIndex = 0;
                operationMenuIndex = 0;
                shapeKeyMenuIndex = 0;
                optionMenuIndex = 0;

                // ジャンプ機能をリセット
                jumpToGroup = null;
                needScrollToGroup = false;
                currentGroupDisplay = "";

                // オプションをリセット
                skipNonZeroValues = false;
                option1Enabled = false;
                option2Enabled = false;
                option3Enabled = false;

                // シェイプキーの名前を元に戻す
                ResetShapeKeyNames();

                // カスタムグループを削除（「その他」グループは保持）
                var groupsToRemove = new List<string>();
                foreach (var group in groupedShapes)
                {
                    if (group.Key != "その他")
                    {
                        groupsToRemove.Add(group.Key);
                    }
                }

                foreach (var groupName in groupsToRemove)
                {
                    groupedShapes.Remove(groupName);
                }

                // シェイプキーのデータを再構築
                UpdateBlendShapes();

                // TreeViewを更新
                TreeViewPart.Reload();

                EditorUtility.DisplayDialog(
                    "初期化完了",
                    "TreeViewのすべての操作を初期化しました。",
                    "OK"
                );
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "エラー",
                    $"TreeView操作の初期化に失敗しました:\n{ex.Message}",
                    "OK"
                );
            }
        }
        
        /// <summary>
        /// 高速探査スライダーをリセット
        /// </summary>
        internal void ResetTestSliders()
        {
            if (groupTestSliders != null)
            {
                foreach (var groupName in groupTestSliders.Keys.ToList())
                {
                    groupTestSliders[groupName] = 0f;
                }
            }
            
            // 元値キャッシュもクリア
            if (originalWeights != null)
            {
                originalWeights.Clear();
            }
        }

        /// <summary>
        /// シェイプキーの名前を元のメッシュの名前に戻す
        /// </summary>
        internal void ResetShapeKeyNames()
        {
            if (selectedRenderer == null || sharedMesh == null)
                return;

            try
            {
                // メッシュから元の名前を取得して、blendShapesの名前をリセット
                for (int i = 0; i < sharedMesh.blendShapeCount && i < blendShapes.Count; i++)
                {
                    string originalName = sharedMesh.GetBlendShapeName(i);
                    if (blendShapes[i] != null && blendShapes[i].name != originalName)
                    {
                        blendShapes[i].name = originalName;
                    }
                }

                // groupedShapesの名前も更新
                foreach (var group in groupedShapes)
                {
                    foreach (var shape in group.Value)
                    {
                        // メッシュから元の名前を検索
                        for (int i = 0; i < sharedMesh.blendShapeCount; i++)
                        {
                            string originalName = sharedMesh.GetBlendShapeName(i);
                            if (shape.index == i && shape.name != originalName)
                            {
                                shape.name = originalName;
                                break;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"シェイプキー名のリセットエラー: {ex.Message}");
            }
        }
    }
}
