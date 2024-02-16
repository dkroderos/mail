using Merrsoft.MerrMail.Application.Contracts;
using Merrsoft.MerrMail.Application.Services;
using Merrsoft.MerrMail.Domain.Contracts;
using Merrsoft.MerrMail.Domain.Models;
using Merrsoft.MerrMail.Infrastructure.External;
using Merrsoft.MerrMail.Infrastructure.Services;
using Merrsoft.MerrMail.Presentation;
using Serilog;
using Serilog.Events;

try
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] - {Message:lj}{NewLine}{Exception}")
        .CreateLogger();

    Log.Information("Starting Merr Mail");
    Log.Information("Configuring Services");

    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddSerilog();
    builder.Services.AddHostedService<MerrMailWorker>();

    builder.Services.AddHttpClient();
    builder.Services.AddSingleton<IConfigurationSettings, EnvironmentVariables>();

    builder.Services.AddSingleton<IApplicationService, ApplicationService>();
    builder.Services.AddSingleton<IEmailApiService, GmailApiService>();
    // builder.Services.AddSingleton<IConfigurationReader, EnvConfigurationReader>();
    // builder.Services.AddOptions<ApplicationOptions>();
    builder.Services.AddSingleton<IOAuthClientCredentialsReader, GoogleOAuthClientCredentialsReader>();
    
    builder.Services
        .AddOptions<ApplicationOptions>()
        .BindConfiguration("")
        .ValidateDataAnnotations() // <== this if for the [Required]
        .ValidateOnStart();
    
    // builder
    //     .Services
    //     .AddOptions<ApplicationOptions>()
    //     .BindConfiguration("");
    
    // builder
    //     .Services
    //     .AddOptions<ApplicationOptions>()
    //     .BindConfiguration("")
    //     .Validate(options => options.HostAddress != null, "put a clear message here")
    //     .Validate(..... same per field)
    //     .ValidateOnStartup()
    //     ;
    
    // builder.Services
    //     .AddOptions<EnvironmentVariables>()
    //     .BindConfiguration("")
    //     .ValidateDataAnnotations() // <== this if for the [Required]
    //     .ValidateOnStart();

    var host = builder.Build();
    
    host.Run();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(ex.ToString());
}
finally
{
    Log.Information("Stopping Merr Mail");
    Log.CloseAndFlush();
}