# KAIEDU Explorer

Windows 파일 탐색기 두 창을 Google Drive와 로컬 디스크에 맞춰 좌우로 배치하고, 마지막 경로를 기억하며, Google Drive의 파일·폴더 웹 주소를 우클릭 메뉴에서 복사하는 초경량 오픈소스 도구입니다.

## 주요 기능

- 실행 시 Google Drive 데스크톱 앱의 실제 스트리밍 위치를 자동 탐지해 왼쪽에, `C:\`를 오른쪽에 50:50으로 배치
- 두 창 사이의 구분선을 드래그해 30:70부터 70:30까지 조절하고, 더블클릭으로 50:50 복원
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

1. 저장소의 `src` 폴더와 `installer` 폴더를 함께 내려받습니다.
2. `installer/Install-DualDriveExplorer.ps1`을 PowerShell로 실행합니다.
3. 작업 표시줄 알림 영역의 아이콘을 우클릭해 **Connect Google account...**를 선택합니다.
4. 본인이 만든 Google OAuth 2.0 **Desktop app** 클라이언트 JSON을 선택합니다.

Google Drive가 폴더 위치로 스트리밍되거나 여러 위치가 감지되면 트레이 메뉴의 **Select Google Drive location...**에서 `My Drive` 또는 `내 드라이브`가 들어 있는 위치를 선택합니다.

탐색기 두 창의 중앙선에 마우스를 올리면 좌우 크기 조절 커서가 나타납니다. 원하는 위치로 끌어 놓거나 중앙선을 더블클릭해 50:50으로 복원할 수 있습니다.

초기 설정과 수동 시험 절차는 [사용설명서](https://kaiedu.center/download/)를 참고하세요.

설치 프로그램은 현재 사용자 영역만 사용하며 관리자 권한이 필요하지 않습니다. 제거는 `installer/Uninstall-DualDriveExplorer.ps1`을 실행합니다.

> Windows Smart App Control과의 호환성을 위해 무서명 EXE 대신 Microsoft 서명 Windows PowerShell 호스트에서 작은 C# 소스를 메모리 컴파일해 실행합니다.

## Google Cloud 설정

1. Google Cloud 프로젝트에서 Google Drive API를 활성화합니다.
2. OAuth 동의 화면을 구성합니다.
3. 데이터 액세스 범위에 `https://www.googleapis.com/auth/drive.metadata.readonly`만 추가합니다.
4. OAuth 클라이언트 유형으로 **Desktop app**을 생성하고 JSON을 내려받습니다.
5. JSON은 저장소에 커밋하지 마세요. `.gitignore`가 일반적인 자격증명 파일명을 차단합니다.

## 프로젝트 구조

```text
KAIEDU_Explorer/
├─ src/
│  ├─ DualDriveExplorer.ps1   # 서명된 PowerShell 호스트용 진입점
│  └─ Program.cs              # 트레이 앱, 탐색기 배치, OAuth 및 Drive API
├─ installer/
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
