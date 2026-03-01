/**
 * NuVatis 대규모 ORM 벤치마크 데이터베이스 스키마
 * 작성자: 최진호
 * 작성일: 2026-03-01
 *
 * 총 15개 테이블, 예상 데이터 크기: ~70GB
 * 대상: PostgreSQL 14+
 */

-- 기존 테이블 삭제 (역순)
DROP TABLE IF EXISTS audit_logs CASCADE;
DROP TABLE IF EXISTS wishlists CASCADE;
DROP TABLE IF EXISTS user_coupons CASCADE;
DROP TABLE IF EXISTS inventory_logs CASCADE;
DROP TABLE IF EXISTS shipments CASCADE;
DROP TABLE IF EXISTS payments CASCADE;
DROP TABLE IF EXISTS reviews CASCADE;
DROP TABLE IF EXISTS order_items CASCADE;
DROP TABLE IF EXISTS orders CASCADE;
DROP TABLE IF EXISTS product_images CASCADE;
DROP TABLE IF EXISTS products CASCADE;
DROP TABLE IF EXISTS coupons CASCADE;
DROP TABLE IF EXISTS categories CASCADE;
DROP TABLE IF EXISTS addresses CASCADE;
DROP TABLE IF EXISTS users CASCADE;

-- 1. users (100K)
-- 기본 사용자 정보
CREATE TABLE users (
    id                BIGSERIAL PRIMARY KEY,
    user_name         VARCHAR(100) NOT NULL,
    email             VARCHAR(255) NOT NULL UNIQUE,
    full_name         VARCHAR(200) NOT NULL,
    password_hash     VARCHAR(255) NOT NULL,
    date_of_birth     DATE,
    phone_number      VARCHAR(20),
    is_active         BOOLEAN DEFAULT true,
    created_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_user_name ON users(user_name);
CREATE INDEX idx_users_created_at ON users(created_at);

-- 2. addresses (150K)
-- 사용자 주소 정보 (1:N)
CREATE TABLE addresses (
    id                BIGSERIAL PRIMARY KEY,
    user_id           BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    address_type      VARCHAR(20) NOT NULL, -- 'shipping', 'billing'
    street_address    VARCHAR(500) NOT NULL,
    city              VARCHAR(100) NOT NULL,
    state             VARCHAR(100),
    postal_code       VARCHAR(20),
    country           VARCHAR(100) NOT NULL,
    is_default        BOOLEAN DEFAULT false,
    created_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_addresses_user_id ON addresses(user_id);

-- 3. categories (500)
-- 계층형 카테고리 (자기참조)
CREATE TABLE categories (
    id                BIGSERIAL PRIMARY KEY,
    parent_id         BIGINT REFERENCES categories(id) ON DELETE SET NULL,
    category_name     VARCHAR(200) NOT NULL,
    description       TEXT,
    display_order     INT DEFAULT 0,
    is_active         BOOLEAN DEFAULT true,
    created_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_categories_parent_id ON categories(parent_id);
CREATE INDEX idx_categories_name ON categories(category_name);

-- 4. products (50K)
-- 상품 정보
CREATE TABLE products (
    id                BIGSERIAL PRIMARY KEY,
    category_id       BIGINT NOT NULL REFERENCES categories(id) ON DELETE RESTRICT,
    product_name      VARCHAR(300) NOT NULL,
    description       TEXT,
    price             DECIMAL(12, 2) NOT NULL,
    cost_price        DECIMAL(12, 2),
    stock_quantity    INT NOT NULL DEFAULT 0,
    sku               VARCHAR(100) UNIQUE,
    is_active         BOOLEAN DEFAULT true,
    created_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_products_category_id ON products(category_id);
CREATE INDEX idx_products_sku ON products(sku);
CREATE INDEX idx_products_price ON products(price);
CREATE INDEX idx_products_name ON products(product_name);

-- 5. product_images (100K)
-- 상품 이미지 (1:N)
CREATE TABLE product_images (
    id                BIGSERIAL PRIMARY KEY,
    product_id        BIGINT NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    image_url         VARCHAR(500) NOT NULL,
    alt_text          VARCHAR(255),
    display_order     INT DEFAULT 0,
    is_primary        BOOLEAN DEFAULT false,
    created_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_product_images_product_id ON product_images(product_id);

-- 6. coupons (1K)
-- 쿠폰 마스터
CREATE TABLE coupons (
    id                BIGSERIAL PRIMARY KEY,
    coupon_code       VARCHAR(50) NOT NULL UNIQUE,
    discount_type     VARCHAR(20) NOT NULL, -- 'percentage', 'fixed'
    discount_value    DECIMAL(12, 2) NOT NULL,
    min_order_amount  DECIMAL(12, 2),
    max_discount      DECIMAL(12, 2),
    valid_from        TIMESTAMP NOT NULL,
    valid_until       TIMESTAMP NOT NULL,
    usage_limit       INT,
    usage_count       INT DEFAULT 0,
    is_active         BOOLEAN DEFAULT true,
    created_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_coupons_code ON coupons(coupon_code);
CREATE INDEX idx_coupons_valid_dates ON coupons(valid_from, valid_until);

-- 7. orders (10M)
-- 주문 정보 (3년치 데이터)
CREATE TABLE orders (
    id                BIGSERIAL PRIMARY KEY,
    user_id           BIGINT NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    order_number      VARCHAR(50) NOT NULL UNIQUE,
    order_status      VARCHAR(20) NOT NULL, -- 'pending', 'processing', 'shipped', 'delivered', 'cancelled'
    subtotal          DECIMAL(12, 2) NOT NULL,
    discount_amount   DECIMAL(12, 2) DEFAULT 0,
    tax_amount        DECIMAL(12, 2) DEFAULT 0,
    shipping_fee      DECIMAL(12, 2) DEFAULT 0,
    total_amount      DECIMAL(12, 2) NOT NULL,
    coupon_id         BIGINT REFERENCES coupons(id) ON DELETE SET NULL,
    created_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_orders_user_id ON orders(user_id);
CREATE INDEX idx_orders_user_created ON orders(user_id, created_at DESC);
CREATE INDEX idx_orders_number ON orders(order_number);
CREATE INDEX idx_orders_status ON orders(order_status);
CREATE INDEX idx_orders_created_at ON orders(created_at DESC);

-- 8. order_items (50M)
-- 주문 상세 항목
CREATE TABLE order_items (
    id                BIGSERIAL PRIMARY KEY,
    order_id          BIGINT NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    product_id        BIGINT NOT NULL REFERENCES products(id) ON DELETE RESTRICT,
    quantity          INT NOT NULL,
    unit_price        DECIMAL(12, 2) NOT NULL,
    discount_amount   DECIMAL(12, 2) DEFAULT 0,
    total_price       DECIMAL(12, 2) NOT NULL,
    created_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_order_items_order_id ON order_items(order_id);
CREATE INDEX idx_order_items_product_id ON order_items(product_id);
CREATE INDEX idx_order_items_order_product ON order_items(order_id, product_id);

-- 9. reviews (5M)
-- 상품 리뷰
CREATE TABLE reviews (
    id                BIGSERIAL PRIMARY KEY,
    user_id           BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    product_id        BIGINT NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    rating            INT NOT NULL CHECK (rating BETWEEN 1 AND 5),
    title             VARCHAR(200),
    content           TEXT,
    is_verified       BOOLEAN DEFAULT false,
    helpful_count     INT DEFAULT 0,
    created_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_reviews_product_id ON reviews(product_id);
CREATE INDEX idx_reviews_product_created ON reviews(product_id, created_at DESC);
CREATE INDEX idx_reviews_user_id ON reviews(user_id);
CREATE INDEX idx_reviews_rating ON reviews(rating);

-- 10. payments (10M)
-- 결제 정보 (1:1 with orders)
CREATE TABLE payments (
    id                BIGSERIAL PRIMARY KEY,
    order_id          BIGINT NOT NULL UNIQUE REFERENCES orders(id) ON DELETE RESTRICT,
    payment_method    VARCHAR(50) NOT NULL, -- 'card', 'bank_transfer', 'paypal'
    payment_status    VARCHAR(20) NOT NULL, -- 'pending', 'completed', 'failed', 'refunded'
    transaction_id    VARCHAR(100) UNIQUE,
    amount            DECIMAL(12, 2) NOT NULL,
    paid_at           TIMESTAMP,
    created_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_payments_order_id ON payments(order_id);
CREATE INDEX idx_payments_status ON payments(payment_status);
CREATE INDEX idx_payments_transaction_id ON payments(transaction_id);

-- 11. shipments (10M)
-- 배송 정보 (1:1 with orders)
CREATE TABLE shipments (
    id                BIGSERIAL PRIMARY KEY,
    order_id          BIGINT NOT NULL UNIQUE REFERENCES orders(id) ON DELETE RESTRICT,
    tracking_number   VARCHAR(100) UNIQUE,
    carrier           VARCHAR(100),
    shipment_status   VARCHAR(20) NOT NULL, -- 'preparing', 'shipped', 'in_transit', 'delivered'
    shipped_at        TIMESTAMP,
    delivered_at      TIMESTAMP,
    created_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_shipments_order_id ON shipments(order_id);
CREATE INDEX idx_shipments_tracking ON shipments(tracking_number);
CREATE INDEX idx_shipments_status ON shipments(shipment_status);

-- 12. inventory_logs (20M)
-- 재고 변동 이력
CREATE TABLE inventory_logs (
    id                BIGSERIAL PRIMARY KEY,
    product_id        BIGINT NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    change_type       VARCHAR(20) NOT NULL, -- 'restock', 'sale', 'return', 'adjustment'
    quantity_change   INT NOT NULL,
    quantity_before   INT NOT NULL,
    quantity_after    INT NOT NULL,
    reference_id      BIGINT, -- order_id or other reference
    notes             TEXT,
    created_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_inventory_logs_product_id ON inventory_logs(product_id);
CREATE INDEX idx_inventory_logs_product_created ON inventory_logs(product_id, created_at DESC);
CREATE INDEX idx_inventory_logs_type ON inventory_logs(change_type);

-- 13. user_coupons (500K)
-- 사용자-쿠폰 관계 (N:M)
CREATE TABLE user_coupons (
    id                BIGSERIAL PRIMARY KEY,
    user_id           BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    coupon_id         BIGINT NOT NULL REFERENCES coupons(id) ON DELETE CASCADE,
    is_used           BOOLEAN DEFAULT false,
    used_at           TIMESTAMP,
    created_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(user_id, coupon_id)
);

CREATE INDEX idx_user_coupons_user_id ON user_coupons(user_id);
CREATE INDEX idx_user_coupons_coupon_id ON user_coupons(coupon_id);

-- 14. wishlists (2M)
-- 위시리스트 (N:M)
CREATE TABLE wishlists (
    id                BIGSERIAL PRIMARY KEY,
    user_id           BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    product_id        BIGINT NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    created_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(user_id, product_id)
);

CREATE INDEX idx_wishlists_user_id ON wishlists(user_id);
CREATE INDEX idx_wishlists_product_id ON wishlists(product_id);

-- 15. audit_logs (50M)
-- 감사 로그 (모든 변경 이력)
CREATE TABLE audit_logs (
    id                BIGSERIAL PRIMARY KEY,
    table_name        VARCHAR(100) NOT NULL,
    record_id         BIGINT NOT NULL,
    action            VARCHAR(20) NOT NULL, -- 'INSERT', 'UPDATE', 'DELETE'
    old_values        JSONB,
    new_values        JSONB,
    changed_by        BIGINT, -- user_id
    created_at        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_audit_logs_table_name ON audit_logs(table_name);
CREATE INDEX idx_audit_logs_record_id ON audit_logs(record_id);
CREATE INDEX idx_audit_logs_table_record ON audit_logs(table_name, record_id);
CREATE INDEX idx_audit_logs_created_at ON audit_logs(created_at DESC);

-- 통계 정보 업데이트 (벤치마크 전 실행 필수)
ANALYZE users;
ANALYZE addresses;
ANALYZE categories;
ANALYZE products;
ANALYZE product_images;
ANALYZE coupons;
ANALYZE orders;
ANALYZE order_items;
ANALYZE reviews;
ANALYZE payments;
ANALYZE shipments;
ANALYZE inventory_logs;
ANALYZE user_coupons;
ANALYZE wishlists;
ANALYZE audit_logs;

-- 테이블 크기 확인 쿼리
/*
SELECT
    schemaname,
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size,
    pg_total_relation_size(schemaname||'.'||tablename) AS bytes
FROM pg_tables
WHERE schemaname = 'public'
ORDER BY bytes DESC;
*/
