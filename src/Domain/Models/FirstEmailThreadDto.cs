using System.Security.Cryptography.X509Certificates;

namespace Merrsoft.MerrMail.Domain.Models;

public record FirstEmailOnThreadDto(string Sender, string Subject, string Body, string ThreadId);