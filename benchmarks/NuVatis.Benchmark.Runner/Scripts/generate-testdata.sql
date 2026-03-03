-- 벤치마크 테스트 데이터 생성 스크립트
-- 작성자: 최진호
-- 작성일: 2026-03-04
--
-- 목표: 10만 로우 이상의 현실적인 테스트 데이터 생성
-- 테이블 구조:
--   - users (10만): 핵심 테이블
--   - addresses (15만): 1:N 관계, 사용자당 1-2개
--   - categories (100): 상품 카테고리 계층 (parent_id)
--   - products (1만): 카테고리별 상품
--   - orders (5만): 사용자 주문
--   - order_items (20만): 주문 상세 (M:N)
--   - reviews (3만): 상품 리뷰
-- 총 로우: ~48만 로우

-- 기존 데이터 삭제 (테스트용)
TRUNCATE TABLE nuvatest.reviews CASCADE;
TRUNCATE TABLE nuvatest.order_items CASCADE;
TRUNCATE TABLE nuvatest.orders CASCADE;
TRUNCATE TABLE nuvatest.products CASCADE;
TRUNCATE TABLE nuvatest.categories CASCADE;
TRUNCATE TABLE nuvatest.addresses CASCADE;
TRUNCATE TABLE nuvatest.users CASCADE;

-- ========================================
-- 1. users 테이블: 10만 로우
-- ========================================
-- 전략: generate_series + random 함수로 대량 생성

INSERT INTO nuvatest.users (user_name, email, full_name, password_hash, date_of_birth, phone_number, is_active, created_at, updated_at)
SELECT
    'user' || i::TEXT,
    'user' || i::TEXT || '@test' || (i % 10)::TEXT || '.com',
    'User ' || i::TEXT || ' Full Name',
    '$2a$10$' || md5(random()::TEXT),
    DATE '1950-01-01' + (random() * 25550)::INT,
    '010-' || LPAD((random() * 10000)::INT::TEXT, 4, '0') || '-' || LPAD((random() * 10000)::INT::TEXT, 4, '0'),
    CASE WHEN random() > 0.1 THEN true ELSE false END,
    TIMESTAMP '2020-01-01' + (random() * 1460)::INT * INTERVAL '1 day',
    TIMESTAMP '2020-01-01' + (random() * 1460)::INT * INTERVAL '1 day'
FROM generate_series(1, 100000) AS i;

-- 인덱스 활용을 위한 통계 업데이트
ANALYZE nuvatest.users;

-- ========================================
-- 2. addresses 테이블: 15만 로우
-- ========================================
-- 전략: users와 조인하여 1:1.5 비율로 생성

INSERT INTO nuvatest.addresses (user_id, street, city, state, country, postal_code, is_primary, created_at)
SELECT
    u.id,
    'Street ' || (random() * 1000)::INT::TEXT,
    CASE (random() * 10)::INT
        WHEN 0 THEN '서울'
        WHEN 1 THEN '부산'
        WHEN 2 THEN '대구'
        WHEN 3 THEN '인천'
        WHEN 4 THEN '광주'
        WHEN 5 THEN '대전'
        WHEN 6 THEN '울산'
        WHEN 7 THEN '세종'
        WHEN 8 THEN '경기'
        ELSE '강원'
    END,
    CASE (random() * 5)::INT
        WHEN 0 THEN '경기도'
        WHEN 1 THEN '강원도'
        WHEN 2 THEN '충청도'
        WHEN 3 THEN '전라도'
        ELSE '경상도'
    END,
    'Korea',
    LPAD((random() * 100000)::INT::TEXT, 5, '0'),
    row_number() OVER (PARTITION BY u.id ORDER BY random()) = 1,
    u.created_at
FROM nuvatest.users u
CROSS JOIN generate_series(1, 2) -- 사용자당 0-2개 주소
WHERE random() < 0.75; -- 75% 확률로 생성

ANALYZE nuvatest.addresses;

-- ========================================
-- 3. categories 테이블: 100개 (계층 구조)
-- ========================================
-- 전략: 루트 카테고리 + 서브 카테고리 2단계

-- 루트 카테고리 (10개)
INSERT INTO nuvatest.categories (name, parent_id, description, created_at)
SELECT
    'Category ' || i::TEXT,
    NULL,
    'Root category ' || i::TEXT,
    TIMESTAMP '2020-01-01'
FROM generate_series(1, 10) AS i;

-- 서브 카테고리 1단계 (50개)
INSERT INTO nuvatest.categories (name, parent_id, description, created_at)
SELECT
    'Subcategory ' || parent.id::TEXT || '-' || sub::TEXT,
    parent.id,
    'Subcategory of ' || parent.name,
    TIMESTAMP '2020-01-01'
FROM nuvatest.categories parent
CROSS JOIN generate_series(1, 5) AS sub
WHERE parent.parent_id IS NULL;

-- 서브 카테고리 2단계 (40개)
INSERT INTO nuvatest.categories (name, parent_id, description, created_at)
SELECT
    'Sub-subcategory ' || parent.id::TEXT || '-' || sub::TEXT,
    parent.id,
    'Sub-subcategory of ' || parent.name,
    TIMESTAMP '2020-01-01'
FROM (
    SELECT id, name FROM nuvatest.categories WHERE parent_id IS NOT NULL LIMIT 10
) parent
CROSS JOIN generate_series(1, 4) AS sub;

ANALYZE nuvatest.categories;

-- ========================================
-- 4. products 테이블: 1만 로우
-- ========================================
-- 전략: 리프 카테고리에만 상품 배치

INSERT INTO nuvatest.products (category_id, name, description, price, stock, is_active, created_at, updated_at)
SELECT
    c.id,
    'Product ' || i::TEXT || ' in ' || c.name,
    'Description for product ' || i::TEXT,
    (random() * 1000000)::INT / 100.0,
    (random() * 1000)::INT,
    CASE WHEN random() > 0.05 THEN true ELSE false END,
    TIMESTAMP '2020-01-01' + (random() * 1460)::INT * INTERVAL '1 day',
    TIMESTAMP '2020-01-01' + (random() * 1460)::INT * INTERVAL '1 day'
FROM (
    SELECT id, name FROM nuvatest.categories
    WHERE id NOT IN (SELECT DISTINCT parent_id FROM nuvatest.categories WHERE parent_id IS NOT NULL)
    LIMIT 50
) c
CROSS JOIN generate_series(1, 200) AS i;

ANALYZE nuvatest.products;

-- ========================================
-- 5. coupons 테이블: 1000개
-- ========================================
INSERT INTO nuvatest.coupons (coupon_code, discount_rate, is_active, created_at)
SELECT
    'COUPON-' || LPAD(i::TEXT, 6, '0'),
    (random() * 30 + 5)::DECIMAL(5,2), -- 5~35% 할인율
    CASE WHEN random() > 0.2 THEN true ELSE false END,
    TIMESTAMP '2020-01-01' + (random() * 1460)::INT * INTERVAL '1 day'
FROM generate_series(1, 1000) AS i;

ANALYZE nuvatest.coupons;

-- ========================================
-- 6. orders 테이블: 5만 로우
-- ========================================
-- 전략: 활성 사용자의 60%가 평균 1개 주문

INSERT INTO nuvatest.orders (user_id, order_number, order_status, subtotal, discount_amount, tax_amount, shipping_fee, total_amount, coupon_id, created_at, updated_at)
SELECT
    u.id,
    'ORD-' || TO_CHAR(TIMESTAMP '2020-01-01' + (random() * 1460)::INT * INTERVAL '1 day', 'YYYYMMDD') || '-' || LPAD(gen_random_uuid()::TEXT, 8, '0'),
    CASE (random() * 5)::INT
        WHEN 0 THEN 'pending'
        WHEN 1 THEN 'processing'
        WHEN 2 THEN 'shipped'
        WHEN 3 THEN 'delivered'
        ELSE 'cancelled'
    END,
    (random() * 500000)::INT / 100.0, -- subtotal
    CASE WHEN random() > 0.3 THEN (random() * 50000)::INT / 100.0 ELSE 0 END, -- discount_amount (70% 확률로 할인)
    ((random() * 500000)::INT / 100.0) * 0.1, -- tax_amount (10% VAT)
    CASE WHEN (random() * 500000)::INT / 100.0 >= 50000 THEN 0 ELSE 3000 END, -- shipping_fee (5만원 이상 무료배송)
    ((random() * 500000)::INT / 100.0) * 1.1 + CASE WHEN (random() * 500000)::INT / 100.0 >= 50000 THEN 0 ELSE 3000 END, -- total_amount
    CASE WHEN random() > 0.3 THEN (SELECT id FROM nuvatest.coupons WHERE is_active = true ORDER BY random() LIMIT 1) ELSE NULL END, -- coupon_id (70% 확률)
    TIMESTAMP '2020-01-01' + (random() * 1460)::INT * INTERVAL '1 day',
    TIMESTAMP '2020-01-01' + (random() * 1460)::INT * INTERVAL '1 day'
FROM nuvatest.users u
WHERE u.is_active = true
    AND random() < 0.6; -- 활성 사용자의 60%만 주문

ANALYZE nuvatest.orders;

-- ========================================
-- 7. order_items 테이블: 20만 로우
-- ========================================
-- 전략: 주문당 평균 4개 상품

INSERT INTO nuvatest.order_items (order_id, product_id, quantity, unit_price, subtotal, created_at)
SELECT
    o.id,
    p.id,
    (random() * 5)::INT + 1,
    p.price,
    p.price * ((random() * 5)::INT + 1),
    o.created_at
FROM nuvatest.orders o
CROSS JOIN LATERAL (
    SELECT id, price
    FROM nuvatest.products
    WHERE is_active = true
    ORDER BY random()
    LIMIT (random() * 6)::INT + 1
) p;

ANALYZE nuvatest.order_items;

-- ========================================
-- 8. reviews 테이블: 3만 로우
-- ========================================
-- 전략: 구매한 상품의 30%에 대해 리뷰 작성

INSERT INTO nuvatest.reviews (product_id, user_id, rating, comment, created_at)
SELECT DISTINCT ON (oi.product_id, o.user_id)
    oi.product_id,
    o.user_id,
    GREATEST(1, LEAST(5, floor(random() * 5)::INT + 1)), -- 안전하게 1-5 범위 보장
    'Review comment ' || (random() * 100000)::INT::TEXT,
    o.created_at + (random() * 30)::INT * INTERVAL '1 day'
FROM nuvatest.order_items oi
JOIN nuvatest.orders o ON oi.order_id = o.id
WHERE random() < 0.2 -- 20% 확률로 리뷰 작성
LIMIT 30000;

ANALYZE nuvatest.reviews;

-- ========================================
-- 최종 통계 및 검증
-- ========================================

DO $$
DECLARE
    users_count INT;
    addresses_count INT;
    categories_count INT;
    products_count INT;
    orders_count INT;
    order_items_count INT;
    reviews_count INT;
    total_count INT;
BEGIN
    SELECT COUNT(*) INTO users_count FROM nuvatest.users;
    SELECT COUNT(*) INTO addresses_count FROM nuvatest.addresses;
    SELECT COUNT(*) INTO categories_count FROM nuvatest.categories;
    SELECT COUNT(*) INTO products_count FROM nuvatest.products;
    SELECT COUNT(*) INTO orders_count FROM nuvatest.orders;
    SELECT COUNT(*) INTO order_items_count FROM nuvatest.order_items;
    SELECT COUNT(*) INTO reviews_count FROM nuvatest.reviews;

    total_count := users_count + addresses_count + categories_count + products_count + orders_count + order_items_count + reviews_count;

    RAISE NOTICE '========================================';
    RAISE NOTICE '테스트 데이터 생성 완료';
    RAISE NOTICE '========================================';
    RAISE NOTICE 'users:       % rows', users_count;
    RAISE NOTICE 'addresses:   % rows', addresses_count;
    RAISE NOTICE 'categories:  % rows', categories_count;
    RAISE NOTICE 'products:    % rows', products_count;
    RAISE NOTICE 'orders:      % rows', orders_count;
    RAISE NOTICE 'order_items: % rows', order_items_count;
    RAISE NOTICE 'reviews:     % rows', reviews_count;
    RAISE NOTICE '----------------------------------------';
    RAISE NOTICE '총 로우:     % rows', total_count;
    RAISE NOTICE '========================================';

    IF total_count < 100000 THEN
        RAISE WARNING '총 로우 수가 10만 미만입니다: %', total_count;
    END IF;
END $$;

-- 모든 테이블 통계 업데이트 (쿼리 최적화를 위해 필수)
ANALYZE nuvatest.users;
ANALYZE nuvatest.addresses;
ANALYZE nuvatest.categories;
ANALYZE nuvatest.products;
ANALYZE nuvatest.orders;
ANALYZE nuvatest.order_items;
ANALYZE nuvatest.reviews;

-- 완료 메시지
SELECT '테스트 데이터 생성이 완료되었습니다. 벤치마크를 실행할 수 있습니다.' AS message;
