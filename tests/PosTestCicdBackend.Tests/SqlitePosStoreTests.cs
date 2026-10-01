using PosTestCicdBackend.Domain;
using PosTestCicdBackend.Infrastructure;
using Xunit;

namespace PosTestCicdBackend.Tests;

public class SqlitePosStoreTests : IDisposable
{
    private readonly string _dbName;
    private readonly SqlitePosStore _store;

    public SqlitePosStoreTests()
    {
        _dbName = $"test_pos_{Guid.NewGuid():N}.db";
        _store = new SqlitePosStore($"Data Source={_dbName};Cache=Shared");
    }

    public void Dispose()
    {
        _store.Dispose();
        if (File.Exists(_dbName))
        {
            try { File.Delete(_dbName); } catch { }
        }
    }

    [Fact]
    public void InitialSeed_PopulatesCatalogAndUsers()
    {
        var users = _store.GetUsers();
        Assert.NotEmpty(users);

        var categories = _store.GetCategories();
        Assert.NotEmpty(categories);

        var products = _store.GetProducts();
        Assert.True(products.Count >= 10);

        var discounts = _store.GetDiscounts();
        Assert.NotEmpty(discounts);

        var customers = _store.GetCustomers();
        Assert.NotEmpty(customers);
    }

    [Fact]
    public void Authenticate_ValidCredentials_ReturnsProfile()
    {
        var user = _store.Authenticate("admin", "admin");
        Assert.NotNull(user);
        Assert.Equal("admin", user.Username);
        Assert.Equal("Admin", user.Role);

        var byToken = _store.GetUserByToken(user.Token);
        Assert.NotNull(byToken);
        Assert.Equal(user.Id, byToken.Id);

        var byId = _store.GetUserById(user.Id);
        Assert.NotNull(byId);
        Assert.Equal(user.Username, byId.Username);
    }

    [Fact]
    public void Authenticate_InvalidCredentials_ReturnsNull()
    {
        var user = _store.Authenticate("admin", "wrong_password");
        Assert.Null(user);
    }

    [Fact]
    public void Categories_CreateAndGetById_Works()
    {
        var newCat = new Category($"cat-{Guid.NewGuid():N}", "Seasonal Specials", "Autumn seasonal menu", "#FF8C00", "sparkles");
        var created = _store.CreateCategory(newCat);
        Assert.Equal(newCat.Id, created.Id);

        var retrieved = _store.GetCategoryById(newCat.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Seasonal Specials", retrieved.Name);
    }

    [Fact]
    public void Products_FilterAndSearch_Works()
    {
        var coffeeProducts = _store.GetProducts(categoryId: "cat-coffee");
        Assert.All(coffeeProducts, p => Assert.Equal("cat-coffee", p.CategoryId));

        var searched = _store.GetProducts(search: "Croissant");
        Assert.NotEmpty(searched);

        var lowStock = _store.GetProducts(lowStockOnly: true);
        Assert.All(lowStock, p => Assert.True(p.StockQuantity <= p.LowStockThreshold));
    }

    [Fact]
    public void Products_BarcodeLookup_Works()
    {
        var existing = _store.GetProducts().First();
        var byBarcode = _store.GetProductByBarcode(existing.Barcode);
        Assert.NotNull(byBarcode);
        Assert.Equal(existing.Id, byBarcode.Id);

        var notFound = _store.GetProductByBarcode("99999999999999");
        Assert.Null(notFound);
    }

    [Fact]
    public void Products_CreateUpdateDeleteAdjust_Lifecycle()
    {
        var now = DateTimeOffset.UtcNow;
        var p = new Product(
            $"prod-unit-{Guid.NewGuid():N}",
            "TST-SKU-01",
            "123456789012",
            "Unit Test Pastry",
            "Tasty pastry",
            "cat-bakery",
            "Fresh Bakery",
            4.25m,
            1.20m,
            0.08m,
            50,
            10,
            true,
            now,
            now);

        var created = _store.CreateProduct(p);
        Assert.Equal(p.Id, created.Id);

        var updated = _store.UpdateProduct(p.Id, new UpdateProductDto(
            "Updated Pastry",
            "Even tastier",
            "cat-bakery",
            5.00m,
            1.30m,
            0.08m,
            45,
            10,
            true));
        Assert.NotNull(updated);
        Assert.Equal("Updated Pastry", updated.Name);
        Assert.Equal(5.00m, updated.Price);

        var adjusted = _store.AdjustStock(p.Id, -15, "Restock audit");
        Assert.NotNull(adjusted);
        Assert.Equal(30, adjusted.StockQuantity);

        var deleted = _store.DeleteProduct(p.Id);
        Assert.True(deleted);

        var reloaded = _store.GetProductById(p.Id);
        Assert.NotNull(reloaded);
        Assert.False(reloaded.IsActive);
    }

    [Fact]
    public void Customers_LoyaltyPointsAndTier_UpgradesCorrectly()
    {
        var now = DateTimeOffset.UtcNow;
        var cust = new Customer($"cust-unit-{Guid.NewGuid():N}", "Marcus Green", "marcus@green.com", "555-9090", 0, "Bronze", 0m, now);
        _store.CreateCustomer(cust);

        var updated = _store.AddLoyaltyPoints(cust.Id, 200, 250m);
        Assert.NotNull(updated);
        Assert.Equal(200, updated.LoyaltyPoints);
        Assert.Equal(250m, updated.TotalSpent);
        Assert.Equal("Silver", updated.Tier);

        var goldUpdated = _store.AddLoyaltyPoints(cust.Id, 300, 300m);
        Assert.NotNull(goldUpdated);
        Assert.Equal("Gold", goldUpdated.Tier);
    }

    [Fact]
    public void Discounts_CreateAndLookup_Works()
    {
        var disc = new Discount($"disc-{Guid.NewGuid():N}", "SPRING30", "30% off spring sale", "Percentage", 30m, 30m, true);
        _store.CreateDiscount(disc);

        var found = _store.GetDiscountByCode("spring30");
        Assert.NotNull(found);
        Assert.Equal(30m, found.Value);
    }

    [Fact]
    public void Orders_CreateAndReceiptAndRefund_Works()
    {
        var products = _store.GetProducts(categoryId: "cat-coffee");
        var p1 = products[0];
        var initialStock = p1.StockQuantity;

        var orderDto = new CreateOrderDto(
            "REG-01",
            "usr-3",
            "cust-1",
            new List<CreateOrderItemDto> { new(p1.Id, 2) },
            "WELCOME10",
            "Cash",
            20.00m,
            "Table 4 order");

        var order = _store.CreateOrder(orderDto, "POS-TEST-0001");
        Assert.NotNull(order);
        Assert.Equal("Completed", order.Status);
        Assert.Equal(2, order.Items[0].Quantity);
        Assert.True(order.DiscountTotal > 0);
        Assert.True(order.GrandTotal > 0);
        Assert.True(order.ChangeGiven >= 0);

        // Check stock was deducted
        var productAfterOrder = _store.GetProductById(p1.Id);
        Assert.NotNull(productAfterOrder);
        Assert.Equal(initialStock - 2, productAfterOrder.StockQuantity);

        // Get receipt
        var receipt = _store.GetReceipt(order.Id);
        Assert.NotNull(receipt);
        Assert.Equal(order.OrderNumber, receipt.OrderNumber);

        // Refund order
        var refunded = _store.RefundOrder(order.Id, "Customer spilled coffee");
        Assert.NotNull(refunded);
        Assert.Equal("Refunded", refunded.Status);

        // Stock restored
        var productAfterRefund = _store.GetProductById(p1.Id);
        Assert.NotNull(productAfterRefund);
        Assert.Equal(initialStock, productAfterRefund.StockQuantity);
    }

    [Fact]
    public void Orders_VoidOrder_RestoresStock()
    {
        var prod = _store.GetProducts().First();
        var stockBefore = prod.StockQuantity;

        var order = _store.CreateOrder(new CreateOrderDto(
            "REG-01",
            "usr-3",
            null,
            new List<CreateOrderItemDto> { new(prod.Id, 1) },
            null,
            "Card",
            prod.Price * 1.08m,
            null), "POS-TEST-VOID");

        var voided = _store.VoidOrder(order.Id, "Accidental ring-up");
        Assert.NotNull(voided);
        Assert.Equal("Voided", voided.Status);

        var restored = _store.GetProductById(prod.Id);
        Assert.NotNull(restored);
        Assert.Equal(stockBefore, restored.StockQuantity);
    }

    [Fact]
    public void Shifts_OpenCloseCashDrop_Lifecycle()
    {
        var regId = $"REG-{Guid.NewGuid():N}"[..6];
        var openShift = _store.OpenShift(new OpenShiftDto(regId, "usr-3", 150.00m));
        Assert.NotNull(openShift);
        Assert.Equal("Open", openShift.Status);
        Assert.Equal(150.00m, openShift.StartingFloat);

        // Current shift
        var current = _store.GetCurrentShift(regId);
        Assert.NotNull(current);
        Assert.Equal(openShift.Id, current.Id);

        // Cash Drop (payout)
        var drop = _store.AddCashDrop(openShift.Id, new AddCashDropDto("Payout", 25.00m, "Vendor milk delivery"));
        Assert.NotNull(drop);

        // Close shift
        var closed = _store.CloseShift(openShift.Id, 125.00m, "Drawer balanced");
        Assert.NotNull(closed);
        Assert.Equal("Closed", closed.Status);
        Assert.Equal(125.00m, closed.CountedCash);
        Assert.Equal(0.00m, closed.Variance);
    }

    [Fact]
    public void Analytics_Calculations_AreValid()
    {
        var analytics = _store.GetAnalytics();
        Assert.NotNull(analytics);
        Assert.True(analytics.TotalProducts > 0);
        Assert.NotNull(analytics.TopProducts);
        Assert.NotNull(analytics.CategorySales);
    }
}
