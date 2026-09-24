using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PressureTestApp.UI.Views;
using System.Threading.Tasks;

namespace PressureTestApp.UI.Helpers
{
    public static class GenericDialog
    {
        // 显示通用弹窗
        public async static Task<bool> ShowDialog(Window owner, string txt)
        {
            var dialog = new GenericDialogWindow();
            dialog.SetMessage(txt);
            return await dialog.ShowDialog<bool>(owner); // 弹窗返回布尔值
        }

        #region 提示框（仅确定键，无返回值）
        /// <summary>
        /// 提示框 - 只有确定按钮，直接调用无需 await
        /// </summary>
        public static async void ShowMsg(this Window owner, string message, string title = "提示")
        {
            var window = new Window
            {
                Title = title,
                Width = 320,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Brushes.LightGray,
                CanResize = false,
                ShowInTaskbar = false,
                SizeToContent = SizeToContent.Height
            };

            var panel = new StackPanel();
            panel.Margin = new Thickness(20);
            panel.Spacing = 15;

            var text = new TextBlock
            {
                Text = message,
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var btn = new Button
            {
                Content = "确定",
                Width = 100,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            btn.Click += (s, e) => window.Close();

            panel.Children.Add(text);
            panel.Children.Add(btn);
            window.Content = panel;

            await window.ShowDialog(owner);
        }
        #endregion

        #region 确认框（确定+取消键，带返回值）
        /// <summary>
        /// 确认框 - 确定+取消按钮，需要 await 调用
        /// </summary>
        public static async Task<bool> ShowMsgAsync(Window owner, string message, string title = "提示")
        {
            // 创建弹窗窗口
            var window = new Window
            {
                Title = title,
                Width = 320,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = Brushes.LightGray,
                CanResize = false,
                // 禁止关闭按钮直接关闭，规范操作
                ShowInTaskbar = false,
                SizeToContent = SizeToContent.Height
            };

            // 布局面板
            var panel = new StackPanel();
            panel.Margin = new Thickness(20);
            panel.Spacing = 15;

            // 提示文本
            var text = new TextBlock
            {
                Text = message,
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 水平布局放两个按钮
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 20
            };

            // 确定按钮
            var okBtn = new Button
            {
                Content = "确定",
                Width = 100
            };

            // 取消按钮
            var cancelBtn = new Button
            {
                Content = "取消",
                Width = 100
            };

            // 存储结果
            bool dialogResult = false;

            // 绑定点击事件
            okBtn.Click += (s, e) =>
            {
                dialogResult = true;
                window.Close();
            };

            cancelBtn.Click += (s, e) =>
            {
                dialogResult = false;
                window.Close();
            };

            // 添加按钮到面板
            buttonPanel.Children.Add(okBtn);
            buttonPanel.Children.Add(cancelBtn);

            // 添加到主面板
            panel.Children.Add(text);
            panel.Children.Add(buttonPanel);
            window.Content = panel;

            // 显示弹窗并等待关闭
            await window.ShowDialog(owner);

            // 返回用户选择结果
            return dialogResult;
        }
        #endregion
    }
}
