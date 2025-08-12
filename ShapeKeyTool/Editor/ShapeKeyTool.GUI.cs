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
            EditorGUILayout.LabelField("検索:", GUILayout.Width(40));
            string newSearchText = EditorGUILayout.TextField(SearchManager.shapeKeySearchText, GUILayout.ExpandWidth(true));
            
            // 検索テキストが変更された場合
            if (newSearchText != SearchManager.shapeKeySearchText)
            {
                SearchManager.shapeKeySearchText = newSearchText;
                // TODO: integrate UIUpdateDispatcher to throttle
                window.RequestRepaintThrottled();
            }
            
            // クリアボタン
            if (GUILayout.Button("クリア", GUILayout.Width(50)))
            {
                SearchManager.shapeKeySearchText = "";
                // TODO: integrate UIUpdateDispatcher to throttle
                window.RequestRepaintThrottled();
            }
            EditorGUILayout.EndHorizontal();
            

            
            // 正規表現が無効な場合の警告
            if (SearchManager.useRegex && !string.IsNullOrEmpty(SearchManager.shapeKeySearchText))
            {
                if (!SearchManager.IsValidRegex(SearchManager.shapeKeySearchText))
                {
                    EditorGUILayout.HelpBox("無効な正規表現です", MessageType.Warning);
                }
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
            if (window.groupedShapes != null && window.groupedShapes.Count > 0)
            {
                foreach (var group in window.groupedShapes)
                {
                    if (!window.groupFoldouts.ContainsKey(group.Key))
                        window.groupFoldouts[group.Key] = true;
                    if (!window.groupTestSliders.ContainsKey(group.Key))
                        window.groupTestSliders[group.Key] = 0f;
                }
            }

            // メニューバー
            EditorGUILayout.BeginHorizontal("box");

            // ファイルメニュー（先頭）
            int newFileIndex = EditorGUILayout.Popup(
                window.ui.fileMenuIndex,
                window.fileMenuOptions,
                GUILayout.Width(70)
            );
            if (newFileIndex != window.ui.fileMenuIndex)
            {
                window.ui.fileMenuIndex = newFileIndex;
                switch (window.ui.fileMenuIndex)
                {
                    case 1: // 手動保存
                        window.ManualSave();
                        window.ui.fileMenuIndex = 0;
                        break;
                    case 2: // 手動読み込み
                        window.ManualLoad();
                        window.ui.fileMenuIndex = 0;
                        break;
                    case 3: // JSON エクスポート
                        Serialization.ExportJson(window);
                        window.ui.fileMenuIndex = 0;
                        break;
                    case 4: // JSON インポート
                        Serialization.ImportJson(window);
                        window.ui.fileMenuIndex = 0;
                        break;
                    case 5: // コンポーネントの削除
                        window.RemovePersistenceComponent();
                        window.ui.fileMenuIndex = 0;
                        break;
                }
            }

            // 表示メニュー
            int newDisplayIndex = EditorGUILayout.Popup(
                window.ui.displayMenuIndex,
                window.displayMenuOptions,
                GUILayout.Width(60)
            );
            if (newDisplayIndex != window.ui.displayMenuIndex)
            {
                window.ui.displayMenuIndex = newDisplayIndex;
                switch (window.ui.displayMenuIndex)
                {
                    case 1: // すべて開く
                        foreach (var group in window.groupedShapes)
                        {
                            window.groupFoldouts[group.Key] = true;
                        }
                        window.ui.displayMenuIndex = 0;
                        break;
                    case 2: // すべて閉じる
                        foreach (var group in window.groupedShapes)
                        {
                            window.groupFoldouts[group.Key] = false;
                        }
                        window.ui.displayMenuIndex = 0;
                        break;
                }
            }

            // 操作メニュー
            int newOperationIndex = EditorGUILayout.Popup(
                window.ui.operationMenuIndex,
                window.operationMenuOptions,
                GUILayout.Width(60)
            );
            if (newOperationIndex != window.ui.operationMenuIndex)
            {
                window.ui.operationMenuIndex = newOperationIndex;
                switch (window.ui.operationMenuIndex)
                {
                    case 1: // ランダム設定
                        foreach (var group in window.groupedShapes)
                        {
                            foreach (var shape in group.Value)
                            {
                                float randomWeight = Random.Range(0f, 100f);
                                ShapeKeyCommandService.SetBlendShapeWeightWithUndo(window, shape, randomWeight);
                            }
                        }
                        window.ui.operationMenuIndex = 0;
                        break;
                    case 2: // すべてロック
                        foreach (var group in window.groupedShapes)
                        {
                            ShapeKeyCommandService.SetMultipleLockStatesWithUndo(window, group.Value, true);
                        }
                        window.RequestReload();
                        window.ui.operationMenuIndex = 0;
                        break;
                    case 3: // すべてアンロック
                        foreach (var group in window.groupedShapes)
                        {
                            ShapeKeyCommandService.SetMultipleLockStatesWithUndo(window, group.Value, false);
                        }
                        window.RequestReload();
                        window.ui.operationMenuIndex = 0;
                        break;
                    case 4: // すべてリセット
                        foreach (var group in window.groupedShapes)
                        {
                            foreach (var shape in group.Value)
                            {
                                ShapeKeyCommandService.SetBlendShapeWeightWithUndo(window, shape, 0f);
                            }
                        }
                        window.operationMenuIndex = 0;
                        break;
                    case 5: // 初期化:TreeViewの操作
                        window.InitializeTreeViewOperations();
                        window.ui.operationMenuIndex = 0;
                        break;
                }
            }

            // シェイプキーメニュー
            int newShapeKeyIndex = EditorGUILayout.Popup(
                window.ui.shapeKeyMenuIndex,
                window.shapeKeyMenuOptions,
                GUILayout.Width(80)
            );
            if (newShapeKeyIndex != window.ui.shapeKeyMenuIndex)
            {
                window.ui.shapeKeyMenuIndex = newShapeKeyIndex;
                switch (window.ui.shapeKeyMenuIndex)
                {
                    case 1: // すべてロック
                        foreach (var group in window.groupedShapes)
                        {
                            ShapeKeyCommandService.SetMultipleLockStatesWithUndo(window, group.Value, true);
                        }
                        window.RequestReload();
                        window.ui.shapeKeyMenuIndex = 0;
                        break;
                    case 2: // すべて解除
                        foreach (var group in window.groupedShapes)
                        {
                            ShapeKeyCommandService.SetMultipleLockStatesWithUndo(window, group.Value, false);
                        }
                        window.RequestReload();
                        window.ui.shapeKeyMenuIndex = 0;
                        break;
                    case 3: // 値が入っているものをロックする
                        foreach (var group in window.groupedShapes)
                        {
                            foreach (var shape in group.Value)
                            {
                                if (shape.weight > 0.01f)
                                {
                                    shape.isLocked = true;
                                    window.lockedShapeKeys[shape.index] = true;
                                }
                            }
                        }
                        EditorUtility.SetDirty(window.selectedRenderer);
                        window.RequestReload();
                        window.ui.shapeKeyMenuIndex = 0;
                        break;
                    case 4: // 拡張シェイプキーを一括削除
                        window.DeleteAllExtendedShapeKeys();
                        window.shapeKeyMenuIndex = 0;
                        break;
                    case 5: // TreeViewをリセットする
                        window.ResetTreeView();
                        window.ui.shapeKeyMenuIndex = 0;
                        break;
                }
            }

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
                $"現在のグループ: {window.currentGroupDisplay} (スクロール位置: {window.scrollPosition.y:F1})",
                EditorStyles.boldLabel
            );
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            if (window.groupedShapes.Count > 0)
            {
                window.scrollPosition = EditorGUILayout.BeginScrollView(window.scrollPosition);

                // ジャンプ機能の処理
                if (window.ui.jumpToGroup != null && window.ui.needScrollToGroup)
                {
                    float targetY = 0f;
                    bool found = false;

                    foreach (var group in window.groupedShapes)
                    {
                        if (group.Key == window.ui.jumpToGroup)
                        {
                            found = true;
                            break;
                        }
                        targetY += EditorGUIUtility.singleLineHeight + 4f;
                        if (window.groupFoldouts.ContainsKey(group.Key) && window.groupFoldouts[group.Key])
                        {
                            targetY += group.Value.Count * (EditorGUIUtility.singleLineHeight + 2f);
                        }
                    }

                    if (found)
                    {
                        window.scrollPosition.y = targetY;
                    }

                    window.ui.jumpToGroup = null;
                    window.ui.needScrollToGroup = false;
                }

                foreach (var group in window.groupedShapes)
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
                        bool currentFoldout = window.groupFoldouts.ContainsKey(groupName) 
                            ? window.groupFoldouts[groupName] 
                            : true;
                        
                        bool newFoldout = EditorGUILayout.Foldout(
                            currentFoldout,
                            $"{groupName} ({visibleShapes.Count})",
                            true
                        );
                        
                        window.groupFoldouts[groupName] = newFoldout;
                        EditorGUILayout.EndHorizontal();

                        // アコーディオンコンテンツ
                        if (window.groupFoldouts.ContainsKey(groupName) && window.groupFoldouts[groupName])
                        {
                            EditorGUI.indentLevel++;

                            // 高速探査スライダー
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Label("高速探査", GUILayout.Width(150));
                            int testValue = window.groupTestSliders.ContainsKey(groupName)
                                ? Mathf.RoundToInt(window.groupTestSliders[groupName])
                                : 0;
                            int maxValue = visibleShapes.Count;
                            int newTestValue = EditorGUILayout.IntSlider(testValue, 0, maxValue);

                            window.groupTestSliders[groupName] = newTestValue;

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
                                        ShapeKeyToolSettings.SkipNonZeroValues
                                    );
                                }
                                else
                                {
                                    Grouping.ApplyTestSliderToGroup(
                                        window,
                                        groupName,
                                        newTestValue,
                                        ShapeKeyToolSettings.SkipNonZeroValues
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
                                    ShapeKeyCommandService.ToggleLockWithUndo(window, blendShape, newLocked);
                                }

                                // ロックボタンと名前の間に間隔を追加
                                GUILayout.Space(15);

                                var labelStyle = new GUIStyle(EditorStyles.label);
                                if (blendShape.isLocked)
                                {
                                    labelStyle.normal.textColor = Color.gray;
                                }
                                
                                // 拡張シェイプキーの場合は色を変更
                                if (blendShape.isExtended)
                                {
                                    labelStyle.normal.textColor = Color.cyan;
                                }
                                
                                // 検索結果のハイライト
                                string displayName = blendShape.name;
                                if (!string.IsNullOrEmpty(SearchManager.shapeKeySearchText))
                                {
                                    displayName = SearchManager.GetHighlightedText(blendShape.name, SearchManager.shapeKeySearchText, SearchManager.useRegex, SearchManager.caseSensitive);
                                }
                                
                                GUILayout.Label(displayName, labelStyle, GUILayout.Width(150));

                                EditorGUI.BeginDisabledGroup(blendShape.isLocked);
                                
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
                                    
                                    float extendedNewWeight = EditorGUILayout.Slider(displayValue, extendedMinValue, extendedMaxValue);
                                    
                                    // 値の表示
                                    GUILayout.Label($"{extendedNewWeight:F1}", GUILayout.Width(50));
                                    
                                    if (!blendShape.isLocked && Mathf.Abs(extendedNewWeight - displayValue) > 0.01f)
                                    {
                                        // スライダーの値を0~100の範囲に正規化してから設定
                                        float firstRange = extendedMaxValue - extendedMinValue;
                                        float normalizedWeight = extendedNewWeight;
                                        
                                        if (firstRange > 0)
                                        {
                                            normalizedWeight = (extendedNewWeight - extendedMinValue) / firstRange * 100f;
                                        }
                                        
                                        blendShape.weight = normalizedWeight;
                                        
                                        // 拡張シェイプキーの場合は永続化マネージャーに情報を更新
                                        if (blendShape.isExtended)
                                        {
                                            var extendedInfo = new ExtendedShapeKeyInfo(blendShape.originalName, blendShape.minValue, blendShape.maxValue);
                                            ExtendedShapeKeyManager.RegisterExtendedShapeKey(blendShape.name, extendedInfo);
                                        }
                                        
                                        // 拡張シェイプキーの場合は直接メッシュに適用（サービス経由）
                                        ShapeKeyCommandService.ApplyExtendedWeightImmediate(window, blendShape);
                                        
                                        // TreeViewを自動更新
                                        window.RequestReload();
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
                                            
                                            blendShape.weight = normalizedWeight;
                                            
                                            // 拡張シェイプキーの場合は永続化マネージャーに情報を更新
                                            if (blendShape.isExtended)
                                            {
                                                var extendedInfoForUpdate = new ExtendedShapeKeyInfo(blendShape.originalName, blendShape.minValue, blendShape.maxValue);
                                                ExtendedShapeKeyManager.RegisterExtendedShapeKey(blendShape.name, extendedInfoForUpdate);
                                            }
                                            
                                        // 拡張シェイプキーの場合は直接メッシュに適用（サービス経由）
                                        ShapeKeyCommandService.ApplyExtendedWeightImmediate(window, blendShape);
                                            
                                            // TreeViewを自動更新
                                            window.RequestReload();
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
                                            PreviewService.BeginMaxHover(window, blendShape, targetIndex);
                                            
                                            // ボタンの色を変更してホバー状態を表示
                                            GUI.color = Color.yellow;
                                            
                                            // マウスオーバー中は継続的にRepaint
                                            window.RequestRepaintThrottled();
                                        }
                                        else if (!isMaxButtonHovered && window.originalWeightsForMaxPreview.ContainsKey(targetIndex))
                                        {
                                            PreviewService.EndMaxHover(window, blendShape, targetIndex);
                                        }
                                        
                                        // スライダーを描画（ホバー中は100%を表示）
                                        float displayWeight = isMaxButtonHovered && window.originalWeightsForMaxPreview.ContainsKey(targetIndex) ? 100f : blendShape.weight;
                                    float normalNewWeight = EditorGUILayout.Slider(displayWeight, 0f, 100f);
                                        
                                        // ボタンを描画
                                        if (GUI.Button(maxButtonRect, "Max"))
                                        {
                                            if (!blendShape.isLocked && targetIndex >= 0)
                                            {
                                                blendShape.weight = 100f;
                                                window.selectedRenderer.SetBlendShapeWeight(targetIndex, 100f);
                                                Utility.MarkRendererDirty(window.selectedRenderer);
                                                
                                                // 拡張シェイプキーが存在する場合は永続化マネージャーを更新
                                                if (blendShape.isExtended)
                                                {
                                                    var extendedInfoForMax = new ExtendedShapeKeyInfo(blendShape.originalName, blendShape.minValue, blendShape.maxValue);
                                                    ExtendedShapeKeyManager.RegisterExtendedShapeKey(blendShape.name, extendedInfoForMax);
                                                }
                                                
                                                window.RequestSceneRepaint();
                                                window.RequestReload();
                                            }
                                        }
                                        
                                        // ボタンの色をリセット
                                        GUI.color = Color.white;
                                        
                                        // スライダーの値が変更された場合
                                        if (!blendShape.isLocked && Mathf.Abs(normalNewWeight - blendShape.weight) > 0.01f)
                                        {
                                            ShapeKeyCommandService.SetBlendShapeWeightWithUndo(window, blendShape, normalNewWeight);
                                            
                                            // 拡張シェイプキーが存在する場合は永続化マネージャーを更新
                                            if (blendShape.isExtended)
                                            {
                                                var extendedInfoForSlider = new ExtendedShapeKeyInfo(blendShape.originalName, blendShape.minValue, blendShape.maxValue);
                                                ExtendedShapeKeyManager.RegisterExtendedShapeKey(blendShape.name, extendedInfoForSlider);
                                            }
                                            
                                             // TreeViewを自動更新
                                             window.RequestReload();
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