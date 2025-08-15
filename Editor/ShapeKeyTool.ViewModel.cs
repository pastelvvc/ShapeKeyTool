using System.Collections.Generic;
using UnityEngine;

namespace ShapeKeyTools
{
	/// <summary>
	/// UI状態の単一ソース(ViewModel)
	/// </summary>
	internal class ShapeKeyViewModel
	{
		public Dictionary<string, List<BlendShape>> GroupedShapes { get; set; } = new Dictionary<string, List<BlendShape>>();
		public Dictionary<string, bool> GroupFoldouts { get; set; } = new Dictionary<string, bool>();

		public Dictionary<string, float> GroupTestSliders { get; set; } = new Dictionary<string, float>();
		public Dictionary<int, bool> LockedShapeKeys { get; set; } = new Dictionary<int, bool>();
		public Dictionary<string, Dictionary<int, float>> OriginalWeights { get; set; } = new Dictionary<string, Dictionary<int, float>>();
		// 高速探査の直近選択（グループごと）
		public Dictionary<string, string> LastTestSelectedShapeName { get; set; } = new Dictionary<string, string>();
		// 高速探査中にユーザーが手動で変更したシェイプ（名前ベース）
		public Dictionary<string, HashSet<string>> UserEditedDuringTest { get; set; } = new Dictionary<string, HashSet<string>>();

		public Vector2 ScrollPosition { get; set; } = Vector2.zero;
		public string CurrentGroupDisplay { get; set; } = "";

		// Policy: expand groups while searching
		public bool ForceExpandOnSearch { get; set; } = true;
	}
}



