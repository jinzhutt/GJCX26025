using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PressureTestApp.UI.ViewModels;

namespace PressureTestApp.UI.Views;

public partial class NumericEditDialogWindow : Window
{
    public NumericEditDialogWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ValueTextBox?.Focus();// �Զ��۽������ѡ��ȫ���ı��������û�ֱ�����븲��
        ValueTextBox?.SelectAll();// ���ڼ�����ɺ�ִ�У�ȷ�� TextBox �Ѿ�׼���ý��ս����ѡ���ı�
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)// ���� Esc ����ȡ�����ر�
    {
        if (e.Key != Key.Escape)
            return;
        if (DataContext is NumericEditDialogViewModel vm)// ���Ϊȡ��
            vm.MarkCancelled();//
        Close();
        e.Handled = true;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)// �����϶�����
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)// ���ڰ������ʱ�϶����������Ҽ��˵��Ƚ�����ͻ
            BeginMoveDrag(e);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)// ȷ�����벢�ر�
    {
        if (DataContext is NumericEditDialogViewModel vm && vm.TryConfirm())
            Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)// ���Ϊȡ�����ر�
    {
        if (DataContext is NumericEditDialogViewModel vm)
            vm.MarkCancelled();
        Close();
    }
}
