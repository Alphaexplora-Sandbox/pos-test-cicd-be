namespace PosTestCicdBackend.Domain;

public interface IPosStore
{
    // Auth & Users
    UserProfile? Authenticate(string username, string password);
    UserProfile? GetUserByToken(string token);
    UserProfile? GetUserById(string id);
    IReadOnlyList<UserProfile> GetUsers();

    // Categories
    IReadOnlyList<Category> GetCategories();
    Category? GetCategoryById(string id);
    Category CreateCategory(Category category);

    // Products & Inventory
    IReadOnlyList<Product> GetProducts(string? categoryId = null, string? search = null, bool lowStockOnly = false);
    Product? GetProductById(string id);
    Product? GetProductByBarcode(string barcode);
    Product CreateProduct(Product product);
    Product? UpdateProduct(string id, UpdateProductDto dto);
    bool DeleteProduct(string id);
    Product? AdjustStock(string id, int quantityChange, string reason);

    // Customers
    IReadOnlyList<Customer> GetCustomers(string? search = null);
    Customer? GetCustomerById(string id);
    Customer CreateCustomer(Customer customer);
    Customer? AddLoyaltyPoints(string customerId, int pointsDelta, decimal spendAmount = 0);

    // Discounts
    IReadOnlyList<Discount> GetDiscounts();
    Discount? GetDiscountByCode(string code);
    Discount CreateDiscount(Discount discount);

    // Orders
    Order CreateOrder(CreateOrderDto dto, string orderNumber);
    IReadOnlyList<Order> GetOrders(string? status = null, string? search = null, DateTimeOffset? from = null, DateTimeOffset? to = null);
    Order? GetOrderById(string id);
    ReceiptDto? GetReceipt(string orderId);
    Order? RefundOrder(string id, string reason);
    Order? VoidOrder(string id, string reason);

    // Shifts & Registers
    IReadOnlyList<RegisterShift> GetShifts(string? registerId = null);
    RegisterShift? GetCurrentShift(string registerId);
    RegisterShift OpenShift(OpenShiftDto dto);
    RegisterShift? CloseShift(string shiftId, decimal countedCash, string? notes);
    CashDrop? AddCashDrop(string shiftId, AddCashDropDto dto);

    // Analytics
    PosAnalytics GetAnalytics();
    IReadOnlyList<TopProductDto> GetTopProducts(int limit = 5);
    IReadOnlyList<CategorySalesDto> GetCategorySales();
}
