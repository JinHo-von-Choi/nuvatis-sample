-- nuvatest 스키마 생성
CREATE SCHEMA IF NOT EXISTS nuvatest;

-- users 테이블
CREATE TABLE IF NOT EXISTS nuvatest.users (
    id BIGSERIAL PRIMARY KEY,
    user_name VARCHAR(100) NOT NULL,
    email VARCHAR(255) NOT NULL UNIQUE,
    full_name VARCHAR(200),
    password_hash VARCHAR(255) NOT NULL,
    date_of_birth DATE,
    phone_number VARCHAR(20),
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- addresses 테이블
CREATE TABLE IF NOT EXISTS nuvatest.addresses (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES nuvatest.users(id) ON DELETE CASCADE,
    street VARCHAR(255),
    city VARCHAR(100),
    state VARCHAR(100),
    country VARCHAR(100),
    postal_code VARCHAR(20),
    is_primary BOOLEAN DEFAULT false,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- categories 테이블
CREATE TABLE IF NOT EXISTS nuvatest.categories (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    parent_id BIGINT REFERENCES nuvatest.categories(id) ON DELETE SET NULL,
    description TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- products 테이블
CREATE TABLE IF NOT EXISTS nuvatest.products (
    id BIGSERIAL PRIMARY KEY,
    category_id BIGINT REFERENCES nuvatest.categories(id) ON DELETE SET NULL,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    price DECIMAL(10, 2) NOT NULL,
    stock INT DEFAULT 0,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- coupons 테이블 (orders 테이블보다 먼저 생성)
CREATE TABLE IF NOT EXISTS nuvatest.coupons (
    id BIGSERIAL PRIMARY KEY,
    coupon_code VARCHAR(50) UNIQUE NOT NULL,
    discount_rate DECIMAL(5, 2) NOT NULL,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- orders 테이블
CREATE TABLE IF NOT EXISTS nuvatest.orders (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL REFERENCES nuvatest.users(id) ON DELETE CASCADE,
    order_number VARCHAR(50) UNIQUE NOT NULL,
    order_status VARCHAR(20) NOT NULL DEFAULT 'pending',
    subtotal DECIMAL(10, 2) NOT NULL DEFAULT 0,
    discount_amount DECIMAL(10, 2) DEFAULT 0,
    tax_amount DECIMAL(10, 2) DEFAULT 0,
    shipping_fee DECIMAL(10, 2) DEFAULT 0,
    total_amount DECIMAL(10, 2) NOT NULL,
    coupon_id BIGINT REFERENCES nuvatest.coupons(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- order_items 테이블
CREATE TABLE IF NOT EXISTS nuvatest.order_items (
    id BIGSERIAL PRIMARY KEY,
    order_id BIGINT NOT NULL REFERENCES nuvatest.orders(id) ON DELETE CASCADE,
    product_id BIGINT NOT NULL REFERENCES nuvatest.products(id) ON DELETE CASCADE,
    quantity INT NOT NULL,
    unit_price DECIMAL(10, 2) NOT NULL,
    subtotal DECIMAL(10, 2) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- reviews 테이블
CREATE TABLE IF NOT EXISTS nuvatest.reviews (
    id BIGSERIAL PRIMARY KEY,
    product_id BIGINT NOT NULL REFERENCES nuvatest.products(id) ON DELETE CASCADE,
    user_id BIGINT NOT NULL REFERENCES nuvatest.users(id) ON DELETE CASCADE,
    rating INT CHECK (rating >= 1 AND rating <= 5),
    comment TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 인덱스 생성
CREATE INDEX IF NOT EXISTS idx_users_email ON nuvatest.users(email);
CREATE INDEX IF NOT EXISTS idx_users_is_active ON nuvatest.users(is_active);
CREATE INDEX IF NOT EXISTS idx_addresses_user_id ON nuvatest.addresses(user_id);
CREATE INDEX IF NOT EXISTS idx_categories_parent_id ON nuvatest.categories(parent_id);
CREATE INDEX IF NOT EXISTS idx_products_category_id ON nuvatest.products(category_id);
CREATE INDEX IF NOT EXISTS idx_products_is_active ON nuvatest.products(is_active);
CREATE INDEX IF NOT EXISTS idx_orders_user_id ON nuvatest.orders(user_id);
CREATE INDEX IF NOT EXISTS idx_orders_status ON nuvatest.orders(order_status);
CREATE INDEX IF NOT EXISTS idx_orders_coupon_id ON nuvatest.orders(coupon_id);
CREATE INDEX IF NOT EXISTS idx_orders_created_at ON nuvatest.orders(created_at);
CREATE INDEX IF NOT EXISTS idx_orders_user_created ON nuvatest.orders(user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_order_items_order_id ON nuvatest.order_items(order_id);
CREATE INDEX IF NOT EXISTS idx_order_items_product_id ON nuvatest.order_items(product_id);
CREATE INDEX IF NOT EXISTS idx_reviews_product_id ON nuvatest.reviews(product_id);
CREATE INDEX IF NOT EXISTS idx_reviews_user_id ON nuvatest.reviews(user_id);
