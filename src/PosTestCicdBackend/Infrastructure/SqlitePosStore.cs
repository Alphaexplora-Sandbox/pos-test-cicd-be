using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PosTestCicdBackend.Domain;

namespace PosTestCicdBackend.Infrastructure;

public sealed class SqlitePosStore : IPosStore, IDisposable
{
    private readonly string _connectionString;
    private readonly object _lock = new();

    public SqlitePosStore(string connectionString = "Data Source=novapos.db;Cache=Shared")
    {
        _connectionString = connectionString;
        InitializeDatabase();
    }

    private SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
    }

    private void InitializeDatabase()
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS users (
                    id TEXT PRIMARY KEY,
                    username TEXT UNIQUE NOT NULL,
                    email TEXT NOT NULL,
                    full_name TEXT NOT NULL,
                    role TEXT NOT NULL,
                    token TEXT NOT NULL,
                    password_hash TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS categories (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    description TEXT,
                    color TEXT NOT NULL,
                    icon TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS products (
                    id TEXT PRIMARY KEY,
                    sku TEXT UNIQUE NOT NULL,
                    barcode TEXT UNIQUE NOT NULL,
                    name TEXT NOT NULL,
                    description TEXT,
                    category_id TEXT NOT NULL,
                    category_name TEXT NOT NULL,
                    price REAL NOT NULL,
                    cost REAL NOT NULL,
                    tax_rate REAL NOT NULL,
                    stock_quantity INTEGER NOT NULL,
                    low_stock_threshold INTEGER NOT NULL,
                    is_active INTEGER NOT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS customers (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    email TEXT UNIQUE NOT NULL,
                    phone TEXT,
                    loyalty_points INTEGER NOT NULL,
                    tier TEXT NOT NULL,
                    total_spent REAL NOT NULL,
                    created_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS discounts (
                    id TEXT PRIMARY KEY,
                    code TEXT UNIQUE NOT NULL,
                    description TEXT,
                    type TEXT NOT NULL,
                    value REAL NOT NULL,
                    min_order_amount REAL NOT NULL,
                    is_active INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS orders (
                    id TEXT PRIMARY KEY,
                    order_number TEXT UNIQUE NOT NULL,
                    register_id TEXT NOT NULL,
                    cashier_id TEXT NOT NULL,
                    cashier_name TEXT NOT NULL,
                    customer_id TEXT,
                    customer_name TEXT,
                    items_json TEXT NOT NULL,
                    subtotal REAL NOT NULL,
                    tax_total REAL NOT NULL,
                    discount_total REAL NOT NULL,
                    grand_total REAL NOT NULL,
                    payment_method TEXT NOT NULL,
                    amount_tendered REAL NOT NULL,
                    change_given REAL NOT NULL,
                    status TEXT NOT NULL,
                    notes TEXT,
                    created_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS shifts (
                    id TEXT PRIMARY KEY,
                    register_id TEXT NOT NULL,
                    cashier_id TEXT NOT NULL,
                    cashier_name TEXT NOT NULL,
                    opened_at TEXT NOT NULL,
                    closed_at TEXT,
                    starting_float REAL NOT NULL,
                    expected_cash REAL NOT NULL,
                    counted_cash REAL,
                    variance REAL,
                    status TEXT NOT NULL,
                    total_sales REAL NOT NULL,
                    total_transactions INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS cash_drops (
                    id TEXT PRIMARY KEY,
                    shift_id TEXT NOT NULL,
                    type TEXT NOT NULL,
                    amount REAL NOT NULL,
                    reason TEXT NOT NULL,
                    timestamp TEXT NOT NULL
                );
            ";
            cmd.ExecuteNonQuery();

            SeedIfEmpty(conn);
        }
    }

    private void SeedIfEmpty(SqliteConnection conn)
    {
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM products;";
        var count = Convert.ToInt32(checkCmd.ExecuteScalar());
        if (count > 0) return;

        var now = DateTimeOffset.UtcNow;

        // 1. Seed Users
        var users = new[]
        {
            new { Id = "usr-1", Username = "admin", Email = "admin@novapos.io", FullName = "Sarah Jenkins (Store Director)", Role = "Admin", Token = "token-admin-session-xyz", Password = "admin" },
            new { Id = "usr-2", Username = "manager", Email = "manager@novapos.io", FullName = "Marcus Bell (Shift Lead)", Role = "Manager", Token = "token-manager-session-xyz", Password = "admin" },
            new { Id = "usr-3", Username = "cashier1", Email = "cashier1@novapos.io", FullName = "Emma Watson (Head Cashier)", Role = "Cashier", Token = "token-cashier-session-xyz", Password = "admin" }
        };

        foreach (var u in users)
        {
            using var uCmd = conn.CreateCommand();
            uCmd.CommandText = "INSERT INTO users VALUES (@id, @u, @e, @fn, @r, @tok, @pw);";
            uCmd.Parameters.AddWithValue("@id", u.Id);
            uCmd.Parameters.AddWithValue("@u", u.Username);
            uCmd.Parameters.AddWithValue("@e", u.Email);
            uCmd.Parameters.AddWithValue("@fn", u.FullName);
            uCmd.Parameters.AddWithValue("@r", u.Role);
            uCmd.Parameters.AddWithValue("@tok", u.Token);
            uCmd.Parameters.AddWithValue("@pw", u.Password);
            uCmd.ExecuteNonQuery();
        }

        // 2. Seed Categories
        var categories = new[]
        {
            new Category("cat-coffee", "Artisan Coffee", "Espresso, pour-overs, specialty roasts", "#8B5A2B", "coffee"),
            new Category("cat-tea", "Specialty Teas", "Organic loose leaf and matcha brews", "#2E8B57", "leaf"),
            new Category("cat-bakery", "Fresh Bakery", "Freshly baked morning goods and pastries", "#D2691E", "croissant"),
            new Category("cat-deli", "Gourmet Deli", "Artisanal toasted sandwiches, wraps, salads", "#CD853F", "utensils"),
            new Category("cat-sweets", "Desserts", "Handmade tarts, cookies, and artisanal cakes", "#C71585", "cake"),
            new Category("cat-merch", "Merchandise", "Reusable tumblers, beans, brewing gear", "#4682B4", "gift")
        };

        foreach (var c in categories)
        {
            using var cCmd = conn.CreateCommand();
            cCmd.CommandText = "INSERT INTO categories VALUES (@id, @name, @desc, @color, @icon);";
            cCmd.Parameters.AddWithValue("@id", c.Id);
            cCmd.Parameters.AddWithValue("@name", c.Name);
            cCmd.Parameters.AddWithValue("@desc", c.Description);
            cCmd.Parameters.AddWithValue("@color", c.Color);
            cCmd.Parameters.AddWithValue("@icon", c.Icon);
            cCmd.ExecuteNonQuery();
        }

        // 3. Seed Products
        var products = new[]
        {
            new Product("prod-1", "COF-ESP-01", "794026110011", "Single Origin Double Espresso", "Intense crema with hints of hazelnut and dark cacao.", "cat-coffee", "Artisan Coffee", 3.75m, 0.85m, 0.08m, 150, 20, true, now, now),
            new Product("prod-2", "COF-FLT-02", "794026110028", "Silky Flat White", "Velvety micro-foam poured over signature double ristretto.", "cat-coffee", "Artisan Coffee", 4.85m, 1.15m, 0.08m, 120, 20, true, now, now),
            new Product("prod-3", "COF-CLD-03", "794026110035", "Nitro Cold Brew Reserve", "Steeped 20 hours, nitrogen-infused for a creamy cascading head.", "cat-coffee", "Artisan Coffee", 5.25m, 1.30m, 0.08m, 85, 15, true, now, now),
            new Product("prod-4", "COF-MAC-04", "794026110042", "Salted Caramel Macchiato", "Vanilla steamed milk, espresso drizzle, artisanal salted caramel.", "cat-coffee", "Artisan Coffee", 5.50m, 1.45m, 0.08m, 95, 15, true, now, now),
            new Product("prod-5", "TEA-MTC-01", "794026220010", "Ceremonial Uji Matcha Latte", "Stone-ground ceremonial grade green tea with oat milk.", "cat-tea", "Specialty Teas", 5.75m, 1.60m, 0.08m, 70, 10, true, now, now),
            new Product("prod-6", "TEA-CHY-02", "794026220027", "Spiced Bombay Chai", "Slow-brewed black tea, cardamom, cinnamon, fresh ginger.", "cat-tea", "Specialty Teas", 4.95m, 1.10m, 0.08m, 90, 15, true, now, now),
            new Product("prod-7", "BAK-CRS-01", "794026330019", "Golden French Butter Croissant", "Laminated with Normandy butter, baked fresh every morning.", "cat-bakery", "Fresh Bakery", 3.95m, 1.20m, 0.08m, 45, 10, true, now, now),
            new Product("prod-8", "BAK-CHOC-02", "794026330026", "Valrhona Pain au Chocolat", "Flaky croissant dough filled with Belgian dark chocolate batons.", "cat-bakery", "Fresh Bakery", 4.45m, 1.35m, 0.08m, 40, 10, true, now, now),
            new Product("prod-9", "BAK-SCN-03", "794026330033", "Wild Blueberry Lemon Scone", "Glazed scone packed with wild mountain blueberries.", "cat-bakery", "Fresh Bakery", 3.85m, 1.05m, 0.08m, 12, 15, true, now, now), // Low stock
            new Product("prod-10", "DEL-TRF-01", "794026440018", "Truffle Roasted Turkey Panini", "Smoked turkey, aged provolone, arugula, black truffle aioli.", "cat-deli", "Gourmet Deli", 9.75m, 3.40m, 0.08m, 35, 10, true, now, now),
            new Product("prod-11", "DEL-CAP-02", "794026440025", "Heirloom Caprese Ciabatta", "Bufala mozzarella, sun-ripened tomatoes, fresh basil pesto.", "cat-deli", "Gourmet Deli", 8.95m, 2.95m, 0.08m, 28, 8, true, now, now),
            new Product("prod-12", "DEL-BAG-03", "794026440032", "Smoked Salmon Everything Bagel", "Wild Alaskan salmon, dill cream cheese, capers, pickled shallots.", "cat-deli", "Gourmet Deli", 10.50m, 4.10m, 0.08m, 8, 10, true, now, now), // Low stock
            new Product("prod-13", "SWT-CHK-01", "794026550017", "San Sebastian Basque Cheesecake", "Caramelized burnt crust with an oozing rich custard center.", "cat-sweets", "Desserts", 6.50m, 2.10m, 0.08m, 25, 8, true, now, now),
            new Product("prod-14", "SWT-BRW-02", "794026550024", "Fudge Walnut Brownie", "Decadent dark chocolate brownie with toasted walnuts.", "cat-sweets", "Desserts", 4.25m, 1.25m, 0.08m, 50, 12, true, now, now),
            new Product("prod-15", "MRC-TMB-01", "794026660016", "Nova 16oz Ceramic Tumbler", "Double-walled vacuum insulated with leak-proof lid.", "cat-merch", "Merchandise", 24.95m, 9.50m, 0.08m, 60, 15, true, now, now)
        };

        foreach (var p in products)
        {
            using var pCmd = conn.CreateCommand();
            pCmd.CommandText = @"
                INSERT INTO products VALUES (
                    @id, @sku, @barcode, @name, @desc, @catId, @catName,
                    @price, @cost, @tax, @stock, @lowStock, @active, @created, @updated
                );";
            pCmd.Parameters.AddWithValue("@id", p.Id);
            pCmd.Parameters.AddWithValue("@sku", p.Sku);
            pCmd.Parameters.AddWithValue("@barcode", p.Barcode);
            pCmd.Parameters.AddWithValue("@name", p.Name);
            pCmd.Parameters.AddWithValue("@desc", p.Description);
            pCmd.Parameters.AddWithValue("@catId", p.CategoryId);
            pCmd.Parameters.AddWithValue("@catName", p.CategoryName);
            pCmd.Parameters.AddWithValue("@price", (double)p.Price);
            pCmd.Parameters.AddWithValue("@cost", (double)p.Cost);
            pCmd.Parameters.AddWithValue("@tax", (double)p.TaxRate);
            pCmd.Parameters.AddWithValue("@stock", p.StockQuantity);
            pCmd.Parameters.AddWithValue("@lowStock", p.LowStockThreshold);
            pCmd.Parameters.AddWithValue("@active", p.IsActive ? 1 : 0);
            pCmd.Parameters.AddWithValue("@created", p.CreatedAt.ToString("O"));
            pCmd.Parameters.AddWithValue("@updated", p.UpdatedAt.ToString("O"));
            pCmd.ExecuteNonQuery();
        }

        // 4. Seed Customers
        var customers = new[]
        {
            new Customer("cust-1", "Alexander Wright", "alexander@example.com", "555-0142", 240, "Gold", 485.50m, now.AddMonths(-3)),
            new Customer("cust-2", "Elena Rostova", "elena.r@example.com", "555-0189", 110, "Silver", 220.00m, now.AddMonths(-1)),
            new Customer("cust-3", "David Chen", "david.chen@example.com", "555-0199", 35, "Bronze", 75.00m, now.AddDays(-14))
        };

        foreach (var cust in customers)
        {
            using var custCmd = conn.CreateCommand();
            custCmd.CommandText = "INSERT INTO customers VALUES (@id, @name, @email, @phone, @points, @tier, @spent, @created);";
            custCmd.Parameters.AddWithValue("@id", cust.Id);
            custCmd.Parameters.AddWithValue("@name", cust.Name);
            custCmd.Parameters.AddWithValue("@email", cust.Email);
            custCmd.Parameters.AddWithValue("@phone", cust.Phone);
            custCmd.Parameters.AddWithValue("@points", cust.LoyaltyPoints);
            custCmd.Parameters.AddWithValue("@tier", cust.Tier);
            custCmd.Parameters.AddWithValue("@spent", (double)cust.TotalSpent);
            custCmd.Parameters.AddWithValue("@created", cust.CreatedAt.ToString("O"));
            custCmd.ExecuteNonQuery();
        }

        // 5. Seed Discounts
        var discounts = new[]
        {
            new Discount("disc-1", "WELCOME10", "10% off your order", "Percentage", 10m, 10m, true),
            new Discount("disc-2", "SUMMER20", "20% off orders over $25", "Percentage", 20m, 25m, true),
            new Discount("disc-3", "FIVEBUCKS", "$5 off orders over $20", "FixedAmount", 5m, 20m, true),
            new Discount("disc-4", "VIP15", "15% off VIP exclusive discount", "Percentage", 15m, 15m, true)
        };

        foreach (var d in discounts)
        {
            using var dCmd = conn.CreateCommand();
            dCmd.CommandText = "INSERT INTO discounts VALUES (@id, @code, @desc, @type, @val, @min, @active);";
            dCmd.Parameters.AddWithValue("@id", d.Id);
            dCmd.Parameters.AddWithValue("@code", d.Code);
            dCmd.Parameters.AddWithValue("@desc", d.Description);
            dCmd.Parameters.AddWithValue("@type", d.Type);
            dCmd.Parameters.AddWithValue("@val", (double)d.Value);
            dCmd.Parameters.AddWithValue("@min", (double)d.MinOrderAmount);
            dCmd.Parameters.AddWithValue("@active", d.IsActive ? 1 : 0);
            dCmd.ExecuteNonQuery();
        }

        // 6. Seed Current Open Shift
        using var shiftCmd = conn.CreateCommand();
        shiftCmd.CommandText = @"
            INSERT INTO shifts VALUES (
                'shift-001', 'REG-01', 'usr-3', 'Emma Watson (Head Cashier)',
                @opened, NULL, 200.0, 200.0, NULL, NULL, 'Open', 0.0, 0
            );";
        shiftCmd.Parameters.AddWithValue("@opened", now.AddHours(-4).ToString("O"));
        shiftCmd.ExecuteNonQuery();

        // 7. Seed Initial Completed Order for Analytics
        var sampleItems = new List<OrderItem>
        {
            new OrderItem("prod-2", "COF-FLT-02", "Silky Flat White", 4.85m, 2, 0.78m, 0m, 9.70m),
            new OrderItem("prod-7", "BAK-CRS-01", "Golden French Butter Croissant", 3.95m, 1, 0.32m, 0m, 3.95m)
        };
        var subtotal = 13.65m;
        var tax = 1.10m;
        var grand = 14.75m;

        using var ordCmd = conn.CreateCommand();
        ordCmd.CommandText = @"
            INSERT INTO orders VALUES (
                'ord-init-1', 'POS-2026-0001', 'REG-01', 'usr-3', 'Emma Watson (Head Cashier)',
                'cust-1', 'Alexander Wright', @itemsJson, @sub, @tax, 0.0, @grand,
                'Card', 14.75, 0.0, 'Completed', 'First morning rush transaction', @created
            );";
        ordCmd.Parameters.AddWithValue("@itemsJson", JsonSerializer.Serialize(sampleItems));
        ordCmd.Parameters.AddWithValue("@sub", (double)subtotal);
        ordCmd.Parameters.AddWithValue("@tax", (double)tax);
        ordCmd.Parameters.AddWithValue("@grand", (double)grand);
        ordCmd.Parameters.AddWithValue("@created", now.AddHours(-2).ToString("O"));
        ordCmd.ExecuteNonQuery();
    }

    // ---------------- AUTH ----------------
    public UserProfile? Authenticate(string username, string password)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, username, email, full_name, role, token FROM users WHERE LOWER(username) = @u AND password_hash = @pw;";
            cmd.Parameters.AddWithValue("@u", username.Trim().ToLowerInvariant());
            cmd.Parameters.AddWithValue("@pw", password);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new UserProfile(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5));
            }
            return null;
        }
    }

    public UserProfile? GetUserByToken(string token)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, username, email, full_name, role, token FROM users WHERE token = @tok;";
            cmd.Parameters.AddWithValue("@tok", token);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new UserProfile(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5));
            }
            return null;
        }
    }

    public UserProfile? GetUserById(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, username, email, full_name, role, token FROM users WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new UserProfile(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5));
            }
            return null;
        }
    }

    public IReadOnlyList<UserProfile> GetUsers()
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, username, email, full_name, role, token FROM users ORDER BY full_name;";
            using var reader = cmd.ExecuteReader();
            var list = new List<UserProfile>();
            while (reader.Read())
            {
                list.Add(new UserProfile(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5)));
            }
            return list;
        }
    }

    // ---------------- CATEGORIES ----------------
    public IReadOnlyList<Category> GetCategories()
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, description, color, icon FROM categories ORDER BY name;";
            using var reader = cmd.ExecuteReader();
            var list = new List<Category>();
            while (reader.Read())
            {
                list.Add(new Category(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4)));
            }
            return list;
        }
    }

    public Category? GetCategoryById(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, description, color, icon FROM categories WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new Category(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4));
            }
            return null;
        }
    }

    public Category CreateCategory(Category category)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO categories VALUES (@id, @n, @d, @c, @i);";
            cmd.Parameters.AddWithValue("@id", category.Id);
            cmd.Parameters.AddWithValue("@n", category.Name);
            cmd.Parameters.AddWithValue("@d", category.Description);
            cmd.Parameters.AddWithValue("@c", category.Color);
            cmd.Parameters.AddWithValue("@i", category.Icon);
            cmd.ExecuteNonQuery();
            return category;
        }
    }

    // ---------------- PRODUCTS ----------------
    public IReadOnlyList<Product> GetProducts(string? categoryId = null, string? search = null, bool lowStockOnly = false)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            var sql = "SELECT id, sku, barcode, name, description, category_id, category_name, price, cost, tax_rate, stock_quantity, low_stock_threshold, is_active, created_at, updated_at FROM products WHERE 1=1";

            if (!string.IsNullOrEmpty(categoryId))
            {
                sql += " AND category_id = @catId";
                cmd.Parameters.AddWithValue("@catId", categoryId);
            }
            if (!string.IsNullOrEmpty(search))
            {
                sql += " AND (LOWER(name) LIKE @s OR LOWER(sku) LIKE @s OR barcode LIKE @s)";
                cmd.Parameters.AddWithValue("@s", $"%{search.Trim().ToLowerInvariant()}%");
            }
            if (lowStockOnly)
            {
                sql += " AND stock_quantity <= low_stock_threshold";
            }
            sql += " ORDER BY name;";

            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            var list = new List<Product>();
            while (reader.Read())
            {
                list.Add(ReadProduct(reader));
            }
            return list;
        }
    }

    public Product? GetProductById(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, sku, barcode, name, description, category_id, category_name, price, cost, tax_rate, stock_quantity, low_stock_threshold, is_active, created_at, updated_at FROM products WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadProduct(reader) : null;
        }
    }

    public Product? GetProductByBarcode(string barcode)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, sku, barcode, name, description, category_id, category_name, price, cost, tax_rate, stock_quantity, low_stock_threshold, is_active, created_at, updated_at FROM products WHERE barcode = @bc;";
            cmd.Parameters.AddWithValue("@bc", barcode.Trim());
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadProduct(reader) : null;
        }
    }

    public Product CreateProduct(Product product)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO products VALUES (
                    @id, @sku, @barcode, @name, @desc, @catId, @catName,
                    @price, @cost, @tax, @stock, @lowStock, @active, @created, @updated
                );";
            cmd.Parameters.AddWithValue("@id", product.Id);
            cmd.Parameters.AddWithValue("@sku", product.Sku);
            cmd.Parameters.AddWithValue("@barcode", product.Barcode);
            cmd.Parameters.AddWithValue("@name", product.Name);
            cmd.Parameters.AddWithValue("@desc", product.Description);
            cmd.Parameters.AddWithValue("@catId", product.CategoryId);
            cmd.Parameters.AddWithValue("@catName", product.CategoryName);
            cmd.Parameters.AddWithValue("@price", (double)product.Price);
            cmd.Parameters.AddWithValue("@cost", (double)product.Cost);
            cmd.Parameters.AddWithValue("@tax", (double)product.TaxRate);
            cmd.Parameters.AddWithValue("@stock", product.StockQuantity);
            cmd.Parameters.AddWithValue("@lowStock", product.LowStockThreshold);
            cmd.Parameters.AddWithValue("@active", product.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@created", product.CreatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("@updated", product.UpdatedAt.ToString("O"));
            cmd.ExecuteNonQuery();
            return product;
        }
    }

    public Product? UpdateProduct(string id, UpdateProductDto dto)
    {
        lock (_lock)
        {
            var existing = GetProductById(id);
            if (existing is null) return null;

            var cat = GetCategoryById(dto.CategoryId);
            var catName = cat?.Name ?? existing.CategoryName;
            var now = DateTimeOffset.UtcNow;

            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE products SET
                    name = @name,
                    description = @desc,
                    category_id = @catId,
                    category_name = @catName,
                    price = @price,
                    cost = @cost,
                    tax_rate = @tax,
                    stock_quantity = @stock,
                    low_stock_threshold = @lowStock,
                    is_active = @active,
                    updated_at = @updated
                WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@name", dto.Name);
            cmd.Parameters.AddWithValue("@desc", dto.Description);
            cmd.Parameters.AddWithValue("@catId", dto.CategoryId);
            cmd.Parameters.AddWithValue("@catName", catName);
            cmd.Parameters.AddWithValue("@price", (double)dto.Price);
            cmd.Parameters.AddWithValue("@cost", (double)dto.Cost);
            cmd.Parameters.AddWithValue("@tax", (double)dto.TaxRate);
            cmd.Parameters.AddWithValue("@stock", dto.StockQuantity);
            cmd.Parameters.AddWithValue("@lowStock", dto.LowStockThreshold);
            cmd.Parameters.AddWithValue("@active", dto.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@updated", now.ToString("O"));
            cmd.ExecuteNonQuery();

            return GetProductById(id);
        }
    }

    public bool DeleteProduct(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE products SET is_active = 0, updated_at = @u WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@u", DateTimeOffset.UtcNow.ToString("O"));
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public Product? AdjustStock(string id, int quantityChange, string reason)
    {
        lock (_lock)
        {
            var product = GetProductById(id);
            if (product is null) return null;

            var newQuantity = Math.Max(0, product.StockQuantity + quantityChange);
            var now = DateTimeOffset.UtcNow;

            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE products SET stock_quantity = @stock, updated_at = @u WHERE id = @id;";
            cmd.Parameters.AddWithValue("@stock", newQuantity);
            cmd.Parameters.AddWithValue("@u", now.ToString("O"));
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();

            return GetProductById(id);
        }
    }

    private static Product ReadProduct(SqliteDataReader reader)
    {
        return new Product(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? "" : reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            Convert.ToDecimal(reader.GetDouble(7)),
            Convert.ToDecimal(reader.GetDouble(8)),
            Convert.ToDecimal(reader.GetDouble(9)),
            reader.GetInt32(10),
            reader.GetInt32(11),
            reader.GetInt32(12) == 1,
            DateTimeOffset.Parse(reader.GetString(13)),
            DateTimeOffset.Parse(reader.GetString(14)));
    }

    // ---------------- CUSTOMERS ----------------
    public IReadOnlyList<Customer> GetCustomers(string? search = null)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            var sql = "SELECT id, name, email, phone, loyalty_points, tier, total_spent, created_at FROM customers";
            if (!string.IsNullOrEmpty(search))
            {
                sql += " WHERE LOWER(name) LIKE @s OR LOWER(email) LIKE @s OR phone LIKE @s";
                cmd.Parameters.AddWithValue("@s", $"%{search.Trim().ToLowerInvariant()}%");
            }
            sql += " ORDER BY name;";
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            var list = new List<Customer>();
            while (reader.Read())
            {
                list.Add(new Customer(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? "" : reader.GetString(3),
                    reader.GetInt32(4),
                    reader.GetString(5),
                    Convert.ToDecimal(reader.GetDouble(6)),
                    DateTimeOffset.Parse(reader.GetString(7))));
            }
            return list;
        }
    }

    public Customer? GetCustomerById(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, name, email, phone, loyalty_points, tier, total_spent, created_at FROM customers WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new Customer(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? "" : reader.GetString(3),
                    reader.GetInt32(4),
                    reader.GetString(5),
                    Convert.ToDecimal(reader.GetDouble(6)),
                    DateTimeOffset.Parse(reader.GetString(7)));
            }
            return null;
        }
    }

    public Customer CreateCustomer(Customer customer)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO customers VALUES (@id, @n, @e, @p, @pts, @t, @sp, @c);";
            cmd.Parameters.AddWithValue("@id", customer.Id);
            cmd.Parameters.AddWithValue("@n", customer.Name);
            cmd.Parameters.AddWithValue("@e", customer.Email);
            cmd.Parameters.AddWithValue("@p", customer.Phone);
            cmd.Parameters.AddWithValue("@pts", customer.LoyaltyPoints);
            cmd.Parameters.AddWithValue("@t", customer.Tier);
            cmd.Parameters.AddWithValue("@sp", (double)customer.TotalSpent);
            cmd.Parameters.AddWithValue("@c", customer.CreatedAt.ToString("O"));
            cmd.ExecuteNonQuery();
            return customer;
        }
    }

    public Customer? AddLoyaltyPoints(string customerId, int pointsDelta, decimal spendAmount = 0)
    {
        lock (_lock)
        {
            var customer = GetCustomerById(customerId);
            if (customer is null) return null;

            var newPoints = Math.Max(0, customer.LoyaltyPoints + pointsDelta);
            var newSpent = customer.TotalSpent + spendAmount;
            var tier = newSpent switch
            {
                >= 1000m => "Platinum",
                >= 400m => "Gold",
                >= 150m => "Silver",
                _ => "Bronze"
            };

            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE customers SET loyalty_points = @pts, total_spent = @sp, tier = @t WHERE id = @id;";
            cmd.Parameters.AddWithValue("@pts", newPoints);
            cmd.Parameters.AddWithValue("@sp", (double)newSpent);
            cmd.Parameters.AddWithValue("@t", tier);
            cmd.Parameters.AddWithValue("@id", customerId);
            cmd.ExecuteNonQuery();

            return GetCustomerById(customerId);
        }
    }

    // ---------------- DISCOUNTS ----------------
    public IReadOnlyList<Discount> GetDiscounts()
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, code, description, type, value, min_order_amount, is_active FROM discounts WHERE is_active = 1;";
            using var reader = cmd.ExecuteReader();
            var list = new List<Discount>();
            while (reader.Read())
            {
                list.Add(new Discount(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                    reader.GetString(3),
                    Convert.ToDecimal(reader.GetDouble(4)),
                    Convert.ToDecimal(reader.GetDouble(5)),
                    reader.GetInt32(6) == 1));
            }
            return list;
        }
    }

    public Discount? GetDiscountByCode(string code)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, code, description, type, value, min_order_amount, is_active FROM discounts WHERE UPPER(code) = @code AND is_active = 1;";
            cmd.Parameters.AddWithValue("@code", code.Trim().ToUpperInvariant());
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new Discount(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                    reader.GetString(3),
                    Convert.ToDecimal(reader.GetDouble(4)),
                    Convert.ToDecimal(reader.GetDouble(5)),
                    reader.GetInt32(6) == 1);
            }
            return null;
        }
    }

    public Discount CreateDiscount(Discount discount)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO discounts VALUES (@id, @c, @d, @t, @v, @min, @a);";
            cmd.Parameters.AddWithValue("@id", discount.Id);
            cmd.Parameters.AddWithValue("@c", discount.Code.ToUpperInvariant());
            cmd.Parameters.AddWithValue("@d", discount.Description);
            cmd.Parameters.AddWithValue("@t", discount.Type);
            cmd.Parameters.AddWithValue("@v", (double)discount.Value);
            cmd.Parameters.AddWithValue("@min", (double)discount.MinOrderAmount);
            cmd.Parameters.AddWithValue("@a", discount.IsActive ? 1 : 0);
            cmd.ExecuteNonQuery();
            return discount;
        }
    }

    // ---------------- ORDERS ----------------
    public Order CreateOrder(CreateOrderDto dto, string orderNumber)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();

            var user = GetUserById(dto.CashierId);
            var cashierName = user?.FullName ?? "Cashier Staff";

            Customer? customer = null;
            if (!string.IsNullOrEmpty(dto.CustomerId))
            {
                customer = GetCustomerById(dto.CustomerId);
            }

            var lineItems = new List<OrderItem>();
            decimal subtotal = 0m;
            decimal taxTotal = 0m;

            foreach (var itemDto in dto.Items)
            {
                var product = GetProductById(itemDto.ProductId);
                if (product is null)
                {
                    throw new InvalidOperationException($"Product ID {itemDto.ProductId} not found.");
                }

                var itemPrice = product.Price * itemDto.Quantity;
                var itemTax = Math.Round(itemPrice * product.TaxRate, 2);
                subtotal += itemPrice;
                taxTotal += itemTax;

                lineItems.Add(new OrderItem(
                    product.Id,
                    product.Sku,
                    product.Name,
                    product.Price,
                    itemDto.Quantity,
                    itemTax,
                    0m,
                    itemPrice + itemTax));

                // Deduct stock in transaction
                using var stockCmd = conn.CreateCommand();
                stockCmd.Transaction = tx;
                stockCmd.CommandText = "UPDATE products SET stock_quantity = MAX(0, stock_quantity - @qty), updated_at = @u WHERE id = @id;";
                stockCmd.Parameters.AddWithValue("@qty", itemDto.Quantity);
                stockCmd.Parameters.AddWithValue("@u", DateTimeOffset.UtcNow.ToString("O"));
                stockCmd.Parameters.AddWithValue("@id", product.Id);
                stockCmd.ExecuteNonQuery();
            }

            // Calculate discount
            decimal discountTotal = 0m;
            if (!string.IsNullOrEmpty(dto.DiscountCode))
            {
                var discount = GetDiscountByCode(dto.DiscountCode);
                if (discount is not null && subtotal >= discount.MinOrderAmount)
                {
                    discountTotal = discount.Type == "Percentage"
                        ? Math.Round(subtotal * (discount.Value / 100m), 2)
                        : Math.Min(subtotal, discount.Value);
                }
            }

            var grandTotal = Math.Max(0m, subtotal - discountTotal + taxTotal);
            var changeGiven = dto.PaymentMethod == "Cash" ? Math.Max(0m, dto.AmountTendered - grandTotal) : 0m;
            var orderId = $"ord-{Guid.NewGuid():N}";
            var now = DateTimeOffset.UtcNow;

            using var orderCmd = conn.CreateCommand();
            orderCmd.Transaction = tx;
            orderCmd.CommandText = @"
                INSERT INTO orders VALUES (
                    @id, @ordNum, @regId, @cashId, @cashName, @custId, @custName,
                    @items, @sub, @tax, @disc, @grand, @pm, @tend, @change,
                    'Completed', @notes, @created
                );";
            orderCmd.Parameters.AddWithValue("@id", orderId);
            orderCmd.Parameters.AddWithValue("@ordNum", orderNumber);
            orderCmd.Parameters.AddWithValue("@regId", dto.RegisterId);
            orderCmd.Parameters.AddWithValue("@cashId", dto.CashierId);
            orderCmd.Parameters.AddWithValue("@cashName", cashierName);
            orderCmd.Parameters.AddWithValue("@custId", (object?)customer?.Id ?? DBNull.Value);
            orderCmd.Parameters.AddWithValue("@custName", (object?)customer?.Name ?? DBNull.Value);
            orderCmd.Parameters.AddWithValue("@items", JsonSerializer.Serialize(lineItems));
            orderCmd.Parameters.AddWithValue("@sub", (double)subtotal);
            orderCmd.Parameters.AddWithValue("@tax", (double)taxTotal);
            orderCmd.Parameters.AddWithValue("@disc", (double)discountTotal);
            orderCmd.Parameters.AddWithValue("@grand", (double)grandTotal);
            orderCmd.Parameters.AddWithValue("@pm", dto.PaymentMethod);
            orderCmd.Parameters.AddWithValue("@tend", (double)dto.AmountTendered);
            orderCmd.Parameters.AddWithValue("@change", (double)changeGiven);
            orderCmd.Parameters.AddWithValue("@notes", (object?)dto.Notes ?? DBNull.Value);
            orderCmd.Parameters.AddWithValue("@created", now.ToString("O"));
            orderCmd.ExecuteNonQuery();

            // Update shift expected cash and sales
            using var shiftUpdateCmd = conn.CreateCommand();
            shiftUpdateCmd.Transaction = tx;
            var cashAdd = dto.PaymentMethod == "Cash" ? (double)grandTotal : 0.0;
            shiftUpdateCmd.CommandText = @"
                UPDATE shifts SET
                    expected_cash = expected_cash + @cashAdd,
                    total_sales = total_sales + @sale,
                    total_transactions = total_transactions + 1
                WHERE register_id = @reg AND status = 'Open';";
            shiftUpdateCmd.Parameters.AddWithValue("@cashAdd", cashAdd);
            shiftUpdateCmd.Parameters.AddWithValue("@sale", (double)grandTotal);
            shiftUpdateCmd.Parameters.AddWithValue("@reg", dto.RegisterId);
            shiftUpdateCmd.ExecuteNonQuery();

            // Add loyalty points if customer attached
            if (customer is not null)
            {
                var pointsEarned = (int)Math.Floor(grandTotal);
                using var custUpdCmd = conn.CreateCommand();
                custUpdCmd.Transaction = tx;
                custUpdCmd.CommandText = "UPDATE customers SET loyalty_points = loyalty_points + @pts, total_spent = total_spent + @amt WHERE id = @id;";
                custUpdCmd.Parameters.AddWithValue("@pts", pointsEarned);
                custUpdCmd.Parameters.AddWithValue("@amt", (double)grandTotal);
                custUpdCmd.Parameters.AddWithValue("@id", customer.Id);
                custUpdCmd.ExecuteNonQuery();
            }

            tx.Commit();

            return new Order(
                orderId,
                orderNumber,
                dto.RegisterId,
                dto.CashierId,
                cashierName,
                customer?.Id,
                customer?.Name,
                lineItems,
                subtotal,
                taxTotal,
                discountTotal,
                grandTotal,
                dto.PaymentMethod,
                dto.AmountTendered,
                changeGiven,
                "Completed",
                dto.Notes,
                now);
        }
    }

    public IReadOnlyList<Order> GetOrders(string? status = null, string? search = null, DateTimeOffset? from = null, DateTimeOffset? to = null)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            var sql = "SELECT id, order_number, register_id, cashier_id, cashier_name, customer_id, customer_name, items_json, subtotal, tax_total, discount_total, grand_total, payment_method, amount_tendered, change_given, status, notes, created_at FROM orders WHERE 1=1";

            if (!string.IsNullOrEmpty(status))
            {
                sql += " AND status = @st";
                cmd.Parameters.AddWithValue("@st", status);
            }
            if (!string.IsNullOrEmpty(search))
            {
                sql += " AND (order_number LIKE @s OR customer_name LIKE @s OR cashier_name LIKE @s)";
                cmd.Parameters.AddWithValue("@s", $"%{search.Trim()}%");
            }
            if (from.HasValue)
            {
                sql += " AND created_at >= @from";
                cmd.Parameters.AddWithValue("@from", from.Value.ToString("O"));
            }
            if (to.HasValue)
            {
                sql += " AND created_at <= @to";
                cmd.Parameters.AddWithValue("@to", to.Value.ToString("O"));
            }
            sql += " ORDER BY created_at DESC;";

            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            var list = new List<Order>();
            while (reader.Read())
            {
                list.Add(ReadOrder(reader));
            }
            return list;
        }
    }

    public Order? GetOrderById(string id)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, order_number, register_id, cashier_id, cashier_name, customer_id, customer_name, items_json, subtotal, tax_total, discount_total, grand_total, payment_method, amount_tendered, change_given, status, notes, created_at FROM orders WHERE id = @id OR order_number = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadOrder(reader) : null;
        }
    }

    public ReceiptDto? GetReceipt(string orderId)
    {
        var order = GetOrderById(orderId);
        if (order is null) return null;

        return new ReceiptDto(
            order.OrderNumber,
            "NovaPOS Flagship Cafe & Store",
            "100 Innovation Boulevard, Tech District",
            "(555) 019-4822",
            order.CreatedAt,
            order.CashierName,
            order.CustomerName,
            order.Items,
            order.Subtotal,
            order.TaxTotal,
            order.DiscountTotal,
            order.GrandTotal,
            order.PaymentMethod,
            order.AmountTendered,
            order.ChangeGiven,
            order.Status);
    }

    public Order? RefundOrder(string id, string reason)
    {
        lock (_lock)
        {
            var order = GetOrderById(id);
            if (order is null || order.Status == "Refunded") return null;

            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();

            // Restore items to stock
            foreach (var item in order.Items)
            {
                using var stockCmd = conn.CreateCommand();
                stockCmd.Transaction = tx;
                stockCmd.CommandText = "UPDATE products SET stock_quantity = stock_quantity + @qty, updated_at = @u WHERE id = @pid;";
                stockCmd.Parameters.AddWithValue("@qty", item.Quantity);
                stockCmd.Parameters.AddWithValue("@u", DateTimeOffset.UtcNow.ToString("O"));
                stockCmd.Parameters.AddWithValue("@pid", item.ProductId);
                stockCmd.ExecuteNonQuery();
            }

            using var updateCmd = conn.CreateCommand();
            updateCmd.Transaction = tx;
            updateCmd.CommandText = "UPDATE orders SET status = 'Refunded', notes = COALESCE(notes || ' | Refund Reason: ' || @r, 'Refund Reason: ' || @r) WHERE id = @id;";
            updateCmd.Parameters.AddWithValue("@r", reason);
            updateCmd.Parameters.AddWithValue("@id", order.Id);
            updateCmd.ExecuteNonQuery();

            tx.Commit();

            return GetOrderById(order.Id);
        }
    }

    public Order? VoidOrder(string id, string reason)
    {
        lock (_lock)
        {
            var order = GetOrderById(id);
            if (order is null || order.Status == "Voided") return null;

            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();

            // Restore items to stock
            foreach (var item in order.Items)
            {
                using var stockCmd = conn.CreateCommand();
                stockCmd.Transaction = tx;
                stockCmd.CommandText = "UPDATE products SET stock_quantity = stock_quantity + @qty, updated_at = @u WHERE id = @pid;";
                stockCmd.Parameters.AddWithValue("@qty", item.Quantity);
                stockCmd.Parameters.AddWithValue("@u", DateTimeOffset.UtcNow.ToString("O"));
                stockCmd.Parameters.AddWithValue("@pid", item.ProductId);
                stockCmd.ExecuteNonQuery();
            }

            using var updateCmd = conn.CreateCommand();
            updateCmd.Transaction = tx;
            updateCmd.CommandText = "UPDATE orders SET status = 'Voided', notes = COALESCE(notes || ' | Void Reason: ' || @r, 'Void Reason: ' || @r) WHERE id = @id;";
            updateCmd.Parameters.AddWithValue("@r", reason);
            updateCmd.Parameters.AddWithValue("@id", order.Id);
            updateCmd.ExecuteNonQuery();

            tx.Commit();

            return GetOrderById(order.Id);
        }
    }

    private static Order ReadOrder(SqliteDataReader reader)
    {
        var itemsJson = reader.GetString(7);
        var items = JsonSerializer.Deserialize<List<OrderItem>>(itemsJson) ?? new List<OrderItem>();

        return new Order(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            items,
            Convert.ToDecimal(reader.GetDouble(8)),
            Convert.ToDecimal(reader.GetDouble(9)),
            Convert.ToDecimal(reader.GetDouble(10)),
            Convert.ToDecimal(reader.GetDouble(11)),
            reader.GetString(12),
            Convert.ToDecimal(reader.GetDouble(13)),
            Convert.ToDecimal(reader.GetDouble(14)),
            reader.GetString(15),
            reader.IsDBNull(16) ? null : reader.GetString(16),
            DateTimeOffset.Parse(reader.GetString(17)));
    }

    // ---------------- SHIFTS ----------------
    public IReadOnlyList<RegisterShift> GetShifts(string? registerId = null)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            var sql = "SELECT id, register_id, cashier_id, cashier_name, opened_at, closed_at, starting_float, expected_cash, counted_cash, variance, status, total_sales, total_transactions FROM shifts";
            if (!string.IsNullOrEmpty(registerId))
            {
                sql += " WHERE register_id = @reg";
                cmd.Parameters.AddWithValue("@reg", registerId);
            }
            sql += " ORDER BY opened_at DESC;";
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            var list = new List<RegisterShift>();
            while (reader.Read())
            {
                list.Add(ReadShift(reader));
            }
            return list;
        }
    }

    public RegisterShift? GetCurrentShift(string registerId)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, register_id, cashier_id, cashier_name, opened_at, closed_at, starting_float, expected_cash, counted_cash, variance, status, total_sales, total_transactions FROM shifts WHERE register_id = @reg AND status = 'Open' ORDER BY opened_at DESC LIMIT 1;";
            cmd.Parameters.AddWithValue("@reg", registerId);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadShift(reader) : null;
        }
    }

    public RegisterShift OpenShift(OpenShiftDto dto)
    {
        lock (_lock)
        {
            var user = GetUserById(dto.CashierId);
            var cashierName = user?.FullName ?? "Cashier Operator";
            var id = $"shift-{Guid.NewGuid():N}";
            var now = DateTimeOffset.UtcNow;

            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO shifts VALUES (
                    @id, @reg, @cid, @cname, @opened, NULL,
                    @float, @float, NULL, NULL, 'Open', 0.0, 0
                );";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@reg", dto.RegisterId);
            cmd.Parameters.AddWithValue("@cid", dto.CashierId);
            cmd.Parameters.AddWithValue("@cname", cashierName);
            cmd.Parameters.AddWithValue("@opened", now.ToString("O"));
            cmd.Parameters.AddWithValue("@float", (double)dto.StartingFloat);
            cmd.ExecuteNonQuery();

            return new RegisterShift(
                id,
                dto.RegisterId,
                dto.CashierId,
                cashierName,
                now,
                null,
                dto.StartingFloat,
                dto.StartingFloat,
                null,
                null,
                "Open",
                0m,
                0);
        }
    }

    public RegisterShift? CloseShift(string shiftId, decimal countedCash, string? notes)
    {
        lock (_lock)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, register_id, cashier_id, cashier_name, opened_at, closed_at, starting_float, expected_cash, counted_cash, variance, status, total_sales, total_transactions FROM shifts WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", shiftId);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            var existing = ReadShift(reader);
            reader.Close();

            var now = DateTimeOffset.UtcNow;
            var variance = countedCash - existing.ExpectedCash;

            using var updateCmd = conn.CreateCommand();
            updateCmd.CommandText = @"
                UPDATE shifts SET
                    closed_at = @c,
                    counted_cash = @counted,
                    variance = @var,
                    status = 'Closed'
                WHERE id = @id;";
            updateCmd.Parameters.AddWithValue("@c", now.ToString("O"));
            updateCmd.Parameters.AddWithValue("@counted", (double)countedCash);
            updateCmd.Parameters.AddWithValue("@var", (double)variance);
            updateCmd.Parameters.AddWithValue("@id", shiftId);
            updateCmd.ExecuteNonQuery();

            return new RegisterShift(
                existing.Id,
                existing.RegisterId,
                existing.CashierId,
                existing.CashierName,
                existing.OpenedAt,
                now,
                existing.StartingFloat,
                existing.ExpectedCash,
                countedCash,
                variance,
                "Closed",
                existing.TotalSales,
                existing.TotalTransactions);
        }
    }

    public CashDrop? AddCashDrop(string shiftId, AddCashDropDto dto)
    {
        lock (_lock)
        {
            var dropId = $"drop-{Guid.NewGuid():N}";
            var now = DateTimeOffset.UtcNow;
            var isRemoval = dto.Type.Equals("Drop", StringComparison.OrdinalIgnoreCase)
            || dto.Type.Equals("Payout", StringComparison.OrdinalIgnoreCase)
            || dto.Type.Equals("CashOut", StringComparison.OrdinalIgnoreCase)
            || dto.Type.Equals("SafeDrop", StringComparison.OrdinalIgnoreCase);
            var delta = isRemoval ? -(double)dto.Amount : (double)dto.Amount;

            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();

            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO cash_drops VALUES (@id, @sid, @type, @amt, @r, @t);";
            cmd.Parameters.AddWithValue("@id", dropId);
            cmd.Parameters.AddWithValue("@sid", shiftId);
            cmd.Parameters.AddWithValue("@type", dto.Type);
            cmd.Parameters.AddWithValue("@amt", (double)dto.Amount);
            cmd.Parameters.AddWithValue("@r", dto.Reason);
            cmd.Parameters.AddWithValue("@t", now.ToString("O"));
            cmd.ExecuteNonQuery();

            using var shiftCmd = conn.CreateCommand();
            shiftCmd.Transaction = tx;
            shiftCmd.CommandText = "UPDATE shifts SET expected_cash = expected_cash + @delta WHERE id = @sid;";
            shiftCmd.Parameters.AddWithValue("@delta", delta);
            shiftCmd.Parameters.AddWithValue("@sid", shiftId);
            shiftCmd.ExecuteNonQuery();

            tx.Commit();

            return new CashDrop(dropId, shiftId, dto.Type, dto.Amount, dto.Reason, now);
        }
    }

    private static RegisterShift ReadShift(SqliteDataReader reader)
    {
        return new RegisterShift(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            DateTimeOffset.Parse(reader.GetString(4)),
            reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5)),
            Convert.ToDecimal(reader.GetDouble(6)),
            Convert.ToDecimal(reader.GetDouble(7)),
            reader.IsDBNull(8) ? null : Convert.ToDecimal(reader.GetDouble(8)),
            reader.IsDBNull(9) ? null : Convert.ToDecimal(reader.GetDouble(9)),
            reader.GetString(10),
            Convert.ToDecimal(reader.GetDouble(11)),
            reader.GetInt32(12));
    }

    // ---------------- ANALYTICS ----------------
    public PosAnalytics GetAnalytics()
    {
        lock (_lock)
        {
            using var conn = CreateConnection();

            // Total revenue and orders
            using var revCmd = conn.CreateCommand();
            revCmd.CommandText = "SELECT COALESCE(SUM(grand_total), 0), COUNT(*) FROM orders WHERE status = 'Completed';";
            using var revReader = revCmd.ExecuteReader();
            decimal totalRevenue = 0m;
            int totalOrders = 0;
            if (revReader.Read())
            {
                totalRevenue = Convert.ToDecimal(revReader.GetDouble(0));
                totalOrders = revReader.GetInt32(1);
            }
            revReader.Close();

            // Total products and low stock count
            using var prodCmd = conn.CreateCommand();
            prodCmd.CommandText = "SELECT COUNT(*), SUM(CASE WHEN stock_quantity <= low_stock_threshold THEN 1 ELSE 0 END) FROM products WHERE is_active = 1;";
            using var prodReader = prodCmd.ExecuteReader();
            int totalProducts = 0;
            int lowStockCount = 0;
            if (prodReader.Read())
            {
                totalProducts = prodReader.GetInt32(0);
                lowStockCount = prodReader.IsDBNull(1) ? 0 : prodReader.GetInt32(1);
            }
            prodReader.Close();

            // Active shifts
            using var shiftCmd = conn.CreateCommand();
            shiftCmd.CommandText = "SELECT COUNT(*) FROM shifts WHERE status = 'Open';";
            var activeShifts = Convert.ToInt32(shiftCmd.ExecuteScalar());

            var avgOrder = totalOrders > 0 ? Math.Round(totalRevenue / totalOrders, 2) : 0m;
            var topProducts = GetTopProducts(5);
            var categorySales = GetCategorySales();

            return new PosAnalytics(
                totalRevenue,
                totalOrders,
                totalProducts,
                lowStockCount,
                activeShifts,
                avgOrder,
                topProducts,
                categorySales);
        }
    }

    public IReadOnlyList<TopProductDto> GetTopProducts(int limit = 5)
    {
        lock (_lock)
        {
            var orders = GetOrders(status: "Completed");
            var itemStats = new Dictionary<string, (string Name, string Cat, int Units, decimal Revenue)>();

            foreach (var order in orders)
            {
                foreach (var item in order.Items)
                {
                    if (!itemStats.TryGetValue(item.ProductId, out var stats))
                    {
                        var prod = GetProductById(item.ProductId);
                        stats = (item.Name, prod?.CategoryName ?? "General", 0, 0m);
                    }
                    itemStats[item.ProductId] = (stats.Name, stats.Cat, stats.Units + item.Quantity, stats.Revenue + item.TotalPrice);
                }
            }

            return itemStats
                .OrderByDescending(kv => kv.Value.Revenue)
                .Take(limit)
                .Select(kv => new TopProductDto(kv.Key, kv.Value.Name, kv.Value.Cat, kv.Value.Units, kv.Value.Revenue))
                .ToList();
        }
    }

    public IReadOnlyList<CategorySalesDto> GetCategorySales()
    {
        lock (_lock)
        {
            var categories = GetCategories();
            var orders = GetOrders(status: "Completed");
            var catMap = new Dictionary<string, (string Name, int Units, decimal Revenue)>();

            foreach (var c in categories)
            {
                catMap[c.Id] = (c.Name, 0, 0m);
            }

            foreach (var order in orders)
            {
                foreach (var item in order.Items)
                {
                    var prod = GetProductById(item.ProductId);
                    var catId = prod?.CategoryId ?? "cat-coffee";
                    if (catMap.TryGetValue(catId, out var existing))
                    {
                        catMap[catId] = (existing.Name, existing.Units + item.Quantity, existing.Revenue + item.TotalPrice);
                    }
                }
            }

            return catMap
                .Select(kv => new CategorySalesDto(kv.Key, kv.Value.Name, kv.Value.Units, kv.Value.Revenue))
                .ToList();
        }
    }
}
