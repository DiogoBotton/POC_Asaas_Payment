using System.Text.Json.Serialization;

namespace API_Payment_Asaas.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InvoiceStatus
{
    UNKNOWN,
    CONFIGURATION_REQUIRED,
    SCHEDULING,
    SCHEDULE_FAILED,
    SCHEDULED,
    AUTHORIZED,
    ERROR
}