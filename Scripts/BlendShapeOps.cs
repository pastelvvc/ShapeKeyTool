using UnityEngine;
using System.Collections.Generic;

namespace ShapeKeyTools
{
	/// <summary>
	/// 高レベルAPIの結果型
	/// </summary>
	public struct BlendShapeOpResult
	{
		public bool Success;
		public string Message;
		public static BlendShapeOpResult Ok(string msg = null) => new BlendShapeOpResult { Success = true, Message = msg };
		public static BlendShapeOpResult Fail(string msg) => new BlendShapeOpResult { Success = false, Message = msg };
	}

	/// <summary>
	/// BlendShapeLimitBreak のファサード（高レベルAPI）
	/// </summary>
	public static class BlendShapeOps
	{
		public static BlendShapeOpResult CreateExtended(SkinnedMeshRenderer renderer, string originalName, string extendedName, float minValue, float maxValue)
		{
			if (renderer == null || renderer.sharedMesh == null) return BlendShapeOpResult.Fail("レンダラーが無効です");
			bool ok = BlendShapeLimitBreak.ApplyExtendedShapeKeyToMesh(renderer, extendedName, originalName, minValue, maxValue);
			return ok ? BlendShapeOpResult.Ok($"拡張 '{extendedName}' 作成") : BlendShapeOpResult.Fail("拡張の作成に失敗しました");
		}

		public static BlendShapeOpResult RemoveOne(SkinnedMeshRenderer renderer, string shapeKeyName)
		{
			if (renderer == null || renderer.sharedMesh == null) return BlendShapeOpResult.Fail("レンダラーが無効です");
			bool ok = BlendShapeLimitBreak.RemoveBlendShapeFromMesh(renderer, shapeKeyName);
			return ok ? BlendShapeOpResult.Ok($"'{shapeKeyName}' を削除") : BlendShapeOpResult.Fail("削除に失敗しました");
		}

		public static BlendShapeOpResult RemoveMany(SkinnedMeshRenderer renderer, List<string> names)
		{
			if (renderer == null || renderer.sharedMesh == null) return BlendShapeOpResult.Fail("レンダラーが無効です");
			bool ok = BlendShapeLimitBreak.RemoveMultipleBlendShapesFromMesh(renderer, names);
			return ok ? BlendShapeOpResult.Ok($"{names?.Count ?? 0} 件削除") : BlendShapeOpResult.Fail("一括削除に失敗しました");
		}

		public static BlendShapeOpResult Merge(SkinnedMeshRenderer renderer, string newShapeName, Dictionary<string, float> weights)
		{
			if (renderer == null || renderer.sharedMesh == null) return BlendShapeOpResult.Fail("レンダラーが無効です");
			bool ok = BlendShapeLimitBreak.MergeBlendShapes(renderer.sharedMesh, newShapeName, weights);
			return ok ? BlendShapeOpResult.Ok($"'{newShapeName}' を合成") : BlendShapeOpResult.Fail("合成に失敗しました");
		}

		public static BlendShapeOpResult ApplyWeight(SkinnedMeshRenderer renderer, int index, float weight)
		{
			if (renderer == null || renderer.sharedMesh == null || index < 0) return BlendShapeOpResult.Fail("無効な引数です");
			try
			{
				renderer.SetBlendShapeWeight(index, weight);
				return BlendShapeOpResult.Ok();
			}
			catch (System.Exception e)
			{
				return BlendShapeOpResult.Fail($"適用失敗: {e.Message}");
			}
		}
	}
}



