-- ============================================================
-- Web bán hoa quả nhập khẩu 
-- ============================================================

-- BƯỚC 1: Xóa các bảng phụ thuộc (Bảng con - Chứa FK)
IF OBJECT_ID('coupon_usages', 'U')  IS NOT NULL DROP TABLE coupon_usages;
IF OBJECT_ID('payments', 'U')       IS NOT NULL DROP TABLE payments;
IF OBJECT_ID('order_items', 'U')    IS NOT NULL DROP TABLE order_items;
IF OBJECT_ID('inventory_logs', 'U') IS NOT NULL DROP TABLE inventory_logs;
IF OBJECT_ID('cart_items', 'U')     IS NOT NULL DROP TABLE cart_items;
IF OBJECT_ID('reviews', 'U')        IS NOT NULL DROP TABLE reviews;
IF OBJECT_ID('product_images', 'U') IS NOT NULL DROP TABLE product_images;

-- BƯỚC 2: Xóa các bảng nghiệp vụ chính (Chứa FK đến Master Data)
IF OBJECT_ID('orders', 'U')         IS NOT NULL DROP TABLE orders;
IF OBJECT_ID('products', 'U')       IS NOT NULL DROP TABLE products;

-- BƯỚC 3: Xóa các bảng danh mục/gốc (Bảng cha - Master Data)
IF OBJECT_ID('coupons', 'U')        IS NOT NULL DROP TABLE coupons;
IF OBJECT_ID('users', 'U')          IS NOT NULL DROP TABLE users;
IF OBJECT_ID('categories', 'U')     IS NOT NULL DROP TABLE categories;
IF OBJECT_ID('suppliers', 'U')      IS NOT NULL DROP TABLE suppliers;
IF OBJECT_ID('roles', 'U')          IS NOT NULL DROP TABLE roles;


CREATE DATABASE fruit_shop;
GO
USE fruit_shop;
GO

-- 1. ROLES
CREATE TABLE roles (
    id TINYINT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(50) UNIQUE
);
INSERT INTO roles (name) VALUES (N'admin'), (N'user'), (N'guest');

-- 2. USERS
CREATE TABLE users (
    id INT IDENTITY(1,1) PRIMARY KEY,
    role_id TINYINT,
    full_name NVARCHAR(100),
    email NVARCHAR(150) UNIQUE,
    phone NVARCHAR(20),
    password_hash NVARCHAR(255),
    avatar_url VARCHAR(500) DEFAULT NULL,
    is_verified TINYINT DEFAULT 0,
    status TINYINT DEFAULT 1,
    created_at DATETIME2 DEFAULT GETDATE(),
    deleted_at DATETIME2 NULL,
    FOREIGN KEY (role_id) REFERENCES roles(id)
);
ALTER TABLE Users ADD ResetToken NVARCHAR(10);
ALTER TABLE Users ADD ResetTokenExpiry DATETIME;

-- 3. SUPPLIERS
CREATE TABLE suppliers (
    id INT IDENTITY(1,1) PRIMARY KEY,
    name NVARCHAR(150) NOT NULL,
    phone NVARCHAR(20),
    email NVARCHAR(150),
    address NVARCHAR(MAX),
    status TINYINT DEFAULT 1,
    created_at DATETIME2 DEFAULT GETDATE()
);

-- 4. CATEGORIES (Đệ quy)
CREATE TABLE categories (
    id INT IDENTITY(1,1) PRIMARY KEY,
    parent_id INT NULL,
    name NVARCHAR(150),
    status TINYINT DEFAULT 1,
    FOREIGN KEY (parent_id) REFERENCES categories(id)
);


-- 5. PRODUCTS
CREATE TABLE products (
    id INT IDENTITY(1,1) PRIMARY KEY,
    category_id INT,
    supplier_id INT,
    sku NVARCHAR(50) UNIQUE, -- Thêm SKU để quản lý mã hàng
    name NVARCHAR(200),
    price DECIMAL(12,2),
    stock_quantity INT DEFAULT 0,
	origin NVARCHAR(100), -- Xuất xứ (Mỹ, Úc, Nhật...)                                                          │
	unit NVARCHAR(50),   -- Đơn vị (Kg, Hộp, Thùng...)  
	discount_percent DECIMAL(5,2) DEFAULT 0 CHECK (discount_percent >= 0 AND discount_percent <= 100),
	is_featured TINYINT DEFAULT 0, -- kieu san pham hot
	description NVARCHAR(MAX),
    status TINYINT DEFAULT 1,
    created_at DATETIME2 DEFAULT GETDATE(),
    FOREIGN KEY (category_id) REFERENCES categories(id),
    FOREIGN KEY (supplier_id) REFERENCES suppliers(id),
	final_price AS (price - (price * ISNULL(discount_percent, 0) / 100))
);

CREATE TABLE product_images (
    id INT IDENTITY(1,1) PRIMARY KEY,
    product_id INT,
    image_url NVARCHAR(500),
    is_main TINYINT DEFAULT 0,
    FOREIGN KEY (product_id) REFERENCES products(id)
);

select * from product_images;
-- 6. INVENTORY LOGS
CREATE TABLE inventory_logs (
    id INT IDENTITY(1,1) PRIMARY KEY,
    product_id INT,
    change_type NVARCHAR(20), -- import/export/return/adjust
    quantity INT,
    status TINYINT DEFAULT 1,
    note NVARCHAR(MAX),
    created_at DATETIME2 DEFAULT GETDATE(),
    deleted_at DATETIME2 NULL, 
    FOREIGN KEY (product_id) REFERENCES products(id)
);

-- 7. CART (Guest & User)
CREATE TABLE cart_items (
    id INT IDENTITY(1,1) PRIMARY KEY,
    user_id INT NULL,
    session_id NVARCHAR(100),
    product_id INT,
    quantity INT DEFAULT 1,
    status TINYINT DEFAULT 1,
    created_at DATETIME2 DEFAULT GETDATE(),
    UNIQUE (user_id, session_id, product_id),
    FOREIGN KEY (product_id) REFERENCES products(id)
);
--Mã giảm giá 
CREATE TABLE coupons (
    id INT IDENTITY(1,1) PRIMARY KEY,
    code NVARCHAR(50) UNIQUE,
    discount_type NVARCHAR(20), -- percent / fixed
    discount_value DECIMAL(10,2),
    min_order_value DECIMAL(12,2) NULL,
    max_discount DECIMAL(12,2) NULL,
    usage_limit INT NULL,
    used_count INT DEFAULT 0,
    start_date DATETIME2,
    end_date DATETIME2,
    status TINYINT DEFAULT 1,
    created_at DATETIME2 DEFAULT GETDATE()
);

-- 8. ORDERS (Đã thêm thông tin nhận hàng)
CREATE TABLE orders (
    id INT IDENTITY(1,1) PRIMARY KEY,
    user_id INT NULL, 
    total_amount DECIMAL(12,2),
    receiver_name NVARCHAR(100),    -- Bắt buộc cho cả guest/user
    receiver_phone NVARCHAR(20),    -- Bắt buộc
    receiver_address NVARCHAR(500), -- Bắt buộc
    note NVARCHAR(MAX),
    status TINYINT DEFAULT 1, -- 1: Chờ, 2: Xác nhận, 3: Giao, 4: Thành công, 5: Hủy
    created_at DATETIME2 DEFAULT GETDATE(),
    deleted_at DATETIME2 NULL,
	coupon_id INT NULL, -- ma giam gia
    discount_amount DECIMAL(12,2) DEFAULT 0,
    FOREIGN KEY (user_id) REFERENCES users(id),
	FOREIGN KEY (coupon_id) REFERENCES coupons(id)
);

-- 9. ORDER ITEMS
CREATE TABLE order_items (
    id INT IDENTITY(1,1) PRIMARY KEY,
    order_id INT,
    product_id INT,
    quantity INT,
    unit_price DECIMAL(12,2),
    FOREIGN KEY (order_id) REFERENCES orders(id),
    FOREIGN KEY (product_id) REFERENCES products(id)
);

-- Lưu thông tin sử dụng mã
CREATE TABLE coupon_usages (
    id INT IDENTITY(1,1) PRIMARY KEY,
    coupon_id INT,
    user_id INT,
    order_id INT,
    used_at DATETIME2 DEFAULT GETDATE(),
    FOREIGN KEY (coupon_id) REFERENCES coupons(id),
    FOREIGN KEY (user_id) REFERENCES users(id),
    FOREIGN KEY (order_id) REFERENCES orders(id)
);

-- 10. PAYMENTS
CREATE TABLE payments (
    id INT IDENTITY(1,1) PRIMARY KEY,
    order_id INT,
    method NVARCHAR(50), 
    amount DECIMAL(12,2),
    currency NVARCHAR(10) DEFAULT 'VND',
    status TINYINT DEFAULT 0, -- 0: Chờ, 1: Thành công, 2: Thất bại
    transaction_id NVARCHAR(100) NULL,
    created_at DATETIME2 DEFAULT GETDATE(),
    deleted_at DATETIME2 NULL,
    FOREIGN KEY (order_id) REFERENCES orders(id)
);

-- 11. REVIEWS 
CREATE TABLE reviews (
    id INT IDENTITY(1,1) PRIMARY KEY,
    product_id INT,
    user_id INT,
    rating INT CHECK (rating >= 1 AND rating <= 5),
    comment NVARCHAR(MAX),
    status TINYINT DEFAULT 1, 
    created_at DATETIME2 DEFAULT GETDATE(),
    deleted_at DATETIME2 NULL,
    FOREIGN KEY (product_id) REFERENCES products(id),
    FOREIGN KEY (user_id) REFERENCES users(id)
);
GO

-- ============================================================
-- TRIGGER: ĐỒNG BỘ KHO TUYỆT ĐỐI
-- ============================================================
CREATE TRIGGER trg_inventory_safe
ON inventory_logs
INSTEAD OF INSERT
AS
BEGIN
    SET NOCOUNT ON;
    -- Trừ kho
    UPDATE p SET p.stock_quantity = p.stock_quantity - i.quantity
    FROM products p JOIN inserted i ON p.id = i.product_id
    WHERE i.change_type IN ('export', 'adjust_decrease') AND p.stock_quantity >= i.quantity;

    IF EXISTS (SELECT 1 FROM inserted i JOIN products p ON p.id = i.product_id 
               WHERE i.change_type IN ('export', 'adjust_decrease') AND p.stock_quantity < i.quantity)
    BEGIN
        RAISERROR (N'Lỗi: Kho không đủ hàng!', 16, 1);
        ROLLBACK TRANSACTION; RETURN;
    END

    -- Cộng kho
    UPDATE p SET p.stock_quantity = p.stock_quantity + i.quantity
    FROM products p JOIN inserted i ON p.id = i.product_id
    WHERE i.change_type IN ('import', 'return', 'adjust_increase');

    -- Lưu log
    INSERT INTO inventory_logs (product_id, change_type, quantity, status, note, created_at)
    SELECT product_id, change_type, quantity, ISNULL(status, 1), note, GETDATE() FROM inserted;
END;
GO

-- ============================================================
-- INDEXES (Tối ưu truy vấn)
-- ============================================================
CREATE INDEX idx_users_role ON users(role_id);
CREATE UNIQUE INDEX uq_users_email ON users(email) WHERE deleted_at IS NULL;
CREATE INDEX idx_products_category ON products(category_id);
CREATE INDEX idx_orders_user_created ON orders(user_id, created_at DESC);
CREATE INDEX idx_order_items_order ON order_items(order_id);
CREATE UNIQUE INDEX uq_cart_user_product ON cart_items(user_id, product_id) WHERE user_id IS NOT NULL;
CREATE UNIQUE INDEX uq_cart_session_product ON cart_items(session_id, product_id) WHERE session_id IS NOT NULL;

CREATE INDEX idx_product_images_product ON product_images(product_id);
CREATE INDEX idx_coupons_code ON coupons(code);
CREATE INDEX idx_orders_coupon ON orders(coupon_id);
CREATE INDEX idx_coupon_usage_user ON coupon_usages(user_id);


