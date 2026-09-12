@REM Unity プロジェクトの C# コンパイル検査 (Tools/compile-check.ps1 の薄いラッパー)
@REM CI やタスクランナーから .cmd で呼びたい場合に使う。引数はそのまま ps1 へ渡す。
@echo off
chcp 65001 > nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0compile-check.ps1" %*
exit /b %errorlevel%
