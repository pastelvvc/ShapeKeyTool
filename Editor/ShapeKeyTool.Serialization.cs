using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ShapeKeyTools
{
    /// <summary>
    /// JSON シリアライズ用 DTO
    /// </summary>
    [System.Serializable]
    internal class SerializedState
    {
        public List<GroupDTO> groups = new List<GroupDTO>();
        public bool skipNonZeroValues = true;

    }

    [System.Serializable]
    internal class GroupDTO
    {
        public string name;
        public bool foldout;
        public float testSlider;
        public List<ShapeDTO> shapes = new List<ShapeDTO>();
    }

    [System.Serializable]
    internal class ShapeDTO
    {
        public string name;
        public float weight;
        public int index;
        public bool isLocked;
        
        // 拡張シェイプキー用のプロパティ
        public bool isExtended = false;
        public int minValue = -100;
        public int maxValue = 200;
        public string originalName = "";
    }

    /// <summary>
    /// シリアライゼーション関連のユーティリティ
    /// </summary>
    internal static class Serialization
    {
        internal static void ExportJson(ShapeKeyToolWindow window)
        {
            var state = BuildDTOFromState(window);

            string path = EditorUtility.SaveFilePanel("設定をJSONとして保存", "", "ShapeKeySettings.json", "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string json = JsonUtility.ToJson(state, true);
                File.WriteAllText(path, json);
            }
            catch (System.Exception ex)
            {
                // エラー処理
            }
        }

        internal static void ImportJson(ShapeKeyToolWindow window)
        {
            string path = EditorUtility.OpenFilePanel("設定JSONを読み込む", "", "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                var state = JsonUtility.FromJson<SerializedState>(json);
                if (state == null)
                {
                    return;
                }

                ApplyDTOToState(window, state);

                // 画面更新
                TreeViewPart.Reload();
                window.Repaint();
            }
            catch (System.Exception ex)
            {
                // エラー処理
            }
        }

        private static SerializedState BuildDTOFromState(ShapeKeyToolWindow window)
        {
            var dto = new SerializedState();
            dto.skipNonZeroValues = window.skipNonZeroValues;

            // 永続化マネージャーから拡張シェイプキーの情報を取得
            var extendedShapeKeys = ExtendedShapeKeyManager.GetAllExtendedShapeKeys();

            foreach (var group in window.groupedShapes)
            {
                var g = new GroupDTO();
                g.name = group.Key;
                g.foldout = window.groupFoldouts.ContainsKey(group.Key) ? window.groupFoldouts[group.Key] : true;
                g.testSlider = window.groupTestSliders.ContainsKey(group.Key) ? window.groupTestSliders[group.Key] : 0f;

                foreach (var s in group.Value)
                {
                    var sd = new ShapeDTO
                    {
                        name = s.name,
                        weight = s.weight,
                        index = s.index,
                        isLocked = s.isLocked,
                        isExtended = s.isExtended,
                        minValue = s.minValue,
                        maxValue = s.maxValue,
                        originalName = s.originalName
                    };
                    g.shapes.Add(sd);
                }

                dto.groups.Add(g);
            }

            return dto;
        }

        private static void ApplyDTOToState(ShapeKeyToolWindow window, SerializedState state)
        {
            // 既存状態をクリア
            window.groupedShapes.Clear();
            window.groupFoldouts.Clear();
            window.groupTestSliders.Clear();
            window.originalWeights.Clear();
            window.lockedShapeKeys.Clear();

            window.skipNonZeroValues = state.skipNonZeroValues;

            // 永続化マネージャーをクリア
            ExtendedShapeKeyManager.ClearAll();

            // DTOから復元（順序を保持）
            foreach (var g in state.groups)
            {
                var list = new List<BlendShape>();
                foreach (var sd in g.shapes)
                {
                    var bs = new BlendShape
                    {
                        name = sd.name,
                        weight = sd.weight,
                        index = sd.index,
                        isLocked = sd.isLocked,
                        isExtended = sd.isExtended,
                        minValue = sd.minValue,
                        maxValue = sd.maxValue,
                        originalName = sd.originalName
                    };
                    list.Add(bs);

                    // 拡張シェイプキーの場合、永続化マネージャーに登録
                    if (bs.isExtended)
                    {
                        var extendedInfo = new ExtendedShapeKeyInfo(bs.originalName, bs.minValue, bs.maxValue);
                        ExtendedShapeKeyManager.RegisterExtendedShapeKey(bs.name, extendedInfo);
                    }

                    // 実インデックスに対してロック辞書も同期
                    if (bs.index >= 0)
                    {
                        window.lockedShapeKeys[bs.index] = bs.isLocked;
                    }
                }

                window.groupedShapes[g.name] = list;
                window.groupFoldouts[g.name] = g.foldout;
                window.groupTestSliders[g.name] = g.testSlider;
            }
        }
    }
} 