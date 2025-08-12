using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace ShapeKeyTools
{
    /// <summary>
    /// メッシュ/レンダラー操作の集約。副作用入口を統一。
    /// </summary>
    internal static class ShapeKeyCommandService
    {
        public static void ToggleLockWithUndo(ShapeKeyToolWindow window, BlendShape shape, bool isLocked)
        {
            if (shape == null || window == null) return;
            Undo.RecordObject(window.selectedRenderer, isLocked ? "Lock Shape" : "Unlock Shape");
            shape.isLocked = isLocked;
            if (shape.index >= 0)
            {
                window.lockedShapeKeys[shape.index] = isLocked;
            }
            Utility.MarkRendererDirty(window.selectedRenderer);
        }

        public static void SetBlendShapeWeightWithUndo(ShapeKeyToolWindow window, BlendShape shape, float weight)
        {
            if (shape == null || window?.selectedRenderer == null) return;
            if (shape.isLocked) return;

            // Undo: レンダラーの変更を記録
            Undo.RecordObject(window.selectedRenderer, "Set BlendShape Weight");

            // 値を更新
            shape.weight = weight;

            // 実メッシュに適用（index < 0 は擬似）
            if (shape.index >= 0)
            {
                window.selectedRenderer.SetBlendShapeWeight(shape.index, weight);
                Utility.MarkRendererDirty(window.selectedRenderer);
            }
        }

        public static void SetBlendShapeWeightImmediate(ShapeKeyToolWindow window, BlendShape shape, float weight)
        {
            if (shape == null || window?.selectedRenderer == null) return;
            if (shape.isLocked) return;
            shape.weight = weight;
            if (shape.index >= 0)
            {
                window.selectedRenderer.SetBlendShapeWeight(shape.index, weight);
                Utility.MarkRendererDirty(window.selectedRenderer);
            }
        }

        public static void SetMultipleLockStatesWithUndo(ShapeKeyToolWindow window, IEnumerable<BlendShape> shapes, bool isLocked)
        {
            if (window?.selectedRenderer == null) return;
            Undo.RecordObject(window.selectedRenderer, isLocked ? "Lock All Shapes" : "Unlock All Shapes");

            foreach (var s in shapes)
            {
                s.isLocked = isLocked;
                if (s.index >= 0)
                {
                    window.lockedShapeKeys[s.index] = isLocked;
                }
            }
            Utility.MarkRendererDirty(window.selectedRenderer);
        }
    }

    /// <summary>
    /// Maxプレビューホバーなど、短期的な一時適用（Undoなし）を担当。
    /// </summary>
    internal static class PreviewService
    {
        public static void BeginMaxHover(ShapeKeyToolWindow window, BlendShape blendShape, int targetIndex)
        {
            if (window == null || window.selectedRenderer == null) return;
            if (blendShape.isLocked || targetIndex < 0) return;

            if (!window.originalWeightsForMaxPreview.ContainsKey(targetIndex))
            {
                window.originalWeightsForMaxPreview[targetIndex] = blendShape.weight;
            }
            window.selectedRenderer.SetBlendShapeWeight(targetIndex, 100f);
            Utility.MarkRendererDirty(window.selectedRenderer);
            window.RequestSceneRepaint();
        }

        public static void EndMaxHover(ShapeKeyToolWindow window, BlendShape blendShape, int targetIndex)
        {
            if (window == null || window.selectedRenderer == null) return;
            if (targetIndex < 0) return;

            if (window.originalWeightsForMaxPreview.TryGetValue(targetIndex, out var originalWeight))
            {
                window.selectedRenderer.SetBlendShapeWeight(targetIndex, originalWeight);
                Utility.MarkRendererDirty(window.selectedRenderer);
                window.originalWeightsForMaxPreview.Remove(targetIndex);
                window.RequestSceneRepaint();
            }
        }
    }

    /// <summary>
    /// Repaint/Reloadのスロットリング。
    /// EditorApplication.updateで一定時間に1回まで集約適用。
    /// </summary>
    internal class UIUpdateDispatcher
    {
        private readonly ShapeKeyToolWindow window;
        private readonly float minIntervalSec;
        private double lastApplied;
        private bool needReload;
        private bool needRepaint;
        private bool needSceneRepaint;

        public UIUpdateDispatcher(ShapeKeyToolWindow w, float minIntervalSec = 0.05f)
        {
            window = w;
            this.minIntervalSec = minIntervalSec;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        public void RequestReload()
        {
            needReload = true;
        }

        public void RequestRepaint()
        {
            needRepaint = true;
        }

        public void RequestSceneRepaint()
        {
            needSceneRepaint = true;
        }

        private void OnUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - lastApplied < minIntervalSec) return;

            lastApplied = now;
            if (needReload)
            {
                TreeViewPart.Reload();
                needReload = false;
            }
            if (needRepaint)
            {
                window.Repaint();
                needRepaint = false;
            }
            if (needSceneRepaint)
            {
                SceneView.RepaintAll();
                needSceneRepaint = false;
            }
        }
    }
}


