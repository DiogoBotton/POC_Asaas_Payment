using System.Text.Json.Serialization;

namespace API_Payment_Asaas.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentStatus
{
    UNKNOWN,
    CREATING,
    CREATION_FAILED,
    PENDING,
    OVERDUE,
    DELETED,
    CONFIRMED,
    RECEIVED,
    RECEIVED_IN_CASH,
    REFUNDED,
    REFUND_REQUESTED,
    CHARGEBACK,
    AWAITING_RISK_ANALYSIS,
    PROCESSING
}