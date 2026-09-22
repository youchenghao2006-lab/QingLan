# 青岚 · 书课之间

一个面向 Windows 桌面的阅读与校园生活小工具。以蓝绿色、山水线条和中文阅读字体，把每日散文、随心记、课表和备忘录放在一起。

采用 C# / .NET 10 / WinUI 3，界面、业务逻辑和本地存储分层组织。本仓库为独立整理的 MIT 开源版，不包含语音朗读功能、个人数据或原版来源不明的 60 篇离线文章。

## 功能

- 每日从中文维基文库获取文章，支持刷新、已下载文章离线阅读、收藏及往日文章浏览。并非全网搜索引擎。
- 随心记独立保存、查看和删除；从写下后的第 3–10 天中选择日期，以“时光来信”再次出现，保留写作日期。日期冲突会顺延，需在应用中查看，不是后台推送。
- 宋体、楷体、仿宋和四档字号，选择后保存；支持全屏和阅读进度。
- 先设置学期起止日期，再按周查看课程；支持自定义时段、课程、老师、教室和单双周/指定周次。
- 备忘录支持时点与时间段；默认展示开始时间最早的三项未删除事件，包含待处理的过期事件；确认删除后不再出现。
- 对应日期的课表星期标题显示醒目的橙色下划线，跨天事项会标记涉及的日期。

校园功能详细操作见 [校园日程](docs/校园日程.md)。

## 开发环境

- Windows x64；建议在 Windows 11 上开发和测试。目前仅提供 x64 配置，未验证 ARM64 或其他系统。
- Visual Studio 2026，安装 WinUI / Windows 应用开发所需组件，包含 .NET 桌面工具与 Windows SDK 10.0.26100。
- .NET SDK 10.0.401（`global.json` 允许同一 feature band 的补丁更新）。
- 首次还原需要访问 NuGet.org。

项目当前引用 Windows App SDK `1.8.260317003` 和 SDK BuildTools `10.0.26100.7705`。运行时按自包含、非 MSIX 方式构建，不需要证书、开发者模式或在线服务密钥。系统最低目标声明是 Windows 10 1809，但这不是对所有旧版系统的兼容性验证。

## 打开和运行

1. 用 Visual Studio 2026 打开根目录 `QingLan.slnx`。
2. 将 `src/QingLan` 设为启动项目，平台选择 `x64`。
3. 等待还原完成，按 F5；或者使用下面的发布脚本。

在仓库根目录的 PowerShell 中运行：

```powershell
dotnet --version
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish.ps1
.\artifacts\publish\QingLan.exe
```

`ExecutionPolicy Bypass` 仅用于本次脚本进程，不修改系统策略；先阅读脚本再执行。若设备有组织策略限制，请遵循管理员要求。

测试是两个独立控制台测试程序，失败返回非零退出码，不使用 `dotnet test`。它们只生成临时虚构数据，不读取你的真实笔记，也不需要连接在线文库。发布结果在 `artifacts/publish`，分享运行版需复制整个目录，不能只拿出其中的 EXE。发布脚本自动附带项目许可、说明，以及已还原 NuGet 包提供的许可文件、声明和许可元数据。

GitHub Actions 在推送和 Pull Request 时运行测试、构建 Windows x64 程序，并检查发布包的许可文件。Dependabot 每周检查 NuGet 和工作流依赖，更新通过 Pull Request 审阅。

## 目录结构

```text
src/QingLan/
  Core/          文章获取、随心记/收藏存储、校园日程规则
  Views/         阅读、首页、集合、随心记编辑和校园页面
  Controls/      XAML 山水装饰
  Assets/        三篇演示文章、可再生成的图标
tests/           阅读与校园领域逻辑回归检查
scripts/         测试、发布、图标生成
docs/            使用、隐私和资源说明
```

## 数据与网络

开源版默认数据目录是 `%LOCALAPPDATA%\QingLanCommunity`，与原程序的 `QingLanReader` 分开。个人收藏、随心记、课表与备忘录不会随源码附送；开源版默认也不读取旧版 `DailyEssayApp` 数据。

应用启动、跨日或手动刷新时向中文维基文库请求文章；不能保证外部接口在所有网络环境下可用。无法联网时展示缓存或三篇演示文章。三篇演示为本次整理时 AI 辅助新写的文本，不是网络转载；不冒充真实作者作品。

应用自身不上传随心记或校园日程。详见 [隐私与数据](docs/PRIVACY.md)。在线文章的版权与授权以原文页面为准，不能因程序采用 MIT 就当作所有文章都允许自由再发布。

## 贡献与许可

项目自有代码、文档、几何图形和新写的演示文本按 [MIT](LICENSE) 提供（在依法享有相关权利的范围内），允许商用；第三方软件和在线文章不因此变更许可。参见 [第三方与资源说明](THIRD_PARTY_NOTICES.md) 及 [贡献指南](CONTRIBUTING.md)。

发布前请检查提交中没有 `.vs`、`bin`、`obj`、本地数据库、密钥、私人截图或下载文章。运行版放 GitHub Releases，源码仓库不应包含编译产物或 NuGet 缓存。项目仓库：[youchenghao2006-lab/QingLan](https://github.com/youchenghao2006-lab/QingLan)。
