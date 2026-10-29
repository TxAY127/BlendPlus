# BlendPlus · 剪映混合模式扩展

为 Windows 版剪映添加 15 种实验性混合模式，提供简洁的安装、恢复界面，并记住便携版安装路径。

**当前版本：0.4.0-alpha.1。尚未实现完整 AE 38 种模式，也未完成与 AE 的逐模式一致性验证。菜单滚动修正待实际界面验证。**

## 使用

1. 从仓库的 **Releases** 下载 Windows 压缩包，解压并运行 `BlendPlus.exe`。
2. 保存工程，完全退出剪映，选择对应版本，点击“安装”或“重新安装”。
3. 重新打开剪映，在“画面 → 基础 → 混合”中选择新增模式。点击工具中的“恢复”可移除扩展。

未检测到版本时，点击“浏览…”选择 `JianyingPro.exe`；仅在提示无写入权限时，以管理员身份运行工具。

## 新增模式

| 类型 | 模式 |
| --- | --- |
| 运算 | 差值、排除、线性减淡（添加）、相减、相除 |
| 对比 | 线性光、亮光、点光、纯色混合 |
| 颜色选择 | 较深的颜色、较浅的颜色 |
| 颜色分量 | 色相、饱和度、颜色、发光度 |

这 15 种是额外添加的模式，剪映原有模式继续保留；不能将数量理解为完整 AE 模式列表。

## 兼容性与验证状态

| 环境 / 功能 | 当前状态 |
| --- | --- |
| Windows 10 / 11，.NET Framework 4.8 | 构建与运行目标；未完成所有系统版本实测 |
| 剪映 9.7.1.13726 | 已验证扩展资源安装；差值的纯色预览和视频导出曾通过抽样检查 |
| 剪映 8.9.0.13361 | 已验证资源结构与安装；图像效果未逐项验证 |
| 菜单 8 行上限与滚轮 | 已实现补丁加载修正，实际滚动效果待用户验证 |
| 其他版本 | 根据资源结构检查，结构不匹配会拒绝安装，不保证兼容 |

差值的抽样验证不能代表其他模式、透明度、色彩空间或复杂工程均与 AE 一致。当前只改写 GLES 混合着色器，其他渲染后端未适配。轮廓 Alpha、模板 Alpha、溶解等剩余 AE 模式尚未实现。

工具会修改所选剪映的混合资源及外部 Qt 界面文件，不修改厂商签名 DLL。同一 Windows 用户的共享混合资源也可能被其他剪映版本使用。备份和路径记录保存在 `%LOCALAPPDATA%\BlendPlus`；剪映升级后应重新检查兼容性。

## 从源码构建

Windows 自带的 Windows PowerShell 5.1 或 PowerShell 7，以及 .NET Framework 4.8 即可；脚本无需下载依赖。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Package.ps1
```

产物位于 `artifacts/`。自动测试仅使用临时模拟资源，不启动剪映、不修改真实安装。也可用 Visual Studio 打开 `src/BlendPlus/BlendPlus.csproj`，需安装 .NET Framework 4.8 开发工具。

## 开发与发布

- [实现说明](docs/DEVELOPMENT.md)
- [人工验证清单](docs/VALIDATION.md)
- [GitHub 发布步骤](docs/RELEASING.md)
- [更新记录](CHANGELOG.md)

提交 Issue 时请提供剪映完整版本号、系统版本、复现步骤和结果；附图前请隐藏个人路径及工程内容。

本项目为独立实验项目，与剪映、字节跳动、Adobe 无隶属或背书关系。项目原创代码采用 [MIT 许可证](LICENSE)，不分发剪映、AE 或 Qt 的厂商程序及资源。
