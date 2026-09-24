using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressureTestApp.Core.Configs;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Core.Services;
using PressureTestApp.UI.Views;

namespace PressureTestApp.UI.ViewModels;

/// <summary>
/// MAC ????????????? GJCS frmAuthentication ?????????
/// </summary>
public partial class DeviceAuthViewModel : ObservableObject
{
    private readonly ITaskApiService _taskApi;
    private readonly AuthenticationConfig _authConfig;

    public ObservableCollection<MacAddressHelper.MacEntry> MacEntries { get; } = new();

    [ObservableProperty] private MacAddressHelper.MacEntry? _selectedMac;
    [ObservableProperty] private string _statusText = "?????? MAC ??????????????????MAC????????";
    [ObservableProperty] private string _currentDeviceIdText = "????????";
    [ObservableProperty] private bool _isBusy;

    public bool DialogResultOk { get; private set; }
    public Window? HostWindow { get; set; }

    public DeviceAuthViewModel(ITaskApiService taskApi, AuthenticationConfig authConfig)
    {
        _taskApi = taskApi;
        _authConfig = authConfig;
        RefreshDeviceIdDisplay();
    }

    private void RefreshDeviceIdDisplay()
    {
        var id = _taskApi.DeviceNumber ?? _authConfig.Authentication;
        CurrentDeviceIdText = string.IsNullOrWhiteSpace(id)
            ? "????????"
            : $"?????????{id}";
    }

    [RelayCommand]
    private void ScanMac()
    {
        MacEntries.Clear();
        SelectedMac = null;

        foreach (var entry in MacAddressHelper.ScanAvailable())
            MacEntries.Add(entry);

        if (MacEntries.Count == 0)
        {
            StatusText = "?????????????????????????????????";
            NlogHelper.Warn("MAC ???s?????????");
            return;
        }

        StatusText = $"????? {MacEntries.Count} ???????????????????????";
        NlogHelper.Info($"MAC ????????? {MacEntries.Count} ??");
    }

    [RelayCommand]
    private async Task RequestAuthAsync()
    {
        if (SelectedMac == null)
        {
            StatusText = "?????????????? MAC ?????";
            await ShowNoticeAsync("?????????????? MAC ?????");
            return;
        }

        IsBusy = true;
        StatusText = "????????????????";
        try
        {
            var result = await _taskApi.AuthorizeDeviceAsync(SelectedMac.MacAddress);
            if (result.Success)
            {
                DialogResultOk = true;
                RefreshDeviceIdDisplay();
                StatusText = result.Message;
                NlogHelper.Info($"???????????{result.DeviceNumber}");
                await ShowNoticeAsync(result.Message);
                RequestClose?.Invoke();
            }
            else
            {
                StatusText = result.Message;
                NlogHelper.Warn($"??????????{result.Message}");
                await ShowNoticeAsync(result.Message);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"???????????{ex.Message}";
            NlogHelper.Error(ex, "????????????");
            await ShowNoticeAsync($"??????????????{ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Close() => RequestClose?.Invoke();

    public Action? RequestClose { get; set; }

    private async Task ShowNoticeAsync(string message)
    {
        if (HostWindow == null)
            return;
        await new InfoNoticeWindow(message, "???").ShowDialog(HostWindow);
    }
}
