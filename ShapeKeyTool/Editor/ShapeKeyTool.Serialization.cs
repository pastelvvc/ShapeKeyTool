using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

namespace ShapeKeyTools
{
    /// <summary>
    /// JSON シリアライズ用のデータ構造（手動保存と同じ構造）
    /// </summary>
    [System.Serializable]
    internal class JsonExportData
    {
        public List<ShapeKeyPersistence.GroupData> groups = new List<ShapeKeyPersistence.GroupData>();
        public Dictionary<string, bool> groupFoldouts = new Dictionary<string, bool>();
        public Dictionary<string, float> groupTestSliders = new Dictionary<string, float>();
        public Dictionary<int, bool> lockedShapeKeys = new Dictionary<int, bool>();
    }

    /// <summary>
    /// シリアライゼーション関連のユーティリティ
    /// </summary>
    internal static class Serialization
    {
        internal static void ExportJson(ShapeKeyToolWindow window)
        {
            var exportData = BuildExportDataFromState(window);

            string path = EditorUtility.SaveFilePanel("設定をJSONとして保存", "", "ShapeKeySettings.json", "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string json = JsonUtility.ToJson(exportData, true);
                File.WriteAllText(path, json);
                
                EditorUtility.DisplayDialog(
                    "エクスポート完了",
                    "シェイプキー設定をJSONファイルに保存しました。",
                    "OK"
                );
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "エラー",
                    $"JSONエクスポートに失敗しました:\n{ex.Message}",
                    "OK"
                );
            }
        }

        internal static void ImportJson(ShapeKeyToolWindow window)
        {
            string path = EditorUtility.OpenFilePanel("設定JSONを読み込む", "", "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                var exportData = JsonUtility.FromJson<JsonExportData>(json);
                if (exportData == null)
                {
                    EditorUtility.DisplayDialog(
                        "エラー",
                        "JSONファイルの形式が正しくありません。",
                        "OK"
                    );
                    return;
                }

                ApplyExportDataToState(window, exportData);

                // 画面更新
                TreeViewPart.Reload();
                window.Repaint();
                
                EditorUtility.DisplayDialog(
                    "インポート完了",
                    "シェイプキー設定をJSONファイルから読み込みました。",
                    "OK"
                );
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "エラー",
                    $"JSONインポートに失敗しました:\n{ex.Message}",
                    "OK"
                );
            }
        }

        private static JsonExportData BuildExportDataFromState(ShapeKeyToolWindow window)
        {
            var exportData = new JsonExportData();

            // グループデータを保存（手動保存と同じ構造）
            foreach (var group in window.groupedShapes)
            {
                var groupData = new ShapeKeyPersistence.GroupData
                {
                    groupName = group.Key,
                    shapeKeys = new List<ShapeKeyPersistence.ShapeKeyData>()
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

                exportData.groups.Add(groupData);
            }

            // グループの展開状態を保存
            foreach (var foldout in window.groupFoldouts)
            {
                exportData.groupFoldouts[foldout.Key] = foldout.Value;
            }

            // テストスライダーの値を保存
            foreach (var slider in window.groupTestSliders)
            {
                exportData.groupTestSliders[slider.Key] = slider.Value;
            }

            // ロック状態を保存
            foreach (var locked in window.lockedShapeKeys)
            {
                exportData.lockedShapeKeys[locked.Key] = locked.Value;
            }

            return exportData;
        }

        internal static void ApplyExportDataToState(ShapeKeyToolWindow window, JsonExportData exportData)
        {
            ApplyExportDataToStateWithOptions(window, exportData, true, true, true, true, true, true, true, true, true, true, true);
        }

        internal static JsonExportData CreateExportData()
        {
            return new JsonExportData();
        }

        internal static void ApplyExportDataToStateWithOptions(ShapeKeyToolWindow window, JsonExportData exportData,
            bool loadGroupStructure, bool loadShapeKeyValues, bool loadLockedStates, bool loadExtendedInfo,
            bool loadFoldouts, bool loadTestSliders, bool loadLockedStatesFromGroups,
            bool loadGroupNames, bool loadShapeKeyNames, bool loadShapeKeyOrder, bool validateMeshCompatibility)
        {
            // 手動保存と同じロジックで処理（persistence.HasData()チェックを除く）
            var groups = exportData.groups;
            var groupFoldouts = exportData.groupFoldouts;
            var groupTestSliders = exportData.groupTestSliders;
            var lockedShapeKeys = exportData.lockedShapeKeys;

            // グループ構成を復元
            if (loadGroupStructure)
            {
                // 現在のメッシュの状態を確認
                if (window.selectedRenderer == null || window.sharedMesh == null)
                {
                    EditorUtility.DisplayDialog(
                        "エラー",
                        "メッシュが選択されていません。",
                        "OK"
                    );
                    return;
                }

                // 現在のメッシュに存在するシェイプキーのリストを作成
                var existingShapeKeys = new HashSet<string>();
                for (int i = 0; i < window.sharedMesh.blendShapeCount; i++)
                {
                    existingShapeKeys.Add(window.sharedMesh.GetBlendShapeName(i));
                }

                // 保存されたデータの整合性をチェック
                if (validateMeshCompatibility)
                {
                    var missingShapeKeys = new List<string>();
                    var validGroups = new List<ShapeKeyPersistence.GroupData>();

                    // デバッグ用：現在のメッシュのシェイプキー名をログ出力（verbose）
                    ToolLogger.Verbose("現在のメッシュのシェイプキー:");
                    foreach (var meshShapeName in existingShapeKeys)
                    {
                        ToolLogger.Verbose($"  - {meshShapeName}");
                    }

                    foreach (var groupData in groups)
                    {
                        var validShapeKeys = new List<ShapeKeyPersistence.ShapeKeyData>();
                        foreach (var shapeData in groupData.shapeKeys)
                        {
                            // デバッグ用：保存されたデータの名前をログ出力（verbose）
                            ToolLogger.Verbose($"チェック中: name='{shapeData.name}', originalName='{shapeData.originalName}'");
                            
                            // 名前またはoriginalNameで存在チェック（大文字小文字を無視）
                            bool exists = existingShapeKeys.Any(key => string.Equals(key, shapeData.name, StringComparison.OrdinalIgnoreCase));
                            if (!exists && !string.IsNullOrEmpty(shapeData.originalName))
                            {
                                exists = existingShapeKeys.Any(key => string.Equals(key, shapeData.originalName, StringComparison.OrdinalIgnoreCase));
                            }
                            
                            if (exists)
                            {
                                validShapeKeys.Add(shapeData);
                                ToolLogger.Verbose($"  存在確認: {shapeData.name}");
                            }
                            else
                            {
                                missingShapeKeys.Add(shapeData.name);
                                ToolLogger.Verbose($"  存在しない: {shapeData.name}");
                            }
                        }

                        if (validShapeKeys.Count > 0)
                        {
                            var validGroup = new ShapeKeyPersistence.GroupData
                            {
                                groupName = groupData.groupName,
                                shapeKeys = validShapeKeys
                            };
                            validGroups.Add(validGroup);
                        }
                    }

                    // 存在しないシェイプキーがある場合は警告を表示
                    if (missingShapeKeys.Count > 0)
                    {
                        string missingList = string.Join("\n• ", missingShapeKeys.Take(10));
                        if (missingShapeKeys.Count > 10)
                        {
                            missingList += $"\n... 他 {missingShapeKeys.Count - 10} 個";
                        }

                        bool continueLoading = EditorUtility.DisplayDialog(
                            "シェイプキーの整合性チェック",
                            $"以下のシェイプキーが現在のメッシュに存在しません：\n\n• {missingList}\n\n" +
                            "これらのシェイプキーをスキップして読み込みを続行しますか？",
                            "続行",
                            "キャンセル"
                        );

                        if (!continueLoading)
                        {
                            return;
                        }

                        // 有効なグループのみを使用
                        groups = validGroups;
                    }
                }

                window.groupedShapes.Clear();
                foreach (var groupData in groups)
                {
                    var shapeList = new List<BlendShape>();
                    foreach (var shapeData in groupData.shapeKeys)
                    {
                        // 現在のメッシュに存在するシェイプキーのみを読み込み（大文字小文字を無視）
                        bool exists = existingShapeKeys.Any(key => string.Equals(key, shapeData.name, StringComparison.OrdinalIgnoreCase));
                        if (!exists && !string.IsNullOrEmpty(shapeData.originalName))
                        {
                            exists = existingShapeKeys.Any(key => string.Equals(key, shapeData.originalName, StringComparison.OrdinalIgnoreCase));
                        }
                        
                        if (exists)
                        {
                            // シェイプキーのインデックスを取得（大文字小文字を無視）
                            int shapeIndex = -1;
                            for (int i = 0; i < window.sharedMesh.blendShapeCount; i++)
                            {
                                string meshShapeName = window.sharedMesh.GetBlendShapeName(i);
                                if (string.Equals(meshShapeName, shapeData.name, StringComparison.OrdinalIgnoreCase) || 
                                    string.Equals(meshShapeName, shapeData.originalName, StringComparison.OrdinalIgnoreCase))
                                {
                                    shapeIndex = i;
                                    break;
                                }
                            }

                            // インデックスが見つからない場合はスキップ
                            if (shapeIndex == -1)
                                continue;

                            var shape = new BlendShape
                            {
                                name = shapeData.name, // 常に名前を設定（必須）
                                index = shapeIndex, // インデックスを設定
                                weight = loadShapeKeyValues ? shapeData.weight : window.selectedRenderer.GetBlendShapeWeight(shapeIndex),
                                isLocked = loadLockedStatesFromGroups ? shapeData.isLocked : false,
                                isExtended = loadExtendedInfo ? shapeData.isExtended : false,
                                originalName = loadExtendedInfo ? shapeData.originalName : "",
                                minValue = loadExtendedInfo ? shapeData.minValue : 0f,
                                maxValue = loadExtendedInfo ? shapeData.maxValue : 100f
                            };
                            shapeList.Add(shape);
                        }
                    }

                    // 空でないグループのみを追加
                    if (shapeList.Count > 0)
                    {
                        string groupName = loadGroupNames ? groupData.groupName : "その他";
                        window.groupedShapes[groupName] = shapeList;
                    }
                }

                // グループ化処理を実行して整合性を保つ（読み込んだデータを保持）
                if (loadShapeKeyOrder)
                {
                    // 読み込んだグループ情報を保持しながらグループ化
                    var loadedGroups = new Dictionary<string, List<BlendShape>>(window.groupedShapes);
                    
                    ToolLogger.Verbose($"GroupShapes実行前: {loadedGroups.Count}グループ");
                    foreach (var group in loadedGroups)
                    {
                        ToolLogger.Verbose($"  {group.Key}: {group.Value.Count}個");
                    }
                    
                    // グループ化処理を実行するが、読み込んだグループ名を保持
                    window.GroupShapesPreserveLoadedGroups(loadedGroups);
                    
                    ToolLogger.Verbose($"GroupShapes実行後: {window.groupedShapes.Count}グループ");
                    foreach (var group in window.groupedShapes)
                    {
                        ToolLogger.Verbose($"  {group.Key}: {group.Value.Count}個");
                    }
                }
                else
                {
                    // グループ化処理を実行しない場合は、現在のメッシュの状態を更新
                    window.UpdateBlendShapes();
                }
            }

            // グループの展開状態を復元
            if (loadFoldouts)
            {
                window.groupFoldouts.Clear();
                foreach (var foldout in groupFoldouts)
                {
                    window.groupFoldouts[foldout.Key] = foldout.Value;
                }
            }

            // テストスライダーの値を復元
            if (loadTestSliders)
            {
                window.groupTestSliders.Clear();
                foreach (var slider in groupTestSliders)
                {
                    window.groupTestSliders[slider.Key] = slider.Value;
                }
            }

            // ロック状態を復元（グループデータから）
            if (loadLockedStatesFromGroups && loadGroupStructure)
            {
                window.lockedShapeKeys.Clear();
                foreach (var groupData in groups)
                {
                    foreach (var shapeData in groupData.shapeKeys)
                    {
                        // シェイプキーのインデックスを取得
                        if (window.selectedRenderer != null && window.sharedMesh != null)
                        {
                            for (int i = 0; i < window.sharedMesh.blendShapeCount; i++)
                            {
                                string meshShapeName = window.sharedMesh.GetBlendShapeName(i);
                                if (string.Equals(meshShapeName, shapeData.name, StringComparison.OrdinalIgnoreCase) || 
                                    string.Equals(meshShapeName, shapeData.originalName, StringComparison.OrdinalIgnoreCase))
                                {
                                    window.lockedShapeKeys[i] = shapeData.isLocked;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // ロック状態を復元（独立したロック状態データから）
            if (loadLockedStates)
            {
                foreach (var locked in lockedShapeKeys)
                {
                    window.lockedShapeKeys[locked.Key] = locked.Value;
                }
            }

            // TreeViewを更新
            TreeViewPart.Reload();
            window.Repaint();
        }
    }
} 