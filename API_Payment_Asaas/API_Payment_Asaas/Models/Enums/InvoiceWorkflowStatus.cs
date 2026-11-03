using System.Text.Json.Serialization;

namespace API_Payment_Asaas.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InvoiceWorkflowStatus
{
    CONFIGURATION_REQUIRED,
    SCHEDULING,
    SCHEDULE_FAILED
}