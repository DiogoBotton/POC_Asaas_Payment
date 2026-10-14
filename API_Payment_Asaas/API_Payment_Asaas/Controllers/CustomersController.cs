using API_Payment_Asaas.Services;
using Microsoft.AspNetCore.Mvc;

namespace API_Payment_Asaas.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(ICustomerService customerService) : ControllerBase
{
    /// <summary>Lists locally stored customers and their registered addresses.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CustomerResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomerResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await customerService.GetAllAsync(cancellationToken));
    }

    /// <summary>Finds a locally stored customer by CPF or CNPJ.</summary>
    [HttpGet("lookup")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> FindByCpfCnpj(
        [FromQuery] string cpfCnpj,
        CancellationToken cancellationToken)
    {
        var customer = await customerService.FindByCpfCnpjAsync(cpfCnpj, cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }
}