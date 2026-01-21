namespace SampleWebService.Models;

/// <summary>
/// Account entity matching the ACCOUNT table.
/// </summary>
public sealed class Account
{
    public int AccountId { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public string? AccountType { get; set; }
    public decimal? Balance { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool? IsActive { get; set; }
}
