# WindowSpot

macOS **Spotlight**을 분석해서 Windows용으로 재구현한 빠른 실행/검색 런처. C# / .NET 8 (WPF)로 작성됨.

> **참고**: 이 코드는 Windows 전용(WPF, Win32 API, Windows Search 인덱스 사용)입니다.
> 개발이 진행된 환경은 Linux(ARM)라 여기서 직접 빌드·실행해 검증할 수 없었습니다.
> 실제 빌드/실행은 아래 안내대로 **Windows 10/11 + .NET 8 SDK** 환경에서 진행해 주세요.

---

## 1. macOS Spotlight 분석

Spotlight의 핵심은 네 가지 축으로 나뉩니다.

| 축 | macOS 구현 | 역할 |
|---|---|---|
| **트리거** | `Cmd+Space` 전역 단축키 | 어떤 앱을 쓰고 있어도 즉시 검색창 호출 |
| **인덱스** | `mdworker`/`mdimporter`가 파일 메타데이터를 미리 색인 (Core Spotlight DB) | 실시간 파일시스템 스캔 없이 즉시 검색 가능 |
| **랭킹** | 사용 빈도·최근성·타입(앱 > 계산기 > 문서 > 웹) 가중치로 결과 정렬 | 가장 원하는 결과가 항상 최상단에 |
| **액션** | Enter=열기, Cmd+Enter=Finder에서 보기, 계산기/사전/단위변환은 그 자리에서 결과 표시 | 검색과 실행을 분리하지 않고 하나의 흐름으로 |

즉 Spotlight는 "검색창 UI" 자체보다 **① 상시 대기 중인 전역 인덱스**와 **② 입력 즉시 반응하는 랭킹된 통합 검색**이 본질입니다.

## 2. Windows 버전 매핑

| Spotlight 개념 | WindowSpot 구현 | 위치 |
|---|---|---|
| `Cmd+Space` | `RegisterHotKey` Win32 API로 `Alt+Space` 전역 등록 (Flow Launcher, PowerToys Run과 동일한 기본값) | `Services/HotkeyManager.cs` |
| LaunchServices 앱 DB | Windows엔 앱 전용 인덱스가 없으므로, 시작 메뉴(`.lnk`)를 직접 스캔해 자체 인덱스 구축 + `FileSystemWatcher`로 실시간 갱신 | `Services/AppIndexer.cs`, `ShortcutResolver.cs` |
| mdworker 파일 인덱스 | 새로 만들지 않고 OS가 이미 유지하는 **Windows Search 인덱서**를 OLE DB(`Search.CollatorDSO`)로 질의 | `Providers/FileSearchProvider.cs` |
| Spotlight 계산기 | 자체 재귀 하강 수식 파서 (`+ - * / % ^ ()`) | `Services/ExpressionEvaluator.cs`, `Providers/CalculatorProvider.cs` |
| 웹 검색 폴백 | 매칭 결과가 없어도 항상 하단에 "웹에서 검색" 항목 표시 | `Providers/WebSearchProvider.cs` |
| 랭킹 | VSCode 스타일 퍼지 매칭(연속 매치·단어 경계·시작 위치 가중치) + provider별 우선순위 가중치 합산 | `Services/FuzzyMatcher.cs`, `Services/SearchEngine.cs` |
| Cmd+Enter (Finder에서 보기) | `Ctrl+Enter` → 탐색기에서 파일 위치 열기 (`explorer /select,`) | `MainWindow.xaml.cs`, `SearchResult.OpenContainingFolder` |
| 항상 위에 뜨는 중앙 팝업 UI | `WindowStyle=None` + `AllowsTransparency` 반투명 다크 UI, 활성 화면 중앙 상단에 표시, 포커스를 잃으면 자동 숨김 | `MainWindow.xaml(.cs)` |

## 3. 프로젝트 구조

```
WindowSpot.sln
src/WindowSpot/
  App.xaml(.cs)                앱 진입점: 단일 인스턴스, 트레이 아이콘, 전역 단축키 등록
  MainWindow.xaml(.cs)         Spotlight 스타일 검색창 UI (즐겨찾기/자주 쓰는 앱/AI 채팅 포함)
  SettingsWindow.xaml(.cs)     Ask AI(OpenRouter) API 키/모델 설정 창
  AddFavoriteWindow.xaml(.cs)  즐겨찾기 추가 다이얼로그 (앱 검색 + URL 직접 추가)
  NativeMethods.cs             RegisterHotKey/UnregisterHotKey P/Invoke
  Models/
    SearchResult.cs             검색 결과 공통 모델
    IconButtonItem.cs           즐겨찾기/자주 쓰는 앱 행의 아이콘 버튼 모델
    ChatMessageView.cs          AI 채팅 말풍선 모델
  Providers/                    ISearchProvider 구현체
    CalculatorProvider.cs        사칙연산 계산기
    ExchangeRateProvider.cs      실시간 환율 변환 (frankfurter.app API, KRW 환산)
    UrlProvider.cs                입력이 URL/도메인이면 바로 "열기" 결과 제공
    AppSearchProvider.cs          퍼지 매칭 + 한글 초성 검색 + 사용빈도 랭킹
    FileSearchProvider.cs         Windows Search 인덱서 질의
    WebSearchProvider.cs          네이버 검색 폴백
  Services/
    SearchEngine.cs              provider 병렬 실행 + 랭킹 병합
    FuzzyMatcher.cs               퍼지 매칭 스코어링
    KoreanSearch.cs                한글 초성/자모 분해 검색
    AppIndexer.cs                 시작 메뉴 스캔 + 실시간 감시
    ShortcutResolver.cs           .lnk → 실제 경로 (COM IShellLinkW)
    IconExtractor.cs               셸 아이콘 추출/캐싱
    HotkeyManager.cs               전역 단축키 등록/해제
    ExpressionEvaluator.cs         산술식 파서
    AppDataPaths.cs                %AppData%\WindowSpot 경로 헬퍼
    UsageStore.cs                  앱 실행 횟수 저장 (자주 쓰는 앱 랭킹)
    FavoritesStore.cs              즐겨찾기 목록 저장
    SettingsStore.cs               OpenRouter API 키/모델 저장
    OpenRouterClient.cs            OpenRouter chat completions 호출
    ChatSessionStore.cs            마지막 AI 채팅 세션 저장 ("/resume")
```

## 4. 빌드 & 실행 (Windows에서)

**요구사항**: Windows 10/11, [.NET 8 SDK](https://dotnet.microsoft.com/download)

```powershell
cd windowspot
dotnet restore
dotnet build -c Release
dotnet run --project src/WindowSpot/WindowSpot.csproj
```

- `System.Data.OleDb` 패키지 버전이 복원되지 않으면: `dotnet add src/WindowSpot/WindowSpot.csproj package System.Data.OleDb` 로 최신 버전을 다시 받으세요.
- 실행 후 **Alt+Space**를 누르면 검색창이 뜹니다. Esc로 닫히고, 트레이 아이콘에서도 열기/종료가 가능합니다.
- 배포용 단일 실행 파일:
  ```powershell
  dotnet publish src/WindowSpot/WindowSpot.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
  ```
- 시작프로그램 등록은 `Win+R` → `shell:startup` 폴더에 publish된 exe의 바로가기를 넣으면 됩니다.

## 5. 사용법

- `Alt+Space` — 검색창 열기/닫기
- 입력 즉시 결과 표시 (Applications → Files → 웹검색 순 우선순위, 계산식/환율이면 최상단에 결과)
- 앱 이름은 한글 초성(예: "카카오톡" → `ㅋㅋㅇㅌ`)으로도 검색 가능
- 자주 실행한 앱은 자동으로 검색 점수가 올라가 위로 올라옴
- 검색창이 비어 있을 때: 즐겨찾기 행(+ 버튼으로 앱/사이트 추가, 아이콘 우클릭으로 제거) · 자주 쓰는 앱 행 표시
- `10usd`, `$10` 같은 입력은 실시간 환율로 원화 환산, 도메인(`github.com` 등)을 입력하면 바로 "열기" 결과 제공
- `Tab` — 입력 중인 문자열과 일치하는 앱 이름/흔한 도메인 자동완성 (고스트 텍스트로 미리보기)
- `↑`/`↓` — 결과 이동, `Enter` — 실행, `Ctrl+Enter` — (파일/폴더) 탐색기에서 위치 열기, `Esc` — 닫기
- 계산기/환율 결과에서 `Enter` — 값 클립보드 복사
- 매칭 결과가 없으면 네이버 검색으로 폴백
- **Ask AI**: 트레이 아이콘 → "AI 설정..."에서 [OpenRouter](https://openrouter.ai) API 키와 모델(예: `anthropic/claude-3.5-sonnet`)을 등록하면 검색창 하단에 "Ask AI" 항목이 나타남. 클릭하면 채팅 모드로 전환되며, 채팅창에 `/resume` 입력 시 마지막 대화를 불러옴

## 6. 알려진 한계 (향후 개선 가능 항목)

- **UWP/Microsoft Store 앱 미포함**: `AppsFolder` 기반 shell link는 일반 파일 경로로 해석되지 않아 스캔에서 제외됨. `Windows.Management.Deployment.PackageManager` 연동으로 확장 가능.
- **파일 내용 전문 검색 없음**: 현재는 파일명만 검색 (`System.FileName`). Windows Search 인덱서가 지원하는 `System.Search.Contents`로 바꾸면 문서 내용 검색도 가능.
- **블러/아크릴 배경 미적용**: 반투명 단색 배경만 사용. `DwmSetWindowAttribute`/`SetWindowCompositionAttribute` 같은 비공식 API로 Windows 11 아크릴 효과를 추가할 수 있음.
- 단축키(`Alt+Space`)는 코드에 고정되어 있음 — 설정 UI로 변경 가능하게 하려면 config 파일 + 설정 창 추가 필요.
- Ask AI는 사용자가 직접 OpenRouter API 키를 발급받아 등록해야 동작함 (앱에 기본 내장된 키 없음).
