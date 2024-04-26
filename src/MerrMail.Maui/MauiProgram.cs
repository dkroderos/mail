using MerrMail.Maui.ViewModels;
using MerrMail.Maui.Views;
using Merrsoft.MerrMail.Application.Contracts;
using Merrsoft.MerrMail.Infrastructure.External;
using Microsoft.Extensions.Logging;

namespace MerrMail.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<EmailContextsViewModel>();
        builder.Services.AddSingleton<EmailContextsPage>();
        builder.Services.AddSingleton<IDataStorageContext, SqliteDataStorageContext>();

        return builder.Build();
    }
}