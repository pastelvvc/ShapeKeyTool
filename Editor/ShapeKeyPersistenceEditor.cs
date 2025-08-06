using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace ShapeKeyTools
{
    /// <summary>
    /// エディター専用のShapeKeyPersistence操作クラス
    /// </summary>
    public static class ShapeKeyPersistenceEditor
    {
        /// <summary>
        /// データを保存
        /// </summary>
        public static void SaveData(ShapeKeyPersistence persistence, ShapeKeyToolWindow window)
        {
            if (persistence == null || window == null || window.groupedShapes == null)
                return;

            var groups = new List<ShapeKeyPersistence.GroupData>();
            var groupFoldouts = new Dictionary<string, bool>();
            var groupTestSliders = new Dictionary<string, float>();
            var lockedShapeKeys = new Dictionary<int, bool>();

            // グループデータを保存
            foreach (var group in window.groupedShapes)
            {
                var groupData = new ShapeKeyPersistence.GroupData
                {
                    groupName = group.Key
                };

                foreach (var shape in group.Value)
                {
                    var shapeData = new ShapeKeyPersistence.ShapeKeyData
                    {
                        name = shape.name,
                        weight = shape.weight,
                        isLocked = shape.isLocked,
                        isExtended = shape.isExtended,
                        originalName = shape.originalName,
                        minValue = shape.minValue,
                        maxValue = shape.maxValue
                    };
                    groupData.shapeKeys.Add(shapeData);
                }

                groups.Add(groupData);
            }

            // グループの展開状態を保存
            foreach (var foldout in window.groupFoldouts)
            {
                groupFoldouts[foldout.Key] = foldout.Value;
            }

            // テストスライダーの値を保存
            foreach (var slider in window.groupTestSliders)
            {
                groupTestSliders[slider.Key] = slider.Value;
            }

            // ロック状態を保存
            foreach (var locked in window.lockedShapeKeys)
            {
                lockedShapeKeys[locked.Key] = locked.Value;
            }

            // データを設定
            persistence.SetGroups(groups);
            persistence.SetGroupFoldouts(groupFoldouts);
            persistence.SetGroupTestSliders(groupTestSliders);
            persistence.SetLockedShapeKeys(lockedShapeKeys);

            // 変更をマーク
            EditorUtility.SetDirty(persistence);
        }

        /// <summary>
        /// データを読み込み
        /// </summary>
        public static void LoadData(ShapeKeyPersistence persistence, ShapeKeyToolWindow window)
        {
            if (persistence == null || window == null || !persistence.HasData())
                return;

            var groups = persistence.GetGroups();
            var groupFoldouts = persistence.GetGroupFoldouts();
            var groupTestSliders = persistence.GetGroupTestSliders();
            var lockedShapeKeys = persistence.GetLockedShapeKeys();

            // グループデータを復元
            window.groupedShapes.Clear();
            foreach (var groupData in groups)
            {
                var shapeList = new List<BlendShape>();
                foreach (var shapeData in groupData.shapeKeys)
                {
                    var shape = new BlendShape
                    {
                        name = shapeData.name,
                        weight = shapeData.weight,
                        isLocked = shapeData.isLocked,
                        isExtended = shapeData.isExtended,
                        originalName = shapeData.originalName,
                        minValue = (int)shapeData.minValue,
                        maxValue = (int)shapeData.maxValue
                    };
                    shapeList.Add(shape);
                }
                window.groupedShapes[groupData.groupName] = shapeList;
            }

            // グループの展開状態を復元
            window.groupFoldouts.Clear();
            foreach (var foldout in groupFoldouts)
            {
                window.groupFoldouts[foldout.Key] = foldout.Value;
            }

            // テストスライダーの値を復元
            window.groupTestSliders.Clear();
            foreach (var slider in groupTestSliders)
            {
                window.groupTestSliders[slider.Key] = slider.Value;
            }

            // ロック状態を復元
            window.lockedShapeKeys.Clear();
            foreach (var locked in lockedShapeKeys)
            {
                window.lockedShapeKeys[locked.Key] = locked.Value;
            }
        }
    }
} 