using UnityEditor;
using UnityEngine;
using System.Linq; // Added for FirstOrDefault

namespace ShapeKeyTools
{
    /// <summary>
    /// 汎用ユーティリティ
    /// </summary>
    internal static class Utility
    {
        internal static void MarkDirty(UnityEngine.Object o)
        {
            if (o != null) EditorUtility.SetDirty(o);
        }

        internal static void MarkRendererDirty(SkinnedMeshRenderer renderer)
        {
            if (renderer != null)
                EditorUtility.SetDirty(renderer);
        }

        internal static void SetWeight(ShapeKeyToolWindow window, BlendShape s, float w)
        {
            // index < 0 は擬似シェイプキー。ロック中は操作しない
            if (s == null) return;
            if (s.isLocked) return;
            if (window.selectedRenderer == null) return;
            
            s.weight = w;
            
            // 拡張シェイプキーの場合は元のシェイプキーのインデックスを使用
            int targetIndex = s.index;
            if (s.isExtended && !string.IsNullOrEmpty(s.originalName))
            {
                // 元のシェイプキーを探す
                var originalShape = window.blendShapes.FirstOrDefault(shape => shape.name == s.originalName);
                if (originalShape != null)
                {
                    targetIndex = originalShape.index;
                }
            }
            
            // インデックスが有効な場合のみSetBlendShapeWeightを呼び出す
            if (targetIndex >= 0)
            {
                // メッシュのblendShapeCountをチェック
                var mesh = window.selectedRenderer.sharedMesh;
                if (mesh != null && targetIndex < mesh.blendShapeCount)
                {
                    window.selectedRenderer.SetBlendShapeWeight(targetIndex, w);
                    
                    // 自動保存を無効化（シェイプキーの値は保存しない）
                    // ShapeKeyPersistenceManager.AutoSave(window);
                }
                else
                {
                    Debug.LogWarning($"シェイプキー '{s.name}' のインデックス {targetIndex} が無効です。メッシュのblendShapeCount: {mesh?.blendShapeCount ?? 0}");
                }
            }
            else
            {
                Debug.LogWarning($"シェイプキー '{s.name}' のインデックスが無効です: {targetIndex}");
            }
        }
    }
} 