using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ShapeKeyTools
{
    public partial class ShapeKeyToolWindow
    {
        internal void GroupShapes()
        {
            var result = GroupingEngine.ComputeGroups(blendShapes, viewModel.GroupedShapes, new ByHeaderPatternStrategy());
            
            // ユーザーが変更したグループ名を適用
            ApplyUserRenamedGroups(result);
            
            viewModel.GroupedShapes = result;
        }
        
        /// <summary>
        /// ユーザーが変更したグループ名を適用
        /// </summary>
        private void ApplyUserRenamedGroups(Dictionary<string, List<BlendShape>> groupedShapes)
        {
            if (viewModel.UserRenamedGroups == null || viewModel.UserRenamedGroups.Count == 0)
                return;
                
            // ユーザーが変更したグループ名を適用
            foreach (var renameInfo in viewModel.UserRenamedGroups)
            {
                string originalGroupName = renameInfo.Key;
                string newGroupName = renameInfo.Value;
                
                // 元のグループ名が存在し、新しいグループ名が存在しない場合
                if (groupedShapes.ContainsKey(originalGroupName) && !groupedShapes.ContainsKey(newGroupName))
                {
                    // グループ名を変更
                    var shapes = groupedShapes[originalGroupName];
                    groupedShapes.Remove(originalGroupName);
                    groupedShapes[newGroupName] = shapes;
                    
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

        internal void GroupShapesPreserveLoadedGroups(Dictionary<string, List<BlendShape>> loadedGroups)
        {
            var result = GroupingEngine.ComputeGroups(blendShapes, loadedGroups, new PreserveOrderStrategy());
            viewModel.GroupedShapes = result;
        }

        // 旧内部実装は純粋関数へ移譲

        private bool IsGroupHeader(string shapeName)
        {
            foreach (string pattern in HeaderPatterns)
            {
                if (!string.IsNullOrEmpty(shapeName) && shapeName.StartsWith(pattern))
                    return true;
            }
            return false;
        }

        private string ExtractGroupName(string headerName)
        {
            if (string.IsNullOrEmpty(headerName)) return "その他";
            foreach (string pattern in HeaderPatterns)
            {
                if (headerName.StartsWith(pattern))
                {
                    string groupName = headerName.Substring(pattern.Length);
                    
                    // 末尾の記号を除去
                    while (groupName.Length > 0 && 
                           (char.IsPunctuation(groupName[groupName.Length - 1]) || 
                            char.IsSymbol(groupName[groupName.Length - 1])))
                    {
                        groupName = groupName.Substring(0, groupName.Length - 1);
                    }
                    return groupName.Trim();
                }
            }
            return "その他";
        }

        private string GetGroupName(string shapeName)
        {
            if (IsGroupHeader(shapeName))
            {
                string groupName = ExtractGroupName(shapeName);
                return groupName;
            }
            return currentGroup;
        }

        private void EnsureGroupKeys(string groupName)
        {
            if (!viewModel.GroupedShapes.ContainsKey(groupName))
                viewModel.GroupedShapes[groupName] = new List<BlendShape>();
            if (!viewModel.GroupFoldouts.ContainsKey(groupName))
                viewModel.GroupFoldouts[groupName] = true;
            if (!viewModel.GroupTestSliders.ContainsKey(groupName))
                viewModel.GroupTestSliders[groupName] = 0f;
        }
    }

    /// <summary>
    /// グループ化関連のユーティリティ
    /// </summary>
    internal static class Grouping
    {
        internal static void ApplyTestSliderToGroup(ShapeKeyToolWindow window, string groupName, int newIndex, bool skipNonZero)
        {
            if (!window.viewModel.GroupedShapes.TryGetValue(groupName, out var shapes) || shapes == null) return;

			using (CompositeUndo.BulkMeshChange(window, "Apply Group Test Slider"))
			{
            // 初回は元値キャッシュ（newIndex > 0 のときに限る）
            if (newIndex > 0 && !window.viewModel.OriginalWeights.ContainsKey(groupName))
            {
                // 重複するindexがある場合の処理
                var weightDict = new Dictionary<int, float>();
                foreach (var shape in shapes)
                {
                    if (!weightDict.ContainsKey(shape.index))
                    {
                        weightDict[shape.index] = shape.weight;
                    }
                }
                window.viewModel.OriginalWeights[groupName] = weightDict;
            }

            // 現在の選択シェイプ名を記録（newIndex > 0 のとき）
            if (newIndex > 0)
            {
                if (newIndex - 1 >= 0 && newIndex - 1 < shapes.Count)
                {
                    window.viewModel.LastTestSelectedShapeName[groupName] = shapes[newIndex - 1].name;
                }
            }

            for (int i = 0; i < shapes.Count; i++)
            {
                float targetWeight = 0f;

                if (newIndex == 0)
                {
                    // 0では何もしない。直前の選択シェイプが100のままなら、ユーザー未操作とみなし0へ戻す
                    targetWeight = shapes[i].weight; // デフォルトは現状維持
                    if (window.viewModel.LastTestSelectedShapeName.TryGetValue(groupName, out var lastName)
                        && shapes[i].name == lastName)
                    {
                        // ユーザーが触っていない（開始時からの差分がない）場合のみ0へ戻す
                        if (window.viewModel.OriginalWeights.TryGetValue(groupName, out var startDict)
                            && startDict.TryGetValue(shapes[i].index, out var startW)
                            && Mathf.Abs(shapes[i].weight - 100f) < 0.01f
                            && Mathf.Abs(startW - 100f) > 0.01f)
                        {
                            targetWeight = 0f;
                        }
                    }
                }
                else if (newIndex > 0 && i == newIndex - 1)
                {
                    // 選択対象でも水色フラグ（開始時非ゼロ or ユーザー編集）がある場合は100にしない
                    Dictionary<int, float> startDict;
                    float startWVal = 0f;
                    bool hasStart = window.viewModel.OriginalWeights.TryGetValue(groupName, out startDict)
                                             && startDict.TryGetValue(shapes[i].index, out startWVal);
                    float startWeight = hasStart ? startWVal : shapes[i].weight;
                    bool wasNonZeroAtStart = hasStart && startWeight > 0.01f;

                    bool markedUserEdited = window.viewModel.UserEditedDuringTest.ContainsKey(groupName)
                        && window.viewModel.UserEditedDuringTest[groupName].Contains(shapes[i].name);

                    if ((skipNonZero && wasNonZeroAtStart) || markedUserEdited)
                    {
                        goto ContinueLoopGroup;
                    }

                    targetWeight = 100f;
                }
                else if (newIndex > 0)
				{
					// テスト中: 非選択項目の扱い
					Dictionary<int, float> startDict;
					float startWVal = 0f;
					bool hasStart = window.viewModel.OriginalWeights.TryGetValue(groupName, out startDict)
											 && startDict.TryGetValue(shapes[i].index, out startWVal);
                    float startWeight = hasStart ? startWVal : shapes[i].weight;
                    bool wasNonZeroAtStart = hasStart && startWeight > 0.01f;

                    // GUI側でユーザーが操作したフラグを尊重
                    bool markedUserEdited = window.viewModel.UserEditedDuringTest.ContainsKey(groupName)
                        && window.viewModel.UserEditedDuringTest[groupName].Contains(shapes[i].name);

					// skipNonZero: 開始時非ゼロは触らない
                    if (skipNonZero && wasNonZeroAtStart)
					{
						// 触らない
						goto ContinueLoopGroup;
					}
                    // ユーザーがテスト開始後に変更したものは上書きしない
                    if (markedUserEdited)
					{
						goto ContinueLoopGroup;
					}
					// それ以外は0へ（通過後0に戻す仕様）
					targetWeight = 0f;
				}

            // 既存のスキップ条件は上の分岐で処理済み

                BlendShapeCommandService.SetWeight(window, shapes[i], targetWeight);

            ContinueLoopGroup:;
            }

            Utility.MarkRendererDirty(window.selectedRenderer);

            // スライダー0へ戻したら元値キャッシュとユーザー編集フラグを解放
            if (newIndex == 0)
            {
                if (window.viewModel.OriginalWeights.ContainsKey(groupName))
                    window.viewModel.OriginalWeights.Remove(groupName);
                if (window.viewModel.UserEditedDuringTest.ContainsKey(groupName))
                    window.viewModel.UserEditedDuringTest.Remove(groupName);
            }
			}
        }

        internal static void ApplyTestSliderToVisibleShapes(ShapeKeyToolWindow window, string groupName, List<BlendShape> visibleShapes, int newIndex, bool skipNonZero)
        {
            if (visibleShapes == null || visibleShapes.Count == 0) return;

			using (CompositeUndo.BulkMeshChange(window, "Apply Visible Test Slider"))
			{
			// 初回は元値キャッシュ（newIndex > 0 のときに限る）
            if (newIndex > 0 && !window.viewModel.OriginalWeights.ContainsKey(groupName))
            {
                // 重複するindexがある場合の処理
                var weightDict = new Dictionary<int, float>();
                foreach (var shape in visibleShapes)
                {
                    if (!weightDict.ContainsKey(shape.index))
                    {
                        weightDict[shape.index] = shape.weight;
                    }
                }
                window.viewModel.OriginalWeights[groupName] = weightDict;
            }

			for (int i = 0; i < visibleShapes.Count; i++)
            {
                float targetWeight = 0f;

                if (newIndex == 0)
                {
                    // 0では何もしない。直前の選択シェイプが100のままなら、ユーザー未操作とみなし0へ戻す
                    targetWeight = visibleShapes[i].weight; // デフォルトは現状維持
                    if (window.viewModel.LastTestSelectedShapeName.TryGetValue(groupName, out var lastName)
                        && visibleShapes[i].name == lastName)
                    {
                        if (window.viewModel.OriginalWeights.TryGetValue(groupName, out var startDict)
                            && startDict.TryGetValue(visibleShapes[i].index, out var startW)
                            && Mathf.Abs(visibleShapes[i].weight - 100f) < 0.01f
                            && Mathf.Abs(startW - 100f) > 0.01f)
                        {
                            targetWeight = 0f;
                        }
                    }
                }
                else if (newIndex > 0 && i == newIndex - 1)
                {
                    // 選択対象でも水色フラグ（開始時非ゼロ or ユーザー編集）がある場合は100にしない
                    Dictionary<int, float> startDict;
                    float startWVal = 0f;
                    bool hasStart = window.viewModel.OriginalWeights.TryGetValue(groupName, out startDict)
                                             && startDict.TryGetValue(visibleShapes[i].index, out startWVal);
                    float startWeight = hasStart ? startWVal : visibleShapes[i].weight;
                    bool wasNonZeroAtStart = hasStart && startWeight > 0.01f;

                    bool markedUserEdited = window.viewModel.UserEditedDuringTest.ContainsKey(groupName)
                        && window.viewModel.UserEditedDuringTest[groupName].Contains(visibleShapes[i].name);

                    if ((skipNonZero && wasNonZeroAtStart) || markedUserEdited)
                    {
                        goto ContinueLoopVisible;
                    }

                    targetWeight = 100f;
                }
                else if (newIndex > 0)
				{
					// テスト中: 非選択項目の扱い
					Dictionary<int, float> startDict;
					float startWVal = 0f;
					bool hasStart = window.viewModel.OriginalWeights.TryGetValue(groupName, out startDict)
											 && startDict.TryGetValue(visibleShapes[i].index, out startWVal);
                    float startWeight = hasStart ? startWVal : visibleShapes[i].weight;
                    bool wasNonZeroAtStart = hasStart && startWeight > 0.01f;

                    bool markedUserEdited = window.viewModel.UserEditedDuringTest.ContainsKey(groupName)
                        && window.viewModel.UserEditedDuringTest[groupName].Contains(visibleShapes[i].name);

					if (skipNonZero && wasNonZeroAtStart)
					{
						// 触らない
						goto ContinueLoopVisible;
					}
                    if (markedUserEdited)
					{
						goto ContinueLoopVisible;
					}
					targetWeight = 0f;
				}

                BlendShapeCommandService.SetWeight(window, visibleShapes[i], targetWeight);

            ContinueLoopVisible:;
            }

            Utility.MarkRendererDirty(window.selectedRenderer);

            // スライダー0へ戻したら元値キャッシュとユーザー編集フラグを解放
            if (newIndex == 0)
            {
                if (window.viewModel.OriginalWeights.ContainsKey(groupName))
                    window.viewModel.OriginalWeights.Remove(groupName);
                if (window.viewModel.UserEditedDuringTest.ContainsKey(groupName))
                    window.viewModel.UserEditedDuringTest.Remove(groupName);
            }
			}
        }
    }
} 