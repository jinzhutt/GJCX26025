using System;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PressureTestApp.Core.Helpers;
using PressureTestApp.UI.ViewModels;

namespace PressureTestApp.UI.Views;

public partial class TaskViewWindow : Window
{
    private const BindingFlags DataGridInternal = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private TaskViewViewModel? _boundVm;
    private bool _suppressBottomHScrollSync;

    private ScrollViewer? _taskGridMainScrollViewer;
    private bool _taskRedoVerticalScrollHooked;
    private enum TaskRedoVScrollSource { None, DataGrid, RedoRail }
    private TaskRedoVScrollSource _taskRedoVScrollSource;

    public TaskViewWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        Loaded += OnLoaded;
        Closed += OnWindowClosed;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_boundVm is not null)
        {
            _boundVm.NavigateToTaskViewFromRedo -= OnNavigateToTaskViewFromRedo;
            _boundVm.RequestTaskSelectConfirm -= OnRequestTaskSelectConfirm;
        }

        UnhookTaskRedoVerticalScrollSync();
        UnhookTaskGrid();
        UnhookBottomScrollSync();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TaskViewViewModel vm)
        {
            _boundVm = vm;
            vm.RequestClose = Close;
            vm.NavigateToTaskViewFromRedo += OnNavigateToTaskViewFromRedo;
            vm.RequestTaskSelectConfirm += OnRequestTaskSelectConfirm;
            _ = vm.InitializeAsync();
        }

        HookBottomScrollSync();
        SyncBottomScrollFromDataGrid();
        // 首帧列宽/Bounds 可能尚未就绪，再同步一次避免横条 Maximum 一直为 0
        Dispatcher.UIThread.Post(SyncBottomScrollFromDataGrid, DispatcherPriority.Loaded);
        Dispatcher.UIThread.Post(TryHookTaskRedoVerticalScrollSync, DispatcherPriority.Loaded);
    }

    private void UnhookTaskGrid()
    {
        _boundVm = null;
    }

    private void HookBottomScrollSync()
    {
        if (TaskResultGrid is null || TaskGridBottomHScroll is null)
            return;

        TaskResultGrid.LayoutUpdated += OnTaskResultGridLayoutUpdated;
        TaskGridBottomHScroll.PropertyChanged += OnBottomHScrollPropertyChanged;
    }

    private void UnhookBottomScrollSync()
    {
        if (TaskResultGrid is not null)
            TaskResultGrid.LayoutUpdated -= OnTaskResultGridLayoutUpdated;
        if (TaskGridBottomHScroll is not null)
            TaskGridBottomHScroll.PropertyChanged -= OnBottomHScrollPropertyChanged;
    }

    private void OnTaskResultGridLayoutUpdated(object? sender, EventArgs e)
    {
        SyncBottomScrollFromDataGrid();
        TryHookTaskRedoVerticalScrollSync();
        SyncTaskRedoOperationHeaderHeightFromGrid();
        SyncTaskRedoRailRowMinHeightsFromGrid();
        ReconcileTaskRedoVerticalScroll();
    }

    private void OnBottomHScrollPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ScrollBar.ValueProperty || _suppressBottomHScrollSync)
            return;
        if (TaskResultGrid is null || !TaskResultGrid.IsVisible)
            return;
        SetDataGridHorizontalOffset(TaskResultGrid, TaskGridBottomHScroll.Value);
    }

    /// <summary>从 DataGrid 同步底部横向条（表内横条 Hidden 时用量算 + HorizontalOffset）。</summary>
    private void SyncBottomScrollFromDataGrid()
    {
        if (TaskResultGrid is null || TaskGridBottomHScroll is null || _suppressBottomHScrollSync)
            return;

        if (!TaskResultGrid.IsVisible)
        {
            _suppressBottomHScrollSync = true;
            try
            {
                // Maximum/Viewport 全 0 时 Semi/Fluent 横条常量成 0 高度，看起来像「没有滑块」
                TaskGridBottomHScroll.Minimum = 0;
                TaskGridBottomHScroll.Maximum = 1;
                TaskGridBottomHScroll.ViewportSize = 1;
                TaskGridBottomHScroll.Value = 0;
                TaskGridBottomHScroll.IsEnabled = false;
            }
            finally
            {
                _suppressBottomHScrollSync = false;
            }

            return;
        }

        var max = GetMaxHorizontalOffsetForDataGrid(TaskResultGrid);
        var type = typeof(DataGrid);
        var offsetProp = type.GetProperty("HorizontalOffset", DataGridInternal);
        var cur = offsetProp?.GetValue(TaskResultGrid) is double v ? v : 0;
        var viewW = TaskResultGrid.Bounds.Width > 2 ? TaskResultGrid.Bounds.Width : 400;

        _suppressBottomHScrollSync = true;
        try
        {
            TaskGridBottomHScroll.Minimum = 0;
            if (max > 0.5)
            {
                TaskGridBottomHScroll.Maximum = max;
                TaskGridBottomHScroll.ViewportSize = Math.Max(48, viewW * 0.4);
                TaskGridBottomHScroll.Value = Math.Clamp(cur, 0, max);
                TaskGridBottomHScroll.IsEnabled = true;
            }
            else
            {
                // 列未超出可视区时仍占位，避免 ViewportSize=0 横条塌成不可见
                TaskGridBottomHScroll.Maximum = 1;
                TaskGridBottomHScroll.ViewportSize = 1;// 0 会导致 Semi/Fluent 横条高度变 0，完全看不见了
                TaskGridBottomHScroll.Value = 0;// HorizontalOffset 也重置为 0，避免用户先滚动横条到中间再调整列宽后横条位置不对
                TaskGridBottomHScroll.IsEnabled = false;
            }
        }
        finally
        {
            _suppressBottomHScrollSync = false;
        }
    }

    private static bool SetDataGridHorizontalOffset(DataGrid dg, double value)
    {
        var update = typeof(DataGrid).GetMethod("UpdateHorizontalOffset", DataGridInternal, Type.DefaultBinder, [typeof(double)], null);
        if (update is null)
            return false;

        var max = GetMaxHorizontalOffsetForDataGrid(dg);// 0.5 是个经验值，避免横条 Maximum 极小但不为 0 时频繁调用更新方法（虽然内部有容错但仍可能引起性能问题）
        if (max <= 0.5)
            return false;

        var next = Math.Clamp(value, 0, max);
        return (bool)update.Invoke(dg, [next])!;
    }

    private static double GetMaxHorizontalOffsetForDataGrid(DataGrid dg)
    {
        var bar = dg.GetVisualDescendants().OfType<ScrollBar>()
            .FirstOrDefault(s => s.Orientation == Orientation.Horizontal && s.Name == "PART_HorizontalScrollbar");
        if (bar is { Maximum: > 0.5 })
            return bar.Maximum;

        double total = 0;
        foreach (DataGridColumn col in dg.Columns)
            total += col.ActualWidth;

        var viewport = dg.Bounds.Width > 2 ? dg.Bounds.Width : dg.DesiredSize.Width;
        return Math.Max(0, total - viewport);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnNavigateToTaskViewFromRedo(object? sender, TaskSearchResultRow row)
    {
        try
        {
            if (!await TaskRedoConfirmWindow.ShowAsync(this, row))
                return;

            await TaskRedoSuccessWindow.ShowAsync(this);

            if (sender is TaskViewViewModel vm)
                await vm.FinishRedoTransferAsync(row);
        }
        catch (Exception ex)
        {
            NlogHelper.Warn(ex, "任务重做确认流程失败");
        }
    }

    private async void OnRequestTaskSelectConfirm(object? sender, TaskSearchResultRow row)
    {
        try
        {
            if (!await TaskRedoConfirmWindow.ShowTaskSelectAsync(this, row))
                return;

            if (sender is TaskViewViewModel vm)
                vm.ApplyConfirmedTaskSelection(row);
        }
        catch (Exception ex)
        {
            NlogHelper.Warn(ex, "任务选择确认流程失败");
        }
    }

    private void UnhookTaskRedoVerticalScrollSync()
    {
        if (!_taskRedoVerticalScrollHooked)
            return;

        if (_taskGridMainScrollViewer is not null)
            _taskGridMainScrollViewer.PropertyChanged -= OnTaskGridMainScrollViewerPropertyChanged;
        if (TaskRedoRailScroll is not null)
            TaskRedoRailScroll.PropertyChanged -= OnTaskRedoRailScrollViewerPropertyChanged;

        _taskGridMainScrollViewer = null;
        _taskRedoVerticalScrollHooked = false;
    }

    private void TryHookTaskRedoVerticalScrollSync()
    {
        if (TaskResultGrid is null || TaskRedoRailScroll is null || !TaskResultGrid.IsVisible)
            return;

        var ideal = FindScrollViewerHostingDataGridRows(TaskResultGrid);
        if (ideal is null)
        {
            if (_taskRedoVerticalScrollHooked)
                UnhookTaskRedoVerticalScrollSync();
            return;
        }

        if (_taskRedoVerticalScrollHooked && ReferenceEquals(_taskGridMainScrollViewer, ideal))
            return;

        if (_taskRedoVerticalScrollHooked)
            UnhookTaskRedoVerticalScrollSync();

        _taskGridMainScrollViewer = ideal;
        ideal.PropertyChanged += OnTaskGridMainScrollViewerPropertyChanged;
        TaskRedoRailScroll.PropertyChanged += OnTaskRedoRailScrollViewerPropertyChanged;
        _taskRedoVerticalScrollHooked = true;

        _taskRedoVScrollSource = TaskRedoVScrollSource.DataGrid;
        try
        {
            ApplyVerticalOffsetPreserveHorizontal(TaskRedoRailScroll, ideal.Offset.Y);
        }
        finally
        {
            _taskRedoVScrollSource = TaskRedoVScrollSource.None;
        }
    }

    private void SyncTaskRedoOperationHeaderHeightFromGrid()
    {
        if (TaskResultGrid is null || TaskRedoOperationHeader is null || !TaskResultGrid.IsVisible)
            return;

        var headers = TaskResultGrid.GetVisualDescendants().OfType<DataGridColumnHeader>().ToList();
        if (headers.Count == 0)
            return;

        var h = headers.Max(ch => ch.Bounds.Height);
        if (h < 8 || double.IsNaN(h))
            return;

        TaskRedoOperationHeader.MinHeight = h;
        TaskRedoOperationHeader.Height = h;
    }

    private void ReconcileTaskRedoVerticalScroll()
    {
        if (!_taskRedoVerticalScrollHooked || _taskGridMainScrollViewer is null || TaskRedoRailScroll is null)
            return;

        var gy = _taskGridMainScrollViewer.Offset.Y;
        var ry = TaskRedoRailScroll.Offset.Y;
        if (Math.Abs(gy - ry) < 0.75)
            return;

        _taskRedoVScrollSource = TaskRedoVScrollSource.DataGrid;
        try
        {
            ApplyVerticalOffsetPreserveHorizontal(TaskRedoRailScroll, gy);
        }
        finally
        {
            _taskRedoVScrollSource = TaskRedoVScrollSource.None;
        }
    }

    /// <summary>将右侧轨首行高度与表内首行 DataGridRow 对齐，减少行高不一致。</summary>
    private void SyncTaskRedoRailRowMinHeightsFromGrid()
    {
        if (TaskResultGrid is null || TaskRedoRailItems is null || !TaskResultGrid.IsVisible)
            return;

        var sampleRow = TaskResultGrid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .OrderBy(r => r.TranslatePoint(new Point(), TaskResultGrid)?.Y ?? 0)
            .FirstOrDefault();
        if (sampleRow is null)
            return;

        var h = sampleRow.Bounds.Height;
        if (h < 8 || double.IsNaN(h))
            return;

        var n = TaskRedoRailItems.Items?.Count ?? 0;
        for (var i = 0; i < n; i++)
        {
            var c = TaskRedoRailItems.ContainerFromIndex(i);
            if (c is ContentPresenter cp)
                cp.MinHeight = h;
        }
    }

    private void OnTaskGridMainScrollViewerPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ScrollViewer.OffsetProperty)
            return;
        if (_taskRedoVScrollSource != TaskRedoVScrollSource.None)
            return;
        if (_taskGridMainScrollViewer is null || TaskRedoRailScroll is null)
            return;

        _taskRedoVScrollSource = TaskRedoVScrollSource.DataGrid;
        try
        {
            ApplyVerticalOffsetPreserveHorizontal(TaskRedoRailScroll, _taskGridMainScrollViewer.Offset.Y);
        }
        finally
        {
            _taskRedoVScrollSource = TaskRedoVScrollSource.None;
        }
    }

    private void OnTaskRedoRailScrollViewerPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ScrollViewer.OffsetProperty)
            return;
        if (_taskRedoVScrollSource != TaskRedoVScrollSource.None)
            return;
        if (_taskGridMainScrollViewer is null || TaskRedoRailScroll is null)
            return;

        _taskRedoVScrollSource = TaskRedoVScrollSource.RedoRail;
        try
        {
            ApplyVerticalOffsetPreserveHorizontal(_taskGridMainScrollViewer, TaskRedoRailScroll.Offset.Y);
        }
        finally
        {
            _taskRedoVScrollSource = TaskRedoVScrollSource.None;
        }
    }

    private static void ApplyVerticalOffsetPreserveHorizontal(ScrollViewer target, double newY)
    {
        var maxY = Math.Max(0, target.Extent.Height - target.Viewport.Height);
        var y = Math.Clamp(newY, 0, maxY);
        var o = target.Offset;
        if (Math.Abs(o.Y - y) < 0.25)
            return;

        target.Offset = new Vector(o.X, y);
    }

    /// <summary>沿首条 DataGridRow 向上找到真正承载行内容的 ScrollViewer（避免首帧无行时误绑到错误内部条，且搜索后行出现时可重绑）。</summary>
    private static ScrollViewer? FindScrollViewerHostingDataGridRows(DataGrid dg)
    {
        var row = dg.GetVisualDescendants()
            .OfType<DataGridRow>()
            .OrderBy(r => r.TranslatePoint(new Point(), dg)?.Y ?? 0)
            .FirstOrDefault();
        if (row is null)
            return null;

        for (Visual? p = row; p != null; p = p.GetVisualParent() as Visual)
        {
            if (p is ScrollViewer sv)
                return sv;
        }

        return null;
    }
}
