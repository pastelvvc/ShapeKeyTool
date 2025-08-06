using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ShapeKeyTools
{
    /// <summary>
    /// シェイプキー情報を永続化するコンポーネント
    /// </summary>
    public class ShapeKeyPersistence : MonoBehaviour
    {
        [System.Serializable]
        public class ShapeKeyData
        {
            public string name;
            public float weight;
            public bool isLocked;
            public bool isExtended;
            public string originalName;
            public float minValue;
            public float maxValue;
        }

        [System.Serializable]
        public class GroupData
        {
            public string groupName;
            public List<ShapeKeyData> shapeKeys = new List<ShapeKeyData>();
        }

        [Header("シェイプキー永続化データ")]
        [SerializeField] private List<GroupData> groups = new List<GroupData>();
        [SerializeField] private Dictionary<string, bool> groupFoldouts = new Dictionary<string, bool>();
        [SerializeField] private Dictionary<string, float> groupTestSliders = new Dictionary<string, float>();
        [SerializeField] private Dictionary<int, bool> lockedShapeKeys = new Dictionary<int, bool>();

        /// <summary>
        /// グループデータを取得
        /// </summary>
        public List<GroupData> GetGroups()
        {
            return groups;
        }

        /// <summary>
        /// グループデータを設定
        /// </summary>
        public void SetGroups(List<GroupData> newGroups)
        {
            groups = newGroups ?? new List<GroupData>();
        }

        /// <summary>
        /// グループの展開状態を取得
        /// </summary>
        public Dictionary<string, bool> GetGroupFoldouts()
        {
            return groupFoldouts;
        }

        /// <summary>
        /// グループの展開状態を設定
        /// </summary>
        public void SetGroupFoldouts(Dictionary<string, bool> foldouts)
        {
            groupFoldouts = foldouts ?? new Dictionary<string, bool>();
        }

        /// <summary>
        /// テストスライダーの値を取得
        /// </summary>
        public Dictionary<string, float> GetGroupTestSliders()
        {
            return groupTestSliders;
        }

        /// <summary>
        /// テストスライダーの値を設定
        /// </summary>
        public void SetGroupTestSliders(Dictionary<string, float> sliders)
        {
            groupTestSliders = sliders ?? new Dictionary<string, float>();
        }

        /// <summary>
        /// ロック状態を取得
        /// </summary>
        public Dictionary<int, bool> GetLockedShapeKeys()
        {
            return lockedShapeKeys;
        }

        /// <summary>
        /// ロック状態を設定
        /// </summary>
        public void SetLockedShapeKeys(Dictionary<int, bool> locked)
        {
            lockedShapeKeys = locked ?? new Dictionary<int, bool>();
        }

        /// <summary>
        /// データをクリア
        /// </summary>
        public void ClearData()
        {
            groups.Clear();
            groupFoldouts.Clear();
            groupTestSliders.Clear();
            lockedShapeKeys.Clear();
        }

        /// <summary>
        /// データが存在するかチェック
        /// </summary>
        public bool HasData()
        {
            return groups != null && groups.Count > 0;
        }
    }
} 