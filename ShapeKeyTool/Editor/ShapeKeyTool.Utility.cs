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

        // 重い副作用のゲートは BlendShapeCommandService に一本化したため、旧APIは呼び出し側から置換済み
    }
} 