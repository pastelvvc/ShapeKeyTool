using System.Collections.Generic;
using System.Linq;

namespace ShapeKeyTools
{
	internal interface IGroupingStrategy
	{
		Dictionary<string, List<BlendShape>> Merge(
			Dictionary<string, List<BlendShape>> computed,
			Dictionary<string, List<BlendShape>> loaded
		);
	}

	/// <summary>
	/// 読み込みデータの順序を優先（存在するものはそのまま、欠損のみ補う）
	/// </summary>
	internal class PreserveOrderStrategy : IGroupingStrategy
	{
		public Dictionary<string, List<BlendShape>> Merge(Dictionary<string, List<BlendShape>> computed, Dictionary<string, List<BlendShape>> loaded)
		{
			if (loaded == null || loaded.Count == 0) return computed;
			var result = new Dictionary<string, List<BlendShape>>();
			foreach (var g in loaded)
			{
				result[g.Key] = new List<BlendShape>(g.Value);
			}
			// 追加分を後ろへ
			foreach (var g in computed)
			{
				if (!result.ContainsKey(g.Key)) result[g.Key] = new List<BlendShape>();
				foreach (var s in g.Value)
				{
					if (!result[g.Key].Any(x => x.name == s.name)) result[g.Key].Add(s);
				}
			}
			return result;
		}
	}

	/// <summary>
	/// ヘッダー規則に従い再構築（完全再計算）
	/// </summary>
	internal class ByHeaderPatternStrategy : IGroupingStrategy
	{
		public Dictionary<string, List<BlendShape>> Merge(Dictionary<string, List<BlendShape>> computed, Dictionary<string, List<BlendShape>> loaded)
		{
			return computed;
		}
	}

	/// <summary>
	/// 読み込み順を可能な限り維持しつつ、新規は安定挿入
	/// </summary>
	internal class StableInsertStrategy : IGroupingStrategy
	{
		public Dictionary<string, List<BlendShape>> Merge(Dictionary<string, List<BlendShape>> computed, Dictionary<string, List<BlendShape>> loaded)
		{
			if (loaded == null || loaded.Count == 0) return computed;
			var result = new Dictionary<string, List<BlendShape>>();
			foreach (var g in loaded)
			{
				result[g.Key] = new List<BlendShape>(g.Value);
			}
			foreach (var g in computed)
			{
				if (!result.ContainsKey(g.Key)) result[g.Key] = new List<BlendShape>();
				var list = result[g.Key];
				foreach (var s in g.Value)
				{
					if (!list.Any(x => x.name == s.name))
					{
						// 元のシンプル規則: originalName の直後へ。それがなければ末尾
						int insert = list.FindIndex(x => x.name == s.originalName);
						if (insert == -1) list.Add(s); else list.Insert(insert + 1, s);
					}
				}
			}
			return result;
		}
	}
}


