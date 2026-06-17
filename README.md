# CrazyVideoTag

一款基于 WPF / .NET 8 的本地视频标签管理工具，支持给本地视频打标签、按演员/普通标签筛选、自动生成封面、剪切粘贴等功能。深色主题，适合管理大量本地视频。

当前版本：**v1.1.17**

## 功能特性

### 视频管理
- 选择文件夹后自动扫描其中的视频文件，记住上次打开的文件夹
- 支持常见视频格式（mp4 / mkv / mov / avi / wmv / flv / webm 等）
- 自动跳过 `$Recycle.Bin`、`Recycle.Bin` 及系统/隐藏目录
- 文件夹树形展示，根目录可选中
- 视频卡片显示封面、文件名、修改时间、文件大小、视频时长
- 支持按修改时间或文件大小排序（顶部下拉框切换，自动持久化）
- 支持按视频标题关键字搜索，输入后按回车筛选标题包含关键字的视频
- 双击视频用系统默认播放器打开
- 按 `Delete` 键删除选中视频
- 右键视频可设置自定义封面图片，自定义封面优先于自动生成的封面

### 标签系统
- 普通标签（交集筛选） + 演员标签（并集筛选），互不干扰
- 标签可自定义颜色，预设颜色丰富，颜色会同步显示在筛选区与功能区
- 双击标签可重命名 / 改色
- 普通标签按首字母自动排序，演员标签支持拖动排序
- 选中单个视频时打勾会更新该视频标签
- 选中文件夹时打勾可批量为该文件夹下所有视频应用 / 取消标签（带二次确认弹框）
- 右侧标签功能区以页签切换普通标签 / 演员，便于快速查找

### 筛选
- 普通标签筛选：交集（AND）
- 演员标签筛选：并集（OR）
- 筛选范围是当前根文件夹下的所有视频，不局限于已选中的子文件夹
- 切换回文件夹页选择文件夹时自动清空筛选条件

### 文件操作
- `Ctrl + 点击` 多选视频
- 剪切 / 粘贴：选中视频后剪切，切换到目标文件夹后粘贴，文件被移动且原本的标签随之保留
- 粘贴不会折叠文件夹树

### 封面生成
- `ffprobe` 读取时长，`ffmpeg` 抽取视频 50% 进度处的帧作为封面
- 中心裁剪并缩放，缓存到本地，相同文件夹再次打开不会重复生成
- 选中视频后可手动重新生成封面，失败时显示原因

### 性能优化
- 分页加载（首屏 40 个），每 5 秒后台自动补充 40 个，滚动接近底部时也可触发加载
- 筛选 / 排序在后台线程计算，切换标签页不阻塞 UI
- 标签变更只更新数据，不重建列表，避免视频跳动
- 滚动接近底部时自动加载更多
- 顶部状态栏实时显示「已加载 / 总数」

### 配置
- FFmpeg / ffprobe 路径可在 UI 中配置
- 标签数据 `video-tags.json` 与封面缓存 `thumbs/` 的存储目录可配置

## 环境要求

- Windows 10 / 11
- [.NET 8 Runtime (Desktop)](https://dotnet.microsoft.com/download/dotnet/8.0)
- [FFmpeg](https://ffmpeg.org/download.html)（包含 `ffmpeg.exe` 和 `ffprobe.exe`）

## 使用方法

### 直接运行

1. 从 `publish/` 目录下载对应版本（如 `CrazyVideoTag-v1.1.7-win-x64`）
2. 双击 `CrazyVideoTag.exe` 启动
3. 首次启动点击「配置 FFmpeg」指定 `ffmpeg.exe` 与 `ffprobe.exe` 路径
4. 点击「配置存储目录」指定标签数据与封面缓存的保存位置
5. 点击「选择文件夹」选择需要管理的视频根目录

### 自行构建

```bash
# 还原 + 构建
dotnet build CrazyVideoTag.slnx

# 发布
dotnet publish CrazyVideoTag/CrazyVideoTag.csproj -c Release -r win-x64 \
  --self-contained false -p:PublishSingleFile=false \
  -o publish/CrazyVideoTag-v1.1.17-win-x64
```

## 项目结构

```
CrazyVideoTag/
├── App.xaml / App.xaml.cs           # 应用入口，深色主题资源
├── MainWindow.xaml / .cs            # 主窗口，UI 事件处理
├── Models/
│   ├── AppSettings.cs               # 引导配置（存储目录路径）
│   ├── AppState.cs                  # 持久化状态（标签 / 视频元数据 / 封面缓存）
│   ├── VideoItem.cs                 # 运行时视频模型
│   ├── VideoMetadata.cs             # 持久化视频元数据
│   ├── TagDefinition.cs / TagKind.cs
│   ├── ThumbnailCacheEntry.cs
│   └── FolderNode.cs                # 文件夹树节点
├── ViewModels/
│   ├── MainViewModel.cs             # 核心业务逻辑
│   ├── SelectableTagViewModel.cs
│   └── RelayCommand.cs              # 同步 / 异步命令
├── Services/
│   ├── AppSettingsStore.cs          # app-settings.json 读写
│   ├── AppStateStore.cs             # video-tags.json 读写
│   ├── VideoScanner.cs              # 视频文件扫描
│   ├── ThumbnailService.cs          # FFmpeg 封面生成
│   ├── FileOpenService.cs
│   └── FileDeleteService.cs
├── Converters/                      # WPF 值转换器
└── Views/
    ├── TagEditorDialog.xaml         # 标签编辑对话框
    └── ToolPathDialog.xaml          # FFmpeg 路径对话框
```

## 数据存储

- `app-settings.json`：与 `CrazyVideoTag.exe` 同目录，记录用户配置的存储目录
- `video-tags.json`：保存在用户配置的存储目录，包含标签定义、视频与标签的关联关系、最后打开的文件夹路径、FFmpeg 路径等
- `thumbs/`：保存在用户配置的存储目录，存放生成的封面缓存

## 快捷键

| 操作 | 快捷键 |
| --- | --- |
| 多选视频 | `Ctrl + 单击` |
| 打开视频 | 双击视频卡片 |
| 删除选中视频 | `Delete` |
| 编辑标签 | 双击标签行 |

## 版本历史

发布包位于 `publish/` 目录，每个版本都打包成独立的 win-x64 文件夹。最新版本为 v1.1.17。
