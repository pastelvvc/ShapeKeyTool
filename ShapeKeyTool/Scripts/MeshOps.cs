using UnityEngine;
using System.Collections.Generic;

namespace ShapeKeyTools
{
	/// <summary>
	/// 低レベルなメッシュ操作ユーティリティ（UI/Editorから遮断）
	/// </summary>
	internal static class MeshOps
	{
		internal static Mesh DuplicateMesh(Mesh source)
		{
			if (source == null) return null;
			// 計測ポイント（必要ならStopwatchで詳細取得に切替）
			var start = Time.realtimeSinceStartup;
			var copy = Object.Instantiate(source);
			var elapsed = (Time.realtimeSinceStartup - start) * 1000f;
			// パフォーマンス計測ログ（5ms以上かかった場合のみ）
			if (elapsed > 5f)
			{
				Debug.Log($"MeshOps.DuplicateMesh: {elapsed:F2} ms");
			}
			return copy;
		}

		internal static int FindBlendShapeIndex(Mesh mesh, string shapeName)
		{
			if (mesh == null || string.IsNullOrEmpty(shapeName)) return -1;
			for (int i = 0; i < mesh.blendShapeCount; i++)
			{
				if (mesh.GetBlendShapeName(i) == shapeName) return i;
			}
			return -1;
		}

		internal static Mesh BuildMeshWithInsertedExtended(Mesh source, string extendedName, string originalName, float scaleFactor)
		{
			if (source == null) return null;
			int originalIndex = FindBlendShapeIndex(source, originalName);
			if (originalIndex == -1) return null;

			Mesh newMesh = new Mesh();
			newMesh.name = source.name + "_Extended";
			// コピー
			newMesh.vertices = source.vertices;
			newMesh.normals = source.normals;
			newMesh.tangents = source.tangents;
			newMesh.uv = source.uv;
			newMesh.uv2 = source.uv2;
			newMesh.uv3 = source.uv3;
			newMesh.uv4 = source.uv4;
			newMesh.colors = source.colors;
			newMesh.triangles = source.triangles;
			newMesh.bounds = source.bounds;

			int frameCount = source.GetBlendShapeFrameCount(originalIndex);
			// 既存コピーと挿入
			for (int i = 0; i < source.blendShapeCount; i++)
			{
				string currentShape = source.GetBlendShapeName(i);
				int currentFrameCount = source.GetBlendShapeFrameCount(i);
				for (int f = 0; f < currentFrameCount; f++)
				{
					var dv = new Vector3[source.vertexCount];
					var dn = new Vector3[source.vertexCount];
					var dt = new Vector3[source.vertexCount];
					source.GetBlendShapeFrameVertices(i, f, dv, dn, dt);
					float w = source.GetBlendShapeFrameWeight(i, f);
					newMesh.AddBlendShapeFrame(currentShape, w, dv, dn, dt);
				}

				if (i == originalIndex)
				{
					for (int f = 0; f < frameCount; f++)
					{
						var dv = new Vector3[source.vertexCount];
						var dn = new Vector3[source.vertexCount];
						var dt = new Vector3[source.vertexCount];
						source.GetBlendShapeFrameVertices(originalIndex, f, dv, dn, dt);
						float w = source.GetBlendShapeFrameWeight(originalIndex, f);
						for (int vi = 0; vi < source.vertexCount; vi++)
						{
							dv[vi] *= scaleFactor;
							dn[vi] *= scaleFactor;
							dt[vi] *= scaleFactor;
						}
						newMesh.AddBlendShapeFrame(extendedName, w, dv, dn, dt);
					}
				}
			}

			return newMesh;
		}

		internal static Mesh BuildMeshWithoutShapes(Mesh source, HashSet<string> namesToRemove)
		{
			if (source == null) return null;
			Mesh newMesh = Object.Instantiate(source);
			newMesh.name = source.name + "_Modified";
			newMesh.ClearBlendShapes();
			for (int i = 0; i < source.blendShapeCount; i++)
			{
				string shape = source.GetBlendShapeName(i);
				if (namesToRemove != null && namesToRemove.Contains(shape)) continue;
				int frameCount = source.GetBlendShapeFrameCount(i);
				for (int f = 0; f < frameCount; f++)
				{
					var dv = new Vector3[source.vertexCount];
					var dn = new Vector3[source.vertexCount];
					var dt = new Vector3[source.vertexCount];
					source.GetBlendShapeFrameVertices(i, f, dv, dn, dt);
					float w = source.GetBlendShapeFrameWeight(i, f);
					newMesh.AddBlendShapeFrame(shape, w, dv, dn, dt);
				}
			}
			return newMesh;
		}
	}
}



