# 포포월드 (PopoWorld)

친구들의 작은 캐릭터가 모니터 가장자리를 걸어 다니고, 말풍선으로 대화하고, 공을 던지고, 포포(포인트)로 아이템을 사고 선물하는 Windows 데스크톱 앱.

- 클라이언트: .NET 8, C# 12, WPF (`client/`)
- 서버: Supabase 무료 프로젝트, Postgres + Realtime + RPC (`supabase/`)
- 개발 명세: [`docs/개발명세.md`](docs/개발명세.md) — 기능 ID, 스키마, RPC 계약, 작업 목록 T-01~T-27

## 저장소 구조

```
client/
  PopoWorld.sln
  PopoWorld.App/          WPF 실행 파일 (net8.0-windows)
    Overlay/              투명 표시 창, 핫스팟 입력 창, Win32 interop, 캐릭터 그리기
    Composer/ Store/ Settings/ Login/
    Services/             Supabase, Realtime, RPC, 접속 상태, 설정, 세션(DPAPI), 단축키, 트레이
    Assets/               기본 캐릭터 3종 스프라이트, 공·말풍선·의자, 효과음 (exe 에 포함)
  PopoWorld.Core/         순수 C# — 모델, 물리(WalkAgent/DragSpring/ThrowArc), 검증, 네 방향 레이아웃
  PopoWorld.Core.Tests/   xUnit
supabase/
  migrations/0001_schema.sql    표 5개 + balances 뷰 + Realtime publication + users 자동 생성 트리거
  migrations/0002_functions.sql earn / purchase / gift / equip / admin_grant (request_id 멱등)
  migrations/0003_rls.sql       RLS 와 표 권한
  seed/                         catalog.json, users.csv (예시)
```

## 시작하기

1. **Supabase**: [`supabase/README.md`](supabase/README.md) 순서대로 프로젝트를 만들고 SQL 3개를 실행한다.
2. **키 넣기**: `client/PopoWorld.App/appsettings.json` 에 `SUPABASE_URL`, `SUPABASE_ANON_KEY` 를 넣는다.
   exe 옆에 같은 이름의 파일을 두면 그 파일이 우선한다.
3. **빌드·테스트** (Windows, .NET 8 SDK):

```bash
dotnet test client/PopoWorld.Core.Tests
```

```bash
dotnet run --project client/PopoWorld.App
```

4. **배포** (exe 하나):

```bash
dotnet publish client/PopoWorld.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

`publish/PopoWorld.exe` 를 zip 으로 친구에게 전달한다. 설치 프로그램·자동 업데이트는 없다.

## 로컬 파일

| 경로 | 내용 |
|---|---|
| `%APPDATA%\PopoWorld\settings.json` | 모니터·변·영역 비율·소리·단축키 등 (명세 7절) |
| `%APPDATA%\PopoWorld\session.bin` | 로그인 토큰, DPAPI(CurrentUser) 암호화 |
| `%APPDATA%\PopoWorld\logs\app-YYYYMMDD.log` | Serilog 일 단위 로그. "안 돼요" 하면 이 파일을 받는다 |
| `%APPDATA%\PopoWorld\cache\` | 유료 아이템 이미지, 효과음 |

## 조작

| 입력 | 내 캐릭터 | 친구 캐릭터 |
|---|---|---|
| 클릭 | 입력창 토글 | 공 던지기 |
| 더블클릭 | 입력창 열기 | 공 던지기 1회 (두 번째 클릭 무시) |
| 우클릭 | 말하기, 상점, 설정, 숨기기 | 포포 선물, 이 친구에게 던지기 |
| 드래그 (4px 이상) | 옮기기, 놓으면 통통 튀며 바닥으로 | 같음 (내 화면에서만) |
| `Ctrl+Shift+Space` | 입력창 | |
| `Ctrl+Shift+H` | 세계 숨기기/보이기 | |

트레이 아이콘: 초록 = 연결됨, 회색 = 끊김, 파랑 = 조용히 모드. 툴팁에 잔액과 접속 친구 수가 보인다.

## 명세와 다른 점

- `popo_ledger.peer_id uuid` 컬럼을 추가했다. 받는 쪽 원장(gift_in)에서 보낸 사람 이름을 말풍선에 띄우려면 상대 id 가 필요한데, RLS 상 상대의 gift_out 행은 읽을 수 없기 때문이다.
- `users` 직접 update 는 `last_seen` 과 `name` 두 컬럼만 허용한다. 장착 4개는 보유 검사가 필요하므로 `equip` RPC 로만 바꾼다 (명세 3절과 같은 취지).
- 기본 캐릭터 3종 스프라이트는 코드로 만든 임시 그림이다. `Assets/characters/<id>/sheet.png` 를 같은 규격(48×48, 한 줄 8프레임, 줄 순서 idle/walk/sleep/hit/offline)으로 바꾸면 된다.
- 조용히 모드는 효과음과 새 메시지 알림음을 끈다. 말풍선은 그대로 보인다.
