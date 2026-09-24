using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PressureTestApp.Core.Helpers;
using PressureTestApp.Core.Models;
using PressureTestApp.Data.Entities;
using PressureTestApp.Data.Repositories;
using PressureTestApp.UI.Views;

namespace PressureTestApp.UI.ViewModels;

public record UserRoleOption(UserRole Role, string Label);

public partial class UserEditDialogViewModel : ObservableObject
{
    private readonly UserRepository _userRepo;

    public IReadOnlyList<UserRoleOption> RoleOptions { get; } =
    [
        new(UserRole.Operator, "操作员"),
        new(UserRole.Assistant, "工艺员"),
        new(UserRole.Inspector, "互检员"),
        new(UserRole.QualityChecker, "质检员"),
        new(UserRole.Admin, "管理员")
    ];

    private readonly int? _editingUserId;
    private readonly bool _openedForEdit;

    /// <summary>窗口标题：添加 / 更改（本地示例行 Id≤0 仍显示「更改」，保存走新增）</summary>
    public string WindowTitle => _openedForEdit ? "更改用户" : "添加用户";

    [ObservableProperty] private string _nameText = "";
    [ObservableProperty] private string _jobNumberText = "";
    [ObservableProperty] private string _teamText = "";
    [ObservableProperty] private string _passwordText = "";
    [ObservableProperty] private string _confirmPasswordText = "";
    [ObservableProperty] private UserRoleOption? _selectedRole;

    /// <summary>添加用户时必填密码；更改用户时留空表示不修改</summary>
    public bool IsAddMode => _editingUserId == null;

    public string PasswordFieldLabel => IsAddMode ? "密码" : "新密码";
    public string PasswordWatermark => IsAddMode ? "请输入" : "留空则不修改";

    public bool Saved { get; private set; }

    public Action? RequestClose { get; set; }

    private Window? _dialogHost;

    public void SetDialogHost(Window host) => _dialogHost = host;

    public UserEditDialogViewModel(UserRepository userRepo, UserListItemViewModel? existing)
    {
        _userRepo = userRepo;
        _openedForEdit = existing != null;
        _editingUserId = existing is { Id: > 0 } ? existing.Id : null;
        if (existing != null)
        {
            NameText = existing.UserName;
            JobNumberText = existing.JobNumber;
            TeamText = existing.TeamName is "—" or null or "" ? "" : existing.TeamName;
            SelectedRole = RoleOptions.FirstOrDefault(r => r.Role == existing.Role) ?? RoleOptions[0];
        }
        else
            SelectedRole = RoleOptions[0];
    }

    [RelayCommand]
    private void Cancel()
    {
        Saved = false;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var name = (NameText ?? "").Trim();
        var job = (JobNumberText ?? "").Trim();
        var team = (TeamText ?? "").Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(job))
        {
            await ShowTipAsync("请填写用户名与工号");
            return;
        }

        if (SelectedRole == null)
        {
            await ShowTipAsync("请选择权限");
            return;
        }

        var password = (PasswordText ?? "").Trim();
        var confirm = (ConfirmPasswordText ?? "").Trim();
        if (IsAddMode)
        {
            if (string.IsNullOrEmpty(password))
            {
                await ShowTipAsync("请设置登录密码");
                return;
            }
            if (password.Length < 4)
            {
                await ShowTipAsync("密码至少 4 位");
                return;
            }
            if (!string.Equals(password, confirm, StringComparison.Ordinal))
            {
                await ShowTipAsync("两次输入的密码不一致");
                return;
            }
        }
        else if (!string.IsNullOrEmpty(password) || !string.IsNullOrEmpty(confirm))
        {
            if (password.Length < 4)
            {
                await ShowTipAsync("新密码至少 4 位");
                return;
            }
            if (!string.Equals(password, confirm, StringComparison.Ordinal))
            {
                await ShowTipAsync("两次输入的新密码不一致");
                return;
            }
        }

        try
        {
            var other = await _userRepo.FindByJobNumberAsync(job);
            if (other != null && (_editingUserId == null || other.Id != _editingUserId))
            {
                await ShowTipAsync($"工号「{job}」已存在");
                return;
            }

            if (_editingUserId == null)
            {
                var entity = new UserEntity
                {
                    JobNumber = job,
                    Name = name,
                    DepName = string.IsNullOrEmpty(team) ? null : team,
                    Role = SelectedRole.Role,
                    PasswordHash = PasswordHelper.SHA512Encode(job, password),
                    Enabled = true
                };
                await _userRepo.InsertAsync(entity);
                NlogHelper.Info($"用户管理·已添加：{job}");
            }
            else
            {
                var list = await _userRepo.GetAllAsync(includeDisabled: true);
                var entity = list.FirstOrDefault(u => u.Id == _editingUserId);
                if (entity == null)
                {
                    await ShowTipAsync("记录不存在");
                    return;
                }

                entity.JobNumber = job;
                entity.Name = name;
                entity.DepName = string.IsNullOrEmpty(team) ? null : team;
                entity.Role = SelectedRole.Role;
                if (!string.IsNullOrEmpty(password))
                    entity.PasswordHash = PasswordHelper.SHA512Encode(job, password);
                await _userRepo.UpdateAsync(entity);
                NlogHelper.Info($"用户管理·已更新：{job}");
            }

            Saved = true;
            if (_dialogHost != null)
            {
                var successMsg = _editingUserId == null ? "保存成功" : "修改成功";
                var notice = new InfoNoticeWindow(successMsg);
                await notice.ShowDialog(_dialogHost);
            }

            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            await ShowTipAsync($"保存失败：{ex.Message}");
        }
    }

    private async Task ShowTipAsync(string message)
    {
        NlogHelper.Info($"编辑用户：{message}");
        if (_dialogHost != null)
            await new InfoNoticeWindow(message).ShowDialog(_dialogHost);
    }

}
