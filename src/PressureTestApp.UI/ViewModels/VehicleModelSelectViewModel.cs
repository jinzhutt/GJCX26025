using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Data.Entities;
using PressureTestApp.Data.Repositories;
using PressureTestApp.UI;

namespace PressureTestApp.UI.ViewModels;

/// <summary>车型选择弹窗中的一行（与表格列一致）。</summary>
public sealed class VehicleModelTableRow
{
    public string ProductType { get; init; } = "";
    public string ProductModel { get; init; } = "";
    public string Remark { get; init; } = "";
}

public partial class VehicleModelSelectViewModel : ObservableObject
{
    private readonly VehicleTypeRepository _vehicleTypeRepo;
    private Dictionary<int, string> _typeIdCategoryMap = new();
    private ObservableCollection<VehicleModelTableRow> _allRows = new();

    public ObservableCollection<VehicleModelTableRow> FilteredRows { get; } = new();

    public ObservableCollection<string> ProductTypeOptions { get; } = new();

    [ObservableProperty] private string? _selectedProductTypeFilter;

    [ObservableProperty] private VehicleModelTableRow? _selectedRow;

    /// <summary>关闭弹窗（取消 / 标题栏关闭）。</summary>
    public Action? RequestClose { get; set; }

    /// <summary>用户点击「选择实行」且已选中行时回调。</summary>
    public Action<VehicleModelTableRow>? OnConfirmed { get; set; }

    public VehicleModelSelectViewModel()
    {
        _vehicleTypeRepo = App.Services.GetRequiredService<VehicleTypeRepository>();
    }

    public async Task LoadAsync()
    {
        try
        {
            var list = await _vehicleTypeRepo.GetAllAsync();
            _typeIdCategoryMap = await _vehicleTypeRepo.GetCategoryNameByTypeIdAsync();
            _allRows = new ObservableCollection<VehicleModelTableRow>(
                list.Select(MapEntity));
        }
        catch (Exception ex)
        {
            NlogHelper.Error(ex, "车型选择：加载产品列表失败，使用演示数据");
            _allRows = new ObservableCollection<VehicleModelTableRow>(GetDemoRows());
        }

        if (_allRows.Count == 0)
            _allRows = new ObservableCollection<VehicleModelTableRow>(GetDemoRows());

        RebuildTypeOptions();
        ApplyFilter();
    }

    private VehicleModelTableRow MapEntity(VehicleTypeEntity t)
    {
        var productType = _typeIdCategoryMap.TryGetValue(t.TypeId, out var cat) && !string.IsNullOrWhiteSpace(cat)
            ? cat
            : "耐压测试";
        return new VehicleModelTableRow
        {
            ProductType = productType,
            ProductModel = t.Name,
            Remark = t.Mark ?? ""
        };
    }

    private static VehicleModelTableRow[] GetDemoRows() =>
    [
        new VehicleModelTableRow { ProductType = "耐压测试", ProductModel = "车型1", Remark = "" },
        new VehicleModelTableRow { ProductType = "耐压测试", ProductModel = "车型2", Remark = "" },
        new VehicleModelTableRow { ProductType = "耐压测试", ProductModel = "车型3", Remark = "" }
    ];

    private void RebuildTypeOptions()
    {
        ProductTypeOptions.Clear();
        ProductTypeOptions.Add("全部");
        foreach (var t in _allRows.Select(r => r.ProductType).Distinct().OrderBy(s => s))
            ProductTypeOptions.Add(t);
        SelectedProductTypeFilter = "全部";
    }

    partial void OnSelectedProductTypeFilterChanged(string? value) => ApplyFilter();

    [RelayCommand]
    private void Search() => ApplyFilter();

    private void ApplyFilter()
    {
        var q = (SelectedProductTypeFilter ?? "全部").Trim();
        IEnumerable<VehicleModelTableRow> src = _allRows;
        if (!string.IsNullOrEmpty(q) && q != "全部")
            src = src.Where(r => r.ProductType == q);

        FilteredRows.Clear();
        foreach (var r in src)
            FilteredRows.Add(r);
    }

    [RelayCommand]
    private void MoveSelectionUp()
    {
        if (SelectedRow == null || FilteredRows.Count == 0) return;
        var i = FilteredRows.IndexOf(SelectedRow);
        if (i > 0)
            SelectedRow = FilteredRows[i - 1];
    }

    [RelayCommand]
    private void MoveSelectionDown()
    {
        if (SelectedRow == null || FilteredRows.Count == 0) return;
        var i = FilteredRows.IndexOf(SelectedRow);
        if (i >= 0 && i < FilteredRows.Count - 1)
            SelectedRow = FilteredRows[i + 1];
    }

    [RelayCommand]
    private void Confirm()
    {
        if (SelectedRow == null)
        {
            NlogHelper.Info("车型选择：未选中行");
            return;
        }

        OnConfirmed?.Invoke(SelectedRow);
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();
}
