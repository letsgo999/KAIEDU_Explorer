# KAIEDU Explorer 사용설명서

## 먼저 확인하세요

Google Drive URL 복사 기능에는 Windows 10/11용 **Google Drive 데스크톱 앱 설치, 실행 및 로그인**이 필요합니다. 앱 자체는 Google Drive 없이도 설치되지만 로컬 디스크를 Drive로 오인하지 않으며, URL 복사 기능은 사용할 수 없습니다.

- Google Drive가 G:, H:, I: 등 어느 문자에 연결되어도 자동으로 찾습니다.
- 로컬 디스크가 이미 G:를 사용하더라도 그 디스크를 바꾸거나 덮어쓰지 않습니다.
- 폴더형 스트리밍 또는 여러 Drive 위치가 있는 경우 알림 영역 메뉴의 **Select Google Drive location...**에서 직접 선택합니다.

## 설치와 첫 연결

1. 최신 ZIP을 내려받아 압축을 풉니다.
2. `installer/Install-DualDriveExplorer.ps1`을 마우스 오른쪽 버튼으로 클릭하고 **PowerShell에서 실행**합니다.
3. 알림 영역의 앱 아이콘을 우클릭하고 **Connect Google account...**를 선택합니다.
4. 본인의 Google OAuth 데스크톱 클라이언트 JSON을 선택한 뒤 브라우저에서 권한을 허용합니다.

## 기능 시험

1. 알림 영역 메뉴에서 **Open / arrange explorers**를 선택합니다.
2. Google Drive가 왼쪽, C:가 오른쪽에 같은 크기로 열리는지 확인합니다.
3. 양쪽에서 다른 폴더로 이동한 뒤 **Save current paths**를 선택합니다.
4. 앱을 종료했다 다시 실행해 두 경로가 복원되는지 확인합니다.
5. Google Drive 안의 동기화된 항목을 우클릭하고 Windows 11에서는 **추가 옵션 표시**를 누릅니다.
6. **구글 클라우드 URL 복사하기**를 선택하고 메모장에 붙여넣어 Drive URL인지 확인합니다.

URL을 찾았다는 사실만으로 외부 공개가 되지는 않습니다. 다른 사람이나 LLM이 열어야 한다면 Google Drive 웹사이트의 공유 설정을 별도로 지정하고 시크릿 창에서 접근 가능 여부를 확인하세요.

## 제거

`installer/Uninstall-DualDriveExplorer.ps1`을 PowerShell에서 실행합니다.
