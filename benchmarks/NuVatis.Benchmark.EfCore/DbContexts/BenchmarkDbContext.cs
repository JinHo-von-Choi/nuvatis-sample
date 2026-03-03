using Microsoft.EntityFrameworkCore;
using NuVatis.Benchmark.Core.Models;

namespace NuVatis.Benchmark.EfCore.DbContexts;

/**
 * EF Core DbContext (벤치마크용)
 */
public class BenchmarkDbContext : DbContext
{
    public BenchmarkDbContext(DbContextOptions<BenchmarkDbContext> options) : base(options)
    {
    }

    public DbSet<User>          Users          { get; set; } = null!;
    public DbSet<Address>       Addresses      { get; set; } = null!;
    public DbSet<Category>      Categories     { get; set; } = null!;
    public DbSet<Product>       Products       { get; set; } = null!;
    public DbSet<ProductImage>  ProductImages  { get; set; } = null!;
    public DbSet<Coupon>        Coupons        { get; set; } = null!;
    public DbSet<Order>         Orders         { get; set; } = null!;
    public DbSet<OrderItem>     OrderItems     { get; set; } = null!;
    public DbSet<Review>        Reviews        { get; set; } = null!;
    public DbSet<Payment>       Payments       { get; set; } = null!;
    public DbSet<Shipment>      Shipments      { get; set; } = null!;
    public DbSet<InventoryLog>  InventoryLogs  { get; set; } = null!;
    public DbSet<UserCoupon>    UserCoupons    { get; set; } = null!;
    public DbSet<Wishlist>      Wishlists      { get; set; } = null!;
    public DbSet<AuditLog>      AuditLogs      { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 테이블명 소문자 + 언더스코어 규칙
        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<Address>().ToTable("addresses");
        modelBuilder.Entity<Category>().ToTable("categories");
        modelBuilder.Entity<Product>().ToTable("products");
        modelBuilder.Entity<ProductImage>().ToTable("product_images");
        modelBuilder.Entity<Coupon>().ToTable("coupons");
        modelBuilder.Entity<Order>().ToTable("orders");
        modelBuilder.Entity<OrderItem>().ToTable("order_items");
        modelBuilder.Entity<Review>().ToTable("reviews");
        modelBuilder.Entity<Payment>().ToTable("payments");
        modelBuilder.Entity<Shipment>().ToTable("shipments");
        modelBuilder.Entity<InventoryLog>().ToTable("inventory_logs");
        modelBuilder.Entity<UserCoupon>().ToTable("user_coupons");
        modelBuilder.Entity<Wishlist>().ToTable("wishlists");
        modelBuilder.Entity<AuditLog>().ToTable("audit_logs");

        // 컬럼명 매핑 (snake_case)
        ConfigureUser(modelBuilder);
        ConfigureAddress(modelBuilder);
        ConfigureCategory(modelBuilder);
        ConfigureProduct(modelBuilder);
        ConfigureOrder(modelBuilder);
        ConfigureOrderItem(modelBuilder);
        ConfigureReview(modelBuilder);

        // 관계 설정
        ConfigureRelationships(modelBuilder);
    }

    private void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserName).HasColumnName("user_name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            entity.Property(e => e.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
            entity.Property(e => e.DateOfBirth).HasColumnName("date_of_birth");
            entity.Property(e => e.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp without time zone");

            entity.HasIndex(e => e.Email).IsUnique();
        });
    }

    private void ConfigureAddress(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Address>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.AddressType).HasColumnName("address_type").HasMaxLength(20).IsRequired(); // DB: address_type
            entity.Property(e => e.StreetAddress).HasColumnName("street_address").HasMaxLength(200).IsRequired(); // DB: street_address
            entity.Property(e => e.City).HasColumnName("city").HasMaxLength(100).IsRequired();
            entity.Property(e => e.State).HasColumnName("state").HasMaxLength(50);
            entity.Property(e => e.PostalCode).HasColumnName("postal_code").HasMaxLength(20);
            entity.Property(e => e.Country).HasColumnName("country").HasMaxLength(50).IsRequired();
            entity.Property(e => e.IsDefault).HasColumnName("is_default").HasDefaultValue(false); // DB: is_default
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp without time zone");
        });
    }

    private void ConfigureCategory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ParentId).HasColumnName("parent_id");
            entity.Property(e => e.CategoryName).HasColumnName("name").HasMaxLength(200).IsRequired(); // DB: name
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Ignore(e => e.DisplayOrder); // DB에 컬럼 없음
            entity.Ignore(e => e.IsActive); // DB에 컬럼 없음
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
            entity.Ignore(e => e.UpdatedAt); // DB에 컬럼 없음
        });
    }

    private void ConfigureProduct(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.ProductName).HasColumnName("name").HasMaxLength(300).IsRequired(); // DB: name
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Price).HasColumnName("price").HasColumnType("decimal(12,2)");
            entity.Ignore(e => e.CostPrice); // DB에 컬럼 없음
            entity.Property(e => e.StockQuantity).HasColumnName("stock").HasDefaultValue(0); // DB: stock
            entity.Ignore(e => e.Sku); // DB에 컬럼 없음
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp without time zone");
        });
    }

    private void ConfigureOrder(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.OrderNumber).HasColumnName("order_number").HasMaxLength(50).IsRequired();
            entity.Property(e => e.OrderStatus).HasColumnName("order_status").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Subtotal).HasColumnName("subtotal").HasColumnType("decimal(12,2)");
            entity.Property(e => e.DiscountAmount).HasColumnName("discount_amount").HasColumnType("decimal(12,2)");
            entity.Property(e => e.TaxAmount).HasColumnName("tax_amount").HasColumnType("decimal(12,2)");
            entity.Property(e => e.ShippingFee).HasColumnName("shipping_fee").HasColumnType("decimal(12,2)");
            entity.Property(e => e.TotalAmount).HasColumnName("total_amount").HasColumnType("decimal(12,2)");
            entity.Property(e => e.CouponId).HasColumnName("coupon_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp without time zone");
        });
    }

    private void ConfigureOrderItem(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.UnitPrice).HasColumnName("unit_price").HasColumnType("decimal(12,2)");
            entity.Ignore(e => e.DiscountAmount); // DB에 컬럼 없음
            entity.Property(e => e.TotalPrice).HasColumnName("subtotal").HasColumnType("decimal(12,2)"); // DB에는 subtotal로 저장됨
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
        });
    }

    private void ConfigureReview(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Ignore(e => e.Title); // DB에 컬럼 없음
            entity.Property(e => e.Content).HasColumnName("comment"); // DB: comment
            entity.Ignore(e => e.IsVerified); // DB에 컬럼 없음
            entity.Ignore(e => e.HelpfulCount); // DB에 컬럼 없음
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp without time zone");
            entity.Ignore(e => e.UpdatedAt); // DB에 컬럼 없음
        });
    }

    private void ConfigureRelationships(ModelBuilder modelBuilder)
    {
        // User -> Addresses
        modelBuilder.Entity<Address>()
            .HasOne(a => a.User)
            .WithMany(u => u.Addresses)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Category -> Products
        modelBuilder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Order -> User
        modelBuilder.Entity<Order>()
            .HasOne(o => o.User)
            .WithMany(u => u.Orders)
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // OrderItem -> Order
        modelBuilder.Entity<OrderItem>()
            .HasOne(oi => oi.Order)
            .WithMany(o => o.OrderItems)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // OrderItem -> Product
        modelBuilder.Entity<OrderItem>()
            .HasOne(oi => oi.Product)
            .WithMany(p => p.OrderItems)
            .HasForeignKey(oi => oi.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
