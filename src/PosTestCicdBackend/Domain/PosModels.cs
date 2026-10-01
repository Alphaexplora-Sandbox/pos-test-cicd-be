namespace PosTestCicdBackend.Domain;

public record Category(
    string Id,
    string Name,
    string Description,
    string Color,
    string Icon);

public record Product(
    string Id,
    string Sku,
    string Barcode,
    string Name,
    string Description,
    string CategoryId,
    string CategoryName,
    decimal Price,
    decimal Cost,
    decimal TaxRate,
    int StockQuantity,
    int LowStockThreshold,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record Customer(
    string Id,
    string Name,
    string Email,
    string Phone,
    int LoyaltyPoints,
    string Tier,
    decimal TotalSpent,
    DateTimeOffset CreatedAt);

public record Discount(
    string Id,
    string Code,
    string Description,
    string Type,
    decimal Value,
    decimal MinOrderAmount,
    bool IsActive);

public record OrderItem(
    string ProductId,
    string Sku,
    string Name,
    decimal UnitPrice,
    int Quantity,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal TotalPrice);

public record Order(
    string Id,
    string OrderNumber,
    string RegisterId,
    string CashierId,
    string CashierName,
    string? CustomerId,
    string? CustomerName,
    IReadOnlyList<OrderItem> Items,
    decimal Subtotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal GrandTotal,
    string PaymentMethod,
    decimal AmountTendered,
    decimal ChangeGiven,
    string Status,
    string? Notes,
    DateTimeOffset CreatedAt);

public record RegisterShift(
    string Id,
    string RegisterId,
    string CashierId,
    string CashierName,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal StartingFloat,
    decimal ExpectedCash,
    decimal? CountedCash,
    decimal? Variance,
    string Status,
    decimal TotalSales,
    int TotalTransactions);

public record CashDrop(
    string Id,
    string ShiftId,
    string Type,
    decimal Amount,
    string Reason,
    DateTimeOffset Timestamp);

public record UserProfile(
    string Id,
    string Username,
    string Email,
    string FullName,
    string Role,
    string Token);

public record TopProductDto(
    string ProductId,
    string Name,
    string CategoryName,
    int UnitsSold,
    decimal Revenue);

public record CategorySalesDto(
    string CategoryId,
    string CategoryName,
    int ItemsSold,
    decimal Revenue);

public record PosAnalytics(
    decimal TotalRevenue,
    int TotalOrders,
    int TotalProducts,
    int LowStockCount,
    int ActiveShifts,
    decimal AverageOrderValue,
    IReadOnlyList<TopProductDto> TopProducts,
    IReadOnlyList<CategorySalesDto> CategorySales);

// DTOs for API requests and responses
public record LoginRequest(string Username, string Password);

public record CreateProductDto(
    string Sku,
    string Barcode,
    string Name,
    string Description,
    string CategoryId,
    decimal Price,
    decimal Cost,
    decimal TaxRate,
    int StockQuantity,
    int LowStockThreshold);

public record UpdateProductDto(
    string Name,
    string Description,
    string CategoryId,
    decimal Price,
    decimal Cost,
    decimal TaxRate,
    int StockQuantity,
    int LowStockThreshold,
    bool IsActive);

public record StockAdjustmentDto(int QuantityChange, string Reason);

public record CreateCategoryDto(string Name, string Description, string Color, string Icon);

public record CreateCustomerDto(string Name, string Email, string Phone);

public record CreateDiscountDto(string Code, string Description, string Type, decimal Value, decimal MinOrderAmount);

public record ValidateDiscountRequest(string Code, decimal OrderSubtotal);

public record ValidateDiscountResponse(bool IsValid, decimal DiscountAmount, string? Message);

public record CreateOrderItemDto(string ProductId, int Quantity);

public record CreateOrderDto(
    string RegisterId,
    string CashierId,
    string? CustomerId,
    IReadOnlyList<CreateOrderItemDto> Items,
    string? DiscountCode,
    string PaymentMethod,
    decimal AmountTendered,
    string? Notes);

public record RefundOrderRequest(string Reason);

public record OpenShiftDto(string RegisterId, string CashierId, decimal StartingFloat);

public record CloseShiftDto(decimal CountedCash, string? Notes);

public record AddCashDropDto(string Type, decimal Amount, string Reason);

public record ReceiptDto(
    string OrderNumber,
    string StoreName,
    string StoreAddress,
    string StorePhone,
    DateTimeOffset Timestamp,
    string CashierName,
    string? CustomerName,
    IReadOnlyList<OrderItem> Items,
    decimal Subtotal,
    decimal TaxTotal,
    decimal DiscountTotal,
    decimal GrandTotal,
    string PaymentMethod,
    decimal AmountTendered,
    decimal ChangeGiven,
    string Status);
