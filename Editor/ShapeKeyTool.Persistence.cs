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
        private static bool autoSaveEnabled = true;

        static ShapeKeyPersistenceManager()
        {
            // 初期化ログを削除
        }

        /// <summary>
        /// 自動保存が有効かどうか
        /// </summary>
        public static bool AutoSaveEnabled
        {
            get { return autoSaveEnabled; }
            set { autoSaveEnabled = value; }
        }

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
        /// 自動保存を実行
        /// </summary>
        public static void AutoSave(ShapeKeyToolWindow window)
        {
            if (!autoSaveEnabled)
            {
                return;
            }
            
            if (window?.selectedRenderer == null)
            {
                return;
            }

            var target = window.selectedRenderer.gameObject;
            
            // 自動保存が有効な場合は、コンポーネントがなくても自動で追加
            var persistence = GetOrCreateComponent(target);
            if (persistence != null)
            {
                ShapeKeyPersistenceEditor.SaveData(persistence, window);
                
                // コンポーネントが追加されたことを通知
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(target);
                UnityEditor.AssetDatabase.SaveAssets();
                #endif
            }
        }

        /// <summary>
        /// データを読み込み
        /// </summary>
        public static void LoadData(ShapeKeyToolWindow window)
        {
            if (window?.selectedRenderer == null)
                return;

            var target = window.selectedRenderer.gameObject;
            var persistence = target.GetComponent<ShapeKeyPersistence>();
            if (persistence != null && persistence.HasData())
            {
                ShapeKeyPersistenceEditor.LoadData(persistence, window);
            }
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