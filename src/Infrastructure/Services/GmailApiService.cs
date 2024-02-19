using System.Text;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Merrsoft.MerrMail.Application.Contracts;
using Merrsoft.MerrMail.Domain.Common;
using Merrsoft.MerrMail.Domain.Models;
using Merrsoft.MerrMail.Domain.Options;
using Merrsoft.MerrMail.Infrastructure.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Merrsoft.MerrMail.Infrastructure.Services;

// TODO: Setup GmailService on start
public class GmailApiService(
    ILogger<GmailApiService> logger,
    IOptions<EmailApiOptions> emailApiOptions)
    : IEmailApiService
{
    private readonly EmailApiOptions _emailApiOptions = emailApiOptions.Value;
    private GmailService? _gmailService;
    private bool _hasCreatedLabels = false;

    private async void InitializeAsync()
    {
        _gmailService ??= await GmailApiHelper.GetGmailService(
            _emailApiOptions.OAuthClientCredentialsFilePath,
            _emailApiOptions.AccessTokenDirectoryPath);

        if (_hasCreatedLabels) return;

        CreateLabel("MerrMail: High Priority"); // Red with white text
        CreateLabel("MerrMail: Low Priority"); // Green with black text
        _hasCreatedLabels = true;
    }

    private void CreateLabel(string name)
    {
        var labelsRequest = _gmailService?.Users.Labels.List(_emailApiOptions.HostAddress);
        var labelsResponse = labelsRequest?.Execute();

        if (labelsResponse?.Labels?.Any(label => label.Name == name) is true) return;

        string backgroundColor, textColor;

        switch (name)
        {
            case "MerrMail: High Priority":
                backgroundColor = "#fb4c2f"; // Red
                textColor = "#ffffff"; // White
                break;
            case "MerrMail: Low Priority":
                backgroundColor = "#16a766"; // Green
                textColor = "#ffffff"; // White
                break;
            default:
                backgroundColor = "#000000"; // Black (default)
                textColor = "#ffffff"; // White (default)
                break;
        }

        var label = new Label
        {
            Name = name,
            Color = new LabelColor
            {
                BackgroundColor = backgroundColor,
                TextColor = textColor
            }
        };

        var createLabelRequest = _gmailService?.Users.Labels.Create(label, _emailApiOptions.HostAddress);
        var createdLabel = createLabelRequest?.Execute();

        if (createdLabel != null)
            logger.LogInformation(
                "Label created: {labelName}, Label ID: {labelId}, BackgroundColor: {backgroundColor}, TextColor: {textColor}",
                createdLabel.Name, createdLabel.Id, backgroundColor, textColor);
    }

    public FirstEmailOnThreadDto GetFirstEmailOnThread(string hostAddress)
    {
        var request = _gmailService!.Users.Threads.List(_emailApiOptions.HostAddress);
        var response = request.Execute();

        throw new NotImplementedException();
    }

    public List<FirstEmailOnThreadDto> GetFirstEmailOnThreads()
    {
        InitializeAsync();

        var emails = new List<FirstEmailOnThreadDto>();

        var threadsRequest = _gmailService!.Users.Threads.List(_emailApiOptions.HostAddress);
        var threadsResponse = threadsRequest.Execute();

        foreach (var thread in threadsResponse.Threads)
        {
            var threadDetailsResponse =
                _gmailService!.Users.Threads.Get(_emailApiOptions.HostAddress, thread.Id).Execute();

            if (threadDetailsResponse?.Messages?.Any() != true)
            {
                logger.LogWarning("Thread ID {threadId} has no messages or failed to retrieve details.", thread.Id);
                continue;
            }

            var firstMessage = threadDetailsResponse.Messages.First();
            var firstEmail = _gmailService!.Users.Messages.Get(_emailApiOptions.HostAddress, firstMessage.Id).Execute();

            if (firstEmail == null)
            {
                logger.LogWarning("Failed to retrieve email for thread ID {threadId}. Message is null.", thread.Id);
                continue;
            }

            var from = firstEmail.Payload.Headers?.FirstOrDefault(h => h.Name == "From")?.Value ?? "Unknown Sender";
            var subject = firstEmail.Payload.Headers?.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "No Subject";
            var body = firstEmail.Snippet;

            var email = new FirstEmailOnThreadDto(from, subject, body, thread.Id);
            logger.LogInformation("Email found: {sender} | {body}", email.Sender, email.Body);
            emails.Add(email);
        }

        return emails;
    }

    public List<Email> GetUnreadEmails()
    {
        InitializeAsync();

        var emails = new List<Email>();

        var listRequest = _gmailService!.Users.Messages.List(_emailApiOptions.HostAddress);
        listRequest.LabelIds = "INBOX";
        listRequest.IncludeSpamTrash = false;
        listRequest.Q = "is:unread";

        var listResponse = listRequest.Execute();

        if (listResponse.Messages is null)
            return [];

        foreach (var message in listResponse.Messages)
        {
            var messageContentRequest =
                _gmailService.Users.Messages.Get(_emailApiOptions.HostAddress, message.Id);
            var messageContent = messageContentRequest.Execute();

            if (messageContent is null) continue;

            // TODO: Remove unnecessary variables
            // TODO: Remove unnecessary Email properties
            var from = string.Empty;
            var to = string.Empty;
            var body = string.Empty;
            var subject = string.Empty;
            var date = string.Empty;
            var mailDateTime = DateTime.Now;
            var attachments = new List<string>();
            var id = message.Id;

            foreach (var messageParts in messageContent.Payload.Headers)
            {
                switch (messageParts.Name)
                {
                    case "From":
                        from = messageParts.Value;
                        break;
                    case "Date":
                        date = messageParts.Value;
                        break;
                    case "Subject":
                        subject = messageParts.Value;
                        break;
                }
            }

            if (messageContent.Payload.Parts is not null && messageContent.Payload.Parts.Count > 0)
            {
                var firstPart = messageContent.Payload.Parts[0];

                if (firstPart.Body?.Data != null)
                {
                    var data = firstPart.Body.Data;
                    body = data.ToDecodedString();
                }
            }

            // TODO: Decode the body
            var email = new Email(from, to, body, mailDateTime, attachments, id);
            emails.Add(email);

            logger.LogInformation("Email found, (Message Id: {emailId})", email.MessageId);
        }

        return emails;
    }

    public async Task Reply(string to, string body, string messageId)
    {
        InitializeAsync();

        var originalMessage = _gmailService!.Users.Messages.Get(_emailApiOptions.HostAddress, messageId).Execute();

        var replyMessage = new Message();
        var modifiedSubject = $"Re: {originalMessage.Payload.Headers.First(h => h.Name == "Subject").Value}";

        var modifiedBody =
            $"On {originalMessage.Payload.Headers.First(h => h.Name == "Date").Value}, " +
            $"{originalMessage.Payload.Headers.First(h => h.Name == "From").Value} wrote:\n\n";
        modifiedBody += body;

        replyMessage.Payload = new MessagePart
        {
            Headers = new List<MessagePartHeader>
            {
                new() { Name = "To", Value = to },
                new() { Name = "Subject", Value = modifiedSubject }
            },
            Body = new MessagePartBody { Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(modifiedBody)) }
        };

        var replyRequest = _gmailService.Users.Messages.Send(replyMessage, _emailApiOptions.HostAddress);
        await replyRequest.ExecuteAsync();

        logger.LogInformation("Replied to email (Original Message Id: {originalMessageId})", messageId);
    }

    public void MarkAsRead(string messageId)
    {
        InitializeAsync();

        var mods = new ModifyMessageRequest
        {
            AddLabelIds = null,
            RemoveLabelIds = new List<string> { "UNREAD" }
        };

        _gmailService!.Users.Messages.Modify(mods, _emailApiOptions.HostAddress, messageId).Execute();
        logger.LogInformation("Marked email as read: {messageId}", messageId);
    }
}