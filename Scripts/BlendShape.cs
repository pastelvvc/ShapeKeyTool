using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace ShapeKeyTools
{
    /// <summary>
    /// BlendShape モデルクラス
    /// </summary>
    public class BlendShape
    {
        public string name;
        public float weight;
        public int index;
        public bool isLocked; // ロック状態を追加
        
        // 拡張シェイプキー用のプロパティ
        public bool isExtended = false;
        public int minValue = -100;
        public int maxValue = 200;
        public string originalName = "";
    }
} 