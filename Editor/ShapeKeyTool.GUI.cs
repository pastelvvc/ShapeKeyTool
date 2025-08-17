using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ShapeKeyTools
{
    /// <summary>
    /// GUI描画専用クラス - ロジックは一切変更せず、描画部分のみを担当
    /// </summary>
    internal class ShapeKeyToolGUI
    {
        private readonly ShapeKeyToolWindow window;

        public ShapeKeyToolGUI(ShapeKeyToolWindow win)
        {
            window = win;
        }

        /// <summary>
        /// メインGUI描画
        /// </summary>
        public void OnGUI()
        {
            if (window.selectedRenderer == null)
            {
                EditorGUILayout.HelpBox(
                    "SkinnedMeshRendererを持つオブジェクトを選択してください。",
                    MessageType.Info
                );
                return;
            }

            Splitter.DrawLayout(window, window.position, DrawCenterPanel, DrawRightPanel);
        }

        /// <summary>
        /// 中央パネル描画
        /// </summary>
        private void DrawCenterPanel(Rect r)
        {
            window.centerPane.OnGUI(r);
        }

        /// <summary>
        /// 検索UIを描画
        /// </summary>
        private void DrawSearchUI()
        {
            EditorGUILayout.BeginVertical("box");

            // 検索テキストフィールド
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(UIStrings.LabelSearch, GUILayout.Width(40));
            string newSearchText = EditorGUILayout.TextField(SearchManager.shapeKeySearchText, GUILayout.ExpandWidth(true));
            
            // 検索テキストが変更された場合
            if (newSearchText != SearchManager.shapeKeySearchText)
            {
                UIEventHandlers.OnShapeKeySearchTextChanged(window, newSearchText);
            }
            
            // クリアボタン
            if (GUILayout.Button(UIStrings.LabelClear, GUILayout.Width(50)))
            {
                UIEventHandlers.OnShapeKeySearchClear(window);
            }
            EditorGUILayout.EndHorizontal();
            

            
            // 正規表現が無効な場合の警告（UI統一）
            if (SearchManager.useRegex && !string.IsNullOrEmpty(SearchManager.shapeKeySearchText) && !SearchManager.IsValidRegex(SearchManager.shapeKeySearchText))
            {
                EditorGUILayout.HelpBox("無効な正規表現です", MessageType.Warning);
            }
            
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 右パネル描画
        /// </summary>
        private void DrawRightPanel(Rect r)
        {
            // 右側描画ロジック（元コードの DrawRightPanel 本体を移植）
            GUILayout.BeginArea(r);

            // グループ辞書の初期化を確実に行う
            if (window.viewModel.GroupedShapes != null && window.viewModel.GroupedShapes.Count > 0)
            {
                foreach (var group in window.viewModel.GroupedShapes)
                {
                    if (!window.viewModel.GroupFoldouts.ContainsKey(group.Key))
                        window.viewModel.GroupFoldouts[group.Key] = true;
                    if (!window.viewModel.GroupTestSliders.ContainsKey(group.Key))
                        window.viewModel.GroupTestSliders[group.Key] = 0f;
                }
            }

            // メニューバー
            EditorGUILayout.BeginHorizontal("box");

            // ファイルメニュー（先頭）
            int newFileIndex = EditorGUILayout.Popup(
                window.fileMenuIndex,
                window.fileMenuOptions,
                GUILayout.Width(70)
            );
            if (newFileIndex != window.fileMenuIndex)
            {
                window.fileMenuIndex = newFileIndex;
                switch (window.fileMenuIndex)
                {
                    case 1: // 手動保存
                        window.ManualSave();
                        window.fileMenuIndex = 0;
                        break;
                    case 2: // 手動読み込み
                        window.ManualLoad();
                        window.fileMenuIndex = 0;
                        break;
                    case 3: // JSON エクスポート
                        Serialization.ExportJson(window);
                        window.fileMenuIndex = 0;
                        break;
                    case 4: // JSON インポート
                        Serialization.ImportJson(window);
                        window.fileMenuIndex = 0;
                        break;
                    case 5: // コンポーネントの削除
                        window.RemovePersistenceComponent();
                        window.fileMenuIndex = 0;
                        break;
                }
            }

            // 編集メニュー
            int newEditIndex = EditorGUILayout.Popup(
                window.editMenuIndex,
                window.editMenuOptions,
                GUILayout.Width(60)
            );
            if (newEditIndex != window.editMenuIndex)
            {
                window.editMenuIndex = newEditIndex;
                switch (window.editMenuIndex)
                {
                    case 1: // 値を０に上書きする
                        using (CompositeUndo.BulkMeshChange(window, "Reset All Weights"))
                        {
                            foreach (var group in window.viewModel.GroupedShapes)
                            {
                                foreach (var shape in group.Value)
                                {
                                    BlendShapeCommandService.SetWeight(window, shape, 0f);
                                }
                            }
                        }
                        window.editMenuIndex = 0;
                        break;
                    case 2: // TreeViewをリセット
                        window.ResetTreeView();
                        window.editMenuIndex = 0;
                        break;
                    case 3: // 拡張シェイプキーを一括削除
                        window.DeleteAllExtendedShapeKeys();
                        window.editMenuIndex = 0;
                        break;
                    case 4: // 初期化
                        window.InitializeTreeViewOperations();
                        window.editMenuIndex = 0;
                        break;
                    default:
                        window.editMenuIndex = 0;
                        break;
                }
            }

            // 表示メニュー
            int newDisplayIndex = EditorGUILayout.Popup(
                window.displayMenuIndex,
                window.displayMenuOptions,
                GUILayout.Width(60)
            );
            if (newDisplayIndex != window.displayMenuIndex)
            {
                window.displayMenuIndex = newDisplayIndex;
                switch (window.displayMenuIndex)
                {
                    case 1: // すべて開く
                        foreach (var group in window.viewModel.GroupedShapes)
                        {
                            window.viewModel.GroupFoldouts[group.Key] = true;
                        }
                        window.displayMenuIndex = 0;
                        break;
                    case 2: // すべて閉じる
                        foreach (var group in window.viewModel.GroupedShapes)
                        {
                            window.viewModel.GroupFoldouts[group.Key] = false;
                        }
                        window.displayMenuIndex = 0;
                        break;
                    case 3: // インスペクターと再同期
                        // Inspector の現在値を UI モデルへ取り込み
                        if (window.selectedRenderer != null && window.selectedRenderer.sharedMesh != null)
                        {
                            var mesh = window.selectedRenderer.sharedMesh;
                            foreach (var group in window.viewModel.GroupedShapes)
                            {
                                foreach (var shape in group.Value)
                                {
                                    int idx = -1;
                                    // 拡張は originalName を優先
                                    for (int i = 0; i < mesh.blendShapeCount; i++)
                                    {
                                        string name = mesh.GetBlendShapeName(i);
                                        if (name == shape.name || (!string.IsNullOrEmpty(shape.originalName) && name == shape.originalName))
                                        { idx = i; break; }
                                    }
                                    if (idx >= 0)
                                    {
                                        float inspector = window.selectedRenderer.GetBlendShapeWeight(idx);
                                        BlendShapeCommandService.SetWeight(window, shape, inspector);
                                    }
                                }
                            }
                            TreeViewPart.Reload();
                            ApplyScheduler.RequestRepaint();
                        }
                        window.displayMenuIndex = 0;
                        break;
                }
            }

            // 操作メニュー
            int newOperationIndex = EditorGUILayout.Popup(
                window.operationMenuIndex,
                window.operationMenuOptions,
                GUILayout.Width(60)
            );
            if (newOperationIndex != window.operationMenuIndex)
            {
                window.operationMenuIndex = newOperationIndex;
                switch (window.operationMenuIndex)
                {
                    case 1: // すべてロック
                        foreach (var group in window.viewModel.GroupedShapes)
                        {
                            foreach (var shape in group.Value)
                            {
                                shape.isLocked = true;
                                window.viewModel.LockedShapeKeys[shape.index] = true;
                            }
                        }
                        EditorUtility.SetDirty(window.selectedRenderer);
                        TreeViewPart.Reload();
                        window.operationMenuIndex = 0;
                        break;
                    case 2: // すべてアンロック
                        foreach (var group in window.viewModel.GroupedShapes)
                        {
                            foreach (var shape in group.Value)
                            {
                                shape.isLocked = false;
                                window.viewModel.LockedShapeKeys[shape.index] = false;
                            }
                        }
                        EditorUtility.SetDirty(window.selectedRenderer);
                        TreeViewPart.Reload();
                        window.operationMenuIndex = 0;
                        break;
                    case 3: // 値が入っているものをロックする（移動）
                        foreach (var group in window.viewModel.GroupedShapes)
                        {
                            foreach (var shape in group.Value)
                            {
                                if (shape.weight > 0.01f)
                                {
                                    shape.isLocked = true;
                                    window.viewModel.LockedShapeKeys[shape.index] = true;
                                }
                            }
                        }
                        EditorUtility.SetDirty(window.selectedRenderer);
                        TreeViewPart.Reload();
                        window.operationMenuIndex = 0;
                        break;
                    // リセット/初期化は編集メニューへ移動
                }
            }

            // シェイプキーの親メニューは非表示（削除）

            // オプションメニュー
            Rect optionButtonRect = GUILayoutUtility.GetRect(
                new GUIContent("オプション"),
                GUI.skin.button,
                GUILayout.Width(60)
            );
            if (GUI.Button(optionButtonRect, "オプション"))
            {
                // オプションメニューを表示（ボタンの位置に固定）
                Rect menuRect = new Rect(
                    optionButtonRect.x,
                    optionButtonRect.y + optionButtonRect.height,
                    optionButtonRect.width,
                    0
                );
                window.ShowOptionMenu(menuRect);
            }

            EditorGUILayout.EndHorizontal();

            // 検索UI
            DrawSearchUI();

            // 現在のグループを更新（スクロールビューの外側で実行）
            window.UpdateCurrentGroupDisplay();

            // 現在のグループ表示（スクロールビューの外側で固定表示）
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField(
                $"現在のグループ: {window.viewModel.CurrentGroupDisplay} (スクロール位置: {window.viewModel.ScrollPosition.y:F1})",
                EditorStyles.boldLabel
            );
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (window.viewModel.GroupedShapes.Count > 0)
            {
                window.viewModel.ScrollPosition = EditorGUILayout.BeginScrollView(window.viewModel.ScrollPosition);

                // ジャンプ機能の処理
                if (window.jumpToGroup != null && window.needScrollToGroup)
                {
                    float targetY = 0f;
                    bool found = false;

                    foreach (var group in window.viewModel.GroupedShapes)
                    {
                        if (group.Key == window.jumpToGroup)
                        {
                            found = true;
                            break;
                        }
                        targetY += EditorGUIUtility.singleLineHeight + 4f;
                        if (window.viewModel.GroupFoldouts.ContainsKey(group.Key) && window.viewModel.GroupFoldouts[group.Key])
                        {
                            targetY += group.Value.Count * (EditorGUIUtility.singleLineHeight + 2f);
                        }
                    }

                    if (found)
                    {
                        window.viewModel.ScrollPosition = new Vector2(window.viewModel.ScrollPosition.x, targetY);
                    }

                    window.jumpToGroup = null;
                    window.needScrollToGroup = false;
                }

                foreach (var group in window.viewModel.GroupedShapes)
                {
                    string groupName = group.Key;
                    List<BlendShape> shapes = group.Value;

                    // 検索フィルターを適用
                    bool groupMatchesSearch = SearchManager.ShouldShowInShapeKeyPanel(groupName);
                    var visibleShapes = new List<BlendShape>();
                    bool hasVisibleShapes = false;
                    
                    foreach (var shape in shapes)
                    {
                        if (SearchManager.ShouldShowInShapeKeyPanel(shape.name))
                        {
                            visibleShapes.Add(shape);
                            hasVisibleShapes = true;
                        }
                    }
                    
                    // グループまたはその子アイテムが検索にマッチする場合のみ表示
                    if (groupMatchesSearch || hasVisibleShapes)
                    {
                        // アコーディオンヘッダー
                        EditorGUILayout.BeginHorizontal("box");
                        
                        // 辞書に存在しない場合はデフォルト値を使用
                        bool currentFoldout = window.viewModel.GroupFoldouts.ContainsKey(groupName) 
                            ? window.viewModel.GroupFoldouts[groupName] 
                            : true;
                        
                        bool newFoldout = EditorGUILayout.Foldout(
                            currentFoldout,
                            $"{groupName} ({visibleShapes.Count})",
                            true
                        );
                        
                        window.viewModel.GroupFoldouts[groupName] = newFoldout;
                        EditorGUILayout.EndHorizontal();

                                // アコーディオンコンテンツ
                                bool foldoutOpen = window.viewModel.GroupFoldouts.ContainsKey(groupName) ? window.viewModel.GroupFoldouts[groupName] : true;
                                if (foldoutOpen)
                        {
                            EditorGUI.indentLevel++;

                            // 高速探査スライダー
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Label("高速探査", GUILayout.Width(150));
                            int testValue = window.viewModel.GroupTestSliders.ContainsKey(groupName)
                                ? Mathf.RoundToInt(window.viewModel.GroupTestSliders[groupName])
                                : 0;
                            int maxValue = visibleShapes.Count;
                            int newTestValue = EditorGUILayout.IntSlider(testValue, 0, maxValue);

                            window.viewModel.GroupTestSliders[groupName] = newTestValue;

                            if (newTestValue != testValue)
                            {
                                // 検索フィルターが適用されている場合は、表示されているシェイプキーのみに適用
                                if (!string.IsNullOrEmpty(SearchManager.shapeKeySearchText))
                                {
                                    Grouping.ApplyTestSliderToVisibleShapes(
                                        window,
                                        groupName,
                                        visibleShapes,
                                        newTestValue,
                                        true
                                    );
                                }
                                else
                                {
                                    Grouping.ApplyTestSliderToGroup(
                                        window,
                                        groupName,
                                        newTestValue,
                                        true
                                    );
                                }
                            }
                            EditorGUILayout.EndHorizontal();

                            // シェイプキーのスライダー
                            foreach (var blendShape in visibleShapes)
                            {
                                EditorGUILayout.BeginHorizontal();

                                bool newLocked = EditorGUILayout.Toggle(
                                    blendShape.isLocked,
                                    GUILayout.Width(20)
                                );
                                if (newLocked != blendShape.isLocked)
                                {
                                    blendShape.isLocked = newLocked;
                                    window.viewModel.LockedShapeKeys[blendShape.index] = newLocked;
                                    
                                    // 自動保存を削除 - 手動で保存を行う
                                }

                                // ロックボタンと名前の間に間隔を追加
                                GUILayout.Space(15);

                                var labelStyle = new GUIStyle(EditorStyles.label);
                                // 高速探査で強制100中かどうか
							bool isForcedByTest = false;
                                int currentTestValue = window.viewModel.GroupTestSliders.ContainsKey(groupName)
                                    ? Mathf.RoundToInt(window.viewModel.GroupTestSliders[groupName])
                                    : 0;
                                if (currentTestValue > 0
                                    && window.viewModel.LastTestSelectedShapeName.TryGetValue(groupName, out var lastTestName))
                                {
                                    isForcedByTest = lastTestName == blendShape.name;
                                }

                                if (blendShape.isLocked || isForcedByTest)
                                {
                                    labelStyle.normal.textColor = Color.gray;
                                }
                                
                                // 拡張シェイプキーの場合は色を変更
                                if (blendShape.isExtended)
                                {
                                    labelStyle.normal.textColor = Color.green;
                                }
                                
                                // 検索結果のハイライト（プレーンテキスト描画に統一）
                                string displayName = blendShape.name;
                                if (!string.IsNullOrEmpty(SearchManager.shapeKeySearchText))
                                {
                                    // 簡易ハイライトは省略（プレーンテキスト統一）
                                }
                                GUILayout.Label(displayName, labelStyle, GUILayout.Width(150));

							// 高速探査中は「現在100の項目のみ」無効化する
							bool disableForTest = currentTestValue > 0 && Mathf.Abs(blendShape.weight - 100f) < 0.01f;
							EditorGUI.BeginDisabledGroup(blendShape.isLocked || disableForTest);
                                
                                // 拡張シェイプキーの場合は範囲を拡張
                                if (blendShape.isExtended)
                                {
                                    float extendedMinValue = blendShape.minValue;
                                    float extendedMaxValue = blendShape.maxValue;
                                    
                                    // 現在の正規化された値を拡張範囲の値に変換して表示
                                    float range = extendedMaxValue - extendedMinValue;
                                    float displayValue = blendShape.weight;
                                    
                                    if (range > 0)
                                    {
                                        displayValue = (blendShape.weight / 100f) * range + extendedMinValue;
                                    }
                                    
									// 拡張シェイプキー用の Max ボタン（スライダーの左側）
									var extMaxRect = GUILayoutUtility.GetRect(new GUIContent("Max"), GUI.skin.button, GUILayout.Width(40));
									// マウスオーバー時の100%プレビュー（通常と同挙動）
									bool isExtMaxHovered = extMaxRect.Contains(Event.current.mousePosition);
									int extTargetIndex = -1;
									if (window.selectedRenderer != null && window.selectedRenderer.sharedMesh != null)
									{
										var mesh = window.selectedRenderer.sharedMesh;
										// まず拡張シェイプキー自体を探す
										for (int i = 0; i < mesh.blendShapeCount; i++)
										{
											if (mesh.GetBlendShapeName(i) == blendShape.name) { extTargetIndex = i; break; }
										}
										// 見つからなければ元のシェイプキーを探す
										if (extTargetIndex == -1 && !string.IsNullOrEmpty(blendShape.originalName))
										{
											for (int i = 0; i < mesh.blendShapeCount; i++)
											{
												if (mesh.GetBlendShapeName(i) == blendShape.originalName) { extTargetIndex = i; break; }
											}
										}
									}
									if (isExtMaxHovered && !blendShape.isLocked && extTargetIndex >= 0)
									{
										PreviewService.BeginMaxHover(window, extTargetIndex, blendShape);
										GUI.color = Color.yellow;
									}
									else if (!isExtMaxHovered && extTargetIndex >= 0 && PreviewService.IsPreviewing(window.selectedRenderer, extTargetIndex))
									{
										PreviewService.EndMaxHover(window, extTargetIndex, blendShape);
									}
									if (GUI.Button(extMaxRect, "Max"))
									{
										if (!blendShape.isLocked)
										{
											// 拡張は内部的に 0..100 正規化で保持しているため 100 を適用
											BlendShapeCommandService.SetWeight(window, blendShape, 100f);
											if (window.viewModel.GroupedShapes.TryGetValue(groupName, out var _))
											{
												if (!window.viewModel.UserEditedDuringTest.ContainsKey(groupName))
													window.viewModel.UserEditedDuringTest[groupName] = new HashSet<string>();
												window.viewModel.UserEditedDuringTest[groupName].Add(blendShape.name);
											}
											// 情報を永続化マネージャへ（念のため）
											var extInfoForMaxBtn = new ExtendedShapeKeyInfo(blendShape.originalName, blendShape.minValue, blendShape.maxValue);
											ExtendedShapeKeyManager.RegisterExtendedShapeKey(blendShape.name, extInfoForMaxBtn);
										}
									}

                                    float sliderStartValue = isExtMaxHovered ? extendedMaxValue : displayValue;
                                    EditorGUI.BeginChangeCheck();
                                    float extendedNewWeight = EditorGUILayout.Slider(sliderStartValue, extendedMinValue, extendedMaxValue);
                                    bool sliderChangedExt = EditorGUI.EndChangeCheck();
									// ボタンの色をリセット
									GUI.color = Color.white;

                                    // ユーザー編集/高速探査/ロック適用マーカー: スライダーと数値入力の間
                                    bool isUserEditedFlagExt =
                                        window.viewModel.UserEditedDuringTest.ContainsKey(groupName)
                                        && window.viewModel.UserEditedDuringTest[groupName].Contains(blendShape.name);
                                    bool isFastProbeAppliedExt = isForcedByTest;
                                    bool isNonZeroExt = Mathf.Abs(blendShape.weight) > 0.01f;
                                    Rect midMarkerRectExt = GUILayoutUtility.GetRect(6, EditorGUIUtility.singleLineHeight, GUILayout.Width(6));
                                    bool isLockedExt = blendShape.isLocked;
                                    if (isUserEditedFlagExt || isFastProbeAppliedExt || isLockedExt || isNonZeroExt)
                                    {
                                        var drawRect = new Rect(midMarkerRectExt.x + 1, midMarkerRectExt.y + 2, 3, midMarkerRectExt.height - 4);
										var markerColor = new Color(0f, 1f, 0f, 0.95f); // extended: green
                                        if (isLockedExt)
                                        {
                                            markerColor = new Color(1f, 0f, 0f, 0.95f); // locked: red
                                        }
                                        else if (isFastProbeAppliedExt)
                                        {
                                            markerColor = new Color(1f, 1f, 0f, 0.95f); // fast probe: yellow
                                        }
                                        EditorGUI.DrawRect(drawRect, markerColor);
                                    }

                                    if (!blendShape.isLocked && sliderChangedExt && Mathf.Abs(extendedNewWeight - displayValue) > 0.01f)
                                    {
                                        // スライダーの値を0~100の範囲に正規化してから設定
                                        float firstRange = extendedMaxValue - extendedMinValue;
                                        float normalizedWeight = extendedNewWeight;
                                        
                                        if (firstRange > 0)
                                        {
                                            normalizedWeight = (extendedNewWeight - extendedMinValue) / firstRange * 100f;
                                        }
                                        // 先にモデルへ適用（SetWeight内で model.weight を更新）
                                        BlendShapeCommandService.SetWeight(window, blendShape, normalizedWeight);

                                        // 拡張シェイプキーの場合は永続化マネージャーに情報を更新
                                        if (blendShape.isExtended)
                                        {
                                            var extendedInfo = new ExtendedShapeKeyInfo(blendShape.originalName, blendShape.minValue, blendShape.maxValue);
                                            ExtendedShapeKeyManager.RegisterExtendedShapeKey(blendShape.name, extendedInfo);
                                        }

                                        // ユーザー編集フラグ
                                        if (Mathf.Abs(normalizedWeight) <= 0.01f)
                                        {
                                            // 値が0になった場合はユーザー編集フラグをクリア
                                            if (window.viewModel.UserEditedDuringTest.ContainsKey(groupName))
                                            {
                                                window.viewModel.UserEditedDuringTest[groupName].Remove(blendShape.name);
                                                // 空になった場合はグループ自体を削除
                                                if (window.viewModel.UserEditedDuringTest[groupName].Count == 0)
                                                {
                                                    window.viewModel.UserEditedDuringTest.Remove(groupName);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (window.viewModel.GroupedShapes.TryGetValue(groupName, out var _))
                                            {
                                                if (!window.viewModel.UserEditedDuringTest.ContainsKey(groupName))
                                                    window.viewModel.UserEditedDuringTest[groupName] = new HashSet<string>();
                                                window.viewModel.UserEditedDuringTest[groupName].Add(blendShape.name);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // 通常のシェイプキーでも、拡張パラメータが存在するかチェック
                                    var extendedInfo = ExtendedShapeKeyInfo.TryParseFromName(blendShape.name, out var info);
                                    
                                    if (extendedInfo)
                                    {
                                        // 拡張パラメータが存在する場合は範囲を拡張（applyExtendedShapeKeysの設定に関係なく）
                                        float extendedMinValue = info.minValue;
                                        float extendedMaxValue = info.maxValue;
                                        
                                        // 現在の正規化された値を拡張範囲の値に変換して表示
                                        float displayRange = extendedMaxValue - extendedMinValue;
                                        float displayValue = blendShape.weight;
                                        
                                        if (displayRange > 0)
                                        {
                                            displayValue = (blendShape.weight / 100f) * displayRange + extendedMinValue;
                                        }
                                        
                                        float extendedNewWeight = EditorGUILayout.Slider(displayValue, extendedMinValue, extendedMaxValue);
                                        
                                        // 値の表示
                                        GUILayout.Label($"{extendedNewWeight:F1}", GUILayout.Width(50));
                                        
                                        if (!blendShape.isLocked && Mathf.Abs(extendedNewWeight - displayValue) > 0.01f)
                                        {
                                            // スライダーの値を0~100の範囲に正規化してから設定
                                            float normalizedRange = extendedMaxValue - extendedMinValue;
                                            float normalizedWeight = extendedNewWeight;
                                            
                                            if (normalizedRange > 0)
                                            {
                                                normalizedWeight = (extendedNewWeight - extendedMinValue) / normalizedRange * 100f;
                                            }
                                            // 先にモデルへ適用（SetWeight内で model.weight を更新）
                                            BlendShapeCommandService.SetWeight(window, blendShape, normalizedWeight);

                                            // 拡張シェイプキーの場合は永続化マネージャーに情報を更新
                                            if (blendShape.isExtended)
                                            {
                                                var extendedInfoForUpdate = new ExtendedShapeKeyInfo(blendShape.originalName, blendShape.minValue, blendShape.maxValue);
                                                ExtendedShapeKeyManager.RegisterExtendedShapeKey(blendShape.name, extendedInfoForUpdate);
                                            }

                                            // ユーザー編集フラグ
                                            if (Mathf.Abs(normalizedWeight) <= 0.01f)
                                            {
                                                // 値が0になった場合はユーザー編集フラグをクリア
                                                if (window.viewModel.UserEditedDuringTest.ContainsKey(groupName))
                                                {
                                                    window.viewModel.UserEditedDuringTest[groupName].Remove(blendShape.name);
                                                    // 空になった場合はグループ自体を削除
                                                    if (window.viewModel.UserEditedDuringTest[groupName].Count == 0)
                                                    {
                                                        window.viewModel.UserEditedDuringTest.Remove(groupName);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                if (window.viewModel.GroupedShapes.TryGetValue(groupName, out var _))
                                                {
                                                    if (!window.viewModel.UserEditedDuringTest.ContainsKey(groupName))
                                                        window.viewModel.UserEditedDuringTest[groupName] = new HashSet<string>();
                                                    window.viewModel.UserEditedDuringTest[groupName].Add(blendShape.name);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Maxボタン
                                        var maxButtonRect = GUILayoutUtility.GetRect(new GUIContent("Max"), GUI.skin.button, GUILayout.Width(40));
                                        bool isMaxButtonHovered = maxButtonRect.Contains(Event.current.mousePosition);
                                        
                                        // 拡張シェイプキーの場合は元のシェイプキーのインデックスを使用
                                        int targetIndex = blendShape.index;
                                        if (blendShape.isExtended && !string.IsNullOrEmpty(blendShape.originalName))
                                        {
                                            // 元のシェイプキーを探す
                                            var originalShape = window.blendShapes.FirstOrDefault(s => s.name == blendShape.originalName);
                                            if (originalShape != null)
                                            {
                                                targetIndex = originalShape.index;
                                            }
                                        }
                                        
                                        // マウスオーバーで100%プレビュー
                                        if (isMaxButtonHovered && !blendShape.isLocked && targetIndex >= 0)
                                        {
                                            PreviewService.BeginMaxHover(window, targetIndex, blendShape);
                                            // ボタンの色を変更してホバー状態を表示
                                            GUI.color = Color.yellow;
                                        }
                                        else if (!isMaxButtonHovered && PreviewService.IsPreviewing(window.selectedRenderer, targetIndex))
                                        {
                                            PreviewService.EndMaxHover(window, targetIndex, blendShape);
                                        }
                                        
                                        // プレビュー時は見た目だけ100%扱いにするが、スライダーの実値は model 値を使う
                                        bool isPreviewing = (isMaxButtonHovered || PreviewService.IsPreviewing(window.selectedRenderer, targetIndex));
                                        float previewDisplay = isPreviewing ? 100f : blendShape.weight;
                                        
                                        // ユーザー操作の変更のみ反映する
                                        EditorGUI.BeginChangeCheck();
                                        float normalNewWeight = EditorGUILayout.Slider(previewDisplay, 0f, 100f);
                                        bool sliderChanged = EditorGUI.EndChangeCheck();
                                        
                                        // ボタンを描画
                                        if (GUI.Button(maxButtonRect, "Max"))
                                        {
                                            if (!blendShape.isLocked && targetIndex >= 0)
                                            {
                                                // Max確定はコマンドサービスで適用
                                                BlendShapeCommandService.SetWeight(window, blendShape, 100f);
                                                if (window.viewModel.GroupedShapes.TryGetValue(groupName, out var _))
                                                {
                                                    if (!window.viewModel.UserEditedDuringTest.ContainsKey(groupName))
                                                        window.viewModel.UserEditedDuringTest[groupName] = new HashSet<string>();
                                                    window.viewModel.UserEditedDuringTest[groupName].Add(blendShape.name);
                                                }
                                                if (blendShape.isExtended)
                                                {
                                                    var extendedInfoForMax = new ExtendedShapeKeyInfo(blendShape.originalName, blendShape.minValue, blendShape.maxValue);
                                                    ExtendedShapeKeyManager.RegisterExtendedShapeKey(blendShape.name, extendedInfoForMax);
                                                }
                                            }
                                        }
                                        
                                        // ユーザー編集/高速探査/ロック適用マーカー: スライダーと数値入力の間
                                        bool isUserEditedFlagNorm =
                                            window.viewModel.UserEditedDuringTest.ContainsKey(groupName)
                                            && window.viewModel.UserEditedDuringTest[groupName].Contains(blendShape.name);
                                        bool isFastProbeAppliedNorm = isForcedByTest;
                                        bool isNonZeroNorm = Mathf.Abs(blendShape.weight) > 0.01f;
                                        Rect midMarkerRect = GUILayoutUtility.GetRect(6, EditorGUIUtility.singleLineHeight, GUILayout.Width(6));
                                        bool isLockedNorm = blendShape.isLocked;
                                        if (isUserEditedFlagNorm || isFastProbeAppliedNorm || isLockedNorm || isNonZeroNorm)
                                        {
                                            var drawRect = new Rect(midMarkerRect.x + 1, midMarkerRect.y + 2, 3, midMarkerRect.height - 4);
                                            var markerColor = new Color(0f, 1f, 1f, 0.95f); // user-edited default: cyan (normal)
                                            if (isLockedNorm)
                                            {
                                                markerColor = new Color(1f, 0f, 0f, 0.95f); // locked: red
                                            }
                                            else if (isFastProbeAppliedNorm)
                                            {
                                                markerColor = new Color(1f, 1f, 0f, 0.95f); // fast probe: yellow
                                            }
                                            EditorGUI.DrawRect(drawRect, markerColor);
                                        }

                                        // ボタンの色をリセット
                                        GUI.color = Color.white;
                                        
                                        // スライダーの値がユーザー操作で変更された場合のみ適用
                                        if (!blendShape.isLocked && sliderChanged && Mathf.Abs(normalNewWeight - blendShape.weight) > 0.01f)
                                        {
                                            BlendShapeCommandService.SetWeight(window, blendShape, normalNewWeight);
                                            
                                            // 値が0になった場合はユーザー編集フラグをクリア
                                            if (Mathf.Abs(normalNewWeight) <= 0.01f)
                                            {
                                                if (window.viewModel.UserEditedDuringTest.ContainsKey(groupName))
                                                {
                                                    window.viewModel.UserEditedDuringTest[groupName].Remove(blendShape.name);
                                                    // 空になった場合はグループ自体を削除
                                                    if (window.viewModel.UserEditedDuringTest[groupName].Count == 0)
                                                    {
                                                        window.viewModel.UserEditedDuringTest.Remove(groupName);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                // 高速探査中にユーザーが手で変更したことを記録
                                                if (window.viewModel.GroupedShapes.TryGetValue(groupName, out var _))
                                                {
                                                    if (!window.viewModel.UserEditedDuringTest.ContainsKey(groupName))
                                                        window.viewModel.UserEditedDuringTest[groupName] = new HashSet<string>();
                                                    window.viewModel.UserEditedDuringTest[groupName].Add(blendShape.name);
                                                }
                                            }
                                            
                                            // 拡張シェイプキーが存在する場合は永続化マネージャーを更新
                                            if (blendShape.isExtended)
                                            {
                                                var extendedInfoForSlider = new ExtendedShapeKeyInfo(blendShape.originalName, blendShape.minValue, blendShape.maxValue);
                                                ExtendedShapeKeyManager.RegisterExtendedShapeKey(blendShape.name, extendedInfoForSlider);
                                            }
                                            
                                            // TreeViewを自動更新
                                            TreeViewPart.Reload();
                                        }
                                    }
                                }
                                
                                EditorGUI.EndDisabledGroup();

                                EditorGUILayout.EndHorizontal();
                            }
                            EditorGUI.indentLevel--;
                        }
                    }
                }

                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "選択されたオブジェクトにシェイプキーがありません。",
                    MessageType.Warning
                );
            }

            GUILayout.EndArea();
        }
    }
} 