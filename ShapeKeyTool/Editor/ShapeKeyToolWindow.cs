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

        // 設定に移管（ShapeKeyToolSettings.DebugVerbose）
        internal bool debugVerbose => ShapeKeyToolSettings.DebugVerbose; // 互換プロパティ

        // スクリプト全体で共有していた変数
        internal SkinnedMeshRenderer selectedRenderer;
        internal Mesh sharedMesh;
        internal readonly List<BlendShape> blendShapes = new List<BlendShape>();
        internal Vector2 scrollPosition;

        // シンタックスハイライト用の変数（UIメモ用途）


        // 他ファイル側と共有したいものは internal/protected に
        internal ShapeKeyViewModel viewModel = new ShapeKeyViewModel();
        internal Dictionary<string, List<BlendShape>> groupedShapes => viewModel.groupedShapes;
        internal Dictionary<string, bool> groupFoldouts => viewModel.groupFoldouts;
        internal Dictionary<string, float> groupTestSliders => viewModel.groupTestSliders;
        internal Dictionary<int, bool> lockedShapeKeys => viewModel.lockedShapeKeys;

        // グループごとに「初期 weight」を保持
        internal Dictionary<string, Dictionary<int, float>> originalWeights => viewModel.originalWeights;

        // 高速探査で値が入っているものをスキップするか（Settingsがソースオブトゥルース）
        internal bool skipNonZeroValues => ShapeKeyToolSettings.SkipNonZeroValues;

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
            new GUIContent("手動保存"),
            new GUIContent("手動読み込み"),
            new GUIContent("JSON エクスポート"),
            new GUIContent("JSON インポート"),
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
            new GUIContent("詳細デバッグログを有効化")
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
            Selection.selectionChanged += UpdateSelectedObject;
            InitializeStyles(); // Styles.cs
            Splitter.InitializePrefs(this); // Splitter.cs
            
            // 共通設定を初期化
            ShapeKeyToolSettings.Initialize();
            
            UpdateSelectedObject();
            TreeViewPart.Init(this); // TreeView.cs

            // 3画面構成用のパネルを初期化
            centerPane = new CenterPane(this);
            gui = new ShapeKeyToolGUI(this);
            
            // 自動読み込みを削除 - 手動で読み込みを行う
            
            // 高速探査スライダーを初期化（エラー防止のため）
            ResetTestSliders();
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
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject != null)
            {
                selectedRenderer = selectedObject.GetComponent<SkinnedMeshRenderer>();
                if (selectedRenderer != null)
                {
                    sharedMesh = selectedRenderer.sharedMesh;
                    UpdateBlendShapes();
                    
                    // 自動読み込みを削除 - 手動で読み込みを行う
                    
                    // TreeViewを自動更新
                    TreeViewPart.Reload();
                }
                else
                {
                    selectedRenderer = null;
                    sharedMesh = null;
                    blendShapes.Clear();
                    // groupedShapes.Clear(); // 拡張シェイプキーの情報を保持するため、クリアしない
                    
                    // TreeViewを自動更新
                    TreeViewPart.Reload();
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
            }
        }

        internal void UpdateBlendShapes()
        {
            blendShapes.Clear();
            if (sharedMesh != null)
            {
                // まず、実際のメッシュのBlendShapeを追加
                for (int i = 0; i < sharedMesh.blendShapeCount; i++)
                {
                    string shapeKeyName = sharedMesh.GetBlendShapeName(i);
                    float weight = selectedRenderer.GetBlendShapeWeight(i);
                    bool isLocked = lockedShapeKeys.ContainsKey(i) ? lockedShapeKeys[i] : false;
                    
                    // 拡張パラメータをチェック
                    bool isExtended = false;
                    float minValue = -100f;
                    float maxValue = 200f;
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
                        else
                        {
                            // groupedShapesから既存の情報を探す（後方互換性のため）
                            foreach (var group in groupedShapes)
                            {
                                var existingShape = group.Value.FirstOrDefault(s => s.name == shapeKeyName);
                                if (existingShape != null && existingShape.isExtended)
                                {
                                    isExtended = true;
                                    minValue = existingShape.minValue;
                                    maxValue = existingShape.maxValue;
                                    originalName = existingShape.originalName;
                                    
                                    // 永続化マネージャーに登録
                                    var newInfo = new ExtendedShapeKeyInfo(originalName, minValue, maxValue);
                                    ExtendedShapeKeyManager.RegisterExtendedShapeKey(shapeKeyName, newInfo);
                                    break;
                                }
                            }
                        }
                    }
                    
                    blendShapes.Add(
                        new BlendShape
                        {
                            name = shapeKeyName,
                            weight = weight,
                            index = i,
                            isLocked = isLocked,
                            isExtended = isExtended,
                            minValue = minValue,
                            maxValue = maxValue,
                            originalName = originalName
                        }
                    );
                }

                // 永続化マネージャーから拡張シェイプキーを追加（実際のメッシュに存在しないもの）
                var allExtendedShapeKeys = ExtendedShapeKeyManager.GetAllExtendedShapeKeys();
                
                foreach (var kvp in allExtendedShapeKeys)
                {
                    string extendedName = kvp.Key;
                    var extendedInfo = kvp.Value;
                    
                    // 実際のメッシュに存在しない拡張シェイプキーのみを追加
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
                        // 既存のweight値を取得（groupedShapesから）
                        float existingWeight = 0f;
                        bool isLocked = false;
                        foreach (var group in groupedShapes)
                        {
                            var existingShape = group.Value.FirstOrDefault(s => s.name == extendedName);
                            if (existingShape != null)
                            {
                                existingWeight = existingShape.weight;
                                isLocked = existingShape.isLocked;
                                break;
                            }
                        }
                        
                        blendShapes.Add(
                            new BlendShape
                            {
                                name = extendedName,
                                weight = existingWeight,
                                index = -1, // 実際のメッシュに存在しないため-1
                                isLocked = isLocked,
                                isExtended = true,
                                minValue = extendedInfo.minValue,
                                maxValue = extendedInfo.maxValue,
                                originalName = extendedInfo.originalName
                            }
                        );
                    }
                }
            }
            GroupShapes();
        }

        /// <summary>
        /// ブレンドシェイプを再読み込み（メッシュ更新後の反映用）
        /// </summary>
        internal void RefreshBlendShapes()
        {
            // メッシュの参照を更新
            if (selectedRenderer != null)
            {
                sharedMesh = selectedRenderer.sharedMesh;
            }
            
            // ブレンドシェイプを更新
            UpdateBlendShapes();
            
            // TreeViewを更新
            TreeViewPart.Reload();
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
                // 拡張シェイプキー自体のインデックスを取得
                int extendedIndex = -1;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string shapeName = mesh.GetBlendShapeName(i);
                    if (shapeName == blendShape.name)
                    {
                        extendedIndex = i;
                        break;
                    }
                }

                if (extendedIndex != -1)
                {
                    // 拡張シェイプキーがメッシュに存在する場合は、スライダーの値を0~100の範囲に正規化して適用
                    // スライダーは0~xの範囲を0~100に収めるためのものなので、値を正規化する必要がある
                    float range = blendShape.maxValue - blendShape.minValue;
                    float normalizedWeight = blendShape.weight;
                    
                    if (range > 0)
                    {
                        normalizedWeight = (blendShape.weight - blendShape.minValue) / range * 100f;
                    }
                    
                    selectedRenderer.SetBlendShapeWeight(extendedIndex, normalizedWeight);
                }
                else
                {
                    // 拡張シェイプキーがメッシュに存在しない場合は、元のシェイプキーに正規化して適用
                    int originalIndex = -1;
                    for (int i = 0; i < mesh.blendShapeCount; i++)
                    {
                        string shapeName = mesh.GetBlendShapeName(i);
                        if (shapeName == blendShape.originalName)
                        {
                            originalIndex = i;
                            break;
                        }
                    }

                    if (originalIndex != -1)
                    {
                        // 拡張範囲を0-100に正規化
                        float range = blendShape.maxValue - blendShape.minValue;
                        float normalizedWeight = blendShape.weight;
                        
                        if (range > 0)
                        {
                            normalizedWeight = (blendShape.weight - blendShape.minValue) / range * 100f;
                        }
                        
                        // 元のシェイプキーに適用
                        selectedRenderer.SetBlendShapeWeight(originalIndex, normalizedWeight);
                    }
                }
                
                // メッシュをダーティにする
                Utility.MarkRendererDirty(selectedRenderer);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"BlendShapeLimitBreak: 拡張シェイプキーの重み適用でエラーが発生しました: {e.Message}");
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

                                        // 個別に削除（メッシュの移動を防ぐため）
                foreach (var shape in shapesToRemove)
                {
                    // 実際のメッシュから削除
                    BlendShapeLimitBreak.RemoveBlendShapeFromMesh(selectedRenderer, shape.name);
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

                // メッシュの更新を反映するため、シェイプキーリストを再読み込み
                RefreshBlendShapes();

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
                    ShapeKeyToolSettings.SkipNonZeroValues = !ShapeKeyToolSettings.SkipNonZeroValues;
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
                new GUIContent("詳細デバッグログを有効化"),
                ShapeKeyToolSettings.DebugVerbose,
                () =>
                {
                    ShapeKeyToolSettings.DebugVerbose = !ShapeKeyToolSettings.DebugVerbose;
                }
            );

            // 手動保存/読み込みはファイルメニューに移動したため、ここから削除

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
                "• 拡張シェイプキーの削除\n\n" +
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
        /// 手動保存を実行
        /// </summary>
        internal void ManualSave()
        {
            ShapeKeyPersistenceManager.ManualSave(this);
        }

        /// <summary>
        /// 手動読み込みを実行
        /// </summary>
        internal void ManualLoad()
        {
            ShowLoadOptionsDialog();
        }

        /// <summary>
        /// 読み込みオプション選択ダイアログを表示
        /// </summary>
        private void ShowLoadOptionsDialog()
        {
            if (selectedRenderer == null)
            {
                EditorUtility.DisplayDialog(
                    "エラー",
                    "読み込み対象のオブジェクトが選択されていません。",
                    "OK"
                );
                return;
            }

            var target = selectedRenderer.gameObject;
            var persistence = target.GetComponent<ShapeKeyPersistence>();
            if (persistence == null || !persistence.HasData())
            {
                EditorUtility.DisplayDialog(
                    "エラー",
                    "保存されたデータが見つかりません。\n先にデータを保存してください。",
                    "OK"
                );
                return;
            }

            // 簡易的なダイアログ（実際のUnityではより洗練されたUIが必要）
            string message = "読み込みするデータを選択してください：\n\n" +
                           "✓ グループデータ（シェイプキーの値、拡張情報）\n" +
                           "✓ 展開状態（グループの開閉状態）\n" +
                           "✓ テストスライダー（グループテストの値）\n" +
                           "✓ ロック状態（シェイプキーのロック情報）\n\n" +
                           "「全データ読み込み」を選択すると全て読み込みます。\n" +
                           "「選択読み込み」を選択すると個別に選択できます。";

            int choice = EditorUtility.DisplayDialogComplex(
                "読み込みオプション",
                message,
                "全データ読み込み",
                "選択読み込み",
                "キャンセル"
            );

            switch (choice)
            {
                case 0: // 全データ読み込み
                    ShapeKeyPersistenceManager.ManualLoadWithOptions(this, true, true, true, true);
                    break;
                case 1: // 選択読み込み
                    ShowDetailedLoadOptionsDialog();
                    break;
                case 2: // キャンセル
                    break;
            }
        }

        /// <summary>
        /// 詳細な読み込みオプション選択ダイアログを表示
        /// </summary>
        private void ShowDetailedLoadOptionsDialog()
        {
            LoadOptionsWindow.ShowWindow(this);
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
                ShapeKeyToolSettings.SkipNonZeroValues = false;
                option1Enabled = false;
                option2Enabled = false;
                option3Enabled = false;

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
    }
}
