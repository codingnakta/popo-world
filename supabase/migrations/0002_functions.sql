-- 0002_functions.sql
-- 포포와 보유를 바꾸는 길은 아래 5개 함수뿐이다. 전부 security definer, 호출자는 auth.uid() 로 식별,
-- 결과는 {ok, balance, error} JSON 하나. 실패해도 예외를 던지지 않고 ok=false 와 error 코드를 준다.
--
-- 공통 규칙
-- * request_id 는 클라이언트가 Guid.NewGuid() 로 만들어 보낸다. 같은 (user_id, request_id) 가 이미 원장에 있으면
--   duplicate 대신 그때의 결과(ok=true, 현재 잔액)를 다시 돌려준다. 버튼 연타와 재시도가 안전해진다.
-- * 한도의 "오늘"은 (created_at at time zone 'Asia/Seoul')::date = (now() at time zone 'Asia/Seoul')::date.
-- * 함수 안에서 users 행을 select ... for update 로 잠가 같은 사용자의 동시 호출을 직렬화한다.
-- * 한도 상수는 클라이언트 PopoWorld.Core.Models.PopoRules 와 같아야 한다.

-- ---------- 내부 도우미 ----------

create or replace function public._popo_today(ts timestamptz)
returns date
language sql
immutable
as $$ select (ts at time zone 'Asia/Seoul')::date $$;

create or replace function public._popo_balance(uid uuid)
returns int
language sql
stable
security definer
set search_path = public
as $$ select coalesce(sum(delta), 0)::int from popo_ledger where user_id = uid $$;

create or replace function public._popo_ok(uid uuid)
returns jsonb
language sql
stable
security definer
set search_path = public
as $$ select jsonb_build_object('ok', true, 'balance', public._popo_balance(uid)) $$;

create or replace function public._popo_fail(uid uuid, err text)
returns jsonb
language sql
stable
security definer
set search_path = public
as $$ select jsonb_build_object('ok', false, 'error', err, 'balance', public._popo_balance(uid)) $$;

-- ---------- earn ----------
-- reason ∈ daily_login(5, 하루 1회), message(1, 하루 10회), hit(1, 하루 5회)
-- streak(20, 7일에 1회)는 daily_login 성공 후 서버가 연속 7일 여부를 보고 자동 지급한다. 클라이언트 호출 없음.

create or replace function public.earn(reason text, request_id uuid)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
#variable_conflict use_variable
declare
  uid uuid := auth.uid();
  v_amount int;
  v_limit int;
  v_count int;
  v_days int;
begin
  if uid is null then
    return jsonb_build_object('ok', false, 'error', 'forbidden');
  end if;

  perform 1 from users u where u.id = uid for update;
  if not found then
    return jsonb_build_object('ok', false, 'error', 'forbidden');
  end if;

  -- 같은 요청이면 그때의 결과를 다시 돌려준다
  if exists (select 1 from popo_ledger l where l.user_id = uid and l.request_id = request_id) then
    return public._popo_ok(uid);
  end if;

  case reason
    when 'daily_login' then v_amount := 5;  v_limit := 1;
    when 'message'     then v_amount := 1;  v_limit := 10;
    when 'hit'         then v_amount := 1;  v_limit := 5;
    else return public._popo_fail(uid, 'bad_reason');
  end case;

  select count(*) into v_count
  from popo_ledger l
  where l.user_id = uid and l.reason = reason
    and public._popo_today(l.created_at) = public._popo_today(now());

  if v_count >= v_limit then
    return public._popo_fail(uid, 'limit_reached');
  end if;

  insert into popo_ledger (user_id, delta, reason, request_id)
  values (uid, v_amount, reason, request_id);

  -- 연속 출석 7일: 오늘까지 7일 모두 daily_login 이 있고, 최근 7일 안에 streak 를 받은 적이 없으면 +20
  if reason = 'daily_login' then
    select count(distinct public._popo_today(l.created_at)) into v_days
    from popo_ledger l
    where l.user_id = uid and l.reason = 'daily_login'
      and public._popo_today(l.created_at) > public._popo_today(now()) - 7;

    if v_days >= 7 and not exists (
      select 1 from popo_ledger l
      where l.user_id = uid and l.reason = 'streak'
        and public._popo_today(l.created_at) > public._popo_today(now()) - 7
    ) then
      insert into popo_ledger (user_id, delta, reason, request_id, ref_id)
      values (uid, 20, 'streak', gen_random_uuid(), public._popo_today(now())::text);
    end if;
  end if;

  return public._popo_ok(uid);
end;
$$;

-- ---------- purchase ----------

create or replace function public.purchase(item_id text, request_id uuid)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
#variable_conflict use_variable
declare
  uid uuid := auth.uid();
  v_item catalog%rowtype;
  v_balance int;
begin
  if uid is null then
    return jsonb_build_object('ok', false, 'error', 'forbidden');
  end if;

  perform 1 from users u where u.id = uid for update;
  if not found then
    return jsonb_build_object('ok', false, 'error', 'forbidden');
  end if;

  if exists (select 1 from popo_ledger l where l.user_id = uid and l.request_id = request_id) then
    return public._popo_ok(uid);
  end if;

  select * into v_item from catalog c where c.id = item_id and c.active;
  if not found then
    return public._popo_fail(uid, 'not_found');
  end if;

  if v_item.price = 0 or exists (select 1 from inventory i where i.user_id = uid and i.item_id = item_id) then
    return public._popo_fail(uid, 'already_owned');
  end if;

  v_balance := public._popo_balance(uid);
  if v_balance < v_item.price then
    return public._popo_fail(uid, 'insufficient:' || (v_item.price - v_balance));
  end if;

  insert into popo_ledger (user_id, delta, reason, ref_id, request_id)
  values (uid, -v_item.price, 'purchase', item_id, request_id);

  insert into inventory (user_id, item_id) values (uid, item_id);

  return public._popo_ok(uid);
end;
$$;

-- ---------- gift ----------
-- amount ≥ 1, memo ≤ 20자, 본인 제외, 잔액 ≥ amount, 오늘 gift_out 합 + amount ≤ 30.
-- 원장 2줄: gift_out −amount (peer_id = 받는 사람), gift_in +amount (peer_id = 보낸 사람). 둘 다 ref_id = request_id.

create or replace function public.gift(to_user uuid, amount int, memo text, request_id uuid)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
#variable_conflict use_variable
declare
  uid uuid := auth.uid();
  v_balance int;
  v_sent_today int;
  v_memo text := nullif(trim(coalesce(memo, '')), '');
begin
  if uid is null then
    return jsonb_build_object('ok', false, 'error', 'forbidden');
  end if;

  if to_user is null or to_user = uid then
    return public._popo_fail(uid, 'self');
  end if;
  if amount is null or amount < 1 then
    return public._popo_fail(uid, 'bad_amount');
  end if;
  if v_memo is not null and char_length(v_memo) > 20 then
    return public._popo_fail(uid, 'bad_memo');
  end if;
  if not exists (select 1 from users u where u.id = to_user) then
    return public._popo_fail(uid, 'not_found');
  end if;

  -- 두 사람 행을 id 순서로 잠가 교착을 피한다
  perform 1 from users u where u.id in (uid, to_user) order by u.id for update;

  if exists (select 1 from popo_ledger l where l.user_id = uid and l.request_id = request_id) then
    return public._popo_ok(uid);
  end if;

  v_balance := public._popo_balance(uid);
  if v_balance < amount then
    return public._popo_fail(uid, 'insufficient:' || (amount - v_balance));
  end if;

  select coalesce(sum(-l.delta), 0) into v_sent_today
  from popo_ledger l
  where l.user_id = uid and l.reason = 'gift_out'
    and public._popo_today(l.created_at) = public._popo_today(now());

  if v_sent_today + amount > 30 then
    return public._popo_fail(uid, 'daily_limit');
  end if;

  insert into popo_ledger (user_id, delta, reason, ref_id, request_id, peer_id, memo)
  values (uid, -amount, 'gift_out', request_id::text, request_id, to_user, v_memo);

  insert into popo_ledger (user_id, delta, reason, ref_id, request_id, peer_id, memo)
  values (to_user, amount, 'gift_in', request_id::text, request_id, uid, v_memo);

  return public._popo_ok(uid);
end;
$$;

-- ---------- equip ----------
-- slot ∈ character, bubble, ball, chair. chair 는 item_id null 허용(벗기).
-- 보유(또는 가격 0)인지 확인한 뒤 users 컬럼을 갱신한다.

create or replace function public.equip(slot text, item_id text)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
#variable_conflict use_variable
declare
  uid uuid := auth.uid();
  v_item catalog%rowtype;
begin
  if uid is null then
    return jsonb_build_object('ok', false, 'error', 'forbidden');
  end if;

  if slot not in ('character', 'bubble', 'ball', 'chair') then
    return public._popo_fail(uid, 'bad_slot');
  end if;

  perform 1 from users u where u.id = uid for update;
  if not found then
    return jsonb_build_object('ok', false, 'error', 'forbidden');
  end if;

  if item_id is null or item_id = '' then
    if slot <> 'chair' then
      return public._popo_fail(uid, 'not_owned');
    end if;
    update users u set chair_id = null where u.id = uid;
    return public._popo_ok(uid);
  end if;

  select * into v_item from catalog c where c.id = item_id and c.active and c.category = slot;
  if not found then
    return public._popo_fail(uid, 'not_found');
  end if;

  if v_item.price > 0 and not exists (select 1 from inventory i where i.user_id = uid and i.item_id = item_id) then
    return public._popo_fail(uid, 'not_owned');
  end if;

  case slot
    when 'character' then update users u set character_id = item_id where u.id = uid;
    when 'bubble'    then update users u set bubble_id = item_id where u.id = uid;
    when 'ball'      then update users u set ball_id = item_id where u.id = uid;
    when 'chair'     then update users u set chair_id = item_id where u.id = uid;
  end case;

  return public._popo_ok(uid);
end;
$$;

-- ---------- admin_grant ----------
-- 관리자(users.is_admin)만. reason admin 으로 원장 추가. 음수도 허용(회수).

create or replace function public.admin_grant(to_user uuid, amount int, memo text)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
#variable_conflict use_variable
declare
  uid uuid := auth.uid();
  v_memo text := nullif(left(trim(coalesce(memo, '')), 20), '');
begin
  if uid is null or not exists (select 1 from users u where u.id = uid and u.is_admin) then
    return jsonb_build_object('ok', false, 'error', 'forbidden');
  end if;
  if amount is null or amount = 0 then
    return public._popo_fail(uid, 'bad_amount');
  end if;
  if not exists (select 1 from users u where u.id = to_user) then
    return public._popo_fail(uid, 'not_found');
  end if;

  perform 1 from users u where u.id = to_user for update;

  insert into popo_ledger (user_id, delta, reason, request_id, peer_id, memo)
  values (to_user, amount, 'admin', gen_random_uuid(), uid, v_memo);

  return jsonb_build_object('ok', true, 'balance', public._popo_balance(to_user));
end;
$$;

-- ---------- 권한 ----------
-- 로그인한 사용자만 호출. 도우미 함수는 바깥에서 못 부른다.

revoke all on function public.earn(text, uuid) from public, anon;
revoke all on function public.purchase(text, uuid) from public, anon;
revoke all on function public.gift(uuid, int, text, uuid) from public, anon;
revoke all on function public.equip(text, text) from public, anon;
revoke all on function public.admin_grant(uuid, int, text) from public, anon;
grant execute on function public.earn(text, uuid) to authenticated;
grant execute on function public.purchase(text, uuid) to authenticated;
grant execute on function public.gift(uuid, int, text, uuid) to authenticated;
grant execute on function public.equip(text, text) to authenticated;
grant execute on function public.admin_grant(uuid, int, text) to authenticated;

revoke all on function public._popo_today(timestamptz) from public, anon, authenticated;
revoke all on function public._popo_balance(uuid) from public, anon, authenticated;
revoke all on function public._popo_ok(uuid) from public, anon, authenticated;
revoke all on function public._popo_fail(uuid, text) from public, anon, authenticated;
