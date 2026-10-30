# Supabase 설정 (T-09)

1. https://supabase.com 에서 무료 프로젝트를 만든다.
2. **SQL Editor** 에서 `migrations/0001_schema.sql`, `0002_functions.sql`, `0003_rls.sql` 을 순서대로 실행한다.
   - 대시보드 **Table Editor** 에 표 5개(users, messages, catalog, inventory, popo_ledger)와 카탈로그 12행이 보이면 성공.
3. **Database > Extensions** 에서 `pg_cron` 을 켜고 `0001_schema.sql` 맨 아래의 주석 처리된 `cron.schedule` 을 실행한다 (3일 지난 메시지 삭제).
4. **Authentication > Providers > Email** 에서 "Confirm email" 을 끈다 (가입 화면이 없고 관리자가 계정을 만든다).
5. **Authentication > Users > Add user** 로 친구 계정을 만든다 (`seed/users.csv` 참고. 비밀번호는 직접 정해서 전달).
   - `0001_schema.sql` 의 트리거가 `users` 행을 자동으로 만든다. 이름은 user metadata 의 `name`, 없으면 이메일 앞부분(8자).
   - 관리자 계정은 Table Editor 에서 `users.is_admin = true` 로 바꾼다.
6. **Project Settings > API** 의 Project URL 과 anon public key 를 `client/PopoWorld.App/appsettings.json` 에 넣는다.
   - `service_role` 키는 절대 클라이언트에 넣지 않는다.
7. (유료 아이템) **Storage** 에 공개 버킷 `assets` 를 만들고 `characters/<id>/sheet.png`, `characters/<id>/manifest.json`, `items/<id>.png` 를 올린 뒤 `catalog.image_url` 을 채운다 (`seed/catalog.json` 참고).

## RPC 멱등성 확인 (T-19)

SQL Editor 에서 특정 사용자로 실행해 보려면:

```sql
-- 세션을 그 사용자로 가장
select set_config('request.jwt.claims', json_build_object('sub', '<user-uuid>', 'role', 'authenticated')::text, true);
select public.earn('message', '11111111-1111-1111-1111-111111111111');
select public.earn('message', '11111111-1111-1111-1111-111111111111');  -- 같은 request_id: 원장은 1줄, 응답은 ok
select * from popo_ledger where request_id = '11111111-1111-1111-1111-111111111111';
```

## 관리자 포포 지급

```sql
select public.admin_grant('<friend-uuid>', 10, '환영 선물');
```
