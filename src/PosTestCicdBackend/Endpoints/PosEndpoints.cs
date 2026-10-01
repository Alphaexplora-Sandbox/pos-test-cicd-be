using PosTestCicdBackend.Domain;

namespace PosTestCicdBackend.Endpoints;

public static class PosEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", (IServiceStatus status) =>
            Results.Ok(new HealthResponse(status.CurrentStatus(), ServiceInfo.Name)))
           .Produces<HealthResponse>(StatusCodes.Status200OK);

        app.MapGet("/", () => Results.Ok(new HealthResponse("ready", ServiceInfo.Name)))
           .Produces<HealthResponse>(StatusCodes.Status200OK);

        return app;
    }

    public static WebApplication MapPosEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1");

        // ---------------- AUTH ----------------
        api.MapPost("/auth/login", (LoginRequest request, IPosStore store) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new { message = "Username and password are required." });
            }

            var user = store.Authenticate(request.Username, request.Password);
            return user is not null
                ? Results.Ok(user)
                : Results.Json(new { message = "Invalid credentials. Use demo: admin / admin" }, statusCode: StatusCodes.Status401Unauthorized);
        }).Produces<UserProfile>(StatusCodes.Status200OK);

        api.MapGet("/auth/me", (HttpRequest request, IPosStore store) =>
        {
            var authHeader = request.Headers.Authorization.ToString();
            var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader["Bearer ".Length..].Trim()
                : null;

            if (string.IsNullOrEmpty(token))
            {
                return Results.Unauthorized();
            }

            var user = store.GetUserByToken(token);
            return user is not null ? Results.Ok(user) : Results.Unauthorized();
        }).Produces<UserProfile>(StatusCodes.Status200OK);

        api.MapGet("/auth/users", (IPosStore store) =>
            Results.Ok(store.GetUsers()))
           .Produces<IReadOnlyList<UserProfile>>(StatusCodes.Status200OK);

        // ---------------- CATEGORIES ----------------
        api.MapGet("/categories", (IPosStore store) =>
            Results.Ok(store.GetCategories()))
           .Produces<IReadOnlyList<Category>>(StatusCodes.Status200OK);

        api.MapGet("/categories/{id}", (string id, IPosStore store) =>
        {
            var category = store.GetCategoryById(id);
            return category is not null ? Results.Ok(category) : Results.NotFound(new { message = $"Category {id} not found." });
        }).Produces<Category>(StatusCodes.Status200OK);

        api.MapPost("/categories", (CreateCategoryDto dto, IPosStore store) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return Results.BadRequest(new { message = "Category name is required." });
            }

            var category = new Category(
                $"cat-{Guid.NewGuid():N}",
                dto.Name.Trim(),
                dto.Description ?? "",
                string.IsNullOrWhiteSpace(dto.Color) ? "#4682B4" : dto.Color,
                string.IsNullOrWhiteSpace(dto.Icon) ? "tag" : dto.Icon);

            var created = store.CreateCategory(category);
            return Results.Created($"/api/v1/categories/{created.Id}", created);
        }).Produces<Category>(StatusCodes.Status201Created);

        // ---------------- PRODUCTS ----------------
        api.MapGet("/products", (string? categoryId, string? search, bool? lowStockOnly, IPosStore store) =>
            Results.Ok(store.GetProducts(categoryId, search, lowStockOnly ?? false)))
           .Produces<IReadOnlyList<Product>>(StatusCodes.Status200OK);

        api.MapGet("/products/{id}", (string id, IPosStore store) =>
        {
            var product = store.GetProductById(id);
            return product is not null ? Results.Ok(product) : Results.NotFound(new { message = $"Product {id} not found." });
        }).Produces<Product>(StatusCodes.Status200OK);

        api.MapGet("/products/barcode/{barcode}", (string barcode, IPosStore store) =>
        {
            var product = store.GetProductByBarcode(barcode);
            return product is not null ? Results.Ok(product) : Results.NotFound(new { message = $"Barcode {barcode} not recognized." });
        }).Produces<Product>(StatusCodes.Status200OK);

        api.MapPost("/products", (CreateProductDto dto, IPosStore store) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Sku))
            {
                return Results.BadRequest(new { message = "Name and SKU are required." });
            }
            if (dto.Price < 0)
            {
                return Results.BadRequest(new { message = "Price cannot be negative." });
            }

            var category = store.GetCategoryById(dto.CategoryId);
            var categoryName = category?.Name ?? "General";
            var now = DateTimeOffset.UtcNow;

            var product = new Product(
                $"prod-{Guid.NewGuid():N}",
                dto.Sku.Trim().ToUpperInvariant(),
                string.IsNullOrWhiteSpace(dto.Barcode) ? $"{Random.Shared.Next(10000000, 99999999)}{Random.Shared.Next(1000, 9999)}" : dto.Barcode.Trim(),
                dto.Name.Trim(),
                dto.Description ?? "",
                dto.CategoryId,
                categoryName,
                dto.Price,
                dto.Cost,
                dto.TaxRate,
                dto.StockQuantity,
                dto.LowStockThreshold,
                true,
                now,
                now);

            var created = store.CreateProduct(product);
            return Results.Created($"/api/v1/products/{created.Id}", created);
        }).Produces<Product>(StatusCodes.Status201Created);

        api.MapPut("/products/{id}", (string id, UpdateProductDto dto, IPosStore store) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return Results.BadRequest(new { message = "Product name is required." });
            }

            var updated = store.UpdateProduct(id, dto);
            return updated is not null ? Results.Ok(updated) : Results.NotFound(new { message = $"Product {id} not found." });
        }).Produces<Product>(StatusCodes.Status200OK);

        api.MapDelete("/products/{id}", (string id, IPosStore store) =>
        {
            var deleted = store.DeleteProduct(id);
            return deleted ? Results.Ok(new { success = true, message = $"Product {id} deactivated." }) : Results.NotFound();
        });

        api.MapPost("/products/{id}/adjust-stock", (string id, StockAdjustmentDto dto, IPosStore store) =>
        {
            var adjusted = store.AdjustStock(id, dto.QuantityChange, dto.Reason);
            return adjusted is not null ? Results.Ok(adjusted) : Results.NotFound(new { message = $"Product {id} not found." });
        }).Produces<Product>(StatusCodes.Status200OK);

        // ---------------- CUSTOMERS ----------------
        api.MapGet("/customers", (string? search, IPosStore store) =>
            Results.Ok(store.GetCustomers(search)))
           .Produces<IReadOnlyList<Customer>>(StatusCodes.Status200OK);

        api.MapGet("/customers/{id}", (string id, IPosStore store) =>
        {
            var customer = store.GetCustomerById(id);
            return customer is not null ? Results.Ok(customer) : Results.NotFound(new { message = $"Customer {id} not found." });
        }).Produces<Customer>(StatusCodes.Status200OK);

        api.MapPost("/customers", (CreateCustomerDto dto, IPosStore store) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Email))
            {
                return Results.BadRequest(new { message = "Name and Email are required." });
            }

            var customer = new Customer(
                $"cust-{Guid.NewGuid():N}",
                dto.Name.Trim(),
                dto.Email.Trim().ToLowerInvariant(),
                dto.Phone?.Trim() ?? "",
                0,
                "Bronze",
                0m,
                DateTimeOffset.UtcNow);

            var created = store.CreateCustomer(customer);
            return Results.Created($"/api/v1/customers/{created.Id}", created);
        }).Produces<Customer>(StatusCodes.Status201Created);

        api.MapPost("/customers/{id}/loyalty-points", (string id, int points, IPosStore store) =>
        {
            var updated = store.AddLoyaltyPoints(id, points);
            return updated is not null ? Results.Ok(updated) : Results.NotFound(new { message = $"Customer {id} not found." });
        }).Produces<Customer>(StatusCodes.Status200OK);

        // ---------------- DISCOUNTS ----------------
        api.MapGet("/discounts", (IPosStore store) =>
            Results.Ok(store.GetDiscounts()))
           .Produces<IReadOnlyList<Discount>>(StatusCodes.Status200OK);

        api.MapPost("/discounts", (CreateDiscountDto dto, IPosStore store) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
            {
                return Results.BadRequest(new { message = "Discount code is required." });
            }

            var discount = new Discount(
                $"disc-{Guid.NewGuid():N}",
                dto.Code.Trim().ToUpperInvariant(),
                dto.Description ?? "",
                dto.Type == "FixedAmount" ? "FixedAmount" : "Percentage",
                dto.Value,
                dto.MinOrderAmount,
                true);

            var created = store.CreateDiscount(discount);
            return Results.Created($"/api/v1/discounts/{created.Id}", created);
        }).Produces<Discount>(StatusCodes.Status201Created);

        api.MapPost("/discounts/validate", (ValidateDiscountRequest req, IPosStore store) =>
        {
            if (string.IsNullOrWhiteSpace(req.Code))
            {
                return Results.Ok(new ValidateDiscountResponse(false, 0m, "No code provided."));
            }

            var discount = store.GetDiscountByCode(req.Code);
            if (discount is null)
            {
                return Results.Ok(new ValidateDiscountResponse(false, 0m, "Invalid or expired promo code."));
            }

            if (req.OrderSubtotal < discount.MinOrderAmount)
            {
                return Results.Ok(new ValidateDiscountResponse(false, 0m, $"Minimum order amount of ${discount.MinOrderAmount:F2} required."));
            }

            var amount = discount.Type == "Percentage"
                ? Math.Round(req.OrderSubtotal * (discount.Value / 100m), 2)
                : Math.Min(req.OrderSubtotal, discount.Value);

            return Results.Ok(new ValidateDiscountResponse(true, amount, $"Applied {discount.Description} (-${amount:F2})"));
        }).Produces<ValidateDiscountResponse>(StatusCodes.Status200OK);

        // ---------------- ORDERS ----------------
        api.MapGet("/orders", (string? status, string? search, DateTimeOffset? from, DateTimeOffset? to, IPosStore store) =>
            Results.Ok(store.GetOrders(status, search, from, to)))
           .Produces<IReadOnlyList<Order>>(StatusCodes.Status200OK);

        api.MapGet("/orders/{id}", (string id, IPosStore store) =>
        {
            var order = store.GetOrderById(id);
            return order is not null ? Results.Ok(order) : Results.NotFound(new { message = $"Order {id} not found." });
        }).Produces<Order>(StatusCodes.Status200OK);

        api.MapGet("/orders/{id}/receipt", (string id, IPosStore store) =>
        {
            var receipt = store.GetReceipt(id);
            return receipt is not null ? Results.Ok(receipt) : Results.NotFound(new { message = $"Order {id} receipt not available." });
        }).Produces<ReceiptDto>(StatusCodes.Status200OK);

        api.MapPost("/orders", (CreateOrderDto dto, IPosStore store) =>
        {
            if (dto.Items is null || dto.Items.Count == 0)
            {
                return Results.BadRequest(new { message = "Order must contain at least one line item." });
            }

            var randSuffix = Random.Shared.Next(1000, 9999);
            var orderNumber = $"POS-{DateTime.UtcNow:yyyyMMdd}-{randSuffix}";

            try
            {
                var order = store.CreateOrder(dto, orderNumber);
                return Results.Created($"/api/v1/orders/{order.Id}", order);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).Produces<Order>(StatusCodes.Status201Created);

        api.MapPost("/orders/{id}/refund", (string id, RefundOrderRequest req, IPosStore store) =>
        {
            var refunded = store.RefundOrder(id, req.Reason ?? "Customer requested refund");
            return refunded is not null ? Results.Ok(refunded) : Results.NotFound(new { message = $"Order {id} could not be refunded." });
        }).Produces<Order>(StatusCodes.Status200OK);

        api.MapPost("/orders/{id}/void", (string id, RefundOrderRequest req, IPosStore store) =>
        {
            var voided = store.VoidOrder(id, req.Reason ?? "Voided by manager");
            return voided is not null ? Results.Ok(voided) : Results.NotFound(new { message = $"Order {id} could not be voided." });
        }).Produces<Order>(StatusCodes.Status200OK);

        // ---------------- SHIFTS ----------------
        api.MapGet("/shifts", (string? registerId, IPosStore store) =>
            Results.Ok(store.GetShifts(registerId)))
           .Produces<IReadOnlyList<RegisterShift>>(StatusCodes.Status200OK);

        api.MapGet("/shifts/current", (string? registerId, IPosStore store) =>
        {
            var reg = registerId ?? "REG-01";
            var shift = store.GetCurrentShift(reg);
            return shift is not null ? Results.Ok(shift) : Results.NotFound(new { message = $"No active open shift for register {reg}." });
        }).Produces<RegisterShift>(StatusCodes.Status200OK);

        api.MapPost("/shifts/open", (OpenShiftDto dto, IPosStore store) =>
        {
            var existing = store.GetCurrentShift(dto.RegisterId);
            if (existing is not null)
            {
                return Results.BadRequest(new { message = $"Register {dto.RegisterId} already has an open shift (Shift #{existing.Id})." });
            }

            var shift = store.OpenShift(dto);
            return Results.Created($"/api/v1/shifts/{shift.Id}", shift);
        }).Produces<RegisterShift>(StatusCodes.Status201Created);

        api.MapPost("/shifts/{id}/close", (string id, CloseShiftDto dto, IPosStore store) =>
        {
            var closed = store.CloseShift(id, dto.CountedCash, dto.Notes);
            return closed is not null ? Results.Ok(closed) : Results.NotFound(new { message = $"Shift {id} not found." });
        }).Produces<RegisterShift>(StatusCodes.Status200OK);

        api.MapPost("/shifts/{id}/cash-drop", (string id, AddCashDropDto dto, IPosStore store) =>
        {
            var drop = store.AddCashDrop(id, dto);
            return drop is not null ? Results.Ok(drop) : Results.NotFound(new { message = $"Shift {id} not found." });
        }).Produces<CashDrop>(StatusCodes.Status200OK);

        // ---------------- ANALYTICS ----------------
        api.MapGet("/analytics/overview", (IPosStore store) =>
            Results.Ok(store.GetAnalytics()))
           .Produces<PosAnalytics>(StatusCodes.Status200OK);

        api.MapGet("/analytics/top-products", (int? limit, IPosStore store) =>
            Results.Ok(store.GetTopProducts(limit ?? 5)))
           .Produces<IReadOnlyList<TopProductDto>>(StatusCodes.Status200OK);

        api.MapGet("/analytics/category-sales", (IPosStore store) =>
            Results.Ok(store.GetCategorySales()))
           .Produces<IReadOnlyList<CategorySalesDto>>(StatusCodes.Status200OK);

        return app;
    }
}
