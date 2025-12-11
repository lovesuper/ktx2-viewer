using KtxViewer.Application;
using KtxViewer.Core;
using KtxViewer.Infrastructure;
using KtxViewer.UI.ViewModels;
using System.Windows;

namespace KtxViewer.UI;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        IKtxLoader loader = new KtxLoader();
        var useCase = new LoadImageUseCase(loader);
        var viewModel = new MainViewModel(useCase);
        var mainWindow = new MainWindow(viewModel);

        mainWindow.Show();
    }
}
