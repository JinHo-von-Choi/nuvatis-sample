# DLL 차단 해제 스크립트
# Windows Defender Application Control Policy가 DLL 로딩을 차단하는 문제 해결
#
# 작성자: 최진호
# 작성일: 2026-03-04

$ErrorActionPreference = "Continue"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "DLL 차단 해제 중..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# bin/Release/net8.0 디렉토리의 모든 DLL 차단 해제
$dllPath = "$PSScriptRoot\..\bin\Release\net8.0"

if (Test-Path $dllPath) {
    $dlls = Get-ChildItem -Path $dllPath -Filter "*.dll" -Recurse

    $unblocked = 0
    $alreadyUnblocked = 0
    $failed = 0

    foreach ($dll in $dlls) {
        try {
            # Zone.Identifier 스트림 확인 (차단 여부)
            $zoneId = Get-Content -Path $dll.FullName -Stream Zone.Identifier -ErrorAction SilentlyContinue

            if ($zoneId) {
                # 차단되어 있음 → 차단 해제
                Unblock-File -Path $dll.FullName -ErrorAction Stop
                Write-Host "✓ 차단 해제: $($dll.Name)" -ForegroundColor Green
                $unblocked++
            } else {
                # 이미 차단 해제됨
                $alreadyUnblocked++
            }
        } catch {
            Write-Host "✗ 차단 해제 실패: $($dll.Name) - $($_.Exception.Message)" -ForegroundColor Red
            $failed++
        }
    }

    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "결과 요약:" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "총 DLL 수:       $($dlls.Count)" -ForegroundColor White
    Write-Host "차단 해제됨:     $unblocked" -ForegroundColor Green
    Write-Host "이미 해제됨:     $alreadyUnblocked" -ForegroundColor Gray
    Write-Host "실패:            $failed" -ForegroundColor Red
    Write-Host "========================================`n" -ForegroundColor Cyan

    if ($failed -gt 0) {
        Write-Host "⚠ 일부 DLL 차단 해제에 실패했습니다." -ForegroundColor Yellow
        Write-Host "관리자 권한으로 PowerShell을 실행한 후 다시 시도하세요." -ForegroundColor Yellow
        Write-Host "`n명령어: Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass" -ForegroundColor White
        exit 1
    } elseif ($unblocked -gt 0) {
        Write-Host "✓ 모든 DLL 차단이 해제되었습니다!" -ForegroundColor Green
        Write-Host "이제 벤치마크를 실행할 수 있습니다." -ForegroundColor Green
        exit 0
    } else {
        Write-Host "✓ 모든 DLL이 이미 차단 해제되어 있습니다." -ForegroundColor Green
        exit 0
    }
} else {
    Write-Host "✗ bin/Release/net8.0 디렉토리를 찾을 수 없습니다." -ForegroundColor Red
    Write-Host "먼저 'dotnet build -c Release'를 실행하세요." -ForegroundColor Yellow
    exit 1
}
