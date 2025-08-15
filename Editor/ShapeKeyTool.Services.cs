using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ShapeKeyTools
{
    internal static class ApplyScheduler
    {
        private static double lastApplyTime;
        private static bool pendingRepaint;
        private static bool pendingReload;
        private const double MinInterval = 0.04; // 40ms程度でスロットリング

        internal static void RequestRepaint()
        {
            pendingRepaint = true;
            TryFlush();
        }

        internal static void RequestReload()
        {
            pendingReload = true;
            TryFlush();
        }

		private static void TryFlush()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - lastApplyTime < MinInterval)
            {
                EditorApplication.delayCall += Flush;
                return;
            }
            Flush();
        }

		private static void Flush()
        {
            lastApplyTime = EditorApplication.timeSinceStartup;
            if (pendingReload)
            {
                TreeViewPart.Reload();
                pendingReload = false;
            }
            if (pendingRepaint)
            {
				SceneView.RepaintAll();
				// エディタウィンドウ自体も再描画して右ペインの変更を反映
				TreeViewPart.Repaint();
                pendingRepaint = false;
            }
        }
    }

    /// <summary>
    /// シェイプキー適用の副作用を一元化するコマンドサービス
    /// </summary>
    internal static class BlendShapeCommandService
    {
        internal static void SetWeight(
            ShapeKeyToolWindow window,
            BlendShape blendShape,
            float newWeight
        )
        {
            if (window == null || blendShape == null)
                return;
            if (blendShape.isLocked)
                return;

            // 変更がない場合は何もしない（Undoも記録しない）
            if (Mathf.Abs(blendShape.weight - newWeight) < 0.0001f)
                return;

            CompositeUndo.RecordMeshChange(window, "Set BlendShape Weight");

            blendShape.weight = newWeight;

            // レンダラー反映
            if (window.selectedRenderer != null)
            {
                if (
                    blendShape.isExtended
                    || ExtendedShapeKeyInfo.TryParseFromName(blendShape.name, out _)
                )
                {
                    // 既存ロジックに委譲（正規化等を内部で処理）
                    window.ApplyExtendedShapeKeyWeight(blendShape);
                }
                else if (blendShape.index >= 0)
                {
                    window.selectedRenderer.SetBlendShapeWeight(blendShape.index, newWeight);
                }
                Utility.MarkRendererDirty(window.selectedRenderer);
            }

            // UI反映（コアレス）
            ApplyScheduler.RequestReload();
            ApplyScheduler.RequestRepaint();
        }
    }

    /// <summary>
    /// TreeView 操作系のコマンドを集約
    /// </summary>
    internal static class TreeViewCommandService
    {
        public static void AddNewGroup(ShapeKeyToolWindow tool)
        {
            CompositeUndo.RecordWindow(tool, "Add Group");
            string baseName = "新しいグループ";
            string newGroupName = baseName;
            int counter = 1;
            while (tool.viewModel.GroupedShapes.ContainsKey(newGroupName))
            {
                newGroupName = $"{baseName} ({counter})";
                counter++;
            }
            tool.viewModel.GroupedShapes[newGroupName] = new List<BlendShape>();
            tool.viewModel.GroupFoldouts[newGroupName] = true;
            tool.viewModel.GroupTestSliders[newGroupName] = 0f;
            ApplyScheduler.RequestReload();
            ApplyScheduler.RequestRepaint();
        }

        public static void DeleteGroup(ShapeKeyToolWindow tool, string groupName)
        {
            CompositeUndo.RecordWindow(tool, "Delete Group");
            bool shouldDelete = DialogService.Confirm(
                "グループ削除の確認",
                $"グループ '{groupName}' を削除しますか？\n\nこのグループ内のシェイプキーは「その他」グループに移動されます。",
                UIStrings.DialogDelete,
                UIStrings.DialogCancel
            );
            if (!shouldDelete)
                return;

            if (tool.viewModel.GroupedShapes.ContainsKey(groupName))
            {
                var shapesToMove = tool.viewModel.GroupedShapes[groupName];
                if (!tool.viewModel.GroupedShapes.ContainsKey("その他"))
                {
                    tool.viewModel.GroupedShapes["その他"] = new List<BlendShape>();
                    tool.viewModel.GroupFoldouts["その他"] = true;
                    tool.viewModel.GroupTestSliders["その他"] = 0f;
                }
                tool.viewModel.GroupedShapes["その他"].AddRange(shapesToMove);
                tool.viewModel.GroupedShapes.Remove(groupName);
                tool.viewModel.GroupFoldouts.Remove(groupName);
                tool.viewModel.GroupTestSliders.Remove(groupName);
            }
            ApplyScheduler.RequestReload();
            ApplyScheduler.RequestRepaint();
        }

        public static void CreateExtendedShapeKeyWithDialog(
            ShapeKeyToolWindow tool,
            string originalShapeKeyName
        )
        {
            CompositeUndo.RecordMeshChange(tool, "Create Extended ShapeKey");
            ShapeKeyExtensionWindow.ShowWindow(
                originalShapeKeyName,
                (originalName, minValue, maxValue) =>
                {
                    CreateExtendedShapeKeyInternal(tool, originalName, minValue, maxValue);
                }
            );
        }

        public static void CreateExtendedShapeKeyInternal(
            ShapeKeyToolWindow tool,
            string originalName,
            int minValue,
            int maxValue
        )
        {
            CompositeUndo.RecordMeshChange(tool, "Create Extended ShapeKey");
            if (tool.selectedRenderer == null)
            {
                Debug.LogError("BlendShapeLimitBreak: 選択されたレンダラーがありません");
                return;
            }
            try
            {
                string targetGroup = "その他";
                int originalIndex = -1;
                int originalGroupIndex = -1;
                foreach (var group in tool.viewModel.GroupedShapes)
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
                    if (originalIndex != -1)
                        break;
                }
                if (originalIndex == -1)
                {
                    Debug.LogError(
                        $"BlendShapeLimitBreak: 元のシェイプキー '{originalName}' が見つかりません"
                    );
                    return;
                }
                string extendedName = $"{originalName}_min:{minValue}_max:{maxValue}";
                bool alreadyExists = false;
                foreach (var group in tool.viewModel.GroupedShapes)
                {
                    if (group.Value.Any(s => s.name == extendedName))
                    {
                        alreadyExists = true;
                        break;
                    }
                }
                if (alreadyExists)
                {
                    bool shouldReplace = DialogService.Confirm(
                        "拡張シェイプキーの上書き確認",
                        $"拡張シェイプキー '{extendedName}' は既に存在します。\n\n上書きしますか？",
                        "上書き",
                        UIStrings.DialogCancel
                    );
                    if (!shouldReplace)
                        return;
                    BlendShapeLimitBreak.RemoveBlendShapeFromMesh(
                        tool.selectedRenderer,
                        extendedName
                    );
                    foreach (var group in tool.viewModel.GroupedShapes)
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
                bool meshSuccess = BlendShapeLimitBreak.ApplyExtendedShapeKeyToMesh(
                    tool.selectedRenderer,
                    extendedName,
                    originalName,
                    minValue,
                    maxValue
                );
                if (!meshSuccess)
                {
                    Debug.LogError(
                        $"BlendShapeLimitBreak: メッシュへの拡張シェイプキー追加に失敗しました: {extendedName}"
                    );
                    return;
                }
                var extendedShape = new BlendShape
                {
                    name = extendedName,
                    weight = 0f,
                    index = -1,
                    isLocked = false,
                    isExtended = true,
                    minValue = minValue,
                    maxValue = maxValue,
                    originalName = originalName,
                };
                var extendedInfo = new ExtendedShapeKeyInfo(originalName, minValue, maxValue);
                ExtendedShapeKeyManager.RegisterExtendedShapeKey(extendedName, extendedInfo);
                if (!tool.viewModel.GroupedShapes.ContainsKey(targetGroup))
                {
                    tool.viewModel.GroupedShapes[targetGroup] = new List<BlendShape>();
                    tool.viewModel.GroupFoldouts[targetGroup] = true;
                    tool.viewModel.GroupTestSliders[targetGroup] = 0f;
                }
                int insertIndex = originalGroupIndex + 1;
                tool.viewModel.GroupedShapes[targetGroup].Insert(insertIndex, extendedShape);
                tool.RefreshBlendShapes();
                ApplyScheduler.RequestReload();
                ApplyScheduler.RequestRepaint();
                if (ShapeKeyToolSettings.DebugVerbose)
                {
                    if (alreadyExists)
                        Debug.Log(
                            $"BlendShapeLimitBreak: 拡張シェイプキー '{extendedName}' を上書きしました (範囲: {minValue}〜{maxValue})"
                        );
                    else
                        Debug.Log(
                            $"BlendShapeLimitBreak: 拡張シェイプキー '{extendedName}' を作成しました (範囲: {minValue}〜{maxValue})"
                        );
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"BlendShapeLimitBreak: エラーが発生しました: {e.Message}");
            }
        }

        public static void SetAsRootGroup(ShapeKeyToolWindow tool, string shapeKeyName)
        {
            CompositeUndo.RecordWindow(tool, "Set As Root Group");
            string originalName = shapeKeyName;
            var extendedInfo = ExtendedShapeKeyInfo.TryParseFromName(shapeKeyName, out var info);
            if (extendedInfo)
                originalName = info.originalName;
            string newGroupName = originalName;
            int counter = 1;
            while (tool.viewModel.GroupedShapes.ContainsKey(newGroupName))
            {
                newGroupName = $"{originalName} ({counter})";
                counter++;
            }
            BlendShape targetShape = null;
            string sourceGroup = null;
            foreach (var group in tool.viewModel.GroupedShapes)
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
                tool.viewModel.GroupedShapes[newGroupName] = new List<BlendShape>();
                tool.viewModel.GroupFoldouts[newGroupName] = true;
                tool.viewModel.GroupTestSliders[newGroupName] = 0f;
                tool.viewModel.GroupedShapes[newGroupName].Add(targetShape);
                if (
                    sourceGroup != null
                    && sourceGroup != "その他"
                    && tool.viewModel.GroupedShapes[sourceGroup].Count == 0
                )
                {
                    tool.viewModel.GroupedShapes.Remove(sourceGroup);
                    tool.viewModel.GroupFoldouts.Remove(sourceGroup);
                    tool.viewModel.GroupTestSliders.Remove(sourceGroup);
                }
                ApplyScheduler.RequestReload();
            }
        }

        public static void GroupAsRootGroup(
            ShapeKeyToolWindow tool,
            List<BlendShape> selectedShapes
        )
        {
            CompositeUndo.RecordWindow(tool, "Group As Root Group");
            if (selectedShapes == null || selectedShapes.Count == 0)
                return;
            var sourceGroups = new HashSet<string>();
            foreach (var shape in selectedShapes)
            {
                foreach (var group in tool.viewModel.GroupedShapes)
                {
                    if (group.Value.Contains(shape))
                    {
                        sourceGroups.Add(group.Key);
                        break;
                    }
                }
            }
            string newGroupName = "新しいグループ";
            int counter = 1;
            while (tool.viewModel.GroupedShapes.ContainsKey(newGroupName))
            {
                newGroupName = $"新しいグループ ({counter})";
                counter++;
            }
            foreach (var shape in selectedShapes)
            {
                foreach (var group in tool.viewModel.GroupedShapes)
                {
                    if (group.Value.Contains(shape))
                    {
                        group.Value.Remove(shape);
                        break;
                    }
                }
            }
            tool.viewModel.GroupedShapes[newGroupName] = new List<BlendShape>();
            tool.viewModel.GroupFoldouts[newGroupName] = true;
            tool.viewModel.GroupTestSliders[newGroupName] = 0f;
            tool.viewModel.GroupedShapes[newGroupName].AddRange(selectedShapes);
            foreach (var sg in sourceGroups)
            {
                if (sg != "その他" && tool.viewModel.GroupedShapes[sg].Count == 0)
                {
                    tool.viewModel.GroupedShapes.Remove(sg);
                    tool.viewModel.GroupFoldouts.Remove(sg);
                    tool.viewModel.GroupTestSliders.Remove(sg);
                }
            }
            ApplyScheduler.RequestReload();
        }

        public static void DeleteShapeKey(ShapeKeyToolWindow tool, string shapeKeyName)
        {
            CompositeUndo.RecordMeshChange(tool, "Delete ShapeKey");
            if (string.IsNullOrEmpty(shapeKeyName))
                return;
            bool confirmed = DialogService.Confirm(
                "シェイプキー削除の確認",
                $"シェイプキー「{shapeKeyName}」を削除しますか？\n\nこの操作は元に戻せません。",
                UIStrings.DialogDelete,
                UIStrings.DialogCancel
            );
            if (!confirmed)
                return;
            try
            {
                if (tool.selectedRenderer != null)
                {
                    BlendShapeLimitBreak.RemoveBlendShapeFromMesh(
                        tool.selectedRenderer,
                        shapeKeyName
                    );
                }
                if (ExtendedShapeKeyInfo.TryParseFromName(shapeKeyName, out _))
                {
                    ExtendedShapeKeyManager.RemoveExtendedShapeKey(shapeKeyName);
                }
                var groupsToRemoveFrom = new List<string>();
                foreach (var group in tool.viewModel.GroupedShapes)
                {
                    group.Value.RemoveAll(s => s.name == shapeKeyName);
                    if (group.Value.Count == 0 && group.Key != "その他")
                        groupsToRemoveFrom.Add(group.Key);
                }
                foreach (var name in groupsToRemoveFrom)
                {
                    tool.viewModel.GroupedShapes.Remove(name);
                    tool.viewModel.GroupFoldouts.Remove(name);
                    tool.viewModel.GroupTestSliders.Remove(name);
                }
                var lockedKeysToRemove = new List<int>();
                foreach (var locked in tool.viewModel.LockedShapeKeys)
                {
                    foreach (var group in tool.viewModel.GroupedShapes)
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
                foreach (var k in lockedKeysToRemove)
                    tool.viewModel.LockedShapeKeys.Remove(k);
                tool.RefreshBlendShapes();
                ApplyScheduler.RequestReload();
                DialogService.Notify("削除完了", $"シェイプキー「{shapeKeyName}」を削除しました。");
            }
            catch (System.Exception ex)
            {
                DialogService.Notify(UIStrings.DialogError, $"シェイプキーの削除に失敗しました:\n{ex.Message}", DialogType.Error);
            }
        }

        public static void MoveGroupToEnd(ShapeKeyToolWindow tool, string groupName)
        {
            CompositeUndo.RecordWindow(tool, "Move Group To End");
            var list = tool.viewModel.GroupedShapes.Keys.ToList();
            list.Remove(groupName);
            list.Add(groupName);
            var newGroups = new Dictionary<string, List<BlendShape>>();
            var newFoldouts = new Dictionary<string, bool>();
            var newTest = new Dictionary<string, float>();
            foreach (var g in list)
            {
                newGroups[g] = tool.viewModel.GroupedShapes[g];
                if (tool.viewModel.GroupFoldouts.ContainsKey(g))
                    newFoldouts[g] = tool.viewModel.GroupFoldouts[g];
                if (tool.viewModel.GroupTestSliders.ContainsKey(g))
                    newTest[g] = tool.viewModel.GroupTestSliders[g];
            }
            tool.viewModel.GroupedShapes = newGroups;
            tool.viewModel.GroupFoldouts = newFoldouts;
            tool.viewModel.GroupTestSliders = newTest;
            ApplyScheduler.RequestReload();
        }

        public static void ReorderGroups(
            ShapeKeyToolWindow tool,
            string sourceGroup,
            string targetGroup,
            int insertIndex
        )
        {
            CompositeUndo.RecordWindow(tool, "Reorder Groups");
            var list = tool.viewModel.GroupedShapes.Keys.ToList();
            list.Remove(sourceGroup);
            int adjusted = insertIndex;
            if (list.Count > 0 && adjusted >= list.Count)
                adjusted = list.Count;
            list.Insert(adjusted, sourceGroup);
            var newGroups = new Dictionary<string, List<BlendShape>>();
            var newFoldouts = new Dictionary<string, bool>();
            var newTest = new Dictionary<string, float>();
            foreach (var g in list)
            {
                newGroups[g] = tool.viewModel.GroupedShapes[g];
                if (tool.viewModel.GroupFoldouts.ContainsKey(g))
                    newFoldouts[g] = tool.viewModel.GroupFoldouts[g];
                if (tool.viewModel.GroupTestSliders.ContainsKey(g))
                    newTest[g] = tool.viewModel.GroupTestSliders[g];
            }
            tool.viewModel.GroupedShapes = newGroups;
            tool.viewModel.GroupFoldouts = newFoldouts;
            tool.viewModel.GroupTestSliders = newTest;
            ApplyScheduler.RequestReload();
        }

        public static void MoveShapeKeyToGroup(
            ShapeKeyToolWindow tool,
            string shapeName,
            string targetGroup
        )
        {
            CompositeUndo.RecordWindow(tool, "Move ShapeKey To Group");
            BlendShape shapeToMove = null;
            string sourceGroup = null;
            foreach (var group in tool.viewModel.GroupedShapes)
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
                tool.viewModel.GroupedShapes[sourceGroup].Remove(shapeToMove);
                if (!tool.viewModel.GroupedShapes.ContainsKey(targetGroup))
                    tool.viewModel.GroupedShapes[targetGroup] = new List<BlendShape>();
                tool.viewModel.GroupedShapes[targetGroup].Add(shapeToMove);
                ApplyScheduler.RequestReload();
            }
        }

        public static void MoveShapeKeyToEnd(ShapeKeyToolWindow tool, string shapeName)
        {
            CompositeUndo.RecordWindow(tool, "Move ShapeKey To End");
            foreach (var group in tool.viewModel.GroupedShapes)
            {
                var shapes = group.Value;
                int idx = shapes.FindIndex(s => s.name == shapeName);
                if (idx != -1)
                {
                    var s = shapes[idx];
                    shapes.RemoveAt(idx);
                    shapes.Add(s);
                    break;
                }
            }
            ApplyScheduler.RequestReload();
        }

        public static void ReorderShapeKey(
            ShapeKeyToolWindow tool,
            string sourceShape,
            string targetShape,
            int insertIndex
        )
        {
            CompositeUndo.RecordWindow(tool, "Reorder ShapeKey");
            foreach (var group in tool.viewModel.GroupedShapes)
            {
                var shapes = group.Value;
                int sourceIndex = shapes.FindIndex(s => s.name == sourceShape);
                int targetIndex = shapes.FindIndex(s => s.name == targetShape);
                if (sourceIndex != -1 && targetIndex != -1)
                {
                    var shapeToMove = shapes[sourceIndex];
                    shapes.RemoveAt(sourceIndex);
                    int idx = Mathf.Clamp(insertIndex, 0, shapes.Count);
                    shapes.Insert(idx, shapeToMove);
                    break;
                }
            }
            ApplyScheduler.RequestReload();
        }
    }

    /// <summary>
    /// 一時プレビュー（Maxホバー等）の管理
    /// </summary>
    internal static class PreviewService
    {
        private static readonly Dictionary<
            SkinnedMeshRenderer,
            Dictionary<int, float>
        > rendererToOriginalWeights = new Dictionary<SkinnedMeshRenderer, Dictionary<int, float>>();

        internal static bool IsPreviewing(SkinnedMeshRenderer renderer, int blendShapeIndex)
        {
            if (renderer == null || blendShapeIndex < 0)
                return false;
            return rendererToOriginalWeights.TryGetValue(renderer, out var dict)
                && dict.ContainsKey(blendShapeIndex);
        }

        internal static void BeginMaxHover(
            ShapeKeyToolWindow window,
            int blendShapeIndex,
            BlendShape model
        )
        {
            if (window?.selectedRenderer == null || blendShapeIndex < 0)
                return;
            if (model != null && model.isLocked)
                return;

            if (!rendererToOriginalWeights.TryGetValue(window.selectedRenderer, out var dict))
            {
                dict = new Dictionary<int, float>();
                rendererToOriginalWeights[window.selectedRenderer] = dict;
            }

            if (!dict.ContainsKey(blendShapeIndex))
            {
                float current =
                    model != null
                        ? model.weight
                        : window.selectedRenderer.GetBlendShapeWeight(blendShapeIndex);
                dict[blendShapeIndex] = current;
            }

            window.selectedRenderer.SetBlendShapeWeight(blendShapeIndex, 100f);
            Utility.MarkRendererDirty(window.selectedRenderer);
            SceneView.RepaintAll();
            window.Repaint();
        }

        internal static void EndMaxHover(
            ShapeKeyToolWindow window,
            int blendShapeIndex,
            BlendShape model
        )
        {
            if (window?.selectedRenderer == null || blendShapeIndex < 0)
                return;

            if (
                rendererToOriginalWeights.TryGetValue(window.selectedRenderer, out var dict)
                && dict.TryGetValue(blendShapeIndex, out var originalWeight)
            )
            {
                // モデルの値がホバー前と変化していなければ（＝確定操作なし）、元値に戻す
                bool modelUnchangedFromOriginal = model == null || Mathf.Abs((model.weight) - originalWeight) < 0.01f;
                if (modelUnchangedFromOriginal)
                {
                    window.selectedRenderer.SetBlendShapeWeight(blendShapeIndex, originalWeight);
                    if (model != null)
                    {
                        model.weight = originalWeight;
                    }
                    Utility.MarkRendererDirty(window.selectedRenderer);
                    SceneView.RepaintAll();
                    window.Repaint();
                }

                dict.Remove(blendShapeIndex);
                if (dict.Count == 0)
                {
                    rendererToOriginalWeights.Remove(window.selectedRenderer);
                }
            }
        }
    }
}
