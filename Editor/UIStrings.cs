using UnityEngine;

namespace ShapeKeyTools
{
    internal static class UIStrings
    {
        // Menus
        public const string MenuFile = "ファイル";
        public const string MenuEdit = "編集";
        public const string MenuDisplay = "表示";
        public const string MenuOperation = "操作";
        public const string MenuShapeKey = "シェイプキー";
        public const string MenuOption = "オプション";

        // File menu items
        public const string MenuManualSave = "手動保存";
        public const string MenuManualLoad = "手動読み込み";
        public const string MenuJsonExport = "JSON エクスポート";
        public const string MenuJsonImport = "JSON インポート";
        public const string MenuRemoveComponent = "コンポーネントの削除";

        // Display
        public const string MenuOpenAll = "すべて開く";
        public const string MenuCloseAll = "すべて閉じる";
		public const string MenuResyncInspector = "インスペクターと再同期";

        // Operation common
        public const string MenuLockAll = "すべてロック";
        public const string MenuUnlockAll = "すべてアンロック";
        public const string MenuResetZero = "値を０に上書きする";
        public const string MenuInitializeTree = "初期化";

        // ShapeKey menu (unified labels)
        public const string MenuLockAllShape = "すべてロック";
        public const string MenuUnlockAllShape = "すべて解除";
        public const string MenuLockNonZero = "値が入っているものをロックする";
        public const string MenuDeleteAllExtended = "拡張シェイプキーを一括削除";
        public const string MenuResetTreeView = "TreeViewをリセットする";

        // Option menu
        public const string MenuEnableVerbose = "冗長ログを有効化";

        // Labels
        public const string LabelSearch = "検索:";
        public const string LabelClear = "クリア";
        public const string LabelFastProbe = "高速探査";
        public const string LabelCurrentGroup = "現在のグループ";

        // Dialogs (titles/body snippets)
        public const string DialogError = "エラー";
        public const string DialogOK = "OK";
        public const string DialogDelete = "削除";
        public const string DialogCancel = "キャンセル";
    }
}
