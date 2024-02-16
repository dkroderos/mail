using System.Security.Cryptography.X509Certificates;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace Merrsoft.MerrMail.Infrastructure.Helpers;

public static class GmailApiHelper
{
    public static async Task<GmailService> GetGmailService(string credentialsPath, string accessTokenPath)
    {
        await using var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read);
        
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            (await GoogleClientSecrets.FromStreamAsync(stream)).Secrets,
            new[] { GmailService.Scope.GmailReadonly, GmailService.Scope.GmailModify },
            "user",
            CancellationToken.None,
            new FileDataStore(accessTokenPath, true));
        
        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Gmail API Sample",
        });

        // var certificate = new X509Certificate2(credentialsPath, "notasecret", X509KeyStorageFlags.Exportable);
        // var certificate = new X509Certificate2(credentialsPath);
        // var certificate = new X509Certificate(credentialsPath, "not")
        
        // var credential = GoogleCredential.FromFile(credentialsPath).CreateScoped(GmailService.Scope.MailGoogleCom);
        // var credential = new ServiceAccountCredential(
        //     new ServiceAccountCredential.Initializer("merrsoft.testing@gmail.com")
        //     {
        //         Scopes = new[] { GmailService.Scope.GmailReadonly, GmailService.Scope.GmailModify },
        //     }.FromCertificate(certificate));

        // return new GmailService(new BaseClientService.Initializer
        // {
        //     // HttpClientInitializer = credential,
        //     // ApplicationName = "Gmail API Sample",
        //     ApiKey = api,
        //     ApplicationName = "Merr Mail",
        // });
    } 
}