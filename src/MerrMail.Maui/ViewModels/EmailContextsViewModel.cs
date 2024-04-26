using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;

namespace MerrMail.Maui.ViewModels;

public partial class EmailContextsViewModel() : BaseViewModel
{
    public ObservableCollection<EmailContext> EmailContexts { get; } = [];

    [RelayCommand]
    public async Task GetEmailContextsAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;

            EmailContexts.Clear();
            // var emailContexts = await dataStorageContext.GetEmailContextsAsync();

            foreach (var emailContext in emailContexts)
            {
                EmailContexts.Add(emailContext);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            await Shell.Current.DisplayAlert("Error", $"Unable to get email contexts: {ex.Message}", "Ok");
        }
        finally
        {
            IsBusy = false;
        }
    }
}