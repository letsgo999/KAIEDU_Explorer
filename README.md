# KAIEDU Explorer

Windows 파일 탐색기 두 창을 Google Drive와 로컬 디스크에 맞춰 좌우로 배치하고, 마지막 경로를 기억하며, Google Drive의 파일·폴더 웹 주소를 우클릭 메뉴에서 복사하는 초경량 오픈소스 도구입니다.

## 주요 기능

- 실행 시 Google Drive 데스크톱 앱의 실제 스트리밍 위치를 자동 탐지해 왼쪽에, `C:\`를 오른쪽에 50:50으로 배치
- 두 창 사이의 구분선 또는 탐색기 테두리를 드래그하면 양쪽 창이 동시에 30:70부터 70:30까지 조절되고, 더블클릭으로 50:50 복원
- 마지막 분할 비율을 저장해 다음 실행 때 복원
- 로컬 디스크가 이미 `G:`를 사용하는 환경에서도 Google Drive의 다른 드라이브 문자를 안전하게 탐지
- 자동 탐지가 어려운 폴더형 스트리밍·다중 위치 환경은 트레이 메뉴에서 직접 선택
- 양쪽 창의 마지막 경로를 저장해 다음 실행 때 복원
- Windows 11의 **추가 옵션 표시** 메뉴에 `구글 클라우드 URL 복사하기` 추가
- URL 복사를 선택할 때만 Google Drive API 호출
- `drive.metadata.readonly` 최소 권한만 요청
- OAuth 토큰과 클라이언트 정보는 Windows DPAPI로 현재 사용자에게만 복호화되도록 로컬 저장
- 텔레메트리, 광고, 별도 서버, 파일 내용 업로드 없음

## 설치

Google Drive URL 복사 기능을 사용하려면 **Google Drive 데스크톱 앱이 설치·실행되고 계정 로그인이 완료되어 있어야 합니다.** Google Drive가 없어도 설치는 가능하지만, 앱은 로컬 드라이브를 Google Drive로 오인하지 않고 준비 안내를 표시합니다.

1. 최신 릴리스 ZIP 전체를 한 폴더에 압축 해제합니다.
2. 루트의 `KAIEDU-Explorer-Setup.exe`를 더블클릭합니다.
3. Google Drive가 준비된 뒤 Windows 시작 메뉴에서 **KAIEDU Explorer**를 실행합니다.
4. 작업 표시줄 알림 영역의 아이콘을 우클릭해 **Connect Google account...**를 선택합니다.
5. 본인이 만든 Google OAuth 2.0 **Desktop app** 클라이언트 JSON을 선택합니다.

KAIEDU Explorer는 Windows 부팅 시 자동 실행되지 않는 선택형 도구입니다. Google Drive 및 Hermes의 자동 시작 순서와 분리되어 있으며, 필요할 때 사용자가 직접 실행합니다.

Google Drive가 폴더 위치로 스트리밍되거나 여러 위치가 감지되면 트레이 메뉴의 **Select Google Drive location...**에서 `My Drive` 또는 `내 드라이브`가 들어 있는 위치를 선택합니다.

설치 프로그램은 폴더를 클릭할 때 별도 탐색기가 연속 생성되지 않도록 Windows 탐색기의 **각 폴더를 같은 창에서 열기** 옵션을 설정합니다. 기존 설정값은 `%LOCALAPPDATA%\DualDriveExplorer\explorer-cabinetstate-before-kaiedu.bin`에 한 번 백업하며, 다른 탐색기 옵션은 변경하지 않습니다.

탐색기 두 창의 중앙선에 마우스를 올리면 좌우 크기 조절 커서가 나타납니다. 어느 쪽 경계를 잡더라도 반대편 창이 동시에 따라오며, 중앙선을 더블클릭하면 50:50으로 복원됩니다.

초기 설정과 수동 시험 절차는 [사용설명서](https://kaiedu.center/download/)를 참고하세요.

설치 프로그램은 현재 사용자 영역만 사용하며 관리자 권한이 필요하지 않습니다. 제거는 `installer/Uninstall-DualDriveExplorer.ps1`을 실행합니다.

> 설치 EXE는 아직 상용 코드 서명이 없어 Windows가 ‘알 수 없는 게시자’ 경고를 표시할 수 있습니다. 소스는 공개되어 있으며 설치기는 내부 PowerShell 설치 스크립트를 현재 프로세스에 한해 실행 정책 우회로 호출합니다.

## Google Cloud 설정

1. Google Cloud 프로젝트에서 Google Drive API를 활성화합니다.
2. OAuth 동의 화면을 구성합니다.
3. 데이터 액세스 범위에 `https://www.googleapis.com/auth/drive.metadata.readonly`만 추가합니다.
4. OAuth 클라이언트 유형으로 **Desktop app**을 생성하고 JSON을 내려받습니다.
5. JSON은 저장소에 커밋하지 마세요. `.gitignore`가 일반적인 자격증명 파일명을 차단합니다.

## 프로젝트 구조

```text
KAIEDU_Explorer/
├─ KAIEDU-Explorer-Setup.exe  # 릴리스 ZIP의 더블클릭 설치 프로그램
├─ src/
│  ├─ DualDriveExplorer.ps1   # 서명된 PowerShell 호스트용 진입점
│  └─ Program.cs              # 트레이 앱, 탐색기 배치, OAuth 및 Drive API
├─ installer/
│  ├─ SetupLauncher.cs
│  ├─ Build-SetupLauncher.ps1
│  ├─ Install-DualDriveExplorer.ps1
│  └─ Uninstall-DualDriveExplorer.ps1
└─ website/download/          # OAuth 브랜드 검증용 정적 사이트
```

## 개인정보 및 보안

앱은 선택한 Google Drive 파일·폴더의 이름, 상위 폴더 관계, 식별자와 `webViewLink`만 읽습니다. 파일 본문은 읽거나 전송하지 않습니다. 자세한 내용은 [개인정보처리방침](https://kaiedu.center/download/privacy)을 확인하세요.

## 개발 환경

- Windows 11
- Windows PowerShell 5.1
- .NET Framework 4.8 이상
- 외부 NuGet 패키지 없음

## 라이선스

[MIT License](LICENSE)
