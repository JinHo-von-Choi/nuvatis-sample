#!/bin/bash
# 빠른 검증 스크립트 - 각 카테고리별 하나씩만 실행
# 작성자: 최진호
# 작성일: 2026-03-04

echo "========================================="
echo "벤치마크 DB 매핑 검증 (빠른 테스트)"
echo "========================================="

cd "$(dirname "$0")/.." || exit 1

echo ""
echo "1. CategoryA (Simple CRUD) - 단일 조회 테스트"
dotnet run -c Release -- --filter *A01_PK_Single_Lookup* --job short 2>&1 | grep -E "(Error|Success|Mean|Benchmarks with issues)"

echo ""
echo "2. CategoryB (JOIN) - JOIN 테스트"
dotnet run -c Release -- --filter *B01_TwoTable_INNER_NuVatis* --job short 2>&1 | grep -E "(Error|Success|Mean|Benchmarks with issues)"

echo ""
echo "3. CategoryC (Aggregate) - GROUP BY 테스트"
dotnet run -c Release -- --filter *C01_GROUP_BY_Single_NuVatis* --job short 2>&1 | grep -E "(Error|Success|Mean|Benchmarks with issues)"

echo ""
echo "4. CategoryD (Bulk) - Bulk Insert 테스트"
dotnet run -c Release -- --filter *D01_Bulk_100_NuVatis* --job short 2>&1 | grep -E "(Error|Success|Mean|Benchmarks with issues)"

echo ""
echo "5. CategoryE (Stress) - 100K 조회 테스트"
dotnet run -c Release -- --filter *E01_Query_100K_NuVatis* --job short 2>&1 | grep -E "(Error|Success|Mean|Benchmarks with issues)"

echo ""
echo "========================================="
echo "검증 완료"
echo "========================================="
