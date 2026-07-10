# P-Edu-Launcher

[한국어](README.md) | [English](README.en.md)

P-Edu-Launcher는 프로그램, 파일, 폴더, 바로가기와 웹사이트를 한곳에서 실행할 수 있는 Windows용 데스크톱 런처입니다. .NET 9 WPF로 작성되었으며, 3개 위젯과 총 30개 슬롯을 제공합니다.

## 주요 기능

- 3개 위젯 × 10개 슬롯
- 슬롯 하나에 최대 10개 대상을 등록하고 한 번에 실행
- 프로그램, 일반 파일, 폴더, Windows 바로가기, 웹사이트 바로가기 지원
- 편집 모드에서 끌어놓기, 슬롯 순서 변경, 개별 대상 삭제
- 런처 제목, 위젯 이름, 슬롯 이름 변경
- JSON 파일로 현재 레이아웃 저장 및 불러오기
- 전체 초기화와 위젯별 초기화
- 작업 표시줄 알림 영역 상주, 자동 숨김 및 다시 열기
- 설정창에서 `한글`과 `English`를 즉시 전환하고 다음 실행에도 유지

사용자가 지정한 제목, 위젯명, 슬롯명, 파일명은 사용자 데이터이므로 언어를 바꾸어도 원문을 유지합니다. 앱이 제공하는 메뉴와 안내 문구만 선택한 언어로 전환됩니다.

## 중요한 실행 동작

앱을 실행하면 현재 사용자의 Windows 시작프로그램 폴더에 `P-Edu-Launcher.lnk`를 자동으로 생성하거나 갱신합니다. 앱은 종료 버튼을 누르기 전까지 알림 영역에서 계속 실행됩니다.

앱 데이터는 로컬에 저장됩니다.

| 데이터 | 기본 위치 |
| --- | --- |
| 현재 레이아웃 | `%APPDATA%\PiusLauncher\launcher-items.json` |
| 언어 설정 | `%APPDATA%\PiusLauncher\ui-preferences.json` |
| 앱이 관리하는 바로가기 | `%APPDATA%\PiusLauncher\SlotShortcuts` |

런처 자체에는 원격 동기화나 사용 통계 전송 기능이 없습니다. 등록한 웹사이트나 프로그램을 실행할 때는 해당 대상의 동작과 개인정보 처리방침이 적용됩니다.

## 사용 시 주의

- 저장한 레이아웃 JSON에는 대상 파일이 포함되지 않고 경로만 기록됩니다. 다른 PC나 Windows 계정에서는 같은 경로가 없으면 슬롯이 실행되지 않습니다.
- `.lnk`, `.url`, `.appref-ms` 바로가기를 슬롯에 넣으면 앱이 관리 폴더로 복사한 뒤 원래 바로가기 파일을 삭제할 수 있습니다. 원본 바로가기 보존이 필요하면 먼저 복사본을 사용하세요.
- 현재 앱 안에는 자동 시작을 끄는 설정이 없습니다. 자동 시작을 해제하려면 Windows 시작프로그램 폴더의 `P-Edu-Launcher.lnk`를 직접 삭제해야 합니다.
- English를 선택해도 Windows 파일 대화상자의 시스템 문구는 Windows 표시 언어를 따릅니다. 사용자 데이터와 호환성을 위한 일부 내부 폴더·관리 파일명은 한글로 유지될 수 있습니다.

## 요구 사항

- Windows
- 소스 빌드: [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- 프레임워크 종속 배포본 실행: .NET 9 Desktop Runtime

프로젝트 대상 프레임워크는 `net9.0-windows`입니다.

## 소스에서 빌드

> 주의: `dotnet run`으로 앱을 실행해도 위에서 설명한 시작프로그램 바로가기가 생성됩니다.

```powershell
dotnet restore P-Edu-Launcher.sln
dotnet build P-Edu-Launcher.sln -c Release --no-restore
dotnet test P-Edu-Launcher.sln -c Release --no-build
dotnet run --project src/PiusLauncher/PiusLauncher.csproj -c Release
```

## Windows x64 배포본 만들기

```powershell
dotnet publish src/PiusLauncher/PiusLauncher.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o artifacts/publish/P-Edu-Launcher
```

`artifacts/`와 루트의 실행 바이너리는 Git에서 제외됩니다. 배포 파일은 소스 저장소에 커밋하지 말고 GitHub Releases에 별도로 첨부하는 방식을 권장합니다.

## 프로젝트 구조

```text
src/PiusLauncher/                 WPF 앱 프로젝트와 아이콘/임베디드 리소스
tests/PiusLauncher.Tests/         xUnit 테스트 프로젝트
.github/workflows/ci.yml          Windows 빌드 및 테스트 CI
README.md / README.en.md          한글/영문 문서
LICENSE                           MIT 라이선스
```

`src/PiusLauncher/mainwindow.baml`은 빌드에 필요한 임베디드 WPF 리소스이므로 삭제하면 안 됩니다.

## 공개 저장소에 포함하지 않는 파일

`.gitignore`는 다음 로컬 자료가 실수로 공개되지 않도록 제외합니다.

- `저장된 설정/`의 사용자 레이아웃과 로컬 경로
- `backup/`, `artifacts/`, `bin/`, `obj/`, `TestResults/`
- 루트 실행파일, DLL, PDB, 바로가기와 배포 ZIP
- `.omo/`, `.claude/`, `.codegraph/`, 작업 계획 및 QA 로그

새 파일을 커밋하기 전에는 `git status`에서 개인정보, 로컬 절대 경로, 토큰과 실행 산출물이 없는지 확인하세요.

## 기여와 보안

- 기여 방법: [CONTRIBUTING.md](CONTRIBUTING.md)
- 보안 문제 제보: [SECURITY.md](SECURITY.md)
- 테스트용 타사 패키지: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)
- 최초 공개 전 확인: [PUBLISHING_CHECKLIST.md](PUBLISHING_CHECKLIST.md)

## 라이선스

이 프로젝트의 소스 코드는 [MIT License](LICENSE)로 배포됩니다. NuGet 패키지와 기타 타사 구성 요소에는 각각의 라이선스가 적용됩니다.

최초 공개 전에 소스, 임베디드 BAML과 아이콘을 MIT로 배포할 권리가 있는지 반드시 확인하고, `LICENSE`의 `PiusLauncher`를 실제 법적 저작권자 이름으로 바꾸어야 한다면 먼저 수정하세요.
