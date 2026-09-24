# 一体式新型耐压设备 · Avalonia 跨平台版

> .NET 8 + Avalonia 11 + 人大金仓 KingbaseES + 西门子 S7 PLC + Modbus 串口仪器
> 目标平台：UoS V20-AMD64（也兼容 Windows 10/11）

## 当前进度：阶段 0~3 完成 ✅

### ✅ 阶段 0：环境准备
- [x] .NET 8 项目骨架（4 个子项目）
- [x] 数据库换为人大金仓 KES（PostgreSQL 协议兼容）
- [x] Avalonia + Semi.Avalonia 主题
- [x] 微软雅黑字体（跨平台兜底链）

### ✅ 阶段 1：UI 基础框架
- [x] 自绘标题栏 + 拖拽 + 最大化/最小化
- [x] 三段布局（标题栏 + 模式栏 + 内容 + 状态栏）
- [x] 默认最大化 + 最小尺寸 1366×768
- [x] DI 容器 + NLog 日志

### ✅ 阶段 2：数据层 + EPE API
- [x] 数据库实体对齐 EPE API（HoldTask / HoldTaskDetail 等）
- [x] EpeApiClient 实现 9 个 API（除 3.10/3.11 业务字典/异常上传）
- [x] HeartbeatService 后台心跳（按 API 文档 5 分钟一次独立线程）
- [x] EpeTaskApiService（粘合 API + 数据库）

### ✅ 阶段 3：完整主界面
- [x] 三模式统一主界面（在线 / 离线 / Excel 导入）
- [x] **共用控件**：顶部数据卡 + 手动控制（设定电压/试验时间/保护电流）
- [x] **离线模式独有**：4 路回路卡片
- [x] **在线 / Excel 模式独有**：任务明细切换（上一条/下一条）
- [x] **Excel 模式独有**：横幅显示已加载文件 + 重新导入按钮
- [x] 串口仪器面板（高压表 + 温湿度仪）
- [x] 试验信息面板 + 环境数据 + 状态栏
- [x] 操作栏按模式差异化显示（任务下载/上传 vs 重新导入 vs 选产品型号）

## 项目结构

```
PressureTestApp/
├── src/
│   ├── PressureTestApp.Core/             # 业务核心
│   │   ├── Configs/   Helpers/   Models/   Services/
│   ├── PressureTestApp.Data/             # 数据层（金仓 KES）
│   │   ├── Entities/                     # 8 张表实体（用户/项目/车列/产品/任务/明细/离线记录/计数器）
│   │   ├── Repositories/                 # 5 个仓储
│   │   └── KingbaseContext.cs
│   ├── PressureTestApp.Communication/    # 通信层
│   │   ├── Api/                          # ★ EpeApiClient + HeartbeatService + EpeTaskApiService
│   │   ├── Plc/                          # S7Communicator + Mock
│   │   ├── Modbus/                       # NModbus 串口仪器
│   │   ├── Excel/                        # NPOI Excel 导入
│   │   └── FaceAuth/                     # 人脸识别占位
│   └── PressureTestApp.UI/               # Avalonia UI
│       ├── Styles/   ViewModels/
│       └── Views/                        # 闪屏 + 登录 + 主窗体（三模式统一）
└── README.md
```

## 三模式行为说明

### 在线模式（WorkMode.Online）
- 数据来源：EPE API（findHoldTaskList → findHoldTaskDetail）
- 显示「任务明细切换区」：上一条 / 下一条
- 操作栏：任务下载、任务上传
- 完成后通过 pullHoldTask 上传

### 离线模式（WorkMode.Offline）
- 数据来源：本地产品型号库
- 显示「4 路回路卡片」（点击修改电压/时间）
- 操作栏：选择产品型号
- 数据保存到本地 OfflineRecord 表

### Excel 导入模式（WorkMode.ExcelImport）
- 数据来源：Excel 模板（NPOI 解析）
- 显示「Excel 横幅」+「任务明细切换区」
- 操作栏：重新导入 Excel
- 数据保存到本地，不上传

### 三模式共用
- 顶部数据卡（电压/电流/剩余时间/状态/当前任务）
- 手动控制（设定电压、试验时间、保护电流）
- 耐压曲线 + 试验信息 + 串口仪器面板

## 编译运行

```bash
cd PressureTestApp
dotnet restore
dotnet build
dotnet run --project src/PressureTestApp.UI
```

## 默认账号

- **工号**：`admin`
- **密码**：`admin`

## 配置文件位置

- Windows：`%LocalAppData%\PressureTestApp\`
- Linux：`~/.local/share/PressureTestApp/`

包含：`database.ini` / `plc.ini` / `AuthenticationConfig.ini` / `ProductionSystemConfig.ini` / `app.ini` / `instruments.json`

## EPE 接口配置（与旧版一体式耐压机一致）

`AuthenticationConfig.ini`（设备认证结果）：
```ini
认证编码=
是否开启认证功能=是
```

`ProductionSystemConfig.ini`（各接口完整 URL，与旧版 ProductionSystemConfig.ini 键名一致）：
```ini
设备授权验证接口=http://10.76.11.40:8070/epe/interface/deviceAuthorize.act
心跳接口=http://10.76.11.40:8070/epe/interface/heartbeat.act
获取耐压任务接口=http://10.76.11.40:8070/epe/interface/findHoldTaskList.act
耐压任务明细接口=http://10.76.11.40:8070/epe/interface/findHoldTaskDetail.act
耐压任务结果回传接口=http://10.76.11.40:8070/epe/interface/pullHoldTask.act
...
```

认证成功后「认证编码」由程序自动写入；任务下载等接口使用该编码作为 `deviceNumber`。

## 数据库快速搭建（开发期）

PostgreSQL（本机调试）：
```sql
CREATE DATABASE pressuretest WITH ENCODING 'UTF8';
```

`database.ini` 改：
```ini
Port = 5432
User = postgres
Password = 你的密码
Database = pressuretest
```

启动后会自动建出 8 张表 + admin/admin。

## 下一阶段（阶段 4/5/6）

- [ ] 阶段 4：S7 PLC 真实试验流程 + 试验引擎（按设定电压升压、采集 testProcessValues、超保护中止）
- [ ] 阶段 5：任务下载页（按车型/车列号筛选）+ 任务上传页 + Excel 模板导入页
- [ ] 阶段 6：历史报表 + 详情曲线回放 + 用户管理 + 产品型号管理 + PDF 导出

---

**版本**：v2.0.0-stage3
