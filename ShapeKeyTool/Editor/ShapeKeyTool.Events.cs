using UnityEditor;

namespace ShapeKeyTools
{
	/// <summary>
	/// GUIイベントからの副作用呼び出しを集約
	/// </summary>
	internal static class UIEventHandlers
	{
		internal static void OnTreeSearchTextChanged(ShapeKeyToolWindow window, string newText)
		{
			SearchManager.treeViewSearchText = newText;
			ApplyScheduler.RequestReload();
			ApplyScheduler.RequestRepaint();
		}

		internal static void OnTreeSearchClear(ShapeKeyToolWindow window)
		{
			SearchManager.treeViewSearchText = "";
			ApplyScheduler.RequestReload();
			ApplyScheduler.RequestRepaint();
		}

		internal static void OnShapeKeySearchTextChanged(ShapeKeyToolWindow window, string newText)
		{
			SearchManager.shapeKeySearchText = newText;
			ApplyScheduler.RequestRepaint();
		}

		internal static void OnShapeKeySearchClear(ShapeKeyToolWindow window)
		{
			SearchManager.shapeKeySearchText = "";
			ApplyScheduler.RequestRepaint();
		}
	}
}



