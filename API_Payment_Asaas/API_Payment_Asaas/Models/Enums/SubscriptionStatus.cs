using System.Text.Json.Serialization;

namespace API_Payment_Asaas.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubscriptionStatus
{
    UNKNOWN,
    CREATING,
    CREATION_FAILED,
    ACTIVE,
    INACTIVE,
    DELETED,
    EXPIRED
}