using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace ShapeKeyTools
{
    internal static class TreeViewPart
    {
        private static TreeViewState state;
        private static ShapeKeyTreeView view;
        private static ShapeKeyToolWindow window;

        internal static void Init(ShapeKeyToolWindow w)
        {
            window = w;
            state ??= new TreeViewState();
            view ??= new ShapeKeyTreeView(state, w);
        }

        internal static void Repaint()
        {
            window?.Repaint();
        }

        internal static void OnGUI(Rect r) => view?.OnGUI(r);

        internal static void Reload() => view?.Reload();

        internal static TreeViewState GetTreeViewState() => state;

        private class ShapeKeyTreeView : TreeView
        {
            private ShapeKeyToolWindow tool;

            // 名前変更用の変数
            private bool isRenaming = false;
            private string renameText = "";
            private int renameItemId = -1;

            // ドラッグアンドドロップ用の変数
            private int draggedItemId = -1;
            private string draggedItemName = "";
            private int draggedItemDepth = -1;

            // シェイプキー情報を管理する辞書
            private Dictionary<int, BlendShape> itemIdToShapeKey =
                new Dictionary<int, BlendShape>();

            public ShapeKeyTreeView(TreeViewState st, ShapeKeyToolWindow t)
                : base(st)
            {
                tool = t;
                showAlternatingRowBackgrounds = true;
                showBorder = true;
                useScrollView = true;
                Reload();

                // 初期展開状態を設定
                SetInitialExpandedState();
            }

            private void SetInitialExpandedState()
            {
                // TreeViewStateの展開状態をクリア（groupFoldoutsで管理するため）
                state.expandedIDs.Clear();
            }

            protected override TreeViewItem BuildRoot()
            {
                var root = new TreeViewItem
                {
                    id = 0,
                    depth = -1,
                    displayName = "Root",
                };
                var allItems = new List<TreeViewItem>();

                int id = 1;

                // 辞書をクリア
                itemIdToShapeKey.Clear();

                // グループごとにアイテムを作成
                foreach (var group in tool.viewModel.GroupedShapes)
                {
                    // 検索フィルターを適用
                    bool groupMatchesSearch = SearchManager.ShouldShowInTreeView(group.Key);
                    bool hasVisibleShapes = false;

                    // グループ内のシェイプキーで検索にマッチするものをチェック
                    var visibleShapes = new List<BlendShape>();
                    foreach (var shape in group.Value)
                    {
                        if (SearchManager.ShouldShowInTreeView(shape.name))
                        {
                            visibleShapes.Add(shape);
                            hasVisibleShapes = true;
                        }
                    }

                    // グループまたはその子アイテムが検索にマッチする場合のみ表示
                    if (groupMatchesSearch || hasVisibleShapes)
                    {
                        var groupItem = new TreeViewItem
                        {
                            id = id++,
                            depth = 0,
                            displayName = $"{group.Key} ({visibleShapes.Count})",
                        };
                        allItems.Add(groupItem);

                        // グループが展開されている場合のみシェイプキーを追加
                        // TreeViewStateとgroupFoldoutsの両方をチェック
                        bool isExpanded = IsExpanded(groupItem.id);
                        if (tool.viewModel.GroupFoldouts.ContainsKey(group.Key))
                        {
                            isExpanded = tool.viewModel.GroupFoldouts[group.Key];
                        }

                        // 検索中は強制的に展開
                        if (!string.IsNullOrEmpty(SearchManager.treeViewSearchText))
                        {
                            isExpanded = true;
                        }

                        if (isExpanded)
                        {
                            foreach (var shape in visibleShapes)
                            {
                                string displayName = shape.name;
                                if (shape.isExtended)
                                {
                                    displayName = $"🔧 {shape.name} ({shape.weight:F1})";
                                }
                                else
                                {
                                    displayName = $"{shape.name} ({shape.weight:F1}%)";
                                }

                                // 検索結果をハイライト（TreeViewではリッチテキストがサポートされていないため、色で区別）
                                if (!string.IsNullOrEmpty(SearchManager.treeViewSearchText))
                                {
                                    // 検索にマッチする場合は背景色を変更するフラグを設定
                                    // 実際の色変更はRowGUIで行う
                                }

                                var shapeItem = new TreeViewItem
                                {
                                    id = id++,
                                    depth = 1,
                                    displayName = displayName,
                                };

                                // シェイプキー情報を辞書に保存
                                if (!itemIdToShapeKey.ContainsKey(shapeItem.id))
                                {
                                    itemIdToShapeKey[shapeItem.id] = shape;
                                }

                                allItems.Add(shapeItem);
                            }
                        }
                    }
                }

                // 親子関係を設定
                SetupParentsAndChildrenFromDepths(root, allItems);

                return root;
            }

            private void RestoreExpandedState()
            {
                // TreeView独自の展開状態管理を使用するため、何もしない
            }

            protected override bool CanStartDrag(CanStartDragArgs args)
            {
                return args.draggedItemIDs.Count > 0;
            }

            protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
            {
                if (args.draggedItemIDs.Count > 0)
                {
                    var item = FindItem(args.draggedItemIDs[0], rootItem);
                    if (item != null)
                    {
                        draggedItemId = item.id;
                        string itemName = item.displayName.Split('(')[0].Trim();

                        draggedItemName = itemName;
                        draggedItemDepth = item.depth;

                        // ドラッグデータを設定
                        DragAndDrop.SetGenericData("ShapeKeyItem", item.id);
                        DragAndDrop.StartDrag("ShapeKeyItem");
                    }
                }
            }

            private bool CanDropItem(TreeViewItem targetItem)
            {
                if (draggedItemId == -1)
                    return false;

                var draggedItem = FindItem(draggedItemId, rootItem);
                if (draggedItem == null)
                    return false;

                // 自分自身にはドロップできない
                if (draggedItem.id == targetItem.id)
                    return false;

                // グループ同士の並び替えは可能
                if (draggedItemDepth == 0 && targetItem.depth == 0)
                    return true;

                return true;
            }

            protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
            {
                // ドラッグデータを取得
                object dragData = DragAndDrop.GetGenericData("ShapeKeyItem");
                if (dragData == null)
                    return DragAndDropVisualMode.Rejected;

                var draggedItem = FindItem((int)dragData, rootItem);
                if (draggedItem == null)
                    return DragAndDropVisualMode.Rejected;

                switch (args.dragAndDropPosition)
                {
                    case DragAndDropPosition.UponItem:
                        // 目標アイテム上にドロップ
                        if (CanDropItem(draggedItem, args.parentItem))
                        {
                            if (args.performDrop)
                                PerformDrop(draggedItem, args.parentItem);
                            return DragAndDropVisualMode.Move;
                        }
                        break;

                    case DragAndDropPosition.BetweenItems:
                        // アイテム間にドロップ（insertAtIndex が有効）
                        // グループ同士の並び替えの場合は、targetItemを取得する必要がある
                        TreeViewItem targetItem = null;

                        // グループ同士の並び替えの場合（rootItemの直接の子）
                        if (args.parentItem == rootItem && rootItem.children != null)
                        {
                            int targetIndex = args.insertAtIndex;
                            if (targetIndex >= 0 && targetIndex < rootItem.children.Count)
                            {
                                targetItem = rootItem.children[targetIndex];
                            }
                        }
                        // 通常のアイテム間の並び替え
                        else if (args.parentItem != null && args.parentItem.children != null)
                        {
                            int targetIndex = args.insertAtIndex;
                            if (targetIndex >= 0 && targetIndex < args.parentItem.children.Count)
                            {
                                targetItem = args.parentItem.children[targetIndex];
                            }
                        }

                        if (CanDropItem(draggedItem, targetItem))
                        {
                            if (args.performDrop)
                                PerformDrop(draggedItem, targetItem, args.insertAtIndex);
                            return DragAndDropVisualMode.Move;
                        }
                        break;

                    case DragAndDropPosition.OutsideItems:
                        // リスト外（末尾など）にドロップ
                        if (args.performDrop)
                            PerformDrop(draggedItem, rootItem, -1); // 末尾扱い
                        return DragAndDropVisualMode.Move;
                }
                return DragAndDropVisualMode.Rejected;
            }

            private bool CanDropItem(TreeViewItem draggedItem, TreeViewItem targetItem)
            {
                // targetItemがnullの場合は、末尾への移動として許可
                if (targetItem == null)
                    return true;

                // 自分自身にはドロップできない
                if (draggedItem.id == targetItem.id)
                    return false;

                // グループ同士の並び替えは可能
                if (draggedItem.depth == 0 && targetItem.depth == 0)
                    return true;

                // シェイプキーはグループにドロップ可能
                if (draggedItem.depth == 1 && targetItem.depth == 0)
                    return true;

                // 同じ階層内での移動は可能
                if (draggedItem.depth == targetItem.depth)
                    return true;

                return false;
            }

            private void PerformDrop(
                TreeViewItem draggedItem,
                TreeViewItem targetItem = null,
                int insertIndex = -1
            )
            {
                if (draggedItem == null)
                    return;

                string draggedItemName = draggedItem.displayName.Split('(')[0].Trim();
                string targetItemName =
                    targetItem != null ? targetItem.displayName.Split('(')[0].Trim() : "";

                // グループ同士の上下入替え
                if (
                    draggedItem.depth == 0
                    && targetItem != null
                    && targetItem.depth == 0
                    && insertIndex >= 0
                )
                {
                    TreeViewCommandService.ReorderGroups(
                        tool,
                        draggedItemName,
                        targetItemName,
                        insertIndex
                    );
                }
                // 同一グループ内の並び替え
                else if (
                    draggedItem.depth == 1
                    && targetItem != null
                    && targetItem.depth == 1
                    && insertIndex >= 0
                )
                {
                    TreeViewCommandService.ReorderShapeKey(
                        tool,
                        draggedItemName,
                        targetItemName,
                        insertIndex
                    );
                }
                // シェイプキーを別のグループに移動
                else if (draggedItem.depth == 1 && targetItem != null && targetItem.depth == 0)
                {
                    TreeViewCommandService.MoveShapeKeyToGroup(
                        tool,
                        draggedItemName,
                        targetItemName
                    );
                }
                // 末尾への移動（targetItemがnullまたはinsertIndexが-1）
                else if (targetItem == null || insertIndex == -1)
                {
                    if (draggedItem.depth == 0)
                    {
                        TreeViewCommandService.MoveGroupToEnd(tool, draggedItemName);
                    }
                    else
                    {
                        TreeViewCommandService.MoveShapeKeyToEnd(tool, draggedItemName);
                    }
                }

                Reload();
            }

            private void MoveGroupToEnd(string groupName)
            {
                // グループを末尾に移動
                var list = tool.viewModel.GroupedShapes.Keys.ToList();
                list.Remove(groupName);
                list.Add(groupName);

                // 新しい順序で Dictionary を再構築
                var newGroups = new Dictionary<string, List<BlendShape>>();
                var newFoldouts = new Dictionary<string, bool>();
                var newTestSliders = new Dictionary<string, float>();

                foreach (var group in list)
                {
                    newGroups[group] = tool.viewModel.GroupedShapes[group];
                    if (tool.viewModel.GroupFoldouts.ContainsKey(group))
                        newFoldouts[group] = tool.viewModel.GroupFoldouts[group];
                    if (tool.viewModel.GroupTestSliders.ContainsKey(group))
                        newTestSliders[group] = tool.viewModel.GroupTestSliders[group];
                }

                tool.viewModel.GroupedShapes = newGroups;
                tool.viewModel.GroupFoldouts = newFoldouts;
                tool.viewModel.GroupTestSliders = newTestSliders;

                // TreeViewを更新
                TreeViewPart.Reload();
            }

            private void MoveShapeKeyToEnd(string shapeName)
            {
                // シェイプキーを末尾に移動
                foreach (var group in tool.viewModel.GroupedShapes)
                {
                    var shapes = group.Value;
                    int sourceIndex = shapes.FindIndex(s => s.name == shapeName);

                    if (sourceIndex != -1)
                    {
                        var shapeToMove = shapes[sourceIndex];
                        shapes.RemoveAt(sourceIndex);
                        shapes.Add(shapeToMove);

                        break;
                    }
                }
            }

            private void ReorderGroups(string sourceGroup, string targetGroup, int insertIndex)
            {
                // グループの順序を変更
                var list = tool.viewModel.GroupedShapes.Keys.ToList();

                // ソースグループを削除
                list.Remove(sourceGroup);

                // insertIndexを調整（ソースグループが削除されたため）
                int adjustedInsertIndex = insertIndex;
                if (list.Count > 0 && adjustedInsertIndex >= list.Count)
                {
                    adjustedInsertIndex = list.Count;
                }

                // 新しい位置に挿入
                list.Insert(adjustedInsertIndex, sourceGroup);

                // 新しい順序で Dictionary を再構築
                var newGroups = new Dictionary<string, List<BlendShape>>();
                var newFoldouts = new Dictionary<string, bool>();
                var newTestSliders = new Dictionary<string, float>();

                foreach (var group in list)
                {
                    newGroups[group] = tool.viewModel.GroupedShapes[group];
                    if (tool.viewModel.GroupFoldouts.ContainsKey(group))
                        newFoldouts[group] = tool.viewModel.GroupFoldouts[group];
                    if (tool.viewModel.GroupTestSliders.ContainsKey(group))
                        newTestSliders[group] = tool.viewModel.GroupTestSliders[group];
                }

                tool.viewModel.GroupedShapes = newGroups;
                tool.viewModel.GroupFoldouts = newFoldouts;
                tool.viewModel.GroupTestSliders = newTestSliders;

                // TreeViewを更新
                TreeViewPart.Reload();
            }

            // MoveShapeKeyToGroupとReorderShapeKeyはTreeViewCommandServiceに移行済み

            public void StartRename(int itemId, string currentName)
            {
                isRenaming = true;

                // 名前を取得
                string cleanName = currentName;

                renameText = cleanName;
                renameItemId = itemId;
                TreeViewPart.Repaint();

                // 次のフレームでフォーカスを設定
                EditorApplication.delayCall += () =>
                {
                    TreeViewPart.Repaint();
                };
            }

            protected override void RowGUI(RowGUIArgs args)
            {
                var item = args.item;

                // 検索結果のハイライト処理（背景色のみ。テキストはプレーンに統一）
                bool isSearchMatch = false;
                if (!string.IsNullOrEmpty(SearchManager.treeViewSearchText))
                {
                    string itemName = item.displayName;
                    // 括弧内の情報を除去してシェイプキー名のみを取得
                    int bracketIndex = itemName.IndexOf('(');
                    if (bracketIndex > 0)
                    {
                        itemName = itemName.Substring(0, bracketIndex).Trim();
                    }

                    isSearchMatch = SearchManager.ShouldShowInTreeView(itemName);
                }

                // 検索にマッチする場合は背景色を変更
                if (isSearchMatch)
                {
                    Color originalColor = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1f, 1f, 0.8f, 0.3f); // 薄い黄色

                    // 背景を描画
                    GUI.Box(args.rowRect, "");

                    GUI.backgroundColor = originalColor;
                }

                // 名前変更中の場合はテキストフィールドを表示
                if (isRenaming && renameItemId == item.id)
                {
                    // テキストフィールドを表示
                    string newName = EditorGUI.TextField(args.rowRect, renameText);
                    if (newName != renameText)
                    {
                        renameText = newName;
                    }

                    // キーイベントの処理
                    Event e = Event.current;
                    if (e.type == EventType.KeyDown)
                    {
                        if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                        {
                            ApplyRename();
                            e.Use();
                        }
                        else if (e.keyCode == KeyCode.Escape)
                        {
                            CancelRename();
                            e.Use();
                        }
                    }

                    // マウスクリックでフォーカスが外れた場合
                    if (e.type == EventType.MouseDown && !args.rowRect.Contains(e.mousePosition))
                    {
                        ApplyRename();
                        e.Use();
                    }
                }
                else
                {
                    // 通常の表示
                    if (item.depth == 0)
                    {
                        // グループアイテムの表示（ファイルアイコン付き）
                        DrawGroupItem(args);
                    }
                    else
                    {
                        base.RowGUI(args); // プレーンテキスト描画（リッチテキストは使わない）
                    }
                }

                // 右クリックイベントの処理
                Event currentEvent = Event.current;
                if (
                    currentEvent.type == EventType.MouseDown
                    && currentEvent.button == 1
                    && args.rowRect.Contains(currentEvent.mousePosition)
                )
                {
                    // 右クリックが検出された場合、ContextClickedItemを呼び出す
                    ContextClickedItem(item.id);
                    currentEvent.Use();
                }
            }

            private void ApplyRename()
            {
                if (isRenaming && renameItemId != -1)
                {
                    var item = FindItem(renameItemId, rootItem);
                        if (item != null)
                    {
                        if (item.depth == 0)
                        {
                            // グループ名の変更
                            string oldGroupName = item.displayName.Split('(')[0].Trim();
                            if (tool.viewModel.GroupedShapes.ContainsKey(oldGroupName))
                            {
                                var shapes = tool.viewModel.GroupedShapes[oldGroupName];
                                tool.viewModel.GroupedShapes.Remove(oldGroupName);
                                tool.viewModel.GroupedShapes[renameText] = shapes;

                                // foldoutの状態も移行
                                if (tool.viewModel.GroupFoldouts.ContainsKey(oldGroupName))
                                {
                                    bool foldoutState = tool.viewModel.GroupFoldouts[oldGroupName];
                                    tool.viewModel.GroupFoldouts.Remove(oldGroupName);
                                    tool.viewModel.GroupFoldouts[renameText] = foldoutState;
                                }

                                    // テストスライダーの値も移行
                                    if (tool.viewModel.GroupTestSliders.ContainsKey(oldGroupName))
                                    {
                                        float slider = tool.viewModel.GroupTestSliders[oldGroupName];
                                        tool.viewModel.GroupTestSliders.Remove(oldGroupName);
                                        tool.viewModel.GroupTestSliders[renameText] = slider;
                                    }

                                    // グループごとの元値キャッシュも移行
                                    if (tool.viewModel.OriginalWeights.ContainsKey(oldGroupName))
                                    {
                                        var cache = tool.viewModel.OriginalWeights[oldGroupName];
                                        tool.viewModel.OriginalWeights.Remove(oldGroupName);
                                        tool.viewModel.OriginalWeights[renameText] = cache;
                                    }
                            }
                        }
                        else
                        {
                            // シェイプキー名の変更
                            string oldShapeName = item.displayName.Split('(')[0].Trim();

                            foreach (var group in tool.viewModel.GroupedShapes)
                            {
                                var shape = group.Value.FirstOrDefault(s => s.name == oldShapeName);
                                if (shape != null)
                                {
                                    bool wasExtended = shape.isExtended;
                                    bool oldNameParsed = ExtendedShapeKeyInfo.TryParseFromName(oldShapeName, out var oldParsedInfo);
                                    string prevOriginal = shape.originalName;
                                    float prevMin = shape.minValue;
                                    float prevMax = shape.maxValue;

                                    // 一旦、旧キーでの登録を削除（存在すれば）
                                    ExtendedShapeKeyManager.RemoveExtendedShapeKey(oldShapeName);

                                    // 名称を更新
                                    shape.name = renameText;

                                    // 新しい名前が拡張パターンの場合は、パターンから同期
                                    if (ExtendedShapeKeyInfo.TryParseFromName(renameText, out var newInfo))
                                    {
                                        shape.isExtended = true;
                                        shape.originalName = newInfo.originalName;
                                        shape.minValue = newInfo.minValue;
                                        shape.maxValue = newInfo.maxValue;
                                        ExtendedShapeKeyManager.RegisterExtendedShapeKey(renameText, newInfo);
                                    }
                                    else if (wasExtended || oldNameParsed)
                                    {
                                        // 旧来が拡張だった場合は拡張メタを保持しつつ、表示名のみ変更
                                        shape.isExtended = true;
                                        shape.originalName = prevOriginal;
                                        shape.minValue = prevMin;
                                        shape.maxValue = prevMax;
                                        ExtendedShapeKeyManager.RegisterExtendedShapeKey(renameText, new ExtendedShapeKeyInfo(prevOriginal, prevMin, prevMax));
                                    }
                                    else
                                    {
                                        // 通常シェイプキーの改名: originalName は維持（メッシュ上の元名）
                                        shape.isExtended = false;
                                        // shape.originalName は変更しない
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }

                CancelRename();
                Reload();
                // 右パネル/シーンを更新
                ApplyScheduler.RequestReload();
                ApplyScheduler.RequestRepaint();
            }

            private void CancelRename()
            {
                isRenaming = false;
                renameText = "";
                renameItemId = -1;
            }

            /// <summary>
            /// グループアイテムをファイルアイコン付きで描画
            /// </summary>
            private void DrawGroupItem(RowGUIArgs args)
            {
                var item = args.item;
                var rect = args.rowRect;

                // アイコンの描画
                var iconRect = new Rect(rect.x + GetContentIndent(item), rect.y, 16, 16);
                var icon = EditorGUIUtility.IconContent("FolderOpened Icon");
                if (icon != null)
                {
                    GUI.DrawTexture(iconRect, icon.image);
                }

                // テキストの描画（アイコンの右側）
                var textRect = new Rect(
                    iconRect.x + 18,
                    rect.y,
                    rect.width - iconRect.x - 18,
                    rect.height
                );
                var labelStyle = new GUIStyle(EditorStyles.label);
                labelStyle.alignment = TextAnchor.MiddleLeft;

                // テキストの色はデフォルトのまま

                GUI.Label(textRect, item.displayName, labelStyle);

                // 展開/折りたたみボタンの描画とクリックイベント処理
                var toggleRect = new Rect(rect.x + GetContentIndent(item) - 16, rect.y, 16, 16);
                bool isExpanded = IsExpanded(item.id);
                bool newExpanded = EditorGUI.Foldout(toggleRect, isExpanded, "");
                if (newExpanded != isExpanded)
                {
                    SetExpanded(item.id, newExpanded);
                    Reload();
                }
            }

            protected override void DoubleClickedItem(int id)
            {
                var item = FindItem(id, rootItem);
                if (item != null)
                {
                    // ダブルクリックで名前変更を開始
                    string currentName = item.displayName.Split('(')[0].Trim();
                    StartRename(id, currentName);
                }
            }

            protected override bool CanMultiSelect(TreeViewItem item)
            {
                // シェイプキーアイテム（depth == 1）のみマルチ選択を許可
                return item.depth == 1;
            }

            protected override void SingleClickedItem(int id)
            {
                var item = FindItem(id, rootItem);
                if (item != null)
                {
                    if (
                        item.depth == 1
                        && itemIdToShapeKey.TryGetValue(item.id, out var blendShape)
                    ) // シェイプキーアイテムの場合
                    {
                        // 選択されたシェイプキーの情報をログに出力（デバッグ用）
                    }
                }
            }

            protected override void ContextClicked()
            {
                // 何もないところを右クリックした時の処理
                GenericMenu menu = new GenericMenu();
                menu.AddItem(
                    new GUIContent("Rootグループを追加する"),
                    false,
                    () =>
                    {
                        TreeViewCommandService.AddNewGroup(tool);
                    }
                );
                menu.ShowAsContext();
            }

            private void AddNewGroup()
            {
                // 新しいグループ名を生成（重複しないように）
                string baseName = "新しいグループ";
                string newGroupName = baseName;
                int counter = 1;

                while (tool.viewModel.GroupedShapes.ContainsKey(newGroupName))
                {
                    newGroupName = $"{baseName} ({counter})";
                    counter++;
                }

                // 新しいグループを追加
                tool.viewModel.GroupedShapes[newGroupName] = new List<BlendShape>();
                tool.viewModel.GroupFoldouts[newGroupName] = true; // デフォルトで開く
                tool.viewModel.GroupTestSliders[newGroupName] = 0f; // テストスライダーの初期化

                // TreeViewを更新
                Reload();

                // メインウィンドウも更新
                TreeViewPart.Repaint();
            }

            private void DeleteGroup(string groupName)
            {
                // 削除確認ダイアログ
                bool shouldDelete = EditorUtility.DisplayDialog(
                    "グループ削除の確認",
                    $"グループ '{groupName}' を削除しますか？\n\nこのグループ内のシェイプキーは「その他」グループに移動されます。",
                    "削除",
                    "キャンセル"
                );

                if (!shouldDelete)
                    return;

                // グループ内のシェイプキーを「その他」グループに移動
                if (tool.viewModel.GroupedShapes.ContainsKey(groupName))
                {
                    var shapesToMove = tool.viewModel.GroupedShapes[groupName];

                    // 「その他」グループが存在しない場合は作成
                    if (!tool.viewModel.GroupedShapes.ContainsKey("その他"))
                    {
                        tool.viewModel.GroupedShapes["その他"] = new List<BlendShape>();
                        tool.viewModel.GroupFoldouts["その他"] = true;
                        tool.viewModel.GroupTestSliders["その他"] = 0f;
                    }

                    // シェイプキーを「その他」グループに移動
                    tool.viewModel.GroupedShapes["その他"].AddRange(shapesToMove);

                    // 元のグループを削除
                    tool.viewModel.GroupedShapes.Remove(groupName);
                    tool.viewModel.GroupFoldouts.Remove(groupName);
                    tool.viewModel.GroupTestSliders.Remove(groupName);
                }

                // TreeViewを更新
                Reload();

                // メインウィンドウも更新
                TreeViewPart.Repaint();
            }

            protected override void ContextClickedItem(int id)
            {
                var item = FindItem(id, rootItem);
                if (item != null)
                {
                    // 選択されたアイテムのリストを取得
                    var selectedItems = GetSelection();

                    // 右クリックメニューを表示
                    GenericMenu menu = new GenericMenu();

                    string currentName;
                    if (itemIdToShapeKey.TryGetValue(item.id, out var blendShape))
                    {
                        // 辞書からシェイプキー情報を取得
                        currentName = blendShape.name;
                    }
                    else
                    {
                        // フォールバック: displayNameから解析
                        currentName = item.displayName.Split('(')[0].Trim();
                        // アイコンを除去
                        if (currentName.StartsWith("🔧 ") || currentName.StartsWith("⚡ "))
                        {
                            currentName = currentName.Substring(2);
                        }
                    }

                    // 名前変更メニュー
                    menu.AddItem(
                        new GUIContent("名前を変更"),
                        false,
                        () =>
                        {
                            EditorApplication.delayCall += () => StartRename(id, currentName);
                        }
                    );

                    if (item.depth == 0)
                    {
                        // グループの場合の追加メニュー
                        // 削除メニュー（「その他」グループは削除不可）
                        if (currentName != "その他")
                        {
                            menu.AddSeparator("");
                            menu.AddItem(
                                new GUIContent("グループを削除"),
                                false,
                                () =>
                                {
                                    TreeViewCommandService.DeleteGroup(tool, currentName);
                                }
                            );
                        }
                    }
                    else if (item.depth == 1)
                    {
                        // シェイプキーの場合の追加メニュー
                        menu.AddSeparator("");
                        menu.AddItem(
                            new GUIContent("拡張シェイプキーを作成"),
                            false,
                            () =>
                            {
                                TreeViewCommandService.CreateExtendedShapeKeyWithDialog(
                                    tool,
                                    currentName
                                );
                            }
                        );

                        // Rootグループとして設定メニュー
                        menu.AddItem(
                            new GUIContent("Rootグループとして設定"),
                            false,
                            () =>
                            {
                                TreeViewCommandService.SetAsRootGroup(tool, currentName);
                            }
                        );

                        // 複数選択されている場合、グループ化メニューを追加
                        if (selectedItems.Count > 1)
                        {
                            menu.AddSeparator("");
                            menu.AddItem(
                                new GUIContent("グループ化してRootグループとして設定"),
                                false,
                                () =>
                                {
                                    var shapes = new List<BlendShape>();
                                    foreach (int itemId in selectedItems)
                                    {
                                        if (itemIdToShapeKey.TryGetValue(itemId, out var bs))
                                            shapes.Add(bs);
                                    }
                                    TreeViewCommandService.GroupAsRootGroup(tool, shapes);
                                }
                            );
                        }

                        // シェイプキー削除メニュー
                        menu.AddSeparator("");
                        menu.AddItem(
                            new GUIContent("シェイプキーを削除"),
                            false,
                            () =>
                            {
                                TreeViewCommandService.DeleteShapeKey(tool, currentName);
                            }
                        );
                    }

                    menu.ShowAsContext();
                }
            }

            // CreateExtendedShapeKeyとCreateExtendedShapeKeyInternalはTreeViewCommandServiceに移行済み

            // SetAsRootGroupはTreeViewCommandServiceに移行済み

            // GroupAsRootGroupはTreeViewCommandServiceに移行済み

            // DeleteShapeKeyはTreeViewCommandServiceに移行済み
        }
    }
}
