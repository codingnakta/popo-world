-- 0003_rls.sql
-- 표            select        insert                       update                              delete
-- users         로그인한 모두  없음 (관리자가 대시보드로)    본인 행만, 컬럼은 last_seen, name 만  없음
-- messages      모두          본인(user_id = auth.uid())   없음                                없음
-- catalog       모두          없음                         없음                                없음
-- inventory     모두          없음 (RPC만)                 없음                                없음
-- popo_ledger   본인 행만     없음 (RPC만)                 없음                                없음
--
-- 장착 4개(character/bubble/ball/chair)는 보유 검사가 필요하므로 users 직접 update 가 아니라 equip RPC 로만 바꾼다.

alter table users enable row level security;
alter table messages enable row level security;
alter table catalog enable row level security;
alter table inventory enable row level security;
alter table popo_ledger enable row level security;

-- 표 권한. Supabase 는 기본으로 anon/authenticated 에 모든 권한을 주므로 먼저 전부 거두고 필요한 것만 준다.
-- (RPC 는 security definer 라 함수 소유자 권한으로 쓴다. service_role 은 그대로 둔다.)
revoke all on users, messages, catalog, inventory, popo_ledger from anon, authenticated, public;
revoke all on balances from anon, authenticated, public;
grant select on users, messages, catalog, inventory, popo_ledger to authenticated;
grant select on balances to authenticated;
grant insert on messages to authenticated;
-- users 는 컬럼 단위로 last_seen 과 name 만 고칠 수 있다
grant update (last_seen, name) on users to authenticated;

-- users
create policy users_select on users
  for select to authenticated using (true);
create policy users_update_self on users
  for update to authenticated using (id = auth.uid()) with check (id = auth.uid());

-- messages
create policy messages_select on messages
  for select to authenticated using (true);
create policy messages_insert_self on messages
  for insert to authenticated with check (user_id = auth.uid());

-- catalog
create policy catalog_select on catalog
  for select to authenticated using (true);

-- inventory
create policy inventory_select on inventory
  for select to authenticated using (true);

-- popo_ledger: 본인 행만. Realtime 도 이 정책으로 걸러져 본인 행만 받는다.
create policy ledger_select_self on popo_ledger
  for select to authenticated using (user_id = auth.uid());

-- balances 뷰는 기본(security invoker)으로 두어 RLS 가 적용되게 한다 (Postgres 15+)
alter view balances set (security_invoker = true);
