namespace EventTicketing.Models.Options;

public class StripeOptions
{
    public string? SecretKey { get; set; }
    public string? WebhookSecret { get; set; }
}