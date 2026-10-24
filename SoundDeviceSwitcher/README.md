# Sound Device Switcher

一个用于 Windows 的小工具，包含两个页面：

- `系统默认设备`：列出当前电脑的播放设备和录音设备，点击某个设备后把它设置为 Windows 默认声音设备，并在设备前显示勾选标记。
- `Voicemeeter`：列出 Voicemeeter Remote API 能看到的输入/输出设备，点击输入设备会设置 `Hardware Input 1`，点击输出设备会设置 `A1`。

默认策略：第一次运行时，如果还没有保存过用户选择，程序会尝试把 Windows 默认输出设置为 `VoiceMeeter Input (VB-Audio VoiceMeeter VAIO)`，默认输入设置为 `VoiceMeeter Output (VB-Audio VoiceMeeter VAIO)`。用户手动点击过系统设备后，会保存选择，不再按首次默认策略覆盖。

新版 Voicemeeter 中默认输入端点可能显示为 `VoiceMeeter B1`，程序已同时兼容 `VoiceMeeter Output` 和 `VoiceMeeter B1`。

系统默认设备页底部会显示物理硬件设备和主 Voicemeeter 设备的音量，并支持直接调节音量。

## 构建

本项目不需要 NuGet，也不需要 .NET SDK；会使用 Windows 自带的 .NET Framework 64 位编译器。

```powershell
.\build.ps1
```

生成文件：

```text
bin\SoundDeviceSwitcher.exe
```

## 发布

推送 `v*` 格式的 tag 会触发 GitHub Actions 发布流程：

- 在 Windows runner 上构建 `SoundDeviceSwitcher.exe`
- 生成 `SoundDeviceSwitcher-windows-x64.zip`
- 创建 GitHub Release
- 使用 GitHub Attestations 为 `dist/*` 产物生成构建证明

## 运行要求

- Windows 10/11。
- 已安装 Voicemeeter / Voicemeeter Banana / Potato。
- `VoicemeeterRemote64.dll` **仅从 Windows 的正式 Program Files 安装目录**加载（由系统定位 Program Files，不假定系统一定安装在 `C:`）：
  - `C:\Program Files (x86)\VB\Voicemeeter\VoicemeeterRemote64.dll`
  - `C:\Program Files\VB\Voicemeeter\VoicemeeterRemote64.dll`
- **不会加载放在程序同目录、当前工作目录或 PATH 中的同名 DLL。** 如果没有检测到上述安装目录中的 DLL，Voicemeeter 功能会提示错误；请从 VB-Audio 正式安装 Voicemeeter，而不是把 DLL 复制到 EXE 旁边。

## 说明

Voicemeeter 页面的“只显示物理/耳机/内置设备”会隐藏名称或硬件 ID 中包含 `Voicemeeter`、`VB-Audio`、`Virtual`、`Sonar`、`Voicemod`、`Cable` 等关键词的设备；切到“显示全部设备”可以看到完整列表。


## 安全与分发注意事项

- 已采用**方案 A**：严格禁止 EXE 同目录旁加载 Voicemeeter DLL，只接受由 Windows 系统定位的 Program Files 路径下 `VB\Voicemeeter\VoicemeeterRemote64.dll`。安装路径或 DLL 本身是符号链接/目录联接等重解析点时拒绝加载，避免路径重定向。
- 通过 `LoadLibraryEx` 限制了原生 DLL 的直接依赖搜索路径，防止从当前工作目录 / `PATH` 目录随意加载依赖。但**位置检查不等于 Authenticode 签名或文件哈希校验**：官方安装目录权限遭破坏或已被入侵仍有风险。建议保持 Windows 和 Voicemeeter 为可信来源且使用普通用户权限运行。
- 此源码包不包含重新编译的 EXE。务必在 Windows 上使用 `build.ps1` 重建，并完成真实音频设备/Voicemeeter 操作测试后再部署。
- 本仓库自带 `tests/test_security_regressions.py` 静态回归检查，可用 `python -m unittest discover -s tests -v` 执行；它不能取代 Windows 编译、GUI、原生 DLL 测试。
- CI 使用 GitHub Actions `@v*` 可移动主版本标签；升级和不可变 SHA 锁定尚未执行，参见 `SECURITY_AUDIT_REPORT.md` 中的待决事项。
