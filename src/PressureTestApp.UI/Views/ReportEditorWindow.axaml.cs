using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PressureTestApp.UI.Services;
using PressureTestApp.UI.ViewModels;

namespace PressureTestApp.UI.Views;

public partial class ReportEditorWindow : Window
{
    private readonly ReportEditorViewModel _viewModel;

    public ReportEditorWindow()
    {
        InitializeComponent();
        
        var reportService = App.Services.GetService<IReportService>() ?? new ReportService();
        _viewModel = new ReportEditorViewModel(reportService);
        _viewModel.CloseWindow = Close;
        DataContext = _viewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}