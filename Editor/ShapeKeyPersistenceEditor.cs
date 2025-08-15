using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;

namespace ShapeKeyTools
{
    /// <summary>
    /// シェイプキー永続化エディター
    /// </summary>
    public static class ShapeKeyPersistenceEditor
    {
        /// <summary>
        /// データを保存
        /// </summary>
        public static void SaveData(ShapeKeyPersistence persistence, ShapeKeyToolWindow window)
        {
            if (persistence == null || window == null)
                return;

            var groups = new List<GroupDataDto>();
            var groupFoldouts = new Dictionary<string, bool>();
            var groupTestSliders = new Dictionary<string, float>();
            var lockedShapeKeys = new Dictionary<int, bool>();

            // グループデータを保存
            foreach (var group in window.viewModel.GroupedShapes)
            {
                var groupData = new GroupDataDto
                {
                    groupName = group.Key,
                    shapeKeys = new List<ShapeKeyDataDto>()
                };

                foreach (var shape in group.Value)
                {
                    var shapeData = new ShapeKeyDataDto
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
            foreach (var foldout in window.viewModel.GroupFoldouts)
            {
                groupFoldouts[foldout.Key] = foldout.Value;
            }

            // テストスライダーの値を保存
            foreach (var slider in window.viewModel.GroupTestSliders)
            {
                groupTestSliders[slider.Key] = slider.Value;
            }

            // ロック状態を保存
            foreach (var locked in window.viewModel.LockedShapeKeys)
            {
                lockedShapeKeys[locked.Key] = locked.Value;
            }

            // データを設定
            // DTO -> Persistence 変換
            var pGroups = new List<ShapeKeyPersistence.GroupData>();
            foreach (var g in groups)
            {
                var pg = new ShapeKeyPersistence.GroupData
                {
                    groupName = g.groupName,
                    shapeKeys = new List<ShapeKeyPersistence.ShapeKeyData>()
                };
                foreach (var s in g.shapeKeys)
                {
                    pg.shapeKeys.Add(new ShapeKeyPersistence.ShapeKeyData
                    {
                        name = s.name,
                        weight = s.weight,
                        isLocked = s.isLocked,
                        isExtended = s.isExtended,
                        originalName = s.originalName,
                        minValue = s.minValue,
                        maxValue = s.maxValue
                    });
                }
                pGroups.Add(pg);
            }
            persistence.SetGroups(pGroups);
            persistence.SetGroupFoldouts(groupFoldouts);
            persistence.SetGroupTestSliders(groupTestSliders);
            persistence.SetLockedShapeKeys(lockedShapeKeys);

            // 変更をマーク
            EditorUtility.SetDirty(persistence);
        }

        /// <summary>
        /// データを読み込み（全データ）
        /// </summary>
        public static void LoadData(ShapeKeyPersistence persistence, ShapeKeyToolWindow window)
        {
            LoadDataWithOptions(persistence, window, true, true, true, true, true, true, true, true, true, true, true);
        }

        /// <summary>
        /// 選択的なデータ読み込み（詳細版）
        /// </summary>
        public static void LoadDataWithOptions(ShapeKeyPersistence persistence, ShapeKeyToolWindow window, 
            bool loadGroupStructure, bool loadShapeKeyValues, bool loadLockedStates, bool loadExtendedInfo,
            bool loadFoldouts, bool loadTestSliders, bool loadLockedStatesFromGroups,
            bool loadGroupNames, bool loadShapeKeyNames, bool loadShapeKeyOrder, bool validateMeshCompatibility)
        {
            if (persistence == null || window == null || !persistence.HasData())
                return;

            // 共通の読み込みロジックを使用
            var exportData = ShapeKeyTools.Serialization.CreateExportData();
            // Persistence -> DTO 変換
            foreach (var pg in persistence.GetGroups())
            {
                var dtoGroup = new GroupDataDto
                {
                    groupName = pg.groupName,
                    shapeKeys = new List<ShapeKeyDataDto>()
                };
                foreach (var ps in pg.shapeKeys)
                {
                    dtoGroup.shapeKeys.Add(new ShapeKeyDataDto
                    {
                        name = ps.name,
                        weight = ps.weight,
                        isLocked = ps.isLocked,
                        isExtended = ps.isExtended,
                        originalName = ps.originalName,
                        minValue = ps.minValue,
                        maxValue = ps.maxValue
                    });
                }
                exportData.groups.Add(dtoGroup);
            }
            exportData.groupFoldouts = persistence.GetGroupFoldouts();
            exportData.groupTestSliders = persistence.GetGroupTestSliders();
            exportData.lockedShapeKeys = persistence.GetLockedShapeKeys();

            Serialization.ApplyExportDataToStateWithOptions(window, exportData, 
                loadGroupStructure, loadShapeKeyValues, loadLockedStates, loadExtendedInfo,
                loadFoldouts, loadTestSliders, loadLockedStatesFromGroups,
                loadGroupNames, loadShapeKeyNames, loadShapeKeyOrder, validateMeshCompatibility);

            // TreeViewを更新（念のため明示的に追加）
            TreeViewPart.Reload();
            window.Repaint();
        }

        /// <summary>
        /// 選択的なデータ読み込み（旧版 - 後方互換性のため）
        /// </summary>
        public static void LoadDataWithOptions(ShapeKeyPersistence persistence, ShapeKeyToolWindow window, 
            bool loadGroups, bool loadFoldouts, bool loadTestSliders, bool loadLockedStates)
        {
            // 旧版の呼び出しを新版に変換
            LoadDataWithOptions(persistence, window, 
                loadGroups, loadGroups, loadLockedStates, loadGroups, 
                loadFoldouts, loadTestSliders, loadGroups,
                loadGroups, loadGroups, loadGroups, loadGroups);
        }
    }
} 