using System.ComponentModel.DataAnnotations;

public interface IKeycloakClient
{
    Task<ValidationResult> ValidateTokenAsync(string token);
}