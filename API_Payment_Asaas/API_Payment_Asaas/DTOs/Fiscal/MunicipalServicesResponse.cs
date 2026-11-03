namespace API_Payment_Asaas.DTOs.Fiscal;

public sealed record MunicipalServiceResponse(string Id, string Description, decimal? IssTax);

public sealed record MunicipalServicesResponse(
    int Offset,
    int Limit,
    int TotalCount,
    bool HasMore,
    IReadOnlyList<MunicipalServiceResponse> Data);