# Contribution Guide

## 実装フロー

`issueを作成` → `main` から `branch` を作成 → その `branch` から `develop` に PR → ある程度しっかりしてきたら `develop` から `main` に PR

## Issue ルール

- `title` には種別を明示する（例: `feature`, `fix`, `docs`, `chore`, `assets`）
- `本文` にはブランチ名候補を記載する

## ブランチ命名規則

- 形式: `[suffix]/[issue-number]/[branch-name]`
- `issue-number` は 3 桁ゼロ埋め推奨（例: `048`）
- `branch-name` はケバブケースを使用する

例:

- `feature/048/player-movement`
- `fix/102/null-reference-on-start`
