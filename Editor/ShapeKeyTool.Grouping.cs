using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ShapeKeyTools
{
    public partial class ShapeKeyToolWindow
    {
        internal void GroupShapes()
        {
            // 既存の拡張シェイプキー情報とグループ情報を保存
            var existingExtendedShapes = new Dictionary<string, BlendShape>();
            var extendedShapeGroups = new Dictionary<string, string>(); // 拡張シェイプキー名 -> グループ名
            
            foreach (var group in groupedShapes)
            {
                foreach (var shape in group.Value)
                {
                    if (shape.isExtended)
                    {
                        existingExtendedShapes[shape.name] = shape;
                        extendedShapeGroups[shape.name] = group.Key;
                    }
                }
            }

            groupedShapes.Clear();
            groupFoldouts.Clear();
            currentGroup = "その他";

            // まず、通常のシェイプキーをグループ化
            foreach (var shape in blendShapes)
            {
                string groupName = GetGroupName(shape.name);

                // グループヘッダーの場合、現在のグループを更新
                if (IsGroupHeader(shape.name))
                {
                    currentGroup = groupName;
                }

                // 現在のグループに追加
                EnsureGroupKeys(currentGroup);

                // グループヘッダー自体は追加しない
                if (!IsGroupHeader(shape.name))
                {
                    // 拡張シェイプキーでない場合のみ追加
                    if (!existingExtendedShapes.ContainsKey(shape.name))
                    {
                        groupedShapes[currentGroup].Add(shape);
                    }
                }
            }

            // 次に、拡張シェイプキーを適切な位置に挿入
            foreach (var kvp in existingExtendedShapes)
            {
                var extendedShape = kvp.Value;
                var extendedShapeName = kvp.Key;
                
                if (extendedShapeGroups.TryGetValue(extendedShapeName, out var originalGroup))
                {
                    EnsureGroupKeys(originalGroup);
                    
                    // 元のシェイプキーの位置を名前で検索
                    int insertIndex = groupedShapes[originalGroup].Count; // デフォルトは最後
                    if (!string.IsNullOrEmpty(extendedShape.originalName))
                    {
                        // 元のシェイプキーを名前で検索
                        for (int i = 0; i < groupedShapes[originalGroup].Count; i++)
                        {
                            if (groupedShapes[originalGroup][i].name == extendedShape.originalName)
                            {
                                // 元のシェイプキーの直後に挿入
                                insertIndex = i + 1;
                                break;
                            }
                        }
                    }
                    
                    // 拡張シェイプキーを挿入
                    groupedShapes[originalGroup].Insert(insertIndex, extendedShape);
                }
            }
        }

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
            if (!groupedShapes.ContainsKey(groupName))
                groupedShapes[groupName] = new List<BlendShape>();
            if (!groupFoldouts.ContainsKey(groupName))
                groupFoldouts[groupName] = true;
            if (!groupTestSliders.ContainsKey(groupName))
                groupTestSliders[groupName] = 0f;
        }
    }

    /// <summary>
    /// グループ化関連のユーティリティ
    /// </summary>
    internal static class Grouping
    {
        internal static void ApplyTestSliderToGroup(ShapeKeyToolWindow window, string groupName, int newIndex, bool skipNonZero)
        {
            if (!window.groupedShapes.TryGetValue(groupName, out var shapes) || shapes == null) return;

            // 初回は元値キャッシュ
            if (!window.originalWeights.ContainsKey(groupName))
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
                window.originalWeights[groupName] = weightDict;
            }

            for (int i = 0; i < shapes.Count; i++)
            {
                float targetWeight = 0f;

                if (newIndex == 0)
                {
                    if (window.originalWeights[groupName].TryGetValue(shapes[i].index, out var orig))
                        targetWeight = orig;
                }
                else if (newIndex > 0 && i == newIndex - 1)
                {
                    targetWeight = 100f;
                }

                bool wasNonZeroAtStart = window.originalWeights[groupName].TryGetValue(shapes[i].index, out var startW)
                                         && startW > 0.01f;

                if (skipNonZero && wasNonZeroAtStart) continue;

                Utility.SetWeight(window, shapes[i], targetWeight);
            }

            Utility.MarkRendererDirty(window.selectedRenderer);

            // スライダー0へ戻したら元値キャッシュを解放
            if (newIndex == 0)
                window.originalWeights.Remove(groupName);
        }

        internal static void ApplyTestSliderToVisibleShapes(ShapeKeyToolWindow window, string groupName, List<BlendShape> visibleShapes, int newIndex, bool skipNonZero)
        {
            if (visibleShapes == null || visibleShapes.Count == 0) return;

            // 初回は元値キャッシュ
            if (!window.originalWeights.ContainsKey(groupName))
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
                window.originalWeights[groupName] = weightDict;
            }

            for (int i = 0; i < visibleShapes.Count; i++)
            {
                float targetWeight = 0f;

                if (newIndex == 0)
                {
                    if (window.originalWeights[groupName].TryGetValue(visibleShapes[i].index, out var orig))
                        targetWeight = orig;
                }
                else if (newIndex > 0 && i == newIndex - 1)
                {
                    targetWeight = 100f;
                }

                bool wasNonZeroAtStart = window.originalWeights[groupName].TryGetValue(visibleShapes[i].index, out var startW)
                                         && startW > 0.01f;

                if (skipNonZero && wasNonZeroAtStart) continue;

                Utility.SetWeight(window, visibleShapes[i], targetWeight);
            }

            Utility.MarkRendererDirty(window.selectedRenderer);

            // スライダー0へ戻したら元値キャッシュを解放
            if (newIndex == 0)
                window.originalWeights.Remove(groupName);
        }
    }
} 