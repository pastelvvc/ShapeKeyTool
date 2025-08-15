using UnityEditor;

namespace ShapeKeyTools
{
	internal enum DialogType { Info, Warning, Error }

	internal static class DialogService
	{
		public static bool Confirm(string title, string message, string ok = UIStrings.DialogOK, string cancel = UIStrings.DialogCancel)
		{
			return EditorUtility.DisplayDialog(title, message, ok, cancel);
		}

		public static int Choice(string title, string message, string ok, string alt, string cancel)
		{
			return EditorUtility.DisplayDialogComplex(title, message, ok, alt, cancel);
		}

		public static void Notify(string title, string message, DialogType type = DialogType.Info)
		{
			// EditorUtility.DisplayDialogはアイコン制御が限定的なため、タイトルで簡易区別
			EditorUtility.DisplayDialog(title, message, UIStrings.DialogOK);
		}
	}
}


