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

		// 冗長デバッグフラグは ShapeKeyToolSettings.DebugVerbose に集約

        // スクリプト全体で共有していた変数
        internal SkinnedMeshRenderer selectedRenderer;
        internal Mesh sharedMesh;
        internal readonly List<BlendShape> blendShapes = new List<BlendShape>();
		// スクロール位置は ViewModel に集約

        // シンタックスハイライト用の変数（UIメモ用途）


		// ViewModel に集約
		internal ShapeKeyViewModel viewModel = new ShapeKeyViewModel();

		// 高速探査のスキップ設定は ShapeKeyToolSettings.SkipNonZeroValues に集約

        // 拡張シェイプキーの適用設定


        // Maxボタンのプレビュー用
        internal Dictionary<int, float> originalWeightsForMaxPreview = new Dictionary<int, float>();

        // 現在のグループ名を追跡
        internal string currentGroup = "その他";

        // メニュー用の変数
        internal int fileMenuIndex = 0;
        internal int editMenuIndex = 0;
        internal int displayMenuIndex = 0;
        internal int operationMenuIndex = 0;
        internal int shapeKeyMenuIndex = 0;
        internal int optionMenuIndex = 0;

		internal GUIContent[] fileMenuOptions = new GUIContent[]
        {
			new GUIContent(UIStrings.MenuFile),
			new GUIContent(UIStrings.MenuManualSave),
			new GUIContent(UIStrings.MenuManualLoad),
			new GUIContent(UIStrings.MenuJsonExport),
			new GUIContent(UIStrings.MenuJsonImport),
			new GUIContent(UIStrings.MenuRemoveComponent),
        };
        internal GUIContent[] editMenuOptions = new GUIContent[]
        {
            new GUIContent(UIStrings.MenuEdit),
            new GUIContent(UIStrings.MenuResetZero),
            new GUIContent(UIStrings.MenuResetTreeView),
            new GUIContent(UIStrings.MenuDeleteAllExtended),
            new GUIContent(UIStrings.MenuInitializeTree),
        };
		internal GUIContent[] displayMenuOptions = new GUIContent[]
        {
			new GUIContent(UIStrings.MenuDisplay),
			new GUIContent(UIStrings.MenuOpenAll),
			new GUIContent(UIStrings.MenuCloseAll),
			new GUIContent(UIStrings.MenuResyncInspector),
        };
        internal GUIContent[] operationMenuOptions = new GUIContent[]
        {
            new GUIContent(UIStrings.MenuOperation),
            new GUIContent(UIStrings.MenuLockAll),
            new GUIContent(UIStrings.MenuUnlockAll),
            new GUIContent(UIStrings.MenuLockNonZero),
        };
        // シェイプキー親メニューは廃止
		internal GUIContent[] optionMenuOptions = new GUIContent[]
        {
			new GUIContent(UIStrings.MenuOption),
        };

        // ジャンプ機能用の変数
        internal string jumpToGroup = null; // ジャンプ先のグループ名
        internal bool needScrollToGroup = false; // スクロールが必要かどうかのフラグ

		// 現在のグループ表示用の変数は ViewModel に集約

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
                    bool isLocked = viewModel.LockedShapeKeys.ContainsKey(i) ? viewModel.LockedShapeKeys[i] : false;
                    
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
                            // viewModel.GroupedShapesから既存の情報を探す（後方互換性のため）
                            foreach (var group in viewModel.GroupedShapes)
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
                    
                    // ユーザーが変更した名前がある場合は使用
                    string displayName = shapeKeyName;
                    if (viewModel.UserRenamedShapes.ContainsKey(shapeKeyName))
                    {
                        displayName = viewModel.UserRenamedShapes[shapeKeyName];
                    }
                    
                    blendShapes.Add(
                        new BlendShape
                        {
                            name = displayName,
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
                foreach (var group in viewModel.GroupedShapes)
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
        
        /// <summary>
        /// ブレンドシェイプを再読み込み（グループ名と順序は保持、グループ化は行わない）
        /// </summary>
        internal void RefreshBlendShapesWithoutRegrouping()
        {
            // メッシュの参照を更新
            if (selectedRenderer != null)
            {
                sharedMesh = selectedRenderer.sharedMesh;
            }
            
            // 既存のグループ順序を保持
            var existingGroupOrder = viewModel.GroupedShapes.Keys.ToList();
            
            // ブレンドシェイプの基本データのみ更新（グループ化は行わない）
            blendShapes.Clear();
            if (sharedMesh != null)
            {
                for (int i = 0; i < sharedMesh.blendShapeCount; i++)
                {
                    string shapeName = sharedMesh.GetBlendShapeName(i);
                    float weight = selectedRenderer.GetBlendShapeWeight(i);
                    
                    // 既存のグループから情報を取得
                    bool isLocked = false;
                    bool isExtended = false;
                    float minValue = -100f;
                    float maxValue = 200f;
                    string originalName = "";
                    
                    foreach (var group in viewModel.GroupedShapes)
                    {
                        var existingShape = group.Value.FirstOrDefault(s => s.name == shapeName);
                        if (existingShape != null)
                        {
                            isLocked = existingShape.isLocked;
                            isExtended = existingShape.isExtended;
                            minValue = existingShape.minValue;
                            maxValue = existingShape.maxValue;
                            originalName = existingShape.originalName;
                            break;
                        }
                    }
                    
                    // 元の名前が設定されていない場合は、メッシュ上の名前を設定
                    if (string.IsNullOrEmpty(originalName))
                    {
                        originalName = shapeName;
                    }
                    
                    blendShapes.Add(
                        new BlendShape
                        {
                            name = shapeName,
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
                
                // 拡張シェイプキーの情報を復元
                foreach (var extendedInfo in ExtendedShapeKeyManager.GetAllExtendedShapeKeys())
                {
                    string extendedName = extendedInfo.Key;
                    var info = extendedInfo.Value;
                    
                    // 既存のグループから情報を取得
                    float existingWeight = 0f;
                    bool isLocked = false;
                    foreach (var group in viewModel.GroupedShapes)
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
                            minValue = info.minValue,
                            maxValue = info.maxValue,
                            originalName = extendedName
                        }
                    );
                }
            }
            
            // 既存のグループ順序を維持しつつ、シェイプキーを適切に配置
            MaintainExistingGroupOrder(existingGroupOrder);
        }
        
        /// <summary>
        /// ユーザーが変更した名前を復元
        /// </summary>
        private void RestoreUserRenamedNames(Dictionary<string, string> userRenamedGroups, Dictionary<string, string> userRenamedShapes)
        {
            try
            {
                // ユーザーが変更したグループ名を復元
                if (userRenamedGroups != null)
                {
                    foreach (var renameInfo in userRenamedGroups)
                    {
                        string originalGroupName = renameInfo.Key;
                        string newGroupName = renameInfo.Value;
                        
                        // 元のグループ名が存在し、新しいグループ名が存在しない場合
                        if (viewModel.GroupedShapes.ContainsKey(originalGroupName) && !viewModel.GroupedShapes.ContainsKey(newGroupName))
                        {
                            // グループ名を変更
                            var shapes = viewModel.GroupedShapes[originalGroupName];
                            viewModel.GroupedShapes.Remove(originalGroupName);
                            viewModel.GroupedShapes[newGroupName] = shapes;
                            
                            // 関連する状態も移行
                            if (viewModel.GroupFoldouts.ContainsKey(originalGroupName))
                            {
                                viewModel.GroupFoldouts[newGroupName] = viewModel.GroupFoldouts[originalGroupName];
                                viewModel.GroupFoldouts.Remove(originalGroupName);
                            }
                            
                            if (viewModel.GroupTestSliders.ContainsKey(originalGroupName))
                            {
                                viewModel.GroupTestSliders[newGroupName] = viewModel.GroupTestSliders[originalGroupName];
                                viewModel.GroupTestSliders.Remove(originalGroupName);
                            }
                            
                            if (viewModel.OriginalWeights.ContainsKey(originalGroupName))
                            {
                                viewModel.OriginalWeights[newGroupName] = viewModel.OriginalWeights[originalGroupName];
                                viewModel.OriginalWeights.Remove(originalGroupName);
                            }
                        }
                    }
                }
                
                // ユーザーが変更したシェイプキー名を復元
                if (userRenamedShapes != null)
                {
                    foreach (var renameInfo in userRenamedShapes)
                    {
                        string originalShapeName = renameInfo.Key;
                        string newShapeName = renameInfo.Value;
                        
                        // 各グループ内でシェイプキー名を変更
                        foreach (var group in viewModel.GroupedShapes)
                        {
                            var shape = group.Value.FirstOrDefault(s => s.name == originalShapeName);
                            if (shape != null)
                            {
                                shape.name = newShapeName;
                                break;
                            }
                        }
                    }
                }
                
                // ViewModelの辞書を復元
                viewModel.UserRenamedGroups = userRenamedGroups ?? new Dictionary<string, string>();
                viewModel.UserRenamedShapes = userRenamedShapes ?? new Dictionary<string, string>();
                
                Debug.Log($"ユーザー変更名を復元完了: グループ={userRenamedGroups?.Count ?? 0}, シェイプキー={userRenamedShapes?.Count ?? 0}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ユーザー変更名の復元でエラーが発生しました: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 既存のグループ順序を維持しつつ、シェイプキーを適切に配置
        /// </summary>
        private void MaintainExistingGroupOrder(List<string> existingGroupOrder)
        {
            // 既存のグループ順序を保持した新しい辞書を作成
            var newGroupedShapes = new Dictionary<string, List<BlendShape>>();
            var newGroupFoldouts = new Dictionary<string, bool>();
            var newGroupTestSliders = new Dictionary<string, float>();
            
            // 既存の順序でグループを再構築
            foreach (var groupName in existingGroupOrder)
            {
                if (viewModel.GroupedShapes.ContainsKey(groupName))
                {
                    newGroupedShapes[groupName] = new List<BlendShape>();
                    newGroupFoldouts[groupName] = viewModel.GroupFoldouts.ContainsKey(groupName) ? viewModel.GroupFoldouts[groupName] : false;
                    newGroupTestSliders[groupName] = viewModel.GroupTestSliders.ContainsKey(groupName) ? viewModel.GroupTestSliders[groupName] : 0f;
                }
            }
            
            // シェイプキーを適切なグループに配置
            foreach (var blendShape in blendShapes)
            {
                bool placed = false;
                
                // 既存のグループに配置を試行
                foreach (var groupName in existingGroupOrder)
                {
                    if (viewModel.GroupedShapes.ContainsKey(groupName))
                    {
                        var existingShapes = viewModel.GroupedShapes[groupName];
                        if (existingShapes.Any(s => s.name == blendShape.name))
                        {
                            newGroupedShapes[groupName].Add(blendShape);
                            placed = true;
                            break;
                        }
                    }
                }
                
                // 既存のグループに配置できない場合は、パターンベースでグループ化
                if (!placed)
                {
                    // ユーザーが変更した名前を保持するため、元の名前を確認
                    string originalName = blendShape.originalName;
                    if (!string.IsNullOrEmpty(originalName))
                    {
                        // 元の名前でパターンベースのグループ名を取得
                        string groupName = GetGroupName(originalName);
                        if (!newGroupedShapes.ContainsKey(groupName))
                        {
                            newGroupedShapes[groupName] = new List<BlendShape>();
                            newGroupFoldouts[groupName] = false;
                            newGroupTestSliders[groupName] = 0f;
                        }
                        newGroupedShapes[groupName].Add(blendShape);
                    }
                    else
                    {
                        // 元の名前がない場合は現在の名前でグループ化
                        string groupName = GetGroupName(blendShape.name);
                        if (!newGroupedShapes.ContainsKey(groupName))
                        {
                            newGroupedShapes[groupName] = new List<BlendShape>();
                            newGroupFoldouts[groupName] = false;
                            newGroupTestSliders[groupName] = 0f;
                        }
                        newGroupedShapes[groupName].Add(blendShape);
                    }
                }
            }
            
            // 新しい辞書で既存の辞書を置き換え
            viewModel.GroupedShapes = newGroupedShapes;
            viewModel.GroupFoldouts = newGroupFoldouts;
            viewModel.GroupTestSliders = newGroupTestSliders;
        }

        internal void UpdateCurrentGroupDisplay()
        {
			if (viewModel.GroupedShapes.Count == 0)
            {
				viewModel.CurrentGroupDisplay = "";
                return;
            }

			// スクロール時の軽量近似: 一定距離以上の移動がなければ再計算しない
			const float threshold = 8f;
			if (!string.IsNullOrEmpty(viewModel.CurrentGroupDisplay))
			{
				float dy = Mathf.Abs(viewModel.ScrollPosition.y - _lastScrollYForGroupDisplay);
				if (dy < threshold) return;
			}

            float currentY = viewModel.ScrollPosition.y;
            float headerHeight = EditorGUIUtility.singleLineHeight + 4f;
            float shapeHeight = EditorGUIUtility.singleLineHeight + 2f;
            float testSliderHeight = EditorGUIUtility.singleLineHeight + 2f;

            float accumulatedY = 0f;
            string foundGroup = "";

            foreach (var group in viewModel.GroupedShapes)
            {
                string groupName = group.Key;
                List<BlendShape> shapes = group.Value;

                if (currentY >= accumulatedY && currentY < accumulatedY + headerHeight)
                {
                    foundGroup = groupName;
                    break;
                }

                accumulatedY += headerHeight;

                if (viewModel.GroupFoldouts[groupName])
                {
                    accumulatedY += testSliderHeight;
                    accumulatedY += shapes.Count * shapeHeight;
                }

                if (
                    currentY
                        >= accumulatedY
                            - (
                                viewModel.GroupFoldouts[groupName]
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
                foreach (var group in viewModel.GroupedShapes)
                {
                    string groupName = group.Key;
                    List<BlendShape> shapes = group.Value;

                    float groupHeight = headerHeight;
                    if (viewModel.GroupFoldouts[groupName])
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

			viewModel.CurrentGroupDisplay = foundGroup;
			_lastScrollYForGroupDisplay = viewModel.ScrollPosition.y;
        }

		private float _lastScrollYForGroupDisplay = 0f;

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
                    // GUI 側で 0..100 の正規化を済ませているため、そのまま適用する
                    float normalizedWeight = Mathf.Clamp(blendShape.weight, 0f, 100f);
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
                        // GUI 側で 0..100 の正規化を済ませているため、そのまま適用する
                        float normalizedWeight = Mathf.Clamp(blendShape.weight, 0f, 100f);
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
            bool confirmed = DialogService.Confirm(
                "拡張シェイプキーの一括削除",
                "すべての拡張シェイプキーを削除しますか？\n\nこの操作は元に戻せません。",
                UIStrings.DialogDelete,
                UIStrings.DialogCancel
            );

            if (!confirmed)
                return;

            try
            {
                int deletedCount = 0;
                var shapesToRemove = new List<BlendShape>();

                // 拡張シェイプキーを特定
                foreach (var group in viewModel.GroupedShapes)
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
                    
                    // viewModel.GroupedShapesから削除
                    foreach (var group in viewModel.GroupedShapes)
                    {
                        group.Value.Remove(shape);
                    }

                    // blendShapesからも削除
                    blendShapes.Remove(shape);
                }

                // 空のグループを削除
                var emptyGroups = viewModel.GroupedShapes.Where(g => g.Value.Count == 0).ToList();
                foreach (var group in emptyGroups)
                {
                    viewModel.GroupedShapes.Remove(group.Key);
                    viewModel.GroupFoldouts.Remove(group.Key);
                    viewModel.GroupTestSliders.Remove(group.Key);
                }

                // メッシュの更新を反映するため、シェイプキーリストを再読み込み
                RefreshBlendShapes();

                // UIを更新
                EditorUtility.SetDirty(selectedRenderer);
                TreeViewPart.Reload();

                // 削除完了
                DialogService.Notify("削除完了", $"{deletedCount}個の拡張シェイプキーを削除しました。");
            }
            catch (System.Exception ex)
            {
                DialogService.Notify(UIStrings.DialogError, $"拡張シェイプキーの削除に失敗しました:\n{ex.Message}", DialogType.Error);
            }
        }

        internal void ShowOptionMenu(Rect buttonRect)
        {
            GenericMenu menu = new GenericMenu();

            // チェックボックス付きのメニュー項目を追加
            // 値が入っている物をスキップ: 既定で常時有効化とするためメニューから削除

			menu.AddItem(
				new GUIContent("冗長ログを有効化"),
				ShapeKeyToolSettings.DebugVerbose,
				() =>
				{
					ShapeKeyToolSettings.DebugVerbose = !ShapeKeyToolSettings.DebugVerbose;
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
                DialogService.Notify(UIStrings.DialogError, "リセット対象のオブジェクトが選択されていません。", DialogType.Error);
                return;
            }

            // 確認ダイアログを表示
            bool confirmed = DialogService.Confirm(
                "TreeViewリセットの確認",
                "TreeViewの状態をリセットしますか？\n\n" +
                "以下の変更が元に戻されます：\n" +
                "• グループの展開/折りたたみ状態\n" +
                "• カスタムグループの削除\n" +
                "• シェイプキーの並び順\n" +
                "• 拡張シェイプキーの削除\n\n" +
                "この操作は元に戻せません。",
                "リセット",
                UIStrings.DialogCancel
            );

            if (!confirmed)
                return;

            try
            {
                // 拡張シェイプキーを削除
                DeleteAllExtendedShapeKeys();

                // グループの展開状態をリセット
                viewModel.GroupFoldouts.Clear();
                viewModel.GroupTestSliders.Clear();

                // ユーザーが変更したグループ名を保持
                var userRenamedGroups = new Dictionary<string, string>(viewModel.UserRenamedGroups);
                var userRenamedShapes = new Dictionary<string, string>(viewModel.UserRenamedShapes);
                
                // カスタムグループを削除（「その他」グループは保持）
                var groupsToRemove = new List<string>();
                foreach (var group in viewModel.GroupedShapes)
                {
                    if (group.Key != "その他")
                    {
                        groupsToRemove.Add(group.Key);
                    }
                }

                foreach (var groupName in groupsToRemove)
                {
                    viewModel.GroupedShapes.Remove(groupName);
                }

                // シェイプキーのデータを再構築（グループ化を実行）
                UpdateBlendShapes();
                
                // ユーザーが変更したグループ名を復元
                RestoreUserRenamedNames(userRenamedGroups, userRenamedShapes);

                // TreeViewを更新
                TreeViewPart.Reload();

                DialogService.Notify("リセット完了", "TreeViewの状態をリセットしました。");
            }
            catch (System.Exception ex)
            {
                DialogService.Notify(UIStrings.DialogError, $"TreeViewのリセットに失敗しました:\n{ex.Message}", DialogType.Error);
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
                viewModel.GroupFoldouts.Clear();
                viewModel.GroupTestSliders.Clear();
                
                // すべてのグループを折りたたんだ状態に設定
                foreach (var group in viewModel.GroupedShapes)
                {
                    viewModel.GroupFoldouts[group.Key] = false;
                }

                // ロック状態をリセット
                viewModel.LockedShapeKeys.Clear();

                // スクロール位置をリセット
                viewModel.ScrollPosition = Vector2.zero;

                // TreeViewStateの展開状態をクリア
                if (TreeViewPart.GetTreeViewState() != null)
                {
                    TreeViewPart.GetTreeViewState().expandedIDs.Clear();
                }
                
                // TreeViewの状態を完全にリセット
                TreeViewPart.ResetTreeViewState();

                // メニューインデックスをリセット
                fileMenuIndex = 0;
                displayMenuIndex = 0;
                operationMenuIndex = 0;
                shapeKeyMenuIndex = 0;
                optionMenuIndex = 0;

                // ジャンプ機能をリセット
                jumpToGroup = null;
                needScrollToGroup = false;
                viewModel.CurrentGroupDisplay = "";

                // オプションをリセット（スキップは既定で常時有効）
                ShapeKeyToolSettings.SkipNonZeroValues = true;
                option1Enabled = false;
                option2Enabled = false;
                option3Enabled = false;

                // ユーザーが変更したグループ名を保持
                var userRenamedGroups = new Dictionary<string, string>(viewModel.UserRenamedGroups);
                var userRenamedShapes = new Dictionary<string, string>(viewModel.UserRenamedShapes);
                
                // カスタムグループを削除（「その他」グループは保持）
                var groupsToRemove = new List<string>();
                foreach (var group in viewModel.GroupedShapes)
                {
                    if (group.Key != "その他")
                    {
                        groupsToRemove.Add(group.Key);
                    }
                }

                foreach (var groupName in groupsToRemove)
                {
                    viewModel.GroupedShapes.Remove(groupName);
                }

                // シェイプキーのデータを再構築（グループ化を実行）
                UpdateBlendShapes();
                
                // ユーザーが変更したグループ名を復元
                RestoreUserRenamedNames(userRenamedGroups, userRenamedShapes);

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
            if (viewModel.GroupTestSliders != null)
            {
                foreach (var groupName in viewModel.GroupTestSliders.Keys.ToList())
                {
                    viewModel.GroupTestSliders[groupName] = 0f;
                }
            }
            
            // 元値キャッシュもクリア
                            if (viewModel.OriginalWeights != null)
                {
                    viewModel.OriginalWeights.Clear();
                }
        }
    }
}
