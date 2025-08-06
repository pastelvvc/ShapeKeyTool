using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace ShapeKeyTools
{
    /// <summary>
    /// 検索機能を管理するクラス
    /// </summary>
    internal static class SearchManager
    {
        // 共通の検索オプション
        public static bool useRegex = false;
        public static bool caseSensitive = false;
        
        // TreeView用の検索
        public static string treeViewSearchText = "";
        
        // シェイプキーパネル用の検索
        public static string shapeKeySearchText = "";

        /// <summary>
        /// TreeView用の検索フィルター
        /// </summary>
        public static bool ShouldShowInTreeView(string itemName)
        {
            if (string.IsNullOrEmpty(treeViewSearchText))
                return true;

            try
            {
                if (useRegex)
                {
                    RegexOptions options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    return Regex.IsMatch(itemName, treeViewSearchText, options);
                }
                else
                {
                    StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                    return itemName.IndexOf(treeViewSearchText, comparison) >= 0;
                }
            }
            catch (System.Exception)
            {
                // 正規表現が無効な場合は通常の文字列検索にフォールバック
                StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                return itemName.IndexOf(treeViewSearchText, comparison) >= 0;
            }
        }

        /// <summary>
        /// シェイプキーパネル用の検索フィルター
        /// </summary>
        public static bool ShouldShowInShapeKeyPanel(string itemName)
        {
            if (string.IsNullOrEmpty(shapeKeySearchText))
                return true;

            try
            {
                if (useRegex)
                {
                    RegexOptions options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    return Regex.IsMatch(itemName, shapeKeySearchText, options);
                }
                else
                {
                    StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                    return itemName.IndexOf(shapeKeySearchText, comparison) >= 0;
                }
            }
            catch (System.Exception)
            {
                // 正規表現が無効な場合は通常の文字列検索にフォールバック
                StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                return itemName.IndexOf(shapeKeySearchText, comparison) >= 0;
            }
        }

        /// <summary>
        /// 正規表現が有効かチェック
        /// </summary>
        public static bool IsValidRegex(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return true;

            try
            {
                Regex.IsMatch("", pattern);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 検索結果のハイライト用テキストを生成
        /// </summary>
        public static string GetHighlightedText(string originalText, string searchText, bool useRegex, bool caseSensitive)
        {
            if (string.IsNullOrEmpty(searchText) || string.IsNullOrEmpty(originalText))
                return originalText;

            try
            {
                if (useRegex)
                {
                    RegexOptions options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    return Regex.Replace(originalText, searchText, "<color=yellow>$&</color>", options);
                }
                else
                {
                    StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                    int index = originalText.IndexOf(searchText, comparison);
                    if (index >= 0)
                    {
                        return originalText.Substring(0, index) + 
                               "<color=yellow>" + originalText.Substring(index, searchText.Length) + "</color>" + 
                               originalText.Substring(index + searchText.Length);
                    }
                }
            }
            catch
            {
                // エラーの場合は元のテキストを返す
            }

            return originalText;
        }
    }
} 