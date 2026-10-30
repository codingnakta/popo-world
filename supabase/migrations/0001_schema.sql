-- 0001_schema.sql
-- Supabase SQL Editor 에 그대로 붙여 넣으면 표 5개와 잔액 뷰가 생긴다.
-- users.id 는 Supabase auth 의 사용자 id 와 같다.

create extension if not exists pgcrypto;

create table users (
  id uuid primary key references auth.users(id) on delete cascade,
  name text not null check (char_length(name) between 1 and 8),
  character_id text not null default 'char_hamster',
  bubble_id text not null default 'bubble_basic',
  ball_id text not null default 'ball_soft',
  chair_id text,
  is_admin boolean not null default false,
  last_seen timestamptz not null default now(),
  created_at timestamptz not null default now()
);

create table messages (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references users(id),
  body text not null check (char_length(body) between 1 and 200),
  created_at timestamptz not null default now()
);
create index on messages (created_at desc);

create table catalog (
  id text primary key,                 -- 'char_penguin', 'ball_snow'
  category text not null check (category in ('character','bubble','ball','chair')),
  name text not null,
  price int not null check (price >= 0),
  image_url text,
  effect text,                         -- 'white_flash', 'red_stain' 등, 클라이언트가 해석
  sort int not null default 0,
  active boolean not null default true
);

create table inventory (
  user_id uuid not null references users(id),
  item_id text not null references catalog(id),
  acquired_at timestamptz not null default now(),
  primary key (user_id, item_id)
);

create table popo_ledger (
  id bigint generated always as identity primary key,
  user_id uuid not null references users(id),
  delta int not null check (delta <> 0),
  reason text not null check (reason in
    ('daily_login','message','hit','streak','admin','purchase','gift_out','gift_in')),
  ref_id text,                         -- 선물 쌍·구매 아이템 id 등
  request_id uuid not null,            -- 중복 요청 차단
  peer_id uuid references users(id),   -- 선물 상대 (gift_in: 보낸 사람, gift_out: 받은 사람, admin: 관리자)
  memo text check (char_length(memo) <= 20),
  created_at timestamptz not null default now()
);
create unique index popo_ledger_req on popo_ledger (user_id, request_id);
create index on popo_ledger (user_id, created_at desc);

create view balances as
  select user_id, coalesce(sum(delta), 0)::int as balance
  from popo_ledger group by user_id;

-- 기본 아이템 (가격 0)은 모두 보유한 것으로 본다
insert into catalog (id, category, name, price, sort) values
  ('char_hamster','character','햄스터',0,1),
  ('char_cat','character','고양이',0,2),
  ('char_dog','character','강아지',0,3),
  ('char_penguin','character','펭귄',30,4),
  ('char_rabbit','character','토끼',30,5),
  ('bubble_basic','bubble','기본 말풍선',0,1),
  ('bubble_pixel','bubble','픽셀 테두리',15,2),
  ('bubble_cloud','bubble','구름',15,3),
  ('ball_soft','ball','말랑공',0,1),
  ('ball_snow','ball','눈덩이',10,2),
  ('ball_tomato','ball','토마토',10,3),
  ('chair_small','chair','작은 의자',20,1);
update catalog set effect = 'white_flash' where id = 'ball_snow';
update catalog set effect = 'red_stain' where id = 'ball_tomato';

-- Realtime Postgres Changes 가 이 세 표의 변경을 내보내게 한다
alter publication supabase_realtime add table messages, users, popo_ledger;

-- 관리자가 대시보드(Authentication > Users)에서 계정을 만들면 users 행이 자동으로 생긴다.
-- 이름은 user metadata 의 name, 없으면 이메일 @ 앞부분(8자까지). 대시보드에서 나중에 고쳐도 된다.
create or replace function public.handle_new_auth_user()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
  insert into public.users (id, name)
  values (
    new.id,
    left(coalesce(nullif(new.raw_user_meta_data ->> 'name', ''), split_part(coalesce(new.email, 'popo'), '@', 1)), 8)
  )
  on conflict (id) do nothing;
  return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
  after insert on auth.users
  for each row execute function public.handle_new_auth_user();

-- 3일 지난 메시지 삭제 (Database > Extensions 에서 pg_cron 을 켠 뒤 실행)
-- select cron.schedule('purge-messages', '0 4 * * *',
--   $$delete from messages where created_at < now() - interval '3 days'$$);
