using System.Runtime.InteropServices.JavaScript;
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
using Thread = Google.Apis.Gmail.v1.Data.Thread;

namespace Merrsoft.MerrMail.Infrastructure.Services;

// TODO: Setup GmailService on start
public class GmailApiService(
    ILogger<GmailApiService> logger,
    IOptions<EmailApiOptions> emailApiOptions)
    : IEmailApiService
{
    private readonly EmailApiOptions _emailApiOptions = emailApiOptions.Value;
    private GmailService? _gmailService;
    private bool _hasCreatedLabels;

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
                backgroundColor = "#000000"; // Black
                textColor = "#ffffff"; // White
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

    // public FirstEmailOnThreadDto GetFirstEmailOnThread(string hostAddress)
    // {
    //     var request = _gmailService!.Users.Threads.List(_emailApiOptions.HostAddress);
    //     var response = request.Execute();
    //
    //     throw new NotImplementedException();
    // }

    public List<FirstEmailOnThreadDto> GetFirstEmailOnThreads()
    {
        InitializeAsync();

        var emails = new List<FirstEmailOnThreadDto>();

        var userId = _emailApiOptions.HostAddress;

        // var threadsRequest = _gmailService!.Users.Threads.List(userId);
        // threadsRequest.LabelIds = "INBOX";
        // threadsRequest.IncludeSpamTrash = false;
        // var threadsResponse = threadsRequest.Execute();
        var threadsResponse = GetThreads();

        if (threadsResponse?.Threads is null)
            return [];

        foreach (var thread in threadsResponse.Threads)
        {
            var threadDetailsResponse = _gmailService!.Users.Threads.Get(userId, thread.Id).Execute();

            if (threadDetailsResponse?.Messages?.Any() != true)
            {
                logger.LogWarning("Thread ID {threadId} has no messages or failed to retrieve details.", thread.Id);
                continue;
            }

            var firstMessage = threadDetailsResponse.Messages.First();
            var firstEmail = _gmailService!.Users.Messages.Get(userId, firstMessage.Id).Execute();

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

    private ListThreadsResponse? GetThreads()
    {
        try
        {
            var userId = _emailApiOptions.HostAddress;
    
            var threadsRequest = _gmailService!.Users.Threads.List(userId);
            threadsRequest.LabelIds = "INBOX";
            threadsRequest.IncludeSpamTrash = false;
            var threadsResponse = threadsRequest.Execute();
    
            return threadsResponse;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }
    
    // private ListThreadsResponse? GetThreads()
    // {
    //     try
    //     {
    //         var userId = _emailApiOptions.HostAddress;
    //
    //         // Exclude threads with specific labels (e.g., "MerrMail: High Priority" or "MerrMail: Low Priority")
    //         var excludeLabels = new List<string> { "MerrMail: High Priority", "MerrMail: Low Priority" };
    //
    //         // Get label IDs for exclusion
    //         var excludeLabelIds = excludeLabels.Select(label => GetLabelId(userId, label)).Where(id => id != null).ToList();
    //
    //         var threadsRequest = _gmailService!.Users.Threads.List(userId);
    //         threadsRequest.LabelIds = "INBOX";
    //
    //         // Exclude threads with specific labels
    //         if (excludeLabelIds.Any())
    //         {
    //             threadsRequest.Q = $"-label:{string.Join(" -label:", excludeLabelIds)}";
    //         }
    //
    //         threadsRequest.IncludeSpamTrash = false;
    //
    //         var threadsResponse = threadsRequest.Execute();
    //
    //         return threadsResponse;
    //     }
    //     catch (NullReferenceException)
    //     {
    //         return null;
    //     }
    // }

    public void LabelThread(string threadId, string labelName)
    {
        InitializeAsync();

        var userId = _emailApiOptions.HostAddress;

        var threadDetailsRequest = _gmailService!.Users.Threads.Get(userId, threadId);
        var threadDetailsResponse = threadDetailsRequest.Execute();

        if (threadDetailsResponse?.Messages?.Any() is not true)
        {
            logger.LogWarning("Thread ID {threadId} has no messages or failed to retrieve details.", threadId);
            return;
        }

        // Check if the thread already has "MerrMail: High Priority" or "MerrMail: Low Priority" label
        if (ThreadHasHighOrLowPriorityLabel(threadDetailsResponse))
        {
            logger.LogWarning(
                "Thread ID {threadId} already has 'MerrMail: High Priority' or 'MerrMail: Low Priority' label. Skipping labeling.",
                threadId);
            return;
        }

        var labelExists = LabelExists(userId, labelName);
        if (!labelExists)
        {
            logger.LogWarning("Label '{labelName}' does not exist. Skipping labeling of the thread '{threadId}'.",
                labelName, threadId);
            return;
        }

        // Get the label ID for the specified label name
        var labelId = GetLabelId(userId, labelName);
        if (string.IsNullOrEmpty(labelId))
        {
            logger.LogError(
                "Failed to retrieve label ID for label '{labelName}'. Skipping labeling of the thread '{threadId}'.",
                labelName, threadId);
            return;
        }

        // Apply the label to the thread
        var modifyThreadRequest = new ModifyThreadRequest { AddLabelIds = new List<string> { labelId } };
        var modifyThreadResponse = _gmailService?.Users.Threads.Modify(modifyThreadRequest, userId, threadId).Execute();

        if (modifyThreadResponse != null)
        {
            logger.LogInformation("Thread '{threadId}' labeled with '{labelName}'.", threadId, labelName);
        }
        else
        {
            logger.LogError("Failed to label thread '{threadId}' with '{labelName}'.", threadId, labelName);
        }
    }

    private bool ThreadHasHighOrLowPriorityLabel(Thread thread)
    {
        // Check if any message in the thread has "MerrMail: High Priority" or "MerrMail: Low Priority" label
        return thread.Messages?.Any(message =>
            message.LabelIds?.Any(label =>
                label == GetLabelId(_emailApiOptions.HostAddress, "MerrMail: High Priority") ||
                label == GetLabelId(_emailApiOptions.HostAddress, "MerrMail: Low Priority")) == true) == true;
    }

    private bool LabelExists(string userId, string labelName)
    {
        try
        {
            // Retrieve the list of labels for the user
            var labelsRequest = _gmailService?.Users.Labels.List(userId);
            var labelsResponse = labelsRequest?.Execute();

            // Check if a label with the specified name already exists
            return labelsResponse?.Labels?.Any(label => label.Name == labelName) == true;
        }
        catch (Exception ex)
        {
            logger.LogError("Error checking if label exists: {error}", ex.Message);
            return false;
        }
    }

    private string GetLabelId(string userId, string labelName)
    {
        try
        {
            // Retrieve the list of labels for the user
            var labelsRequest = _gmailService?.Users.Labels.List(userId);
            var labelsResponse = labelsRequest?.Execute();
    
            // Find the label with the specified name and return its ID
            var label = labelsResponse?.Labels?.FirstOrDefault(l => l.Name == labelName);
            return label?.Id;
        }
        catch (Exception ex)
        {
            logger.LogError("Error getting label ID: {error}", ex.Message);
            return null;
        }
    }
    

    public void RemoveThreadFromInbox(string threadId)
    {
        InitializeAsync();

        var userId = _emailApiOptions.HostAddress;

        // Modify the thread to remove it from the Inbox
        var modifyThreadRequest = new ModifyThreadRequest
        {
            RemoveLabelIds = new List<string> { "INBOX" } // Remove from Inbox
        };

        var modifyThreadResponse = _gmailService?.Users.Threads.Modify(modifyThreadRequest, userId, threadId).Execute();

        if (modifyThreadResponse != null)
        {
            logger.LogInformation("Thread '{threadId}' removed from Inbox.", threadId);
        }
        else
        {
            logger.LogError("Failed to remove thread '{threadId}' from Inbox.", threadId);
        }
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
    
    public void ReplyToThread(string threadId, string to, string body)
    {
        InitializeAsync();

        try
        {
            // Get the latest message in the thread
            var threadDetailsRequest = _gmailService!.Users.Threads.Get(_emailApiOptions.HostAddress, threadId);
            var threadDetailsResponse = threadDetailsRequest.Execute();

            if (threadDetailsResponse?.Messages == null || !threadDetailsResponse.Messages.Any())
            {
                logger.LogWarning("Thread ID {threadId} has no messages or failed to retrieve details.", threadId);
                return;
            }

            var latestMessage = threadDetailsResponse.Messages.First();
            var originalSubject = latestMessage.Payload.Headers?.FirstOrDefault(h => h.Name == "Subject")?.Value;

            // Create the reply message
            var replyMessage = new Message
            {
                ThreadId = threadId,
                LabelIds = new List<string> { "INBOX" }, // Assuming you want to move the reply to the INBOX
                Payload = new MessagePart
                {
                    Headers = new List<MessagePartHeader>
                    {
                        new MessagePartHeader { Name = "To", Value = to },
                        new MessagePartHeader { Name = "Subject", Value = originalSubject },
                    },
                    Body = new MessagePartBody { Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(body)) }
                }
            };

            // Send the reply message
            var sendRequest = _gmailService.Users.Messages.Send(replyMessage, _emailApiOptions.HostAddress);
            sendRequest.Execute();

            logger.LogInformation("Replied to thread {threadId} with subject '{originalSubject}'", threadId, originalSubject);
        }
        catch (Exception ex)
        {
            logger.LogError("Error while replying to thread: {error}", ex.Message);
        }
    }

    // public async Task Reply(string to, string body, string messageId)
    // {
    //     InitializeAsync();
    //
    //     var originalMessage = await _gmailService!.Users.Messages.Get(_emailApiOptions.HostAddress, messageId).ExecuteAsync();
    //
    //     var replyMessage = new Message();
    //     var modifiedSubject = $"Re: {originalMessage.Payload.Headers.First(h => h.Name == "Subject").Value}";
    //
    //     var modifiedBody =
    //         $"On {originalMessage.Payload.Headers.First(h => h.Name == "Date").Value}, " +
    //         $"{originalMessage.Payload.Headers.First(h => h.Name == "From").Value} wrote:\n\n";
    //     modifiedBody += body;
    //
    //     replyMessage.Payload = new MessagePart
    //     {
    //         Headers = new List<MessagePartHeader>
    //         {
    //             new() { Name = "To", Value = to },
    //             new() { Name = "Subject", Value = modifiedSubject }
    //         },
    //         Body = new MessagePartBody { Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(modifiedBody)) }
    //     };
    //
    //     var replyRequest = _gmailService.Users.Messages.Send(replyMessage, _emailApiOptions.HostAddress);
    //     await replyRequest.ExecuteAsync();
    //
    //     logger.LogInformation("Replied to email (Original Message Id: {originalMessageId})", messageId);
    // }

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