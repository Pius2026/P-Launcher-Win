# Contributing / 기여 안내

## 한국어

기여해 주셔서 감사합니다.

1. Windows와 .NET 9 SDK를 준비합니다.
2. 별도 브랜치에서 한 가지 목적의 변경만 작업합니다.
3. 기존 명령형 WPF 구조와 사용자 데이터 호환성을 유지합니다.
4. 다음 명령으로 전체 빌드와 테스트를 실행합니다.

```powershell
dotnet restore P-Edu-Launcher.sln
dotnet build P-Edu-Launcher.sln -c Release --no-restore
dotnet test P-Edu-Launcher.sln -c Release --no-build
```

Pull Request에는 변경 이유, 사용자에게 보이는 동작, 검증 방법을 적어 주세요. UI를 변경했다면 한글과 English 양쪽에서 확인해야 합니다.

다음 파일은 커밋하지 마세요.

- 사용자 설정 JSON, 실제 파일 경로, 개인용 바로가기
- `bin/`, `obj/`, `artifacts/`, `backup/`, `TestResults/`
- 실행파일, DLL, PDB, ZIP, QA 스크린샷과 에이전트 로그
- 토큰, 비밀번호, 이메일 주소 등 민감정보

## English

Thank you for contributing.

1. Use Windows with the .NET 9 SDK installed.
2. Make one focused change on a separate branch.
3. Preserve the existing imperative WPF structure and stored-data compatibility.
4. Run the full build and test commands shown above.

Describe the reason for the change, user-visible behavior, and verification in the Pull Request. UI changes must be checked in both Korean and English.

Do not commit user settings, personal paths or shortcuts, build artifacts, release binaries, QA captures, agent logs, credentials, or other sensitive information.
