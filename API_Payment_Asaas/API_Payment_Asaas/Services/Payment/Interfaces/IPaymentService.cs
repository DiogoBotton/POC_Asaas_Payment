using API_Payment_Asaas.Models;
using API_Payment_Asaas.DTOs.Payments;

namespace API_Payment_Asaas.Services;

public interface IPaymentService
{
    Task<PaymentResponse> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<PaymentResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<PaymentResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}