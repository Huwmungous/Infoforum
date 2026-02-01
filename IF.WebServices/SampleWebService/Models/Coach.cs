namespace SampleWebService.Models;

/// <summary>
/// Represents a coach from the rozebowl database.
/// </summary>
public class Coach
{
    public int Idx { get; set; }
    public string? Email { get; set; }
    public string? Name { get; set; }
}
