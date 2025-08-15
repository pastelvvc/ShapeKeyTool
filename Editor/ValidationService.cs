using UnityEngine;
using System.Collections.Generic;

namespace ShapeKeyTools
{
	internal static class ValidationService
	{
		public static HashSet<string> ListShapeKeys(Mesh mesh)
		{
			var set = new HashSet<string>();
			if (mesh == null) return set;
			for (int i = 0; i < mesh.blendShapeCount; i++) set.Add(mesh.GetBlendShapeName(i));
			return set;
		}

		public static bool ContainsShapeKey(Mesh mesh, string name)
		{
			if (mesh == null || string.IsNullOrEmpty(name)) return false;
			for (int i = 0; i < mesh.blendShapeCount; i++) if (mesh.GetBlendShapeName(i) == name) return true;
			return false;
		}

		public static (bool ok, string message) ValidateCompatibility(HashSet<string> existingShapeKeys, IEnumerable<GroupDataDto> groups, int previewLimit = 10)
		{
			var missing = new List<string>();
			foreach (var g in groups)
			{
				foreach (var s in g.shapeKeys)
				{
					bool exists = existingShapeKeys.Contains(s.name) || (!string.IsNullOrEmpty(s.originalName) && existingShapeKeys.Contains(s.originalName));
					if (!exists) missing.Add(s.name);
				}
			}
			if (missing.Count == 0) return (true, "");
			string list = string.Join("\n• ", missing.Count > previewLimit ? missing.GetRange(0, previewLimit) : missing);
			if (missing.Count > previewLimit) list += $"\n... 他 {missing.Count - previewLimit} 個";
			return (false, list);
		}
	}
}


