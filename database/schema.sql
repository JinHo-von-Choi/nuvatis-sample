--
-- NuVatis 샘플 데이터베이스 스키마
-- PostgreSQL
--
-- 작성자: 최진호
-- 작성일: 2026-03-01
--

-- 사용자 테이블
CREATE TABLE IF NOT EXISTS users (
    id         SERIAL PRIMARY KEY,
    user_name  VARCHAR(100) NOT NULL,
    email      VARCHAR(255) NOT NULL UNIQUE,
    full_name  VARCHAR(200),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP,
    is_active  BOOLEAN NOT NULL DEFAULT true
);

-- 상품 테이블
CREATE TABLE IF NOT EXISTS products (
    id           SERIAL PRIMARY KEY,
    product_code VARCHAR(50) NOT NULL UNIQUE,
    product_name VARCHAR(200) NOT NULL,
    description  TEXT,
    price        DECIMAL(18, 2) NOT NULL,
    stock_qty    INTEGER NOT NULL DEFAULT 0,
    category     VARCHAR(100) NOT NULL,
    created_at   TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at   TIMESTAMP,
    is_active    BOOLEAN NOT NULL DEFAULT true
);

-- 주문 테이블
CREATE TABLE IF NOT EXISTS orders (
    id           SERIAL PRIMARY KEY,
    order_no     VARCHAR(50) NOT NULL UNIQUE,
    user_id      INTEGER NOT NULL REFERENCES users(id),
    total_amount DECIMAL(18, 2) NOT NULL,
    status       VARCHAR(20) NOT NULL DEFAULT 'PENDING',
    order_date   TIMESTAMP NOT NULL,
    shipped_date TIMESTAMP,
    created_at   TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at   TIMESTAMP
);

-- 주문 상세 테이블
CREATE TABLE IF NOT EXISTS order_items (
    id         SERIAL PRIMARY KEY,
    order_id   INTEGER NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    product_id INTEGER NOT NULL REFERENCES products(id),
    quantity   INTEGER NOT NULL,
    unit_price DECIMAL(18, 2) NOT NULL,
    subtotal   DECIMAL(18, 2) NOT NULL
);

-- 인덱스 생성
CREATE INDEX IF NOT EXISTS idx_users_email ON users(email);
CREATE INDEX IF NOT EXISTS idx_users_is_active ON users(is_active);
CREATE INDEX IF NOT EXISTS idx_products_category ON products(category);
CREATE INDEX IF NOT EXISTS idx_products_is_active ON products(is_active);
CREATE INDEX IF NOT EXISTS idx_orders_user_id ON orders(user_id);
CREATE INDEX IF NOT EXISTS idx_orders_status ON orders(status);
CREATE INDEX IF NOT EXISTS idx_order_items_order_id ON order_items(order_id);
CREATE INDEX IF NOT EXISTS idx_order_items_product_id ON order_items(product_id);
