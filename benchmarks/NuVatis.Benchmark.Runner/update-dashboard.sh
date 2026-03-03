#!/bin/bash
#
# 벤치마크 결과를 대시보드로 자동 복사
# 작성자: 최진호
# 작성일: 2026-03-04

RESULTS_DIR="./BenchmarkDotNet.Artifacts/results"
DASHBOARD_PUBLIC="D:/jobs/nu/nuvatis-sample/benchmarks/NuVatis.Benchmark.Dashboard/public"

echo "========================================="
echo "벤치마크 결과 → 대시보드 자동 업데이트"
echo "========================================="

# JSON 파일 복사
echo ""
echo "📋 JSON 파일 복사 중..."
cp -v "$RESULTS_DIR"/*-report-full.json "$DASHBOARD_PUBLIC"/

echo ""
echo "✅ 복사 완료!"
echo ""
echo "대시보드 확인:"
echo "  cd D:/jobs/nu/nuvatis-sample/benchmarks/NuVatis.Benchmark.Dashboard"
echo "  npm run dev"
echo ""
