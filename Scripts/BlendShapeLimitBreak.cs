using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ShapeKeyTools
{
    /// <summary>
    /// シェイプキー限界突破ツール
    /// 複数のシェイプキーの変形を合成し、新しいブレンドシェイプとしてメッシュに追加
    /// </summary>
    public static class BlendShapeLimitBreak
    {
        /// <summary>
        /// 拡張シェイプキーを実際のメッシュに追加する（元のシェイプキーの直後に配置）
        /// </summary>
        /// <param name="mesh">対象メッシュ</param>
        /// <param name="shapeKeyName">新しいシェイプキー名</param>
        /// <param name="originalShapeKeyName">元のシェイプキー名</param>
        /// <param name="minValue">最小値</param>
        /// <param name="maxValue">最大値</param>
        /// <returns>成功したかどうか</returns>
        public static bool CreateExtendedShapeKeyInMesh(Mesh mesh, string shapeKeyName, string originalShapeKeyName, float minValue, float maxValue)
        {
            if (mesh == null || string.IsNullOrEmpty(shapeKeyName) || string.IsNullOrEmpty(originalShapeKeyName))
            {
                Debug.LogError("BlendShapeLimitBreak: 無効なパラメータです");
                return false;
            }

            try
            {
                // 元のシェイプキーのインデックスを取得
                int originalIndex = -1;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    if (mesh.GetBlendShapeName(i) == originalShapeKeyName)
                    {
                        originalIndex = i;
                        break;
                    }
                }

                if (originalIndex == -1)
                {
                    Debug.LogError($"BlendShapeLimitBreak: 元のシェイプキー '{originalShapeKeyName}' が見つかりません");
                    return false;
                }

                // 既に存在するかチェック（この時点では既に削除されているはず）
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    if (mesh.GetBlendShapeName(i) == shapeKeyName)
                    {
                        Debug.LogWarning($"BlendShapeLimitBreak: シェイプキー '{shapeKeyName}' は既に存在します（削除処理が不完全です）");
                        return false;
                    }
                }

                // 新しいメッシュを作成して、拡張シェイプキーを元のシェイプキーの直後に配置
                Mesh newMesh = new Mesh();
                newMesh.name = mesh.name + "_Extended";
                
                // 基本メッシュデータをコピー
                newMesh.vertices = mesh.vertices;
                newMesh.normals = mesh.normals;
                newMesh.tangents = mesh.tangents;
                newMesh.uv = mesh.uv;
                newMesh.uv2 = mesh.uv2;
                newMesh.uv3 = mesh.uv3;
                newMesh.uv4 = mesh.uv4;
                newMesh.colors = mesh.colors;
                newMesh.triangles = mesh.triangles;
                newMesh.bounds = mesh.bounds;

                // 元のシェイプキーのフレーム数を取得
                int frameCount = mesh.GetBlendShapeFrameCount(originalIndex);

                // 拡張シェイプキーの頂点データを事前に計算
                Vector3[][] extendedVerticesArray = new Vector3[frameCount][];
                Vector3[][] extendedNormalsArray = new Vector3[frameCount][];
                Vector3[][] extendedTangentsArray = new Vector3[frameCount][];
                float[] extendedWeights = new float[frameCount];

                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    // 元のフレームの頂点データを取得
                    Vector3[] deltaVertices = new Vector3[mesh.vertexCount];
                    Vector3[] deltaNormals = new Vector3[mesh.vertexCount];
                    Vector3[] deltaTangents = new Vector3[mesh.vertexCount];
                    
                    mesh.GetBlendShapeFrameVertices(originalIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                    float originalWeight = mesh.GetBlendShapeFrameWeight(originalIndex, frameIndex);

                    // 拡張範囲に基づいて頂点変形量を調整
                    float scaleFactor = (maxValue - minValue) / 100f; // 100%を基準としたスケール
                    
                    extendedVerticesArray[frameIndex] = new Vector3[mesh.vertexCount];
                    extendedNormalsArray[frameIndex] = new Vector3[mesh.vertexCount];
                    extendedTangentsArray[frameIndex] = new Vector3[mesh.vertexCount];
                    extendedWeights[frameIndex] = originalWeight;

                    for (int i = 0; i < mesh.vertexCount; i++)
                    {
                        extendedVerticesArray[frameIndex][i] = deltaVertices[i] * scaleFactor;
                        extendedNormalsArray[frameIndex][i] = deltaNormals[i] * scaleFactor;
                        extendedTangentsArray[frameIndex][i] = deltaTangents[i] * scaleFactor;
                    }
                }

                // 既存のブレンドシェイプを順番にコピーし、元のシェイプキーの直後に拡張シェイプキーを挿入
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string currentShapeName = mesh.GetBlendShapeName(i);
                    int currentFrameCount = mesh.GetBlendShapeFrameCount(i);

                    // 既存のシェイプキーをコピー
                    for (int frameIndex = 0; frameIndex < currentFrameCount; frameIndex++)
                    {
                        Vector3[] deltaVertices = new Vector3[mesh.vertexCount];
                        Vector3[] deltaNormals = new Vector3[mesh.vertexCount];
                        Vector3[] deltaTangents = new Vector3[mesh.vertexCount];
                        
                        mesh.GetBlendShapeFrameVertices(i, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                        float weight = mesh.GetBlendShapeFrameWeight(i, frameIndex);
                        
                        newMesh.AddBlendShapeFrame(currentShapeName, weight, deltaVertices, deltaNormals, deltaTangents);
                    }

                    // 元のシェイプキーの直後に拡張シェイプキーを挿入
                    if (i == originalIndex)
                    {
                        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                        {
                            newMesh.AddBlendShapeFrame(shapeKeyName, extendedWeights[frameIndex], 
                                extendedVerticesArray[frameIndex], extendedNormalsArray[frameIndex], extendedTangentsArray[frameIndex]);
                        }
                    }
                }

                // 元のメッシュを新しいメッシュで置き換え
                mesh.Clear();
                mesh.vertices = newMesh.vertices;
                mesh.normals = newMesh.normals;
                mesh.tangents = newMesh.tangents;
                mesh.uv = newMesh.uv;
                mesh.uv2 = newMesh.uv2;
                mesh.uv3 = newMesh.uv3;
                mesh.uv4 = newMesh.uv4;
                mesh.colors = newMesh.colors;
                mesh.triangles = newMesh.triangles;
                mesh.bounds = newMesh.bounds;

                // ブレンドシェイプを再追加
                for (int i = 0; i < newMesh.blendShapeCount; i++)
                {
                    string shapeName = newMesh.GetBlendShapeName(i);
                    int frameCount2 = newMesh.GetBlendShapeFrameCount(i);
                    
                    for (int frameIndex = 0; frameIndex < frameCount2; frameIndex++)
                    {
                        Vector3[] deltaVertices = new Vector3[newMesh.vertexCount];
                        Vector3[] deltaNormals = new Vector3[newMesh.vertexCount];
                        Vector3[] deltaTangents = new Vector3[newMesh.vertexCount];
                        
                        newMesh.GetBlendShapeFrameVertices(i, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                        float weight = newMesh.GetBlendShapeFrameWeight(i, frameIndex);
                        
                        mesh.AddBlendShapeFrame(shapeName, weight, deltaVertices, deltaNormals, deltaTangents);
                    }
                }

                Debug.Log($"BlendShapeLimitBreak: 拡張シェイプキー '{shapeKeyName}' をメッシュに追加しました（元のシェイプキーの直後に配置）");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"BlendShapeLimitBreak: エラーが発生しました: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 複数のシェイプキーを合成して新しいシェイプキーを作成
        /// </summary>
        /// <param name="mesh">対象メッシュ</param>
        /// <param name="newShapeName">新しいシェイプキー名</param>
        /// <param name="shapeKeyWeights">シェイプキー名と重みの辞書</param>
        /// <returns>成功したかどうか</returns>
        public static bool MergeBlendShapes(Mesh mesh, string newShapeName, Dictionary<string, float> shapeKeyWeights)
        {
            if (mesh == null || string.IsNullOrEmpty(newShapeName) || shapeKeyWeights == null || shapeKeyWeights.Count == 0)
            {
                Debug.LogError("BlendShapeLimitBreak: 無効なパラメータです");
                return false;
            }

            try
            {
                // 既に存在するかチェック
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    if (mesh.GetBlendShapeName(i) == newShapeName)
                    {
                        Debug.LogWarning($"BlendShapeLimitBreak: シェイプキー '{newShapeName}' は既に存在します");
                        return false;
                    }
                }

                // 各シェイプキーの最大フレーム数を取得
                int maxFrameCount = 0;
                foreach (var kvp in shapeKeyWeights)
                {
                    string shapeKeyName = kvp.Key;
                    for (int i = 0; i < mesh.blendShapeCount; i++)
                    {
                        if (mesh.GetBlendShapeName(i) == shapeKeyName)
                        {
                            int frameCount = mesh.GetBlendShapeFrameCount(i);
                            maxFrameCount = Mathf.Max(maxFrameCount, frameCount);
                            break;
                        }
                    }
                }

                // 各フレームに対して合成シェイプキーを作成
                for (int frameIndex = 0; frameIndex < maxFrameCount; frameIndex++)
                {
                    // 合成用の頂点データ
                    Vector3[] mergedVertices = new Vector3[mesh.vertexCount];
                    Vector3[] mergedNormals = new Vector3[mesh.vertexCount];
                    Vector3[] mergedTangents = new Vector3[mesh.vertexCount];
                    float totalWeight = 0f;
                    int validShapeCount = 0;

                    // 各シェイプキーの変形を重み付きで合成
                    foreach (var kvp in shapeKeyWeights)
                    {
                        string shapeKeyName = kvp.Key;
                        float weight = kvp.Value;

                        // シェイプキーのインデックスを取得
                        int shapeIndex = -1;
                        for (int i = 0; i < mesh.blendShapeCount; i++)
                        {
                            if (mesh.GetBlendShapeName(i) == shapeKeyName)
                            {
                                shapeIndex = i;
                                break;
                            }
                        }

                        if (shapeIndex != -1 && frameIndex < mesh.GetBlendShapeFrameCount(shapeIndex))
                        {
                            // フレームの頂点データを取得
                            Vector3[] deltaVertices = new Vector3[mesh.vertexCount];
                            Vector3[] deltaNormals = new Vector3[mesh.vertexCount];
                            Vector3[] deltaTangents = new Vector3[mesh.vertexCount];
                            
                            mesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                            float frameWeight = mesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex);

                            // 重み付きで合成
                            for (int i = 0; i < mesh.vertexCount; i++)
                            {
                                mergedVertices[i] += deltaVertices[i] * weight;
                                mergedNormals[i] += deltaNormals[i] * weight;
                                mergedTangents[i] += deltaTangents[i] * weight;
                            }

                            totalWeight += frameWeight * weight;
                            validShapeCount++;
                        }
                    }

                    if (validShapeCount > 0)
                    {
                        // 平均重みを計算
                        float averageWeight = totalWeight / validShapeCount;
                        
                        // 新しいブレンドシェイプフレームを追加
                        mesh.AddBlendShapeFrame(newShapeName, averageWeight, mergedVertices, mergedNormals, mergedTangents);
                    }
                }

                Debug.Log($"BlendShapeLimitBreak: 合成シェイプキー '{newShapeName}' をメッシュに追加しました");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"BlendShapeLimitBreak: エラーが発生しました: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 拡張シェイプキーを実際のメッシュに反映
        /// </summary>
        /// <param name="renderer">対象のSkinnedMeshRenderer</param>
        /// <param name="extendedShapeKeyName">拡張シェイプキー名</param>
        /// <param name="originalShapeKeyName">元のシェイプキー名</param>
        /// <param name="minValue">最小値</param>
        /// <param name="maxValue">最大値</param>
        /// <returns>成功したかどうか</returns>
        public static bool ApplyExtendedShapeKeyToMesh(SkinnedMeshRenderer renderer, string extendedShapeKeyName, string originalShapeKeyName, float minValue, float maxValue)
        {
            if (renderer == null || renderer.sharedMesh == null)
            {
                Debug.LogError("BlendShapeLimitBreak: 無効なSkinnedMeshRendererです");
                return false;
            }

            // メッシュのインスタンスを作成（共有メッシュを変更しないため）
            Mesh meshInstance = Object.Instantiate(renderer.sharedMesh);
            
            bool success = CreateExtendedShapeKeyInMesh(meshInstance, extendedShapeKeyName, originalShapeKeyName, minValue, maxValue);
            
            if (success)
            {
                // 新しいメッシュインスタンスをレンダラーに設定
                renderer.sharedMesh = meshInstance;
                
                // 拡張シェイプキーの重みを設定
                for (int i = 0; i < meshInstance.blendShapeCount; i++)
                {
                    if (meshInstance.GetBlendShapeName(i) == extendedShapeKeyName)
                    {
                        renderer.SetBlendShapeWeight(i, 0f); // 初期値は0
                        break;
                    }
                }
            }

            return success;
        }

        /// <summary>
        /// メッシュからブレンドシェイプを削除する（元のメッシュを保持）
        /// </summary>
        /// <param name="renderer">対象のSkinnedMeshRenderer</param>
        /// <param name="shapeKeyNameToRemove">削除するシェイプキー名</param>
        /// <returns>成功したかどうか</returns>
        public static bool RemoveBlendShapeFromMesh(SkinnedMeshRenderer renderer, string shapeKeyNameToRemove)
        {
            if (renderer == null || renderer.sharedMesh == null)
            {
                Debug.LogError("BlendShapeLimitBreak: 無効なSkinnedMeshRendererです");
                return false;
            }

            try
            {
                var originalMesh = renderer.sharedMesh;
                
                // 削除対象のシェイプキーのインデックスを取得
                int targetIndex = -1;
                for (int i = 0; i < originalMesh.blendShapeCount; i++)
                {
                    if (originalMesh.GetBlendShapeName(i) == shapeKeyNameToRemove)
                    {
                        targetIndex = i;
                        break;
                    }
                }

                if (targetIndex == -1)
                {
                    Debug.LogWarning($"BlendShapeLimitBreak: シェイプキー '{shapeKeyNameToRemove}' が見つかりません");
                    return false;
                }

                // 元のメッシュのインスタンスを作成して、元のメッシュの参照を保持
                Mesh newMesh = Object.Instantiate(originalMesh);
                newMesh.name = originalMesh.name;
                
                // 削除対象以外のブレンドシェイプのみを保持
                var shapesToKeep = new List<(string name, Vector3[] vertices, Vector3[] normals, Vector3[] tangents, float weight)>();
                
                for (int i = 0; i < originalMesh.blendShapeCount; i++)
                {
                    if (i != targetIndex)
                    {
                        string shapeKeyName = originalMesh.GetBlendShapeName(i);
                        int frameCount = originalMesh.GetBlendShapeFrameCount(i);
                        
                        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                        {
                            Vector3[] deltaVertices = new Vector3[originalMesh.vertexCount];
                            Vector3[] deltaNormals = new Vector3[originalMesh.vertexCount];
                            Vector3[] deltaTangents = new Vector3[originalMesh.vertexCount];
                            
                            originalMesh.GetBlendShapeFrameVertices(i, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                            float weight = originalMesh.GetBlendShapeFrameWeight(i, frameIndex);
                            
                            shapesToKeep.Add((shapeKeyName, deltaVertices, deltaNormals, deltaTangents, weight));
                        }
                    }
                }
                
                // 新しいメッシュをクリアして保持するシェイプキーのみを追加
                newMesh.ClearBlendShapes();
                
                foreach (var shape in shapesToKeep)
                {
                    newMesh.AddBlendShapeFrame(shape.name, shape.weight, shape.vertices, shape.normals, shape.tangents);
                }
                
                // 新しいメッシュをレンダラーに設定
                renderer.sharedMesh = newMesh;
                
                Debug.Log($"BlendShapeLimitBreak: シェイプキー '{shapeKeyNameToRemove}' をメッシュから削除しました");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"BlendShapeLimitBreak: シェイプキー削除でエラーが発生しました: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// メッシュから複数の拡張シェイプキーを一括削除する（元のメッシュを保持）
        /// </summary>
        /// <param name="renderer">対象のSkinnedMeshRenderer</param>
        /// <param name="shapeKeyNamesToRemove">削除するシェイプキー名のリスト</param>
        /// <returns>成功したかどうか</returns>
        public static bool RemoveMultipleBlendShapesFromMesh(SkinnedMeshRenderer renderer, List<string> shapeKeyNamesToRemove)
        {
            if (renderer == null || renderer.sharedMesh == null)
            {
                Debug.LogError("BlendShapeLimitBreak: 無効なSkinnedMeshRendererです");
                return false;
            }

            if (shapeKeyNamesToRemove == null || shapeKeyNamesToRemove.Count == 0)
            {
                Debug.LogWarning("BlendShapeLimitBreak: 削除対象のシェイプキーが指定されていません");
                return true;
            }

            try
            {
                var originalMesh = renderer.sharedMesh;
                
                // 新しいメッシュを作成（元のメッシュの参照を保持）
                Mesh newMesh = Object.Instantiate(originalMesh);
                newMesh.name = originalMesh.name + "_BulkModified";
                
                // 削除対象以外のブレンドシェイプのみを保持
                var shapesToKeep = new List<(string name, Vector3[] vertices, Vector3[] normals, Vector3[] tangents, float weight)>();
                
                for (int i = 0; i < originalMesh.blendShapeCount; i++)
                {
                    string shapeKeyName = originalMesh.GetBlendShapeName(i);
                    
                    if (!shapeKeyNamesToRemove.Contains(shapeKeyName))
                    {
                        int frameCount = originalMesh.GetBlendShapeFrameCount(i);
                        
                        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                        {
                            Vector3[] deltaVertices = new Vector3[originalMesh.vertexCount];
                            Vector3[] deltaNormals = new Vector3[originalMesh.vertexCount];
                            Vector3[] deltaTangents = new Vector3[originalMesh.vertexCount];
                            
                            originalMesh.GetBlendShapeFrameVertices(i, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                            float weight = originalMesh.GetBlendShapeFrameWeight(i, frameIndex);
                            
                            shapesToKeep.Add((shapeKeyName, deltaVertices, deltaNormals, deltaTangents, weight));
                        }
                    }
                }
                
                // 新しいメッシュをクリアして保持するシェイプキーのみを追加
                newMesh.ClearBlendShapes();
                
                foreach (var shape in shapesToKeep)
                {
                    newMesh.AddBlendShapeFrame(shape.name, shape.weight, shape.vertices, shape.normals, shape.tangents);
                }
                
                // 新しいメッシュをレンダラーに設定
                renderer.sharedMesh = newMesh;
                
                Debug.Log($"BlendShapeLimitBreak: {shapeKeyNamesToRemove.Count}個のシェイプキーをメッシュから一括削除しました");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"BlendShapeLimitBreak: シェイプキー一括削除でエラーが発生しました: {e.Message}");
                return false;
            }
        }
    }
}

