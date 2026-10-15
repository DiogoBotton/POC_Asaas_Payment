using API_Payment_Asaas.DTOs.Subscriptions;

namespace API_Payment_Asaas.Services;

public interface ISubscriptionService
{
    Task<IReadOnlyList<SubscriptionResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<SubscriptionResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<SubscriptionResponse> CreateAsync(CreateSubscriptionRequest request, CancellationToken cancellationToken);
    Task<SubscriptionResponse> UpdateValueAsync(Guid id, UpdateSubscriptionValueRequest request, CancellationToken cancellationToken);
    Task CancelAsync(Guid id, CancellationToken cancellationToken);
}