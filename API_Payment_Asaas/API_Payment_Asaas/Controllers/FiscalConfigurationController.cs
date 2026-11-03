using API_Payment_Asaas.DTOs.Fiscal;
using API_Payment_Asaas.Services;
using Microsoft.AspNetCore.Mvc;

namespace API_Payment_Asaas.Controllers;

[ApiController]
[Route("api/fiscal")]
public sealed class FiscalConfigurationController(IFiscalConfigurationService fiscalConfigurationService) : ControllerBase
{
    /// <summary>Returns the application-wide NFS-e configuration.</summary>
    [HttpGet("configuration")]
    [ProducesResponseType<FiscalConfigurationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FiscalConfigurationResponse>> GetConfiguration(CancellationToken cancellationToken)
    {
        return Ok(await fiscalConfigurationService.GetConfigurationAsync(cancellationToken));
    }

    /// <summary>Saves the application-wide NFS-e configuration using a service available in the Asaas catalog.</summary>
    [HttpPut("configuration")]
    [ProducesResponseType<FiscalConfigurationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FiscalConfigurationResponse>> SaveConfiguration(
        UpdateFiscalConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await fiscalConfigurationService.SaveConfigurationAsync(request, cancellationToken));
    }

    /// <summary>Searches the paginated municipal service catalog provided by Asaas.</summary>
    [HttpGet("municipal-services")]
    [ProducesResponseType<MunicipalServicesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MunicipalServicesResponse>> GetMunicipalServices(
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20,
        [FromQuery] string? description = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await fiscalConfigurationService.GetMunicipalServicesAsync(offset, limit, description, cancellationToken));
    }
}