using UnityEngine;

namespace ShapeKeyTools
{
    /// <summary>
    /// UI一時状態を集約するクラス（メニュー選択やジャンプ先など）。
    /// ViewModelとは独立しておく。
    /// </summary>
    internal class UIState
    {
        public int fileMenuIndex = 0;
        public int displayMenuIndex = 0;
        public int operationMenuIndex = 0;
        public int shapeKeyMenuIndex = 0;
        public int optionMenuIndex = 0;

        public string jumpToGroup = null;
        public bool needScrollToGroup = false;

        public bool treeViewFoldout = true;
    }
}


