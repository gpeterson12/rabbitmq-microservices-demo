using System.Net.Mail;
using OrderService.Models;

namespace OrderService.Validation;

public static class OrderRequestValidator
{
    private const int MaxSkuLength = 128;
    private const int MaxEmailLength = 254;

    public static IReadOnlyList<string> Validate(CreateOrderRequest? request)
    {
        if (request is null)
        {
            return ["request body is required"];
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            errors.Add("sku is required");
        }
        else if (request.Sku.Length > MaxSkuLength)
        {
            errors.Add($"sku must be at most {MaxSkuLength} characters");
        }

        if (request.Quantity <= 0)
        {
            errors.Add("quantity must be greater than zero");
        }

        if (string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
            errors.Add("customerEmail is required");
        }
        else if (request.CustomerEmail.Length > MaxEmailLength ||
                 request.CustomerEmail.Any(char.IsWhiteSpace) ||
                 !MailAddress.TryCreate(request.CustomerEmail, out _))
        {
            errors.Add("customerEmail is not a plausible email address");
        }

        return errors;
    }
}
