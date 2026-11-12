# 간수의 머리 — Gansuu Head

Unity로 제작한 게임 프로토타입입니다. 이 저장소는 PC 프로젝트 `0.1.1-rc1`의 에디터용 소스와 Windows 실행 빌드를 제공합니다.

## 플레이 및 다운로드

| 용도 | 링크 |
| --- | --- |
| PC 브라우저에서 플레이 | [PC Web 0.1.1](https://gansuu-head-web.tndudss78.chatgpt.site/) |
| 모바일 브라우저에서 플레이 | [Mobile Web 0.1.1](https://gansuu-head-mobile.tndudss78.chatgpt.site/) |
| Windows 실행 파일 다운로드 | [Windows 0.1.1-rc1 릴리스](https://github.com/tndudss/gansuu-head-prototype/releases/tag/v0.1.1-rc1) |
| Unity 에디터용 소스 | 이 저장소를 클론하거나 **Code → Download ZIP** |

웹 배포의 버전은 `0.1.1`이며, 이 저장소에 올린 PC 소스와 실행 파일은 사용자가 지정한 `0.1.1-rc1`입니다. 웹 프로젝트 소스는 이 저장소에 포함하지 않습니다.

### Windows에서 실행

1. 릴리스의 **Assets**에서 `GansuuHead_PC_0.1.1-rc1_Windows.zip`을 다운로드합니다.
2. ZIP 전체를 새 폴더에 압축 해제합니다.
3. `GansuuHead.exe`를 실행합니다.

Unity 에디터 설치는 필요하지 않습니다. `GansuuHead_Data/`, `MonoBleedingEdge/`, `D3D12/` 및 DLL 파일을 EXE와 함께 유지하세요.

## Unity 에디터에서 열기

- 제작 버전: **Unity 6000.6.6f1**
- 시작 씬: `Assets/Scenes/MainPrototype.unity`

1. Unity Hub에서 Unity 6000.6.6f1을 설치합니다.
2. 저장소를 클론하거나 ZIP을 내려받아 압축을 해제합니다.
3. Unity Hub의 **Add → Add project from disk**에서 `Assets/`, `Packages/`, `ProjectSettings/`가 있는 프로젝트 루트를 선택합니다.
4. 패키지 복원과 에셋 임포트가 끝나면 시작 씬을 열고 Play를 실행합니다.

첫 실행에는 Unity 패키지를 내려받기 위한 인터넷 연결이 필요할 수 있습니다.

## 포함 파일

- `Assets/`: 스크립트, 씬, 모델, 텍스처, UI, 폰트 및 `.meta` 파일
- `Packages/`: 패키지 목록과 버전 잠금 파일
- `ProjectSettings/`: 프로젝트 및 빌드 설정

`Library/`, `Temp/`, `Logs/`, `UserSettings/` 등의 캐시와 개인 설정은 버전 관리에서 제외합니다. 실행 빌드는 GitHub Releases에서 배포합니다.
