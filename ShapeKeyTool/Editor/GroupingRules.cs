using System.Collections.Generic;

namespace ShapeKeyTools
{
	internal static class GroupingRules
	{
		private static readonly string[] HeaderPatterns = { "==", "!!", "◇◇", "★★", "◆◆", "!!!" };

		internal static bool IsGroupHeader(string shapeName)
		{
			if (string.IsNullOrEmpty(shapeName)) return false;
			foreach (string pattern in HeaderPatterns)
			{
				if (shapeName.StartsWith(pattern)) return true;
			}
			return false;
		}

		internal static string ExtractGroupName(string headerName)
		{
			if (string.IsNullOrEmpty(headerName)) return "その他";
			foreach (string pattern in HeaderPatterns)
			{
				if (headerName.StartsWith(pattern))
				{
					string groupName = headerName.Substring(pattern.Length);
					while (groupName.Length > 0 && (char.IsPunctuation(groupName[groupName.Length - 1]) || char.IsSymbol(groupName[groupName.Length - 1])))
					{
						groupName = groupName.Substring(0, groupName.Length - 1);
					}
					return groupName.Trim();
				}
			}
			return "その他";
		}
	}
}


