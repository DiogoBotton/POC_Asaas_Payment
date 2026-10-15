namespace API_Payment_Asaas.DTOs.Payments;

public sealed record CreditCardDeviceInfo
{
    public int ColorDepth { get; init; }
    public int ScreenHeight { get; init; }
    public int ScreenWidth { get; init; }
    public int TimeZoneOffset { get; init; }
    public required string Language { get; init; }
    public required string UserAgent { get; init; }
}