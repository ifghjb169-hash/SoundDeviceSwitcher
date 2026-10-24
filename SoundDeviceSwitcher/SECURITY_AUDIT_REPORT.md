# Sound Device Switcher 源码安全审核与修复报告

**审核日期：** 2026-10-13  
**审核对象：** 用户上传的 `New project 2.zip`，解包后 `New project 2/`，包含工作树、Git 历史与一份既有 EXE。  
**修复边界：** 原始包内已有的未提交修改（`src/AppSettings.cs`、`src/Localization.cs`、`src/MainForm.cs`）被完整保留。第一轮仅执行不需要产品取舍的加固；**开发者在后续明确选定方案 A，现已落实**。不覆盖上游 Git 仓库，也不重新部署或签名可执行文件。  
**结论：** 已根据开发者选择的方案 A 消除 EXE 同目录 DLL 自动旁加载；同时修复了原生依赖搜索路径、加载失败资源清理和 CI 检出凭据持久化问题。Windows 构建与真实运行尚未验证，因此**不能宣布发布产物已通过完整安全验收**。

## 1. 项目与审计范围

- 类型：本地 **Windows GUI** 桌面程序。主要语言 **C#**，Windows Forms，Win32 CoreAudio COM/PInvoke，Voicemeeter Remote API 原生 DLL，PowerShell 构建脚本，GitHub Actions CI/CD。
- 构建：`build.ps1` 使用 Windows 系统的 `.NET Framework` 64 位 `csc.exe` 编译源码。没有 `.csproj`、`.sln`、NuGet `packages.config`、`PackageReference`、`packages.lock.json`，也没有 `package.json`、`requirements.txt` 或其他语言包依赖清单。
- 直接 **NuGet/第三方托管包：0**。间接包锁版本：**无锁文件，无法列出**。系统依赖（非 NuGet）：.NET Framework/WinForms、Windows CoreAudio 与注册表（真实修补版本由目标机器决定）；外部原生依赖 `VoicemeeterRemote64.dll`（**包中未提供，具体版本待确认**）。
- GitHub Actions 外部调用 **4 个独立 Action**：`actions/checkout@v4`（调用 2 次）、`github/codeql-action@v3`（init/analyze）、`actions/attest-build-provenance@v2`、`softprops/action-gh-release@v2`。这些都是**浮动标签**，不能从文件推断某次实际执行的补丁版本，也不能独立列出 Action 内部打包的确切间接依赖。未发现可疑的拼写仿冒包。
- 审计：全部 `src/*.cs`（9 个文件、约 3,584 行）、`build.ps1`、两条工作流、Dependabot 配置、README、历史报告、Git 暂存工作树和 Git 可达历史对象、提交忽略设置、原有 EXE 的静态特征。没有执行原有 EXE 和任何不受信任的 DLL。
- 不适用的风险面：无 HTTP 服务器、网页模板、SQL/NoSQL、互联网请求、上传/解压功能、JS `eval`、Electron IPC、Android WebView 等可用入口，故 XSS、CSRF、SSRF、SQL 注入不构成当前代码可确认的问题；未来加功能时须重新审核。

## 2. 发现汇总

| 编号 | 类别 | 严重级别 | 位置（修复版行号） | 状态 |
|---|---|---|---|---|
| S-01 | 代码 | **High（有条件）** | `src/VoicemeeterRemoteClient.cs:334-380` | **已修复（方案 A）**：禁止 EXE 同目录原生 DLL，限定 Program Files 安装目录并拒绝重解析点 |
| S-02 | 代码 | Medium | `src/VoicemeeterRemoteClient.cs:289-293,430-431` | **已修复**：收紧原生 DLL 依赖搜索路径 |
| S-03 | 代码 | Low | `src/VoicemeeterRemoteClient.cs:300-320` | **已修复**：缺失导出时释放 DLL 句柄 |
| S-04 | 代码/CI | Medium | `.github/workflows/codeql.yml:25-28`、`release.yml:19-22` | **已修复**：禁止将检出 token 持久化到 `.git` |
| S-05 | 依赖/CI | Medium | `codeql.yml:26,31,41`、`release.yml:20,42,47` | **待开发者决定**：Actions 使用浮动标签，缺 SHA 锁定 |
| S-06 | 代码/CI | Medium | `.github/workflows/release.yml:9-16,18-49` | **待开发者决定**：构建阶段拥有 Release 写权限，建议拆分发布任务 |
| S-07 | 依赖/CI | Low | `.github/workflows/codeql.yml:31,41` | **待开发者决定**：CodeQL Action v3 将在 2026-12 停止更新，迁移 v4 属于主版本升级 |
| S-08 | 代码 | Low | `src/AppSettings.cs:108-140` | **建议关注**：本地 settings.ini 无体积限制、非原子写入；必须本地可修改配置才易触发 |
| S-09 | 交付/依赖 | Medium | 原始上传 `bin/SoundDeviceSwitcher.exe` | **建议关注**：原有 EXE 未因源码修复而自动更新；真实版本与二进制构建信息待确认 |
| S-10 | 密钥 | Low（未发现泄露） | `.gitignore:9-16`、全部源码/历史/二进制 | **建议关注**：离线扫描无命中，不等于证明从未泄露；无可替换凭据 |

### S-01：应用目录原生 DLL 优先加载（已修复，方案 A）

**原问题（原始源码）：** `FindVoicemeeterRemoteDll()` 先检查 `AppDomain.CurrentDomain.BaseDirectory/VoicemeeterRemote64.dll`，因此攻击者只要能够写入 EXE 所在目录，就可放置同名恶意 DLL，使程序在运行时加载，可能以当前用户（若提权运行则为管理员）权限执行恶意代码。

**开发者已批准方案 A；本次修复：** 仅使用 `Environment.GetFolderPath` 查找 Windows `ProgramFilesX86` 和 `ProgramFiles` 下的 `VB\Voicemeeter\VoicemeeterRemote64.dll`。不再检查 EXE 所在目录、工作目录、`PATH` 或自定义路径；发现安装根目录、厂商目录、产品目录或 DLL 存在 NTFS 重解析点时立即拒绝加载，防止明显的路径重定向。两个正式安装位置均不存在 DLL 时抛出清晰错误，不退回便携模式。

**风险余量：** 此项仅验证安装位置，不验证签名、发布者或内容。若 Program Files 权限被错误放宽、攻击者已获得更高权限，或加载时出现目录内容竞争替换，风险依然存在。安装非标准目录会失去 Voicemeeter 联动能力，属于用户批准的方案 A 的兼容性取舍。

**选项 A（推荐，偏安全）：** 仅从官方 Voicemeeter 安装目录加载，不再接受 EXE 同目录的 DLL；减少便携部署兼容性。  
**选项 B（折中）：** 保留便携加载，但要求经明确配置开启且校验 Authenticode 签名/发布者，再结合防重解析点和信任策略；实现较复杂，需要 Windows 真机测试。  
**选项 C（保持兼容）：** 继续允许旁加载，规定只有可信、不可被低权限用户写入的程序目录才允许部署；保留 High 条件风险。

**最终选择：** 开发者已明确批准 **A**，本修复包实现强制官方 Program Files 目录查找，明确取消便携同目录回退。其余 B、C 保留为历史决策选项，并非当前构建启用的行为。

### S-02：原生 DLL 依赖搜索路径（已修复）

**原问题：** 使用 `LoadLibrary(完整路径)` 时，主 DLL 的间接依赖可能仍走 Windows 默认 DLL 搜索顺序，包含当前工作目录、`PATH` 等潜在不可信目录。**触发前提：** 主 DLL 引用可被攻击者在搜索目录放置的非 KnownDLL 依赖项，且该依赖尚未加载。**修复：** 用 `LoadLibraryExW(完整路径, NULL, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32)`，依赖仅从已选 DLL 同目录或 Windows System32 寻找；Windows 10/11 支持该加载标志。**剩余：** DLL 同目录自身如果可被攻击者修改，仍然不可信；`VoicemeeterRemote64.dll` 内部自行加载的 DLL 不受此次单次 LoadLibraryEx 搜索限制。

### S-03：加载部分导出后失败（已修复）

**原问题：** 取得 DLL 句柄后绑定某个导出失败，会抛异常，但未立即释放新加载的模块；后续重试可能保留资源和旧模块。**触发前提：** DLL 版本不匹配、缺少所需导出。**修复：** 将所有 `GetDelegate` 调用纳入 try/catch，失败时调用 `FreeLibrary`、清零句柄、原样重抛异常。

### S-04：检出 GitHub Token 留存（已修复）

**原问题：** `actions/checkout@v4` 默认 `persist-credentials: true`，CI 运行期间 GitHub 凭据可留在 checkout Git 配置中。Release 作业有 `contents: write`，构建代码、工具若被攻破可能读到更高权限凭据。**触发前提：** 工作流其他步骤被攻击者控制且能访问 checkout 文件。**修复：** CodeQL 与 Release 两个 checkout 均设置 `with: persist-credentials: false`；不需要 `git push` 的工作流维持原功能。**剩余：** Release action 自身仍需要 GitHub token；应进一步隔离发布权限（S-06）。

### S-05：工作流 Action 浮动版本（待决定）

**原问题：** `@v2/@v3/@v4` 标签可以被变更，带写权限的 Release 作业存在供应链风险；仓库没有固定 SHA 或工作流依赖锁。**利用条件：** Action 上游遭到攻击或移动主版本 tag，工作流随后执行。**建议：** 对官方 Action 和 `softprops` 均使用来源已核验的完整 40 位提交 SHA（保留版本注释），配合升级 PR 与固定 SHA 的人工审查。**升级本身可能更改 Node/runner 兼容性和 Action 行为**，故未做未经确认的替换。

### S-06：发布流水线权限隔离（待决定）

`release.yml` 在单一 `build` job 给出了 `id-token: write`、`contents: write`、`attestations: write`，编译步骤因此与实际发布步骤共享较高权限。**前提：** 提交者将恶意代码带入构建步骤，或构建依赖受损。**选项 A（推荐）：** `build` job 只授予 `contents: read`，产出带 SHA256 的 artifact；单独的 `release` job 在发布时才授予 `contents: write`、`id-token: write`、`attestations: write`，且加入受保护环境审批。**选项 B：** 保持单 job 与现有手动发布能力，接受额外暴露面。拆分 jobs 将调整发布流程，需要开发者批准与 GitHub CI 验证。

### S-07：CodeQL v3 即将停更（待决定）

GitHub 已公告 CodeQL Action v3 将在 **2026 年 12 月** 停止维护并建议迁移 v4。这是 **主版本升级**，原工作流仍在使用 `init@v3/analyze@v3`。建议批准后统一更换为 `@v4` 并在 Windows runner 上核验；不应把当前使用 v3 误报为某个已确认漏洞。

### S-08：settings.ini 的本地稳健性（建议关注）

程序在用户 `%APPDATA%\SoundDeviceSwitcher\settings.ini` 使用 `File.ReadAllLines()` 全量读取、直接覆盖式 `StreamWriter` 保存。恶意大型文件可能导致启动异常或耗尽内存；突然断电可能截断设置文件。**触发条件：** 可以写入当前用户设置目录；该目录本就位于该用户的受控范围，暂无跨用户权限边界证据。建议评估 1 MiB 上限和原子替换策略，但这会改变错误处理/兼容性，未擅自修改。

### S-09：旧构建产物不是新修复版（建议关注）

压缩包携带原 `bin/SoundDeviceSwitcher.exe`，它未由本次更改重新构建，不能验证与修复后的源代码一致。**交付决定：** 修复版归档将排除 `.git/` 和 `bin/` 中旧 EXE，避免用户误运行未更新程序。部署前须在 Windows 10/11 上编译、手动进行 Voicemeeter 正式安装和对 EXE 同目录 DLL 拒绝加载的边界测试、核验签名/哈希并重新生成 GitHub Attestation。

## 3. 依赖漏洞交叉核对

| 依赖/动作 | 声明版本 | 可确认的已知风险与适用性 | 本次行为 |
|---|---|---|---|
| Windows .NET Framework / WinForms | OS 实装版本**待确认** | 由 Windows/.NET Framework 更新服务修补；目标机补丁状态未知，无法凭源码给出受影响构建号 | 不擅自改平台 |
| VB-Audio Voicemeeter Remote DLL | 未捆绑，版本**待确认** | 包中无 DLL 可哈希/校验；需核验 VB-Audio 供应来源、签名与实际版本 | 收紧依赖路径；已按 A 限定 DLL 来源于 Program Files 安装目录 |
| `actions/checkout` | `@v4`（浮动） | 没有在此版本范围确认必然命中、可重现的 Critical/High 漏洞；仍有标签可变风险 | 禁止凭据持久化；未升级主版本 |
| `github/codeql-action` | `@v3`（浮动） | **GHSA-vqf5-2xx6-9wfm / CVE-2025-24362**：影响 3.26.11–3.28.2 等，修于 **3.28.3**；**本项目仅扫描 C#，没有 Kotlin / Java 调试 artifact 配置，不能认定满足实际利用条件**。v3 计划 2026-12 停更。 | 暂不升级 v4，等待批准 |
| `actions/attest-build-provenance` | `@v2`（浮动） | **GHSA-pf56-329r-95rw / CVE-2026-59891** 涉及底层 `@sigstore/oci <0.7.1`；需指定不可信 OCI 目标且 `push-to-registry: true` 才能触发。此项目只提交 `subject-path: dist/*`，**未使用受影响功能**；打包的精确间接版本待确认。 | 不擅自升级主版本 |
| `softprops/action-gh-release` | `@v2`（浮动） | v2 仍有维护发布；未在已审查的权威公开结果确认此配置的 Critical/High 可利用项，未解析确切补丁版 | 保留，建议锁 SHA |

**本地审核命令限制：** 仓库无 `package.json` / `packages.lock.json` / `.csproj`，因此 `npm audit`、`pip-audit`、`dotnet list package --vulnerable` 等应用依赖审计无对象可运行。操作环境无 `dotnet`、`csc`、`mono`、`powershell`/`pwsh`；不得将“未运行”写成“通过”。Action 内部依赖的版本也无法从移动标签离线还原。

**权威资料（已核验）：**
- GitHub CodeQL 安全公告：https://github.com/advisories/GHSA-vqf5-2xx6-9wfm
- Sigstore OCI 安全公告：https://github.com/advisories/GHSA-pf56-329r-95rw
- GitHub CodeQL v3 停更说明：https://github.blog/changelog/2025-10-28-upcoming-deprecation-of-codeql-action-v3/
- GitHub Actions 安全最佳实践：https://docs.github.com/en/actions/reference/security/secure-use
- Microsoft `LoadLibraryEx` 文档：https://learn.microsoft.com/en-us/windows/win32/api/libloaderapi/nf-libloaderapi-loadlibraryexw
- Microsoft `Environment.GetFolderPath` 文档：https://learn.microsoft.com/en-us/dotnet/api/system.environment.getfolderpath
- Windows DLL 搜索顺序：https://learn.microsoft.com/en-us/windows/win32/dlls/dynamic-link-library-search-order
- .NET Framework 支持/更新：https://learn.microsoft.com/en-us/dotnet/framework/install/

## 4. 密钥与凭据检测

- 对工作树源码、CI、配置、README/历史报告、`.git` 可达历史对象 **26 个 Git blob**、原 EXE 中的可打印 ASCII / UTF-16 候选内容进行了离线启发式扫描。
- 检查 PEM 私钥头、`ghp_`/`github_pat_`、`sk-`、`AKIA`/`ASIA`、`AIza`、通用 password/token/key 赋值、Discord/Slack Webhook 等模式：**未发现命中**。这不是完整的历史凭据泄露保证。只发现正常的公开 GitHub 仓库/发布 URL。
- 没有 `.env`、明文 Token/密码或数据库连接串；`AppSettings.cs` 明文保存的是**声音设备 ID 和语言偏好，不是鉴权凭据**。`.gitignore` 已忽略 `.env`、`.env.*`、私钥与证书扩展名，已跟踪文件中没有发现被忽略的凭据文件。
- **未添加 `.env.example`：** 当前项目根本没有任何需要从环境变量读取的密钥名称；添加虚假的环境变量会误导开发者。如果未来接入 API Key，必须迁至 Windows 安全凭据存储/环境变量，补充仅含变量名的 `.env.example`，并将示例文件加入白名单规则（因为现有 `.env.*` 会匹配 `.env.example`）。
- **如曾向 Git、截图、发布包、日志或第三方披露真实密钥，必须前往所属服务商作废并重新生成；仅在代码中删掉或改为环境变量并不能使已经泄露的密钥失效。**

## 5. 修改明细（只列本次直接修复）

1. `src/VoicemeeterRemoteClient.cs:12-16,289-293,430-431`：改成 `LoadLibraryExW` 并显式限制原生依赖目录。避免因 `PATH` 或当前工作目录攻击者控制而载入依赖 DLL。
2. `src/VoicemeeterRemoteClient.cs:300-320`：导出函数未找到时立即释放 DLL 句柄，防止半初始化模块保留和资源泄漏。
3. `.github/workflows/codeql.yml:25-28`：checkout 增加 `persist-credentials: false`。
4. `.github/workflows/release.yml:19-22`：checkout 增加 `persist-credentials: false`。
5. `tests/test_security_regressions.py`：从 5 个增至 7 个零第三方 Python 标准库静态防回归测试，新增仅官方安装目录和重解析点拒绝逻辑检查。
6. `README.md`：更新为方案 A 的运行要求、移除同目录 DLL 加载说明、提醒非默认安装位置不再自动兼容。
7. 保留原始上传中 3 个已有的未提交源码修改，不回退用户现有声音路由功能。
8. `src/VoicemeeterRemoteClient.cs:334-380`：按用户批准的方案 A 移除 EXE 同目录 DLL 回退，使用 Windows 特殊文件夹定位两个正式安装根目录，并拒绝 junction/符号链接。

## 6. 验证与局限

| 项目 | 结果 |
|---|---|
| 安全回归静态测试 | **7/7 通过**：`python -m unittest discover -s tests -v`（方案 A 后重新运行） |
| 两份 GitHub Workflow YAML 语法解析 | **通过**：使用 PyYAML 的 BaseLoader 检查 jobs 与 `with` 字段存在 |
| 原生 DLL/PInvoke Windows 真机功能测试 | **未执行**：没有 Windows 和 Voicemeeter |
| 项目已有 Windows `build.ps1` 构建 | **未执行成功**：本环境无 PowerShell/.NET Framework CSC，命令 `powershell` not found |
| 原项目自动化测试 | **无测试文件/测试命令**（仅新增静态护栏，不能代替可运行程序验证） |
| `dotnet list package --vulnerable` / npm / pip-audit | **不适用**：没有对应依赖清单及包管理器项目 |
| 新 exe 签名、运行、哈希、CI Attestation | **未执行/待确认**，不能复用旧 EXE |

## 7. 开发者下一步决策（按优先级）

1. **DLL 加载源方案 A 已获批准并完成源码修复；无需再次决定。** 发布前验证标准/非系统盘 Program Files 安装、拒绝 EXE 同目录 DLL、拒绝符号链接。
2. **是否同意重构 Release workflow 为读权限构建、写权限发布的两个 job，并使用受保护环境审批？** 推荐同意；需要 GitHub 端验证制品传输和 Attestation 绑定。
3. **是否同意将 CodeQL v3 升为 v4，所有 Actions 解析经过验证的精确 SHA，固定 Windows runner 版本？** 推荐同意，并通过单独升级 PR/CI 验证；大版本未擅自替换。
4. **是否限制 `%APPDATA%` settings.ini 体积并改原子写入？** 推荐作为低优先级稳定性改进，先确认是否允许舍弃异常巨大设置文件。
5. 发布前在 Windows 上执行 `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1`，运行 GUI、系统录放默认切换、Voicemeeter 设备切换/重启、语言切换以及 Windows 10/11 多终端回归，并为**新产物**生成签名/哈希/来源证明。

---
**待确认：** 生产机器的 Windows/.NET Framework 修补级别、实际安装的 Voicemeeter DLL 版本/签名、正在使用的 Actions 精确补丁版本、旧 EXE 与现有源码的关联、Windows 动态链接实际兼容性、在线 GitHub CI 成功情况。
