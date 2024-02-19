using Merrsoft.MerrMail.Domain.Models;

namespace Merrsoft.MerrMail.Application.Contracts;

public interface IEmailApiService
{
    List<Email> GetUnreadEmails();
    Task Reply(string to, string body, string messageId);
    void MarkAsRead(string messageId);
    List<FirstEmailOnThreadDto> GetFirstEmailOnThreads();
}