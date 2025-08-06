using UnityEngine;
using UnityEditor;

namespace ShapeKeyTools
{
    /// <summary>
    /// ShapeKeyToolの共通設定を管理するクラス
    /// </summary>
    internal static class ShapeKeyToolSettings
    {
        private const string USE_REGEX_KEY = "ShapeKeyTool_UseRegex";
        private const string CASE_SENSITIVE_KEY = "ShapeKeyTool_CaseSensitive";
        private const string SKIP_NON_ZERO_VALUES_KEY = "ShapeKeyTool_SkipNonZeroValues";

        /// <summary>
        /// 正規表現を使用するかどうか
        /// </summary>
        public static bool UseRegex
        {
            get => EditorPrefs.GetBool(USE_REGEX_KEY, false);
            set
            {
                EditorPrefs.SetBool(USE_REGEX_KEY, value);
                SearchManager.useRegex = value;
            }
        }

        /// <summary>
        /// 大文字小文字を区別するかどうか
        /// </summary>
        public static bool CaseSensitive
        {
            get => EditorPrefs.GetBool(CASE_SENSITIVE_KEY, false);
            set
            {
                EditorPrefs.SetBool(CASE_SENSITIVE_KEY, value);
                SearchManager.caseSensitive = value;
            }
        }

        /// <summary>
        /// 値が入っている物はスキップするかどうか
        /// </summary>
        public static bool SkipNonZeroValues
        {
            get => EditorPrefs.GetBool(SKIP_NON_ZERO_VALUES_KEY, true);
            set => EditorPrefs.SetBool(SKIP_NON_ZERO_VALUES_KEY, value);
        }

        /// <summary>
        /// 設定を初期化
        /// </summary>
        public static void Initialize()
        {
            SearchManager.useRegex = UseRegex;
            SearchManager.caseSensitive = CaseSensitive;
        }

        /// <summary>
        /// 設定をリセット
        /// </summary>
        public static void Reset()
        {
            EditorPrefs.DeleteKey(USE_REGEX_KEY);
            EditorPrefs.DeleteKey(CASE_SENSITIVE_KEY);
            EditorPrefs.DeleteKey(SKIP_NON_ZERO_VALUES_KEY);
            Initialize();
        }
    }
} 