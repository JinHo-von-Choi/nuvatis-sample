--
-- NuVatis 샘플 데이터
-- PostgreSQL
--
-- 작성자: 최진호
-- 작성일: 2026-03-01
--

-- 사용자 샘플 데이터
INSERT INTO users (user_name, email, full_name, created_at, is_active) VALUES
('admin', 'admin@example.com', '관리자', CURRENT_TIMESTAMP, true),
('john_doe', 'john.doe@example.com', 'John Doe', CURRENT_TIMESTAMP, true),
('jane_smith', 'jane.smith@example.com', 'Jane Smith', CURRENT_TIMESTAMP, true),
('bob_wilson', 'bob.wilson@example.com', 'Bob Wilson', CURRENT_TIMESTAMP, true),
('alice_johnson', 'alice.johnson@example.com', 'Alice Johnson', CURRENT_TIMESTAMP, false);

-- 상품 샘플 데이터
INSERT INTO products (product_code, product_name, description, price, stock_qty, category, created_at, is_active) VALUES
('LAPTOP-001', 'ThinkPad X1 Carbon', '고성능 비즈니스 노트북', 2500000, 15, 'Electronics', CURRENT_TIMESTAMP, true),
('LAPTOP-002', 'MacBook Pro 16"', 'Apple M3 Max 탑재', 4200000, 8, 'Electronics', CURRENT_TIMESTAMP, true),
('PHONE-001', 'Galaxy S25 Ultra', '최신 플래그십 스마트폰', 1500000, 25, 'Electronics', CURRENT_TIMESTAMP, true),
('PHONE-002', 'iPhone 16 Pro', 'A18 Pro 칩 탑재', 1800000, 20, 'Electronics', CURRENT_TIMESTAMP, true),
('MOUSE-001', 'MX Master 3S', '무선 프리미엄 마우스', 120000, 50, 'Accessories', CURRENT_TIMESTAMP, true),
('KEYBOARD-001', 'MX Keys', '무선 백라이트 키보드', 180000, 35, 'Accessories', CURRENT_TIMESTAMP, true),
('MONITOR-001', 'Dell UltraSharp 27"', '4K UHD 모니터', 650000, 12, 'Electronics', CURRENT_TIMESTAMP, true),
('HEADSET-001', 'Sony WH-1000XM5', '노이즈 캔슬링 헤드셋', 420000, 18, 'Accessories', CURRENT_TIMESTAMP, true),
('TABLET-001', 'iPad Pro 12.9"', 'M2 칩 탑재', 1650000, 10, 'Electronics', CURRENT_TIMESTAMP, true),
('WEBCAM-001', 'Logitech Brio', '4K 웹캠', 280000, 22, 'Accessories', CURRENT_TIMESTAMP, true);

-- 주문 샘플 데이터
INSERT INTO orders (order_no, user_id, total_amount, status, order_date, created_at) VALUES
('ORD-2026-0001', 2, 2620000, 'COMPLETED', '2026-02-15 10:30:00', '2026-02-15 10:30:00'),
('ORD-2026-0002', 3, 4380000, 'SHIPPED', '2026-02-18 14:20:00', '2026-02-18 14:20:00'),
('ORD-2026-0003', 2, 1920000, 'PENDING', '2026-02-25 09:15:00', '2026-02-25 09:15:00'),
('ORD-2026-0004', 4, 830000, 'COMPLETED', '2026-02-28 16:45:00', '2026-02-28 16:45:00');

-- 주문 상세 샘플 데이터
-- 주문 1: ThinkPad + MX Master 3S
INSERT INTO order_items (order_id, product_id, quantity, unit_price, subtotal) VALUES
(1, 1, 1, 2500000, 2500000),
(1, 5, 1, 120000, 120000);

-- 주문 2: MacBook Pro + MX Keys + Webcam
INSERT INTO order_items (order_id, product_id, quantity, unit_price, subtotal) VALUES
(2, 2, 1, 4200000, 4200000),
(2, 6, 1, 180000, 180000);

-- 주문 3: iPhone 16 Pro + MX Master 3S
INSERT INTO order_items (order_id, product_id, quantity, unit_price, subtotal) VALUES
(3, 4, 1, 1800000, 1800000),
(3, 5, 1, 120000, 120000);

-- 주문 4: Dell Monitor + Headset
INSERT INTO order_items (order_id, product_id, quantity, unit_price, subtotal) VALUES
(4, 7, 1, 650000, 650000),
(4, 8, 1, 420000, 420000);

-- 재고 조정 (주문한 만큼 차감)
UPDATE products SET stock_qty = stock_qty - 1 WHERE id IN (1, 2, 4, 5, 6, 7, 8);
UPDATE products SET stock_qty = stock_qty - 2 WHERE id = 5; -- MX Master 3S가 2개 팔림
