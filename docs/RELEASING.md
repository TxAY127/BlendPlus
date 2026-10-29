# 发布到 GitHub

项目已准备源码和发布包，本地打包不会创建远程仓库或自动上传。

## 首次上传

1. 在自己的 GitHub 账号下创建空仓库，例如 `BlendPlus`。
2. 将本项目目录中的文件上传到仓库根目录，包含 `.github`、`.gitignore` 等文件；不要上传 `artifacts/`。
3. 确认默认分支是 `main`，查看 Actions 的构建结果。

使用 Git 时，也可在项目根目录执行下列命令；`<你的仓库地址>` 需替换为自己创建的仓库地址。

```powershell
git init -b main
git add .
git commit -m "Prepare BlendPlus experimental release"
git remote add origin <你的仓库地址>
git push -u origin main
```

## 发布当前实验版

1. 在 Windows 运行 `scripts/Package.ps1`，检查 `artifacts/` 下的 Windows 包、源码包及 `SHA256SUMS.txt`。
2. 参照 `VALIDATION.md` 记录实际验证状态；未验证项继续明确标注。
3. 在 GitHub Releases 创建 **pre-release**，标签为 `v0.4.0-alpha.1`；附件上传两个 ZIP 和校验文件。

如果从本地推送版本标签，`release.yml` 会构建、运行模拟测试，并创建 **草稿预发布**，供维护者检查后手动发布：

```powershell
git tag v0.4.0-alpha.1
git push origin v0.4.0-alpha.1
```

该流程不替代人工界面验证。仓库需要允许 GitHub Actions 使用默认 `GITHUB_TOKEN` 写入 Releases，无需在源码内放入个人令牌。

## 后续版本

同时更新 `VERSION`、`src/BlendPlus/AssemblyInfo.cs`、`README.md` 和 `CHANGELOG.md`。标签必须与 `VERSION` 一致，工作流会校验。

发布包与仓库只包含项目原创内容。请勿添加本机备份、厂商 DLL、原始 QML、私人媒体、草稿工程或带个人路径的日志。
