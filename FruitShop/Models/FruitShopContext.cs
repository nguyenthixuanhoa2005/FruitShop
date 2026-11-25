using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace FruitShop.Models;

public partial class FruitShopContext : DbContext
{
    public FruitShopContext()
    {
    }

    public FruitShopContext(DbContextOptions<FruitShopContext> options)
        : base(options)
    {
    }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Coupon> Coupons { get; set; }

    public virtual DbSet<CouponUsage> CouponUsages { get; set; }

    public virtual DbSet<InventoryLog> InventoryLogs { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<PasswordHistory> PasswordHistories { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductImage> ProductImages { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=.;Database=fruit_shop;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__cart_ite__3213E83F8AC45D47");

            entity.ToTable("cart_items");

            entity.HasIndex(e => new { e.UserId, e.SessionId, e.ProductId }, "UQ__cart_ite__9B62268E2D3E42FE").IsUnique();

            entity.HasIndex(e => new { e.SessionId, e.ProductId }, "uq_cart_session_product")
                .IsUnique()
                .HasFilter("([session_id] IS NOT NULL)");

            entity.HasIndex(e => new { e.UserId, e.ProductId }, "uq_cart_user_product")
                .IsUnique()
                .HasFilter("([user_id] IS NOT NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity)
                .HasDefaultValue(1)
                .HasColumnName("quantity");
            entity.Property(e => e.SessionId)
                .HasMaxLength(100)
                .HasColumnName("session_id");
            entity.Property(e => e.Status)
                .HasDefaultValue((byte)1)
                .HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Product).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__cart_item__produ__73BA3083");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__categori__3213E83FF666F369");

            entity.ToTable("categories");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.ParentId).HasColumnName("parent_id");
            entity.Property(e => e.Status)
                .HasDefaultValue((byte)1)
                .HasColumnName("status");

            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent)
                .HasForeignKey(d => d.ParentId)
                .HasConstraintName("FK__categorie__paren__59FA5E80");
        });

        modelBuilder.Entity<Coupon>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__coupons__3213E83F0B8952C3");

            entity.ToTable("coupons");

            entity.HasIndex(e => e.Code, "UQ__coupons__357D4CF9FDDC6081").IsUnique();

            entity.HasIndex(e => e.Code, "idx_coupons_code");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.DiscountType)
                .HasMaxLength(20)
                .HasColumnName("discount_type");
            entity.Property(e => e.DiscountValue)
                .HasColumnType("decimal(10, 2)")
                .HasColumnName("discount_value");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.MaxDiscount)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("max_discount");
            entity.Property(e => e.MinOrderValue)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("min_order_value");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.Status)
                .HasDefaultValue((byte)1)
                .HasColumnName("status");
            entity.Property(e => e.UsageLimit).HasColumnName("usage_limit");
            entity.Property(e => e.UsedCount)
                .HasDefaultValue(0)
                .HasColumnName("used_count");
        });

        modelBuilder.Entity<CouponUsage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__coupon_u__3213E83F536DAF2A");

            entity.ToTable("coupon_usages");

            entity.HasIndex(e => e.UserId, "idx_coupon_usage_user");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CouponId).HasColumnName("coupon_id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.UsedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("used_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Coupon).WithMany(p => p.CouponUsages)
                .HasForeignKey(d => d.CouponId)
                .HasConstraintName("FK__coupon_us__coupo__07C12930");

            entity.HasOne(d => d.Order).WithMany(p => p.CouponUsages)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK__coupon_us__order__09A971A2");

            entity.HasOne(d => d.User).WithMany(p => p.CouponUsages)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__coupon_us__user___08B54D69");
        });

        modelBuilder.Entity<InventoryLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__inventor__3213E83F777890B0");

            entity.ToTable("inventory_logs", tb => tb.HasTrigger("trg_inventory_safe"));

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ChangeType)
                .HasMaxLength(20)
                .HasColumnName("change_type");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.Status)
                .HasDefaultValue((byte)1)
                .HasColumnName("status");

            entity.HasOne(d => d.Product).WithMany(p => p.InventoryLogs)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__inventory__produ__6D0D32F4");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__orders__3213E83F8A00839B");

            entity.ToTable("orders");

            entity.HasIndex(e => e.CouponId, "idx_orders_coupon");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "idx_orders_user_created").IsDescending(false, true);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CouponId).HasColumnName("coupon_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DiscountAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("discount_amount");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.ReceiverAddress)
                .HasMaxLength(500)
                .HasColumnName("receiver_address");
            entity.Property(e => e.ReceiverName)
                .HasMaxLength(100)
                .HasColumnName("receiver_name");
            entity.Property(e => e.ReceiverPhone)
                .HasMaxLength(20)
                .HasColumnName("receiver_phone");
            entity.Property(e => e.Status)
                .HasDefaultValue((byte)1)
                .HasColumnName("status");
            entity.Property(e => e.TotalAmount)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("total_amount");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Coupon).WithMany(p => p.Orders)
                .HasForeignKey(d => d.CouponId)
                .HasConstraintName("FK__orders__coupon_i__00200768");

            entity.HasOne(d => d.User).WithMany(p => p.Orders)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__orders__user_id__7F2BE32F");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__order_it__3213E83F7948D996");

            entity.ToTable("order_items");

            entity.HasIndex(e => e.OrderId, "idx_order_items_order");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.UnitPrice)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("unit_price");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK__order_ite__order__02FC7413");

            entity.HasOne(d => d.Product).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__order_ite__produ__03F0984C");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__payments__3213E83F008B188E");

            entity.ToTable("payments");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.Currency)
                .HasMaxLength(10)
                .HasDefaultValue("VND")
                .HasColumnName("currency");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.Method)
                .HasMaxLength(50)
                .HasColumnName("method");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.Status)
                .HasDefaultValue((byte)0)
                .HasColumnName("status");
            entity.Property(e => e.TransactionId)
                .HasMaxLength(100)
                .HasColumnName("transaction_id");

            entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK__payments__order___0F624AF8");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__products__3213E83FD454CFE2");

            entity.ToTable("products");

            entity.HasIndex(e => e.Sku, "UQ__products__DDDF4BE7952A84D0").IsUnique();

            entity.HasIndex(e => e.CategoryId, "idx_products_category");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DiscountPercent)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("discount_percent");
            entity.Property(e => e.FinalPrice)
                .HasComputedColumnSql("([price]-([price]*isnull([discount_percent],(0)))/(100))", false)
                .HasColumnType("decimal(23, 8)")
                .HasColumnName("final_price");
            entity.Property(e => e.IsFeatured)
                .HasDefaultValue((byte)0)
                .HasColumnName("is_featured");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.Origin)
                .HasMaxLength(100)
                .HasColumnName("origin");
            entity.Property(e => e.Price)
                .HasColumnType("decimal(12, 2)")
                .HasColumnName("price");
            entity.Property(e => e.Sku)
                .HasMaxLength(50)
                .HasColumnName("sku");
            entity.Property(e => e.Status)
                .HasDefaultValue((byte)1)
                .HasColumnName("status");
            entity.Property(e => e.StockQuantity)
                .HasDefaultValue(0)
                .HasColumnName("stock_quantity");
            entity.Property(e => e.SupplierId).HasColumnName("supplier_id");
            entity.Property(e => e.Unit)
                .HasMaxLength(50)
                .HasColumnName("unit");

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK__products__catego__6383C8BA");

            entity.HasOne(d => d.Supplier).WithMany(p => p.Products)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("FK__products__suppli__6477ECF3");
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__product___3213E83F879AB57C");

            entity.ToTable("product_images");

            entity.HasIndex(e => e.ProductId, "idx_product_images_product");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500)
                .HasColumnName("image_url");
            entity.Property(e => e.IsMain)
                .HasDefaultValue((byte)0)
                .HasColumnName("is_main");
            entity.Property(e => e.ProductId).HasColumnName("product_id");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__product_i__produ__68487DD7");
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__reviews__3213E83F1CD7332F");

            entity.ToTable("reviews");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.Status)
                .HasDefaultValue((byte)1)
                .HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Order).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_reviews_orders");

            entity.HasOne(d => d.Product).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__reviews__product__151B244E");

            entity.HasOne(d => d.User).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__reviews__user_id__160F4887");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__roles__3213E83F4B3B0583");

            entity.ToTable("roles");

            entity.HasIndex(e => e.Name, "UQ__roles__72E12F1BB410A859").IsUnique();

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd()
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__supplier__3213E83FB4219265");

            entity.ToTable("suppliers");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .HasColumnName("email");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasColumnName("phone");
            entity.Property(e => e.Status)
                .HasDefaultValue((byte)1)
                .HasColumnName("status");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__users__3213E83F94509CD6");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "UQ__users__AB6E61645CFF7A9E").IsUnique();

            entity.HasIndex(e => e.RoleId, "idx_users_role");

            entity.HasIndex(e => e.Email, "uq_users_email")
                .IsUnique()
                .HasFilter("([deleted_at] IS NULL)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AvatarUrl)
                .HasMaxLength(500)
                .IsUnicode(false)
                .HasDefaultValueSql("(NULL)")
                .HasColumnName("avatar_url");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .HasColumnName("full_name");
            entity.Property(e => e.IsVerified)
                .HasDefaultValue((byte)0)
                .HasColumnName("is_verified");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasColumnName("phone");
            entity.Property(e => e.ResetToken).HasMaxLength(10);
            entity.Property(e => e.ResetTokenExpiry).HasColumnType("datetime");
            entity.Property(e => e.FailedLoginCount)
                .HasDefaultValue(0)
                .HasColumnName("failed_login_count");
            entity.Property(e => e.LockoutEnd).HasColumnName("lockout_end");
            entity.Property(e => e.LastPasswordChangedAt).HasColumnName("last_password_changed_at");
            entity.Property(e => e.TwoFactorEnabled)
                .HasDefaultValue(false)
                .HasColumnName("two_factor_enabled");
            entity.Property(e => e.TwoFactorSecret)
                .HasMaxLength(128)
                .HasColumnName("two_factor_secret");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.Status)
                .HasDefaultValue((byte)1)
                .HasColumnName("status");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK__users__role_id__52593CB8");
        });

        modelBuilder.Entity<PasswordHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable("password_histories");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "idx_password_histories_user_created");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getutcdate())")
                .HasColumnName("created_at");

            entity.HasOne(d => d.User).WithMany(p => p.PasswordHistories)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_password_histories_users");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
