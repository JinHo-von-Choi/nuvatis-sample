# NuVatis Benchmark Runner

벤치마크 실행을 위한 환경 설정 가이드

## 설정 방법

1. **appsettings.json 생성**
   ```bash
   cp appsettings.example.json appsettings.json
   ```

2. **DB 연결 정보 수정**
   - `appsettings.json` 파일을 열어 실제 PostgreSQL 연결 정보로 수정
   - Host, Port, Database, Username, Password 설정

3. **빌드 및 실행**
   ```bash
   dotnet build -c Release
   dotnet run -c Release
   ```

## 보안 주의사항

- `appsettings.json`은 **절대 커밋하지 마세요**
- 실제 접속 정보는 로컬에만 보관
- 예제 파일(`appsettings.example.json`)만 커밋됨

## 상세 가이드

전체 벤치마크 실행 방법은 프로젝트 루트의 `BENCHMARK_EXECUTION_GUIDE.md`를 참고하세요.
