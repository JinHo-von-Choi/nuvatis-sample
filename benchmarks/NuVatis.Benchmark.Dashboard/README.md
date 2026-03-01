# NuVatis ORM Benchmark Dashboard

**작성자**: 최진호
**작성일**: 2026-03-01

## 개요

NuVatis, Dapper, EF Core의 종합 성능 비교 대시보드입니다.
60개 시나리오에 걸쳐 5가지 차원(복잡도, 반복, 결과 크기, 테이블 조합, 작업 유형)을 측정하고 시각화합니다.

## 기술 스택

- **Frontend**: React 18 + TypeScript
- **Charts**: Recharts (반응형 차트 라이브러리)
- **Styling**: Tailwind CSS
- **Build**: Vite (초고속 빌드 도구)

## 실행 방법

### 1. 의존성 설치

```bash
cd benchmarks/NuVatis.Benchmark.Dashboard
npm install
```

### 2. 개발 서버 실행

```bash
npm run dev
```

브라우저에서 http://localhost:5173 접속

### 3. 프로덕션 빌드

```bash
npm run build
npm run preview
```

## 대시보드 구성

### 1. Overview Dashboard (📈)
- **핵심 메트릭 카드**: ORM별 평균 응답 시간, 처리량, 메모리, 승리 시나리오 수
- **레이더 차트**: 카테고리별 5개 ORM 성능 비교
- **바 차트**: 카테고리별 평균 응답 시간
- **라인 차트**: 시나리오별 응답 시간 추세

### 2. Category Detail (📂)
- **카테고리 선택**: A ~ E 카테고리 전환
- **시나리오 테이블**: 각 시나리오별 3-way 비교 테이블
- **Winner 표시**: 가장 빠른 ORM 하이라이트

### 3. Scenario Comparison (🔍)
- **Mean Latency 비교**: 상위 20개 시나리오 막대 차트
- **P95 Latency 비교**: 95백분위수 응답 시간 비교
- **시나리오별 드릴다운**: 개별 시나리오 상세 분석

### 4. Resource Analysis (💻)
- **메모리 사용량**: 평균/최대 메모리 비교
- **GC 압박**: Gen0/1/2 Collection 횟수
- **리소스 프로파일**: ORM별 종합 리소스 사용량

## 데이터 소스

현재는 mock 데이터를 사용합니다 (`src/data/mockData.ts`).

실제 벤치마크 결과를 표시하려면:
1. BenchmarkDotNet JSON/CSV export 활성화
2. `src/data/loadBenchmarkData.ts` 작성
3. App.tsx에서 mock 데이터 대신 실제 데이터 로드

## 색상 테마

- **NuVatis**: Indigo (#4F46E5)
- **Dapper**: Green (#10B981)
- **EF Core**: Amber (#F59E0B)

## 커스터마이징

### 차트 추가
`src/components/`에 새 컴포넌트 생성 후 App.tsx에서 import

### 메트릭 추가
`src/types/index.ts`의 `BenchmarkResult` 인터페이스 확장

### 스타일 수정
Tailwind CSS 클래스 직접 수정 또는 `tailwind.config.js`에서 테마 확장

## 내보내기 기능 (TODO)

- CSV Export: 모든 벤치마크 결과를 CSV 파일로 저장
- PNG Export: 차트를 PNG 이미지로 저장 (html2canvas 사용)

## 라이선스

MIT License
