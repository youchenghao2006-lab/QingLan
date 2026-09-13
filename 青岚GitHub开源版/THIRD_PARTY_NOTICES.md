# 第三方软件与内容说明

根目录 MIT 许可仅覆盖本项目可授权的自有部分，不替代第三方软件、系统字体或在线文章的许可。

## 开发依赖

| 组件 | 当前版本 | 许可来源 |
| --- | --- | --- |
| Microsoft.WindowsAppSDK | 1.8.260317003 | NuGet 包内 `license.txt`（Microsoft Software License Terms） |
| Microsoft.Windows.SDK.BuildTools | 10.0.26100.7705 | NuGet 元数据指向的 [Windows SDK 许可](https://aka.ms/WinSDKLicenseURL) |
| .NET SDK / Runtime | .NET 10 | [dotnet/runtime 许可](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) 及随发行版附带的第三方声明 |

源码仓库仅引用上述包，不包含包缓存、运行时或微软 SDK 二进制文件。依赖的传递组件也保留各自许可。Windows App SDK 仓库的开源许可与 NuGet 成品 SDK 许可不能简单视为同一个许可。

如分发自包含的运行版，请检查所用版本 NuGet 包和发布输出中的全部许可、第三方声明及再分发要求，并保留所需声明；不要将整个发布目录标成“全部文件都是 MIT”。本次交付只整理源码，不附带第三方运行时安装包。

项目链接：[Windows App SDK](https://github.com/microsoft/WindowsAppSDK)、[Windows App SDK 文档](https://learn.microsoft.com/windows/apps/windows-app-sdk/)。

## 在线文章

程序通过 [中文维基文库 API](https://zh.wikisource.org/w/api.php) 在运行时取文，显示来源并提供原文与版权说明链接。网络请求和使用应遵守站点的 [使用条款](https://foundation.wikimedia.org/wiki/Policy:Terms_of_Use)。

作品、整理内容和不同地区的版权状态可能不同。阅读界面显示的作者通过分类推断，可能缺失或不准确，请以原文核实。源码不附带下载文章，不将在线作品重新许可为 MIT；转载、打包或商业使用文章前需单独确认其权利状态。

## 本仓库素材

- `Assets/builtins.json`：本次整理时 AI 辅助新写的三篇校园主题演示文本，没有复制来源不确定的原版 60 篇文章。适用项目 MIT 的范围以依法可授予的权利为限；不保证 AI 文本在所有地区均具有独占版权。
- `Controls/Landscape.xaml`：项目内的 XAML 几何山水装饰。
- `Assets/AppIcon.ico`：由 `scripts/New-Icon.ps1` 的几何绘制生成，书本、波纹与太阳组成，没有使用第三方图片或商标。图标生成源码随仓库提供。
- 字体：仅按名称请求系统的宋体、楷体、仿宋及 UI 字体；不附带字体文件。没有相应字体时由系统回退。字体名称引用不代表取得字体文件的再分发许可。
- 测试数据：测试程序运行时生成的虚构内容，不包含用户笔记或校园日程。

语音客户端、声音资源、协议实现和相关测试不包含在此版本中。
