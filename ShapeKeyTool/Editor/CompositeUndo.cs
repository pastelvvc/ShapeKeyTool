using System;
using UnityEditor;
using UnityEngine;

namespace ShapeKeyTools
{
	internal static class CompositeUndo
	{
		// Undo登録のスロットリング（短時間に大量記録されるのを防ぐ）
		private static double s_lastMeshRecordTime = -1.0;
		private static SkinnedMeshRenderer s_lastRenderer = null;
		private static string s_lastAction = null;
		private const double s_minUndoIntervalSeconds = 0.03; // 約30ms

		private static bool ShouldRegisterMesh(string action)
		{
			if (string.IsNullOrEmpty(action)) return false;
			// メッシュ構造に影響する操作のみメッシュを登録
			// 例: 拡張シェイプキーの作成・削除
			if (action.Contains("Create Extended ShapeKey")) return true;
			if (action.Contains("Delete ShapeKey")) return true;
			return false;
		}

		// バルク操作のグルーピング管理
		private static int s_bulkDepth = 0;

		internal static bool IsBulkActive => s_bulkDepth > 0;

		internal readonly struct BulkScope : IDisposable
		{
			private readonly bool _isOuter;
			private readonly int _groupAtEnter;
			
			public BulkScope(bool isOuter, int groupAtEnter)
			{
				_isOuter = isOuter;
				_groupAtEnter = groupAtEnter;
			}

			public void Dispose()
			{
				s_bulkDepth = Math.Max(0, s_bulkDepth - 1);
				if (_isOuter)
				{
					try
					{
						Undo.CollapseUndoOperations(_groupAtEnter);
					}
					finally
					{
						// 前回値はリセット
						s_lastRenderer = null;
						s_lastAction = null;
						s_lastMeshRecordTime = -1.0;
					}
				}
			}
		}

		internal static BulkScope BulkMeshChange(ShapeKeyToolWindow window, string action)
		{
			if (window == null || window.selectedRenderer == null)
			{
				return new BulkScope(false, 0);
			}

			bool isOuter = s_bulkDepth == 0;
			int groupAtEnter = 0;
			if (isOuter)
			{
				groupAtEnter = Undo.GetCurrentGroup();
				Undo.IncrementCurrentGroup();
				Undo.SetCurrentGroupName(action);

				// 一度だけ対象をUndoに登録
				Undo.RegisterCompleteObjectUndo(window.selectedRenderer, action);
				if (ShouldRegisterMesh(action) && window.selectedRenderer.sharedMesh != null)
				{
					Undo.RegisterCompleteObjectUndo(window.selectedRenderer.sharedMesh, action);
					var persistence = window.selectedRenderer.gameObject.GetComponent<ShapeKeyPersistence>();
					if (persistence != null)
					{
						Undo.RecordObject(persistence, action);
					}
				}
			}

			s_bulkDepth++;
			return new BulkScope(isOuter, groupAtEnter);
		}

		internal static void RecordWindow(ShapeKeyToolWindow window, string action)
		{
			// EditorWindow自体のUndo記録は危険なので削除
			// if (window != null) Undo.RecordObject(window, action);
		}

		internal static void RecordMeshChange(ShapeKeyToolWindow window, string action)
		{
			if (window == null) return;
			if (IsBulkActive) return; // バルク中は開始時の1回のみ記録
			// EditorWindow自体のUndo記録は危険なので削除
			// Undo.RecordObject(window, action);
			if (window.selectedRenderer != null)
			{
				// 直前と同一レンダラー＆アクションが極短時間に連続する場合はスキップ
				double now = EditorApplication.timeSinceStartup;
				if (
					s_lastRenderer == window.selectedRenderer &&
					s_lastAction == action &&
					s_lastMeshRecordTime > 0 &&
					now - s_lastMeshRecordTime < s_minUndoIntervalSeconds
				)
				{
					return;
				}

				Undo.RegisterCompleteObjectUndo(window.selectedRenderer, action);
				if (ShouldRegisterMesh(action) && window.selectedRenderer.sharedMesh != null)
				{
					Undo.RegisterCompleteObjectUndo(window.selectedRenderer.sharedMesh, action);
					var persistence = window.selectedRenderer.gameObject.GetComponent<ShapeKeyPersistence>();
					if (persistence != null)
					{
						Undo.RecordObject(persistence, action);
					}
				}

				// 記録情報を更新
				s_lastRenderer = window.selectedRenderer;
				s_lastAction = action;
				s_lastMeshRecordTime = now;
			}
		}

		internal static void RecordPersistence(ShapeKeyToolWindow window, string action)
		{
			if (window == null || window.selectedRenderer == null) return;
			var persistence = window.selectedRenderer.gameObject.GetComponent<ShapeKeyPersistence>();
			if (persistence != null) Undo.RecordObject(persistence, action);
		}
	}
}


