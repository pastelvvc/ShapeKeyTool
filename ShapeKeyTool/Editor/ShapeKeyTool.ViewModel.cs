using System.Collections.Generic;
using UnityEngine;

namespace ShapeKeyTools
{
    /// <summary>
    /// 集中状態管理(ViewModel)。UIは読み取り、操作はサービス/コマンド経由で更新。
    /// </summary>
    internal class ShapeKeyViewModel
    {
        public Dictionary<string, List<BlendShape>> groupedShapes = new();
        public Dictionary<string, bool> groupFoldouts = new();
        public Dictionary<string, float> groupTestSliders = new();
        public Dictionary<int, bool> lockedShapeKeys = new();
        public Dictionary<string, Dictionary<int, float>> originalWeights = new();
        public Vector2 scrollPosition;
        public string currentGroupDisplay = "";

        public void Clear()
        {
            groupedShapes.Clear();
            groupFoldouts.Clear();
            groupTestSliders.Clear();
            lockedShapeKeys.Clear();
            originalWeights.Clear();
            scrollPosition = Vector2.zero;
            currentGroupDisplay = "";
        }
    }
}


