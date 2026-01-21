using Microsoft.AspNetCore.Mvc;
using SampleWebService.Models;
using SampleWebService.Repositories;

namespace SampleWebService.Controllers;

/// <summary>
/// Controller for Account operations.
/// Demonstrates transparent operation in both Direct and Relay modes.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly AccountRepository _repository;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        AccountRepository repository,
        ILogger<AccountController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Get all accounts.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Account>>> GetAll(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GET /api/account - Getting all accounts");
        var accounts = await _repository.GetAllAsync(cancellationToken);
        return Ok(accounts);
    }

    /// <summary>
    /// Get account by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Account>> GetById(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("GET /api/account/{AccountId}", id);
        var account = await _repository.GetByIdAsync(id, cancellationToken);

        if (account is null)
        {
            return NotFound(new { Message = $"Account {id} not found" });
        }

        return Ok(account);
    }

    /// <summary>
    /// Get account count.
    /// </summary>
    [HttpGet("count")]
    public async Task<ActionResult<int>> GetCount(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GET /api/account/count");
        var count = await _repository.GetCountAsync(cancellationToken);
        return Ok(new { Count = count });
    }

    /// <summary>
    /// Get active accounts only.
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<Account>>> GetActive(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GET /api/account/active");
        var accounts = await _repository.GetActiveAccountsAsync(cancellationToken);
        return Ok(accounts);
    }

    /// <summary>
    /// Create a new account.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Account>> Create(
        [FromBody] Account account,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("POST /api/account");

        var newId = await _repository.CreateAsync(account, cancellationToken);
        var created = await _repository.GetByIdAsync(newId, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = newId }, created);
    }

    /// <summary>
    /// Update an existing account.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] Account account,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("PUT /api/account/{AccountId}", id);

        if (id != account.AccountId)
        {
            return BadRequest(new { Message = "Account ID mismatch" });
        }

        var success = await _repository.UpdateAsync(account, cancellationToken);

        if (!success)
        {
            return NotFound(new { Message = $"Account {id} not found" });
        }

        return NoContent();
    }

    /// <summary>
    /// Delete an account.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("DELETE /api/account/{AccountId}", id);

        var success = await _repository.DeleteAsync(id, cancellationToken);

        if (!success)
        {
            return NotFound(new { Message = $"Account {id} not found" });
        }

        return NoContent();
    }

}

/// <summary>
/// Request model for balance transfers.
/// </summary>
public sealed class TransferRequest
{
    public int FromAccountId { get; set; }
    public int ToAccountId { get; set; }
    public decimal Amount { get; set; }
}
