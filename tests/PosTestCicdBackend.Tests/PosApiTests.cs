using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PosTestCicdBackend.Domain;
using Xunit;

namespace PosTestCicdBackend.Tests;

public class PosApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public PosApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Auth_Login_SuccessAndFailure()
    {
        // Success
        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "admin"));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var user = await loginRes.Content.ReadFromJsonAsync<UserProfile>();
        Assert.NotNull(user);
        Assert.Equal("admin", user!.Username);

        // Failure
        var failRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "wrong"));
        Assert.Equal(HttpStatusCode.Unauthorized, failRes.StatusCode);

        // Empty
        var emptyRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("", ""));
        Assert.Equal(HttpStatusCode.BadRequest, emptyRes.StatusCode);
    }

    [Fact]
    public async Task Auth_Me_WithAndWithoutToken()
    {
        // Unauthenticated
        var unauthRes = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthRes.StatusCode);

        // Authenticated
        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "admin"));
        var user = await loginRes.Content.ReadFromJsonAsync<UserProfile>();
        Assert.NotNull(user);

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user!.Token);
        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Categories_FullFlow()
    {
        var listRes = await _client.GetAsync("/api/v1/categories");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var categories = await listRes.Content.ReadFromJsonAsync<List<Category>>();
        Assert.NotNull(categories);
        Assert.NotEmpty(categories!);

        var first = categories![0];
        var singleRes = await _client.GetAsync($"/api/v1/categories/{first.Id}");
        Assert.Equal(HttpStatusCode.OK, singleRes.StatusCode);

        // Create new category
        var createRes = await _client.PostAsJsonAsync("/api/v1/categories", new CreateCategoryDto("Cold Drinks", "Iced beverages", "#00CED1", "glass"));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<Category>();
        Assert.NotNull(created);
        Assert.Equal("Cold Drinks", created!.Name);
    }

    [Fact]
    public async Task Products_FullFlow()
    {
        var listRes = await _client.GetAsync("/api/v1/products");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var products = await listRes.Content.ReadFromJsonAsync<List<Product>>();
        Assert.NotNull(products);
        Assert.NotEmpty(products!);

        var p = products![0];
        var getRes = await _client.GetAsync($"/api/v1/products/{p.Id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var barcodeRes = await _client.GetAsync($"/api/v1/products/barcode/{p.Barcode}");
        Assert.Equal(HttpStatusCode.OK, barcodeRes.StatusCode);

        // Create product
        var createDto = new CreateProductDto(
            $"TEST-{Guid.NewGuid():N}"[..10],
            $"99{Random.Shared.Next(10000000, 99999999)}",
            "API Test Drink",
            "Test drink",
            p.CategoryId,
            6.50m,
            1.50m,
            0.08m,
            50,
            10);

        var createRes = await _client.PostAsJsonAsync("/api/v1/products", createDto);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<Product>();
        Assert.NotNull(created);

        // Update product
        var updateDto = new UpdateProductDto(
            "API Test Drink Special",
            "Updated test drink",
            p.CategoryId,
            7.00m,
            1.60m,
            0.08m,
            60,
            15,
            true);
        var updateRes = await _client.PutAsJsonAsync($"/api/v1/products/{created!.Id}", updateDto);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        // Adjust stock
        var adjustRes = await _client.PostAsJsonAsync($"/api/v1/products/{created.Id}/adjust-stock", new StockAdjustmentDto(10, "Inventory restock"));
        Assert.Equal(HttpStatusCode.OK, adjustRes.StatusCode);

        // Delete product
        var delRes = await _client.DeleteAsync($"/api/v1/products/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, delRes.StatusCode);
    }

    [Fact]
    public async Task Customers_FullFlow()
    {
        var listRes = await _client.GetAsync("/api/v1/customers");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var customers = await listRes.Content.ReadFromJsonAsync<List<Customer>>();
        Assert.NotNull(customers);
        Assert.NotEmpty(customers!);

        var first = customers![0];
        var singleRes = await _client.GetAsync($"/api/v1/customers/{first.Id}");
        Assert.Equal(HttpStatusCode.OK, singleRes.StatusCode);

        // Create customer
        var newCust = new CreateCustomerDto("Jane Doe", $"jane.{Guid.NewGuid():N}@example.com", "555-4321");
        var createRes = await _client.PostAsJsonAsync("/api/v1/customers", newCust);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<Customer>();
        Assert.NotNull(created);

        // Loyalty points
        var ptsRes = await _client.PostAsJsonAsync($"/api/v1/customers/{created!.Id}/loyalty-points?points=100", 100);
        Assert.Equal(HttpStatusCode.OK, ptsRes.StatusCode);
    }

    [Fact]
    public async Task Discounts_FullFlow()
    {
        var listRes = await _client.GetAsync("/api/v1/discounts");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);

        var newDisc = new CreateDiscountDto($"TEST{Random.Shared.Next(100, 999)}", "Test promo", "Percentage", 15m, 10m);
        var createRes = await _client.PostAsJsonAsync("/api/v1/discounts", newDisc);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        // Validate discount
        var valRes = await _client.PostAsJsonAsync("/api/v1/discounts/validate", new ValidateDiscountRequest(newDisc.Code, 50m));
        Assert.Equal(HttpStatusCode.OK, valRes.StatusCode);
        var valBody = await valRes.Content.ReadFromJsonAsync<ValidateDiscountResponse>();
        Assert.NotNull(valBody);
        Assert.True(valBody!.IsValid);
        Assert.Equal(7.50m, valBody.DiscountAmount);
    }

    [Fact]
    public async Task Orders_FullFlow()
    {
        var prodList = await _client.GetFromJsonAsync<List<Product>>("/api/v1/products");
        Assert.NotNull(prodList);
        var prod = prodList![0];

        var orderDto = new CreateOrderDto(
            "REG-01",
            "usr-3",
            "cust-1",
            new List<CreateOrderItemDto> { new(prod.Id, 2) },
            "WELCOME10",
            "Cash",
            50m,
            "Window table");

        var createRes = await _client.PostAsJsonAsync("/api/v1/orders", orderDto);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var order = await createRes.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(order);
        Assert.Equal("Completed", order!.Status);

        // Get single order
        var getRes = await _client.GetAsync($"/api/v1/orders/{order.Id}");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        // Receipt
        var receiptRes = await _client.GetAsync($"/api/v1/orders/{order.Id}/receipt");
        Assert.Equal(HttpStatusCode.OK, receiptRes.StatusCode);
        var receipt = await receiptRes.Content.ReadFromJsonAsync<ReceiptDto>();
        Assert.NotNull(receipt);
        Assert.Equal(order.OrderNumber, receipt!.OrderNumber);

        // Refund
        var refundRes = await _client.PostAsJsonAsync($"/api/v1/orders/{order.Id}/refund", new RefundOrderRequest("Customer return"));
        Assert.Equal(HttpStatusCode.OK, refundRes.StatusCode);

        // Create second order to void
        var orderDto2 = new CreateOrderDto(
            "REG-01",
            "usr-3",
            null,
            new List<CreateOrderItemDto> { new(prod.Id, 1) },
            null,
            "Card",
            prod.Price * 1.08m,
            null);
        var createRes2 = await _client.PostAsJsonAsync("/api/v1/orders", orderDto2);
        var order2 = await createRes2.Content.ReadFromJsonAsync<Order>();

        var voidRes = await _client.PostAsJsonAsync($"/api/v1/orders/{order2!.Id}/void", new RefundOrderRequest("Void error"));
        Assert.Equal(HttpStatusCode.OK, voidRes.StatusCode);
    }

    [Fact]
    public async Task Shifts_FullFlow()
    {
        var testReg = $"R-{Guid.NewGuid():N}"[..6];
        var openRes = await _client.PostAsJsonAsync("/api/v1/shifts/open", new OpenShiftDto(testReg, "usr-3", 100m));
        Assert.Equal(HttpStatusCode.Created, openRes.StatusCode);
        var shift = await openRes.Content.ReadFromJsonAsync<RegisterShift>();
        Assert.NotNull(shift);

        var curRes = await _client.GetAsync($"/api/v1/shifts/current?registerId={testReg}");
        Assert.Equal(HttpStatusCode.OK, curRes.StatusCode);

        // Cash drop
        var dropRes = await _client.PostAsJsonAsync($"/api/v1/shifts/{shift!.Id}/cash-drop", new AddCashDropDto("Drop", 50m, "Midday drop to safe"));
        Assert.Equal(HttpStatusCode.OK, dropRes.StatusCode);

        // Close shift
        var closeRes = await _client.PostAsJsonAsync($"/api/v1/shifts/{shift.Id}/close", new CloseShiftDto(50m, "Shift balanced"));
        Assert.Equal(HttpStatusCode.OK, closeRes.StatusCode);
    }

    [Fact]
    public async Task Analytics_ReturnsValidData()
    {
        var overviewRes = await _client.GetAsync("/api/v1/analytics/overview");
        Assert.Equal(HttpStatusCode.OK, overviewRes.StatusCode);
        var overview = await overviewRes.Content.ReadFromJsonAsync<PosAnalytics>();
        Assert.NotNull(overview);
        Assert.True(overview!.TotalProducts > 0);

        var topProdRes = await _client.GetAsync("/api/v1/analytics/top-products");
        Assert.Equal(HttpStatusCode.OK, topProdRes.StatusCode);

        var catSalesRes = await _client.GetAsync("/api/v1/analytics/category-sales");
        Assert.Equal(HttpStatusCode.OK, catSalesRes.StatusCode);
    }
}
