# Public release checklist / 공개 배포 체크리스트

Complete every item before the first public commit.

최초 공개 커밋 전에 모든 항목을 확인하세요.

## Rights and licensing / 권리와 라이선스

- [ ] I own the source or have permission to publish and relicense it under MIT.
- [ ] I have redistribution rights for `src/PiusLauncher/mainwindow.baml`.
- [ ] I own or may redistribute `src/PiusLauncher/app.ico` and `src/PiusLauncher/assets.appicon.ico` under the repository license.
- [ ] `LICENSE` contains the correct legal copyright holder and year.

- [ ] 소스의 권리를 보유했거나 MIT로 공개·재라이선스할 허가를 받았다.
- [ ] `src/PiusLauncher/mainwindow.baml`을 재배포할 권리가 있다.
- [ ] 두 아이콘을 저장소 라이선스로 배포할 권리가 있다.
- [ ] `LICENSE`의 저작권자 이름과 연도가 정확하다.

## Privacy and repository contents / 개인정보와 저장소 내용

- [ ] Run `git status --short --ignored` and confirm that local settings, backups, binaries, logs, and screenshots are ignored.
- [ ] Confirm that `저장된 설정/`, `.omo/`, `backup/`, `artifacts/`, and `메일전송_패키지/` are not staged.
- [ ] Confirm that no token, password, email address, personal path, school document name, or private URL is staged.
- [ ] Confirm that root `P-Edu-Launcher.*` binaries and `P-Edu-Launcher - noclick.lnk` are not staged.

- [ ] `git status --short --ignored`로 로컬 설정, 백업, 바이너리, 로그와 스크린샷이 제외됐는지 확인했다.
- [ ] 개인정보, 학교·개인 자료명, 비공개 URL과 로컬 절대경로가 스테이징되지 않았다.

## Verification / 검증

```powershell
dotnet restore P-Edu-Launcher.sln
dotnet build P-Edu-Launcher.sln -c Release --no-restore
dotnet test P-Edu-Launcher.sln -c Release --no-build
```

- [ ] The commands above pass on a clean checkout.
- [ ] Korean and English UI behavior has been manually checked after the final build.
- [ ] GitHub Actions passes on `windows-latest`.

## Recommended first-commit flow / 권장 최초 커밋 순서

```powershell
git init
git add .gitignore .gitattributes .editorconfig
git add README.md README.en.md LICENSE CONTRIBUTING.md SECURITY.md THIRD_PARTY_NOTICES.md PUBLISHING_CHECKLIST.md
git add P-Edu-Launcher.sln .github src tests
git status --short --ignored
```

Inspect the staged file list before committing. Do not use `git add -f` to override the supplied ignore rules.

커밋 전에 스테이징 목록을 직접 확인하고, 제공된 제외 규칙을 무시하기 위해 `git add -f`를 사용하지 마세요.
