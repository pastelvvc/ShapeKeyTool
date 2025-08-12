using UnityEditor;
using UnityEngine;

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
            s.weight = w;
            if (window.selectedRenderer != null && s.index >= 0)
            {
                window.selectedRenderer.SetBlendShapeWeight(s.index, w);
            }
        }
    }

    /// <summary>
    /// ツール共通ロガー。verboseはShapeKeyToolSettings.DebugVerboseで制御。
    /// </summary>
    internal static class ToolLogger
    {
        public static void Verbose(string message)
        {
            if (ShapeKeyToolSettings.DebugVerbose)
            {
                Debug.Log(message);
            }
        }

        public static void Info(string message)
        {
            Debug.Log(message);
        }

        public static void Warn(string message)
        {
            Debug.LogWarning(message);
        }

        public static void Error(string message)
        {
            Debug.LogError(message);
        }
    }
} 