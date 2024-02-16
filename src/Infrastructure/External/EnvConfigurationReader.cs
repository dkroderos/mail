// using Merrsoft.MerrMail.Application.Contracts;
// using Merrsoft.MerrMail.Domain.Contracts;
// using Merrsoft.MerrMail.Domain.Models;
// using Microsoft.Extensions.Logging;
// using Microsoft.Extensions.Options;
//
// namespace Merrsoft.MerrMail.Infrastructure.External;
//
// public class EnvConfigurationReader(
//     ILogger<EnvConfigurationReader> logger,
//     IOptions<ApplicationOptions> options) : IConfigurationReader
// {
//     private readonly ApplicationOptions _options = options.Value;
//     public void ReadConfiguration()
//     {
//         logger.LogInformation("Reading environment variables...");
//         DotNetEnv.Env.TraversePath().Load();
//
//         var oAuthClientCredentialsPath = DotNetEnv.Env.GetString("OAUTH_CLIENT_CREDENTIALS_PATH");
//         var accessTokenPath = DotNetEnv.Env.GetString("ACCESS_TOKEN_PATH");
//         var databaseConnection = DotNetEnv.Env.GetString("DATABASE_CONNECTION");
//         var hostAddress = DotNetEnv.Env.GetString("HOST_ADDRESS");
//
//         _options.OAuthClientCredentialsPath = oAuthClientCredentialsPath;
//         _options.AccessTokenPath = accessTokenPath;
//         _options.DatabaseConnection = databaseConnection;
//         _options.HostAddress = hostAddress;
//
//         typeof(IConfigurationSettings).GetProperties().ToList().ForEach(property =>
//         {
//             var value = property.GetValue(_options);
//             logger.Log(value is null
//                     ? LogLevel.Critical
//                     : LogLevel.Information,
//                 value is null
//                     ? "{propertyName} is null"
//                     : "{propertyName} is set to: {value}", property.Name, value);
//         });
//     }
// }