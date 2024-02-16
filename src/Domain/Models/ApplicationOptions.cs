using System.ComponentModel.DataAnnotations;

namespace Merrsoft.MerrMail.Domain.Models;

public class ApplicationOptions
{
    [Required]
    public required string OAuthClientCredentialsPath { get; set; }

    [Required]
    public required string AccessTokenPath { get; set; }

    [Required]
    public required string DatabaseConnection { get; set; }

    [Required]
    public required string HostAddress { get; set; }
}