using UnityEditor.IMGUI.Controls;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

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
            private Dictionary<int, BlendShape> itemIdToShapeKey = new Dictionary<int, BlendShape>();

            public ShapeKeyTreeView(TreeViewState st, ShapeKeyToolWindow t) : base(st)
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
                var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
                var allItems = new List<TreeViewItem>();

                int id = 1;
                
                // 辞書をクリア
                itemIdToShapeKey.Clear();

                // グループごとにアイテムを作成
                foreach (var group in tool.groupedShapes)
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
                        var groupItem = new TreeViewItem { id = id++, depth = 0, displayName = $"{group.Key} ({visibleShapes.Count})" };
                        allItems.Add(groupItem);

                        // グループが展開されている場合のみシェイプキーを追加
                        // TreeViewStateとgroupFoldoutsの両方をチェック
                        bool isExpanded = IsExpanded(groupItem.id);
                        if (tool.groupFoldouts.ContainsKey(group.Key))
                        {
                            isExpanded = tool.groupFoldouts[group.Key];
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
                                    displayName = displayName
                                };
                                
                                // シェイプキー情報を辞書に保存
                                itemIdToShapeKey[shapeItem.id] = shape;
                                
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
                if (draggedItemId == -1) return false;

                var draggedItem = FindItem(draggedItemId, rootItem);
                if (draggedItem == null) return false;

                // 自分自身にはドロップできない
                if (draggedItem.id == targetItem.id) return false;

                // グループ同士の並び替えは可能
                if (draggedItemDepth == 0 && targetItem.depth == 0) return true;

                return true;
            }

            protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
            {
                // ドラッグデータを取得
                object dragData = DragAndDrop.GetGenericData("ShapeKeyItem");
                if (dragData == null) return DragAndDropVisualMode.Rejected;

                var draggedItem = FindItem((int)dragData, rootItem);
                if (draggedItem == null) return DragAndDropVisualMode.Rejected;

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
                            PerformDrop(draggedItem, rootItem, -1);    // 末尾扱い
                        return DragAndDropVisualMode.Move;
                }
                return DragAndDropVisualMode.Rejected;
            }

            private bool CanDropItem(TreeViewItem draggedItem, TreeViewItem targetItem)
            {
                // targetItemがnullの場合は、末尾への移動として許可
                if (targetItem == null) return true;

                // 自分自身にはドロップできない
                if (draggedItem.id == targetItem.id) return false;

                // グループ同士の並び替えは可能
                if (draggedItem.depth == 0 && targetItem.depth == 0) return true;

                // シェイプキーはグループにドロップ可能
                if (draggedItem.depth == 1 && targetItem.depth == 0) return true;

                // 同じ階層内での移動は可能
                if (draggedItem.depth == targetItem.depth) return true;

                return false;
            }

            private void PerformDrop(TreeViewItem draggedItem, TreeViewItem targetItem = null, int insertIndex = -1)
            {
                if (draggedItem == null) return;

                string draggedItemName = draggedItem.displayName.Split('(')[0].Trim();
                string targetItemName = targetItem != null ? targetItem.displayName.Split('(')[0].Trim() : "";

                // グループ同士の上下入替え
                if (draggedItem.depth == 0 && targetItem != null && targetItem.depth == 0 && insertIndex >= 0)
                {
                    ReorderGroups(draggedItemName, targetItemName, insertIndex);
                }
                // 同一グループ内の並び替え
                else if (draggedItem.depth == 1 && targetItem != null && targetItem.depth == 1 && insertIndex >= 0)
                {
                    ReorderShapeKey(draggedItemName, targetItemName, insertIndex);
                }
                // シェイプキーを別のグループに移動
                else if (draggedItem.depth == 1 && targetItem != null && targetItem.depth == 0)
                {
                    MoveShapeKeyToGroup(draggedItemName, targetItemName);
                }
                // 末尾への移動（targetItemがnullまたはinsertIndexが-1）
                else if (targetItem == null || insertIndex == -1)
                {
                    if (draggedItem.depth == 0)
                    {
                        MoveGroupToEnd(draggedItemName);
                    }
                    else
                    {
                        MoveShapeKeyToEnd(draggedItemName);
                    }
                }

                Reload();
            }

            private void MoveGroupToEnd(string groupName)
            {
                // グループを末尾に移動
                var list = tool.groupedShapes.Keys.ToList();
                list.Remove(groupName);
                list.Add(groupName);

                // 新しい順序で Dictionary を再構築
                var newGroups = new Dictionary<string, List<BlendShape>>();
                var newFoldouts = new Dictionary<string, bool>();
                var newTestSliders = new Dictionary<string, float>();

                foreach (var group in list)
                {
                    newGroups[group] = tool.groupedShapes[group];
                    if (tool.groupFoldouts.ContainsKey(group))
                        newFoldouts[group] = tool.groupFoldouts[group];
                    if (tool.groupTestSliders.ContainsKey(group))
                        newTestSliders[group] = tool.groupTestSliders[group];
                }

                // 再割り当てではなく内容を入れ替える（ViewModelプロパティは読み取り専用のため）
                tool.groupedShapes.Clear();
                foreach (var kv in newGroups)
                {
                    tool.groupedShapes[kv.Key] = kv.Value;
                }
                tool.groupFoldouts.Clear();
                foreach (var kv in newFoldouts)
                {
                    tool.groupFoldouts[kv.Key] = kv.Value;
                }
                tool.groupTestSliders.Clear();
                foreach (var kv in newTestSliders)
                {
                    tool.groupTestSliders[kv.Key] = kv.Value;
                }

                // TreeViewを更新（スロットリング経由）
                tool.RequestReload();
            }

            private void MoveShapeKeyToEnd(string shapeName)
            {
                // シェイプキーを末尾に移動
                foreach (var group in tool.groupedShapes)
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
                var list = tool.groupedShapes.Keys.ToList();
                
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
                    newGroups[group] = tool.groupedShapes[group];
                    if (tool.groupFoldouts.ContainsKey(group))
                        newFoldouts[group] = tool.groupFoldouts[group];
                    if (tool.groupTestSliders.ContainsKey(group))
                        newTestSliders[group] = tool.groupTestSliders[group];
                }

                // 再割り当てではなく内容を入れ替える（ViewModelプロパティは読み取り専用のため）
                tool.groupedShapes.Clear();
                foreach (var kv in newGroups)
                {
                    tool.groupedShapes[kv.Key] = kv.Value;
                }
                tool.groupFoldouts.Clear();
                foreach (var kv in newFoldouts)
                {
                    tool.groupFoldouts[kv.Key] = kv.Value;
                }
                tool.groupTestSliders.Clear();
                foreach (var kv in newTestSliders)
                {
                    tool.groupTestSliders[kv.Key] = kv.Value;
                }

                // TreeViewを更新（スロットリング経由）
                tool.RequestReload();
            }

            private void MoveShapeKeyToGroup(string shapeName, string targetGroup)
            {
                // シェイプキーを別のグループに移動
                BlendShape shapeToMove = null;
                string sourceGroup = "";

                foreach (var group in tool.groupedShapes)
                {
                    var shape = group.Value.FirstOrDefault(s => s.name == shapeName);
                    if (shape != null)
                    {
                        shapeToMove = shape;
                        sourceGroup = group.Key;
                        break;
                    }
                }

                if (shapeToMove != null && sourceGroup != targetGroup)
                {
                    // 元のグループから削除
                    tool.groupedShapes[sourceGroup].Remove(shapeToMove);

                    // 新しいグループに追加
                    if (!tool.groupedShapes.ContainsKey(targetGroup))
                    {
                        tool.groupedShapes[targetGroup] = new List<BlendShape>();
                    }
                    tool.groupedShapes[targetGroup].Add(shapeToMove);

                    
                }
            }

            private void ReorderShapeKey(string sourceShape, string targetShape, int insertIndex)
            {
                // 同じグループ内でのシェイプキーの順序変更
                foreach (var group in tool.groupedShapes)
                {
                    var shapes = group.Value;
                    int sourceIndex = shapes.FindIndex(s => s.name == sourceShape);

                    if (sourceIndex != -1)
                    {
                        var shapeToMove = shapes[sourceIndex];
                        shapes.RemoveAt(sourceIndex);
                        shapes.Insert(insertIndex, shapeToMove);

                        
                        break;
                    }
                }
            }

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

                // 検索結果のハイライト処理
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
                         base.RowGUI(args);
                     }
                 }

                // 右クリックイベントの処理
                Event currentEvent = Event.current;
                if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1 && args.rowRect.Contains(currentEvent.mousePosition))
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
                            // グループ名の変更 → Commandへ移譲
                            string oldGroupName = item.displayName.Split('(')[0].Trim();
                            ShapeKeyCommandService.RenameGroupWithUndo(tool, oldGroupName, renameText);
                        }
                        else
                        {
                            // シェイプキー名の変更 → Commandへ移譲
                            string oldShapeName = item.displayName.Split('(')[0].Trim();
                            ShapeKeyCommandService.RenameShapeWithUndo(tool, oldShapeName, renameText);
                        }
                    }
                }

                CancelRename();
                Reload();
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
                var textRect = new Rect(iconRect.x + 18, rect.y, rect.width - iconRect.x - 18, rect.height);
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
                    if (item.depth == 1 && itemIdToShapeKey.TryGetValue(item.id, out var blendShape)) // シェイプキーアイテムの場合
                    {
                        // 選択されたシェイプキーの情報をログに出力（デバッグ用）

                    }
                }
            }

            protected override void ContextClicked()
            {
                // 何もないところを右クリックした時の処理
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Rootグループを追加する"), false, () =>
                {
                    AddNewGroup();
                });
                menu.ShowAsContext();
            }

            private void AddNewGroup()
            {
                // 新しいグループ名を生成（重複しないように）
                string baseName = "新しいグループ";
                string newGroupName = baseName;
                int counter = 1;

                while (tool.groupedShapes.ContainsKey(newGroupName))
                {
                    newGroupName = $"{baseName} ({counter})";
                    counter++;
                }

                // 新しいグループを追加
                tool.groupedShapes[newGroupName] = new List<BlendShape>();
                tool.groupFoldouts[newGroupName] = true; // デフォルトで開く
                tool.groupTestSliders[newGroupName] = 0f; // テストスライダーの初期化

                

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

                if (!shouldDelete) return;

                // グループ内のシェイプキーを「その他」グループに移動
                if (tool.groupedShapes.ContainsKey(groupName))
                {
                    var shapesToMove = tool.groupedShapes[groupName];

                    // 「その他」グループが存在しない場合は作成
                    if (!tool.groupedShapes.ContainsKey("その他"))
                    {
                        tool.groupedShapes["その他"] = new List<BlendShape>();
                        tool.groupFoldouts["その他"] = true;
                        tool.groupTestSliders["その他"] = 0f;
                    }

                    // シェイプキーを「その他」グループに移動
                    tool.groupedShapes["その他"].AddRange(shapesToMove);

                    // 元のグループを削除
                    tool.groupedShapes.Remove(groupName);
                    tool.groupFoldouts.Remove(groupName);
                    tool.groupTestSliders.Remove(groupName);


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
                    menu.AddItem(new GUIContent("名前を変更"), false, () =>
                    {

                        EditorApplication.delayCall += () => StartRename(id, currentName);
                    });

                    if (item.depth == 0)
                    {
                        // グループの場合の追加メニュー
                        // 削除メニュー（「その他」グループは削除不可）
                        if (currentName != "その他")
                        {
                            menu.AddSeparator("");
                            menu.AddItem(new GUIContent("グループを削除"), false, () =>
                            {
                                DeleteGroup(currentName);
                            });
                        }
                    }
                    else if (item.depth == 1)
                    {
                        // シェイプキーの場合の追加メニュー
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("拡張シェイプキーを作成"), false, () =>
                        {
                            CreateExtendedShapeKey(currentName);
                        });
                        
                        // Rootグループとして設定メニュー
                        menu.AddItem(new GUIContent("Rootグループとして設定"), false, () =>
                        {
                            SetAsRootGroup(currentName);
                        });

                        // 複数選択されている場合、グループ化メニューを追加
                        if (selectedItems.Count > 1)
                        {
                            menu.AddSeparator("");
                            menu.AddItem(new GUIContent("グループ化してRootグループとして設定"), false, () =>
                            {
                                GroupAsRootGroup(selectedItems);
                            });
                        }
                        
                        // シェイプキー削除メニュー
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("シェイプキーを削除"), false, () =>
                        {
                            DeleteShapeKey(currentName);
                        });
                    }

                    menu.ShowAsContext();
                }
            }

            private void CreateExtendedShapeKey(string originalShapeKeyName)
            {
                // 拡張シェイプキー設定ウィンドウを表示
                ShapeKeyExtensionWindow.ShowWindow(originalShapeKeyName, (originalName, minValue, maxValue) =>
                {
                    // 拡張シェイプキーを作成
                    CreateExtendedShapeKeyInternal(originalName, minValue, maxValue);
                });
            }

            private void CreateExtendedShapeKeyInternal(string originalName, int minValue, int maxValue)
            {
                if (tool.selectedRenderer == null)
                {
                    Debug.LogError("BlendShapeLimitBreak: 選択されたレンダラーがありません");
                    return;
                }

                try
                {
                    // 元のシェイプキーを探して、どのグループに属しているかを特定
                    string targetGroup = "その他";
                    int originalIndex = -1;
                    int originalGroupIndex = -1;
                    
                    foreach (var group in tool.groupedShapes)
                    {
                        for (int i = 0; i < group.Value.Count; i++)
                        {
                            var shape = group.Value[i];
                            if (shape.name == originalName)
                            {
                                targetGroup = group.Key;
                                originalIndex = shape.index;
                                originalGroupIndex = i;
                                break;
                            }
                        }
                        if (originalIndex != -1) break;
                    }

                    if (originalIndex == -1)
                    {
                        Debug.LogError($"BlendShapeLimitBreak: 元のシェイプキー '{originalName}' が見つかりません");
                        return;
                    }

                    // 拡張シェイプキー名を生成
                    string extendedName = $"{originalName}_min:{minValue}_max:{maxValue}";

                    // 既に存在するかチェック
                    bool alreadyExists = false;
                    foreach (var group in tool.groupedShapes)
                    {
                        if (group.Value.Any(s => s.name == extendedName))
                        {
                            alreadyExists = true;
                            break;
                        }
                    }

                    // 既に存在する場合は削除確認ダイアログを表示
                    if (alreadyExists)
                    {
                        bool shouldReplace = EditorUtility.DisplayDialog(
                            "拡張シェイプキーの上書き確認",
                            $"拡張シェイプキー '{extendedName}' は既に存在します。\n\n上書きしますか？",
                            "上書き",
                            "キャンセル"
                        );

                        if (!shouldReplace)
                        {
                            return;
                        }

                        // 既存の拡張シェイプキーを削除
                        BlendShapeLimitBreak.RemoveBlendShapeFromMesh(tool.selectedRenderer, extendedName);
                        
                        // UIからも削除
                        foreach (var group in tool.groupedShapes)
                        {
                            var existingShape = group.Value.FirstOrDefault(s => s.name == extendedName);
                            if (existingShape != null)
                            {
                                group.Value.Remove(existingShape);
                                ExtendedShapeKeyManager.RemoveExtendedShapeKey(extendedName);
                                break;
                            }
                        }
                    }

                    // 実際のメッシュに拡張シェイプキーを追加
                    bool meshSuccess = BlendShapeLimitBreak.ApplyExtendedShapeKeyToMesh(
                        tool.selectedRenderer, 
                        extendedName, 
                        originalName, 
                        minValue, 
                        maxValue
                    );

                    if (!meshSuccess)
                    {
                        Debug.LogError($"BlendShapeLimitBreak: メッシュへの拡張シェイプキー追加に失敗しました: {extendedName}");
                        return;
                    }

                    // 新しいBlendShapeを作成
                    var extendedShape = new BlendShape
                    {
                        name = extendedName,
                        weight = 0f,
                        index = -1, // 新規作成なので-1
                        isLocked = false,
                        isExtended = true,
                        minValue = minValue,
                        maxValue = maxValue,
                        originalName = originalName
                    };

                    // 永続化マネージャーに登録
                    var extendedInfo = new ExtendedShapeKeyInfo(originalName, minValue, maxValue);
                    ExtendedShapeKeyManager.RegisterExtendedShapeKey(extendedName, extendedInfo);

                    // 元のシェイプキーと同じグループに追加
                    if (!tool.groupedShapes.ContainsKey(targetGroup))
                    {
                        tool.groupedShapes[targetGroup] = new List<BlendShape>();
                        tool.groupFoldouts[targetGroup] = true;
                        tool.groupTestSliders[targetGroup] = 0f;
                    }

                    // 元のシェイプキーの直後に挿入
                    int insertIndex = originalGroupIndex + 1;
                    tool.groupedShapes[targetGroup].Insert(insertIndex, extendedShape);

                    // メッシュの更新を反映するため、シェイプキーリストを再読み込み
                    tool.RefreshBlendShapes();

                    // TreeViewを更新
                    Reload();
                    TreeViewPart.Repaint();

                    if (alreadyExists)
                    {
                        Debug.Log($"BlendShapeLimitBreak: 拡張シェイプキー '{extendedName}' を上書きしました (範囲: {minValue}〜{maxValue})");
                    }
                    else
                    {
                        Debug.Log($"BlendShapeLimitBreak: 拡張シェイプキー '{extendedName}' を作成しました (範囲: {minValue}〜{maxValue})");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"BlendShapeLimitBreak: エラーが発生しました: {e.Message}");
                }
            }



            /// <summary>
            /// シェイプキーをRootグループとして設定
            /// </summary>
            private void SetAsRootGroup(string shapeKeyName)
            {
                // シェイプキー名から拡張パラメータ部分を除去して元の名前を取得
                string originalName = shapeKeyName;
                var extendedInfo = ExtendedShapeKeyInfo.TryParseFromName(shapeKeyName, out var info);
                if (extendedInfo)
                {
                    originalName = info.originalName;
                }

                // 新しいグループ名を生成（シェイプキー名をベースに）
                string newGroupName = originalName;
                int counter = 1;

                // 重複しないグループ名を生成
                while (tool.groupedShapes.ContainsKey(newGroupName))
                {
                    newGroupName = $"{originalName} ({counter})";
                    counter++;
                }

                // シェイプキーを現在のグループから削除
                BlendShape targetShape = null;
                string sourceGroup = null;
                foreach (var group in tool.groupedShapes)
                {
                    targetShape = group.Value.FirstOrDefault(s => s.name == shapeKeyName);
                    if (targetShape != null)
                    {
                        sourceGroup = group.Key;
                        group.Value.Remove(targetShape);
                        break;
                    }
                }

                if (targetShape != null)
                {
                    // 新しいグループを作成
                    tool.groupedShapes[newGroupName] = new List<BlendShape>();
                    tool.groupFoldouts[newGroupName] = true; // デフォルトで開く
                    tool.groupTestSliders[newGroupName] = 0f; // テストスライダーの初期化

                    // シェイプキーを新しいグループに追加
                    tool.groupedShapes[newGroupName].Add(targetShape);

                    // 元のグループが空になった場合は削除（「その他」グループは除く）
                    if (sourceGroup != null && sourceGroup != "その他" && tool.groupedShapes[sourceGroup].Count == 0)
                    {
                        tool.groupedShapes.Remove(sourceGroup);
                        tool.groupFoldouts.Remove(sourceGroup);
                        tool.groupTestSliders.Remove(sourceGroup);
                    }

                    // TreeViewを更新
                    Reload();
                }
            }

            /// <summary>
            /// 複数のシェイプキーをグループ化してRootグループとして設定
            /// </summary>
            private void GroupAsRootGroup(IList<int> selectedItemIds)
            {
                // 選択されたシェイプキーを収集
                var selectedShapes = new List<BlendShape>();
                var sourceGroups = new HashSet<string>();

                foreach (int itemId in selectedItemIds)
                {
                    if (itemIdToShapeKey.TryGetValue(itemId, out var blendShape))
                    {
                        selectedShapes.Add(blendShape);
                        
                        // 元のグループを記録
                        foreach (var group in tool.groupedShapes)
                        {
                            if (group.Value.Contains(blendShape))
                            {
                                sourceGroups.Add(group.Key);
                                break;
                            }
                        }
                    }
                }

                if (selectedShapes.Count == 0) return;

                // 新しいグループ名を生成
                string newGroupName = "新しいグループ";
                int counter = 1;

                // 重複しないグループ名を生成
                while (tool.groupedShapes.ContainsKey(newGroupName))
                {
                    newGroupName = $"新しいグループ ({counter})";
                    counter++;
                }

                // 選択されたシェイプキーを元のグループから削除
                foreach (var shape in selectedShapes)
                {
                    foreach (var group in tool.groupedShapes)
                    {
                        if (group.Value.Contains(shape))
                        {
                            group.Value.Remove(shape);
                            break;
                        }
                    }
                }

                // 新しいグループを作成
                tool.groupedShapes[newGroupName] = new List<BlendShape>();
                tool.groupFoldouts[newGroupName] = true; // デフォルトで開く
                tool.groupTestSliders[newGroupName] = 0f; // テストスライダーの初期化

                // 選択されたシェイプキーを新しいグループに追加
                tool.groupedShapes[newGroupName].AddRange(selectedShapes);

                // 元のグループが空になった場合は削除（「その他」グループは除く）
                foreach (var sourceGroup in sourceGroups)
                {
                    if (sourceGroup != "その他" && tool.groupedShapes[sourceGroup].Count == 0)
                    {
                        tool.groupedShapes.Remove(sourceGroup);
                        tool.groupFoldouts.Remove(sourceGroup);
                        tool.groupTestSliders.Remove(sourceGroup);
                    }
                }

                // TreeViewを更新
                Reload();
            }

            /// <summary>
            /// シェイプキーを削除する
            /// </summary>
            private void DeleteShapeKey(string shapeKeyName)
            {
                if (string.IsNullOrEmpty(shapeKeyName))
                    return;

                // 確認ダイアログを表示
                bool confirmed = EditorUtility.DisplayDialog(
                    "シェイプキー削除の確認",
                    $"シェイプキー「{shapeKeyName}」を削除しますか？\n\nこの操作は元に戻せません。",
                    "削除",
                    "キャンセル"
                );

                if (!confirmed)
                    return;

                try
                {
                    // 実際のメッシュからシェイプキーを削除
                    if (tool.selectedRenderer != null)
                    {
                        BlendShapeLimitBreak.RemoveBlendShapeFromMesh(tool.selectedRenderer, shapeKeyName);
                    }

                    // 拡張シェイプキーの場合は永続化マネージャーからも削除
                    if (ExtendedShapeKeyInfo.TryParseFromName(shapeKeyName, out var extendedInfo))
                    {
                        ExtendedShapeKeyManager.RemoveExtendedShapeKey(shapeKeyName);
                    }

                    // すべてのグループから該当するシェイプキーを削除
                    var groupsToRemoveFrom = new List<string>();
                    foreach (var group in tool.groupedShapes)
                    {
                        var shapesToRemove = new List<BlendShape>();
                        foreach (var shape in group.Value)
                        {
                            if (shape.name == shapeKeyName)
                            {
                                shapesToRemove.Add(shape);
                            }
                        }

                        // 該当するシェイプキーを削除
                        foreach (var shape in shapesToRemove)
                        {
                            group.Value.Remove(shape);
                        }

                        // グループが空になった場合は削除対象に追加
                        if (group.Value.Count == 0 && group.Key != "その他")
                        {
                            groupsToRemoveFrom.Add(group.Key);
                        }
                    }

                    // 空のグループを削除
                    foreach (var groupName in groupsToRemoveFrom)
                    {
                        tool.groupedShapes.Remove(groupName);
                        tool.groupFoldouts.Remove(groupName);
                        tool.groupTestSliders.Remove(groupName);
                    }

                    // ロック状態からも削除
                    var lockedKeysToRemove = new List<int>();
                    foreach (var locked in tool.lockedShapeKeys)
                    {
                        // 該当するシェイプキーのインデックスを特定して削除
                        foreach (var group in tool.groupedShapes)
                        {
                            foreach (var shape in group.Value)
                            {
                                if (shape.name == shapeKeyName && shape.index == locked.Key)
                                {
                                    lockedKeysToRemove.Add(locked.Key);
                                    break;
                                }
                            }
                        }
                    }

                    foreach (var key in lockedKeysToRemove)
                    {
                        tool.lockedShapeKeys.Remove(key);
                    }

                    // メッシュの更新を反映するため、シェイプキーリストを再読み込み
                    tool.RefreshBlendShapes();

                    // TreeViewを更新
                    Reload();

                    // メインウィンドウも更新
                    TreeViewPart.Repaint();

                    EditorUtility.DisplayDialog(
                        "削除完了",
                        $"シェイプキー「{shapeKeyName}」を削除しました。",
                        "OK"
                    );
                }
                catch (System.Exception ex)
                {
                    EditorUtility.DisplayDialog(
                        "エラー",
                        $"シェイプキーの削除に失敗しました:\n{ex.Message}",
                        "OK"
                    );
                }
            }
        }
    }
} 