using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ShapeKeyTools
{
    /// <summary>
    /// シェイプキー永続化マネージャー
    /// </summary>
    public static class ShapeKeyPersistenceManager
    {
        /// <summary>
        /// コンポーネントを取得または作成
        /// </summary>
        public static ShapeKeyPersistence GetOrCreateComponent(GameObject target)
        {
            if (target == null)
                return null;

            var persistence = target.GetComponent<ShapeKeyPersistence>();
            if (persistence == null)
            {
                persistence = target.AddComponent<ShapeKeyPersistence>();
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(target);
                #endif
            }

            return persistence;
        }

        /// <summary>
        /// コンポーネントを削除
        /// </summary>
        public static void RemoveComponent(GameObject target)
        {
            if (target == null)
                return;

            var persistence = target.GetComponent<ShapeKeyPersistence>();
            if (persistence != null)
            {
                #if UNITY_EDITOR
                UnityEditor.Undo.DestroyObjectImmediate(persistence);
                #else
                Object.DestroyImmediate(persistence);
                #endif
            }
        }

        /// <summary>
        /// 手動でデータを保存
        /// </summary>
        public static void ManualSave(ShapeKeyToolWindow window)
        {
            if (window?.selectedRenderer == null)
            {
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.DisplayDialog(
                    "エラー",
                    "保存対象のオブジェクトが選択されていません。",
                    "OK"
                );
                #endif
                return;
            }

            var target = window.selectedRenderer.gameObject;
            var persistence = GetOrCreateComponent(target);
            if (persistence != null)
            {
                ShapeKeyPersistenceEditor.SaveData(persistence, window);
                
                // 変更をマーク
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(target);
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.EditorUtility.DisplayDialog(
                    "保存完了",
                    "シェイプキーデータをコンポーネントに保存しました。",
                    "OK"
                );
                #endif
            }
        }

        /// <summary>
        /// 手動でデータを読み込み（全データ）
        /// </summary>
        public static void ManualLoad(ShapeKeyToolWindow window)
        {
            ManualLoadWithOptions(window, true, true, true, true, true, true, true, true, true, true, true);
        }

        /// <summary>
        /// 選択的なデータ読み込み（詳細版）
        /// </summary>
        public static void ManualLoadWithOptions(ShapeKeyToolWindow window, 
            bool loadGroupStructure, bool loadShapeKeyValues, bool loadLockedStates, bool loadExtendedInfo,
            bool loadFoldouts, bool loadTestSliders, bool loadLockedStatesFromGroups,
            bool loadGroupNames, bool loadShapeKeyNames, bool loadShapeKeyOrder, bool validateMeshCompatibility)
        {
            if (window?.selectedRenderer == null)
            {
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.DisplayDialog(
                    "エラー",
                    "読み込み対象のオブジェクトが選択されていません。",
                    "OK"
                );
                #endif
                return;
            }

            var target = window.selectedRenderer.gameObject;
            var persistence = target.GetComponent<ShapeKeyPersistence>();
            if (persistence != null && persistence.HasData())
            {
                ShapeKeyPersistenceEditor.LoadDataWithOptions(persistence, window, 
                    loadGroupStructure, loadShapeKeyValues, loadLockedStates, loadExtendedInfo,
                    loadFoldouts, loadTestSliders, loadLockedStatesFromGroups,
                    loadGroupNames, loadShapeKeyNames, loadShapeKeyOrder, validateMeshCompatibility);
                
                #if UNITY_EDITOR
                string loadedItems = "";
                if (loadGroupStructure) loadedItems += "• グループ構成\n";
                if (loadShapeKeyValues) loadedItems += "• シェイプキー値\n";
                if (loadLockedStates) loadedItems += "• ロック状態（独立）\n";
                if (loadExtendedInfo) loadedItems += "• 拡張シェイプキー情報\n";
                if (loadFoldouts) loadedItems += "• 展開状態\n";
                if (loadTestSliders) loadedItems += "• テストスライダー\n";
                if (loadLockedStatesFromGroups) loadedItems += "• ロック状態（グループ）\n";
                if (loadGroupNames) loadedItems += "• グループ名\n";
                if (loadShapeKeyNames) loadedItems += "• シェイプキー名\n";
                if (loadShapeKeyOrder) loadedItems += "• シェイプキー順序\n";
                if (validateMeshCompatibility) loadedItems += "• メッシュ整合性チェック\n";
                
                UnityEditor.EditorUtility.DisplayDialog(
                    "読み込み完了",
                    $"以下のデータを読み込みました：\n\n{loadedItems}",
                    "OK"
                );
                #endif
            }
            else
            {
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.DisplayDialog(
                    "エラー",
                    "保存されたデータが見つかりません。\n先にデータを保存してください。",
                    "OK"
                );
                #endif
            }
        }

        /// <summary>
        /// 選択的なデータ読み込み（旧版 - 後方互換性のため）
        /// </summary>
        public static void ManualLoadWithOptions(ShapeKeyToolWindow window, 
            bool loadGroups, bool loadFoldouts, bool loadTestSliders, bool loadLockedStates)
        {
            // 旧版の呼び出しを新版に変換
            ManualLoadWithOptions(window, 
                loadGroups, loadGroups, loadLockedStates, loadGroups,
                loadFoldouts, loadTestSliders, loadGroups,
                loadGroups, loadGroups, loadGroups, loadGroups); // Placeholder for new options
        }

        /// <summary>
        /// データをクリア
        /// </summary>
        public static void ClearData(GameObject target)
        {
            if (target == null)
                return;

            var persistence = target.GetComponent<ShapeKeyPersistence>();
            if (persistence != null)
            {
                persistence.ClearData();
            }
        }
    }
} 