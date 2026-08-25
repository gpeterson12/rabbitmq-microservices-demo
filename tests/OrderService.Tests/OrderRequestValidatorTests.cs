using OrderService.Models;
using OrderService.Validation;

namespace OrderService.Tests;

public class OrderRequestValidatorTests
{
    [Fact]
    public void Valid_request_produces_no_errors()
    {
        var request = new CreateOrderRequest { Sku = "SKU-WIDGET", Quantity = 1, CustomerEmail = "a@b.com" };

        Assert.Empty(OrderRequestValidator.Validate(request));
    }

    [Fact]
    public void Null_request_is_rejected()
    {
        Assert.NotEmpty(OrderRequestValidator.Validate(null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_or_blank_sku_is_rejected(string? sku)
    {
        var request = new CreateOrderRequest { Sku = sku!, Quantity = 1, CustomerEmail = "a@b.com" };

        Assert.Contains(OrderRequestValidator.Validate(request), e => e.Contains("sku"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_quantity_is_rejected(int quantity)
    {
        var request = new CreateOrderRequest { Sku = "SKU-WIDGET", Quantity = quantity, CustomerEmail = "a@b.com" };

        Assert.Contains(OrderRequestValidator.Validate(request), e => e.Contains("quantity"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing-at-sign.example.com")]
    [InlineData("@example.com")]
    [InlineData("two@@example.com")]
    [InlineData("spaces in@example.com")]
    public void Implausible_email_is_rejected(string? customerEmail)
    {
        var request = new CreateOrderRequest { Sku = "SKU-WIDGET", Quantity = 1, CustomerEmail = customerEmail! };

        Assert.Contains(OrderRequestValidator.Validate(request), e => e.Contains("customerEmail"));
    }

    [Theory]
    [InlineData("a@b.com")]
    [InlineData("first.last+tag@sub.domain.co")]
    public void Plausible_emails_are_accepted(string customerEmail)
    {
        var request = new CreateOrderRequest { Sku = "SKU-WIDGET", Quantity = 1, CustomerEmail = customerEmail };

        Assert.DoesNotContain(OrderRequestValidator.Validate(request), e => e.Contains("customerEmail"));
    }
}
