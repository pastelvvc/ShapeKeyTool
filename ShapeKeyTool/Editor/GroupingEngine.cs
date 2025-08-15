using System.Collections.Generic;
using System.Linq;

namespace ShapeKeyTools
{
	internal static class GroupingEngine
	{
		internal static Dictionary<string, List<BlendShape>> ComputeGroups(
			List<BlendShape> blendShapes,
			Dictionary<string, List<BlendShape>> loadedOrExisting,
			IGroupingStrategy strategy)
		{
			if (blendShapes == null) return new Dictionary<string, List<BlendShape>>();
			// 既存拡張の配置情報を保持
			var existingExtended = new Dictionary<string, (string group, BlendShape shape)>();
			if (loadedOrExisting != null)
			{
				foreach (var g in loadedOrExisting)
				{
					foreach (var s in g.Value)
					{
						if (s.isExtended) existingExtended[s.name] = (g.Key, s);
					}
				}
			}
			var computed = new Dictionary<string, List<BlendShape>>();
			string currentGroup = "その他";
			foreach (var s in blendShapes)
			{
				if (GroupingRules.IsGroupHeader(s.name))
				{
					currentGroup = GroupingRules.ExtractGroupName(s.name);
					EnsureKeys(computed, currentGroup);
					continue;
				}
				EnsureKeys(computed, currentGroup);
				if (!s.isExtended)
				{
					computed[currentGroup].Add(s);
				}
				else
				{
					// メッシュ上に存在する拡張シェイプキーも表示対象にする。
					// 原則として元のシェイプキーの直後へ挿入。見つからない場合は末尾。
					var list = computed[currentGroup];
					int insertIndex = list.Count;
					if (!string.IsNullOrEmpty(s.originalName))
					{
						for (int i = 0; i < list.Count; i++)
						{
							if (list[i].name == s.originalName) { insertIndex = i + 1; break; }
						}
					}
					// 同名重複を避ける
					if (!list.Any(x => x.name == s.name))
					{
						list.Insert(insertIndex, s);
					}
				}
			}
			// 既存の拡張を元位置へ安定挿入
			foreach (var kv in existingExtended)
			{
				var (grp, ext) = kv.Value;
				EnsureKeys(computed, grp);
				// すでに同名が挿入済みならスキップ
				if (!computed[grp].Any(x => x.name == ext.name))
				{
					int insertIndex = computed[grp].Count;
					if (!string.IsNullOrEmpty(ext.originalName))
					{
						for (int i = 0; i < computed[grp].Count; i++)
						{
							if (computed[grp][i].name == ext.originalName) { insertIndex = i + 1; break; }
						}
					}
					computed[grp].Insert(insertIndex, ext);
				}
			}
			return strategy != null ? strategy.Merge(computed, loadedOrExisting) : computed;
		}

		private static void EnsureKeys(Dictionary<string, List<BlendShape>> dict, string group)
		{
			if (!dict.ContainsKey(group)) dict[group] = new List<BlendShape>();
		}
	}
}


