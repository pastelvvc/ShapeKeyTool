using System.Collections.Generic;
using UnityEngine;

namespace ShapeKeyTools
{
    /// <summary>
    /// 高レベルなメッシュ操作のFacade。UI層からは本APIを利用し、低レベル実装には直接依存しない。
    /// </summary>
    internal static class BlendShapeOps
    {
        public static bool CreateExtended(SkinnedMeshRenderer renderer, string extendedShapeKeyName, string originalShapeKeyName, int minValue, int maxValue)
        {
            return BlendShapeLimitBreak.ApplyExtendedShapeKeyToMesh(renderer, extendedShapeKeyName, originalShapeKeyName, minValue, maxValue);
        }

        public static bool RemoveOne(SkinnedMeshRenderer renderer, string shapeKeyName)
        {
            return BlendShapeLimitBreak.RemoveBlendShapeFromMesh(renderer, shapeKeyName);
        }

        public static bool RemoveMany(SkinnedMeshRenderer renderer, List<string> shapeKeyNames)
        {
            return BlendShapeLimitBreak.RemoveMultipleBlendShapesFromMesh(renderer, shapeKeyNames);
        }
    }
}


