# Contributing

## Commit / PR policy

- 1 機能 / 1 PR を原則に分割してください（最小の差分でレビュー可能にする）
- 大規模変更はフェーズに分け、フェーズごとにテスト可能な状態に保ってください

## Coding style

- C# コードは CSharpier + .editorconfig に準拠
- 命名は英語。UI ラベルは日本語可。ログは一貫ポリシーに従う

## Debug logging

- 冗長ログは `ShapeKeyToolSettings.DebugVerbose` を参照してください
- 直接 `Debug.Log` を呼ばず、`ToolLogger.Verbose/Info/Warn/Error` を使用

## Manual smoke test

リリース前に `README` のスモークテスト項目を手動実施してください


