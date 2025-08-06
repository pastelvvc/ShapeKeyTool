using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace ShapeKeyTools
{
    /// <summary>
    /// 拡張シェイプキー情報を管理するクラス
    /// </summary>
    public class ExtendedShapeKeyInfo
    {
        public string originalName;
        public int minValue;
        public int maxValue;
        public string extendedName;
        
        public ExtendedShapeKeyInfo(string original, int min, int max)
        {
            originalName = original;
            minValue = min;
            maxValue = max;
            extendedName = $"{original}_min:{min}_max:{max}";
        }
        
        public static bool TryParseFromName(string name, out ExtendedShapeKeyInfo info)
        {
            info = null;
            
            if (string.IsNullOrEmpty(name) || !name.Contains("_min:") || !name.Contains("_max:"))
                return false;
                
            try
            {
                // _min:と_max:の位置を特定
                int minIndex = name.IndexOf("_min:");
                int maxIndex = name.IndexOf("_max:");
                
                if (minIndex == -1 || maxIndex == -1 || minIndex >= maxIndex)
                    return false;
                
                // 元の名前を取得（_min:の前の部分）
                string originalName = name.Substring(0, minIndex);
                
                // min値とmax値を抽出
                string minPart = name.Substring(minIndex + 5, maxIndex - (minIndex + 5)); // "_min:"の後から"_max:"の前まで
                string maxPart = name.Substring(maxIndex + 5); // "_max:"の後
                
                // max値の部分から、後ろの余分な部分を除去
                int maxEndIndex = maxPart.IndexOf('_');
                if (maxEndIndex != -1)
                {
                    maxPart = maxPart.Substring(0, maxEndIndex);
                }
                
                var minValue = int.Parse(minPart.Trim());
                var maxValue = int.Parse(maxPart.Trim());
                
                info = new ExtendedShapeKeyInfo(originalName, minValue, maxValue);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 拡張シェイプキーの情報を永続化するための静的クラス
    /// </summary>
    public static class ExtendedShapeKeyManager
    {
        // 拡張シェイプキーの情報を保持する辞書
        // Key: シェイプキー名, Value: ExtendedShapeKeyInfo
        private static Dictionary<string, ExtendedShapeKeyInfo> extendedShapeKeys = new Dictionary<string, ExtendedShapeKeyInfo>();

        /// <summary>
        /// 拡張シェイプキーの情報を登録
        /// </summary>
        public static void RegisterExtendedShapeKey(string shapeKeyName, ExtendedShapeKeyInfo info)
        {
            if (!string.IsNullOrEmpty(shapeKeyName) && info != null)
            {
                extendedShapeKeys[shapeKeyName] = info;
            }
        }

        /// <summary>
        /// 拡張シェイプキーの情報を取得
        /// </summary>
        public static bool TryGetExtendedShapeKeyInfo(string shapeKeyName, out ExtendedShapeKeyInfo info)
        {
            return extendedShapeKeys.TryGetValue(shapeKeyName, out info);
        }

        /// <summary>
        /// 拡張シェイプキーの情報を削除
        /// </summary>
        public static void RemoveExtendedShapeKey(string shapeKeyName)
        {
            if (extendedShapeKeys.ContainsKey(shapeKeyName))
            {
                extendedShapeKeys.Remove(shapeKeyName);
            }
        }

        /// <summary>
        /// すべての拡張シェイプキー情報をクリア
        /// </summary>
        public static void ClearAll()
        {
            extendedShapeKeys.Clear();
        }

        /// <summary>
        /// 現在登録されている拡張シェイプキーの情報を取得
        /// </summary>
        public static Dictionary<string, ExtendedShapeKeyInfo> GetAllExtendedShapeKeys()
        {
            return new Dictionary<string, ExtendedShapeKeyInfo>(extendedShapeKeys);
        }

        /// <summary>
        /// 拡張シェイプキーの情報を設定
        /// </summary>
        public static void SetExtendedShapeKeys(Dictionary<string, ExtendedShapeKeyInfo> shapeKeys)
        {
            extendedShapeKeys.Clear();
            if (shapeKeys != null)
            {
                foreach (var kvp in shapeKeys)
                {
                    extendedShapeKeys[kvp.Key] = kvp.Value;
                }
            }
        }
    }
} 