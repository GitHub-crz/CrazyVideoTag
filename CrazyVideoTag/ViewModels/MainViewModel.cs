using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using CrazyVideoTag.Models;
using CrazyVideoTag.Services;

namespace CrazyVideoTag.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly AppSettingsStore _settingsStore = new();
    private readonly AppStateStore _store = new();
    private readonly VideoScanner _scanner = new();
    private readonly ThumbnailService _thumbnailService = new();
    private readonly FileOpenService _fileOpenService = new();
    private readonly FileDeleteService _fileDeleteService = new();
    private readonly CancellationTokenSource _shutdown = new();
    private const int DisplayPageSize = 40;
    private AppSettings _settings = new();
    private AppState _state = new();
    private List<VideoItem> _allVideos = [];
    private List<VideoItem> _currentDisplaySource = [];
    private string? _currentFolder;
    private FolderNode? _folderRoot;
    private FolderNode? _selectedFolder;
    private VideoItem? _selectedVideo;
    private string _statusText = "请选择一个视频文件夹";
    private int _scanCount;
    private int _thumbnailCompleted;
    private int _thumbnailTotal;
    private int _currentDisplayVersion;
    private CancellationTokenSource? _backgroundLoadCts;
    private bool _isStartPageVisible = true;
    private bool _suppressRightTagChanged;
    private bool _suppressFilterChanged;
    private List<VideoItem> _cutVideos = [];

    public ObservableCollection<VideoItem> DisplayedVideos { get; } = [];
    public ObservableCollection<FolderNode> FolderTreeRoots { get; } = [];
    public ObservableCollection<SelectableTagViewModel> TagRows { get; } = [];
    public ObservableCollection<SelectableTagViewModel> ActorRows { get; } = [];
    public ObservableCollection<SelectableTagViewModel> FilterTagRows { get; } = [];
    public ObservableCollection<SelectableTagViewModel> FilterActorRows { get; } = [];
    public ObservableCollection<TagDefinition> AllTagDefinitions { get; } = [];

    public RelayCommand ChooseFolderCommand { get; }
    public AsyncRelayCommand RescanCommand { get; }
    public RelayCommand AddTagCommand { get; }
    public RelayCommand AddActorCommand { get; }
    public RelayCommand DeleteTagCommand { get; }
    public RelayCommand EditTagCommand { get; }
    public RelayCommand ConfigureToolsCommand { get; }
    public RelayCommand ConfigureStorageCommand { get; }
    public AsyncRelayCommand GenerateSelectedThumbnailCommand { get; }
    public RelayCommand OpenSelectedVideoCommand { get; }
    public AsyncRelayCommand DeleteSelectedVideoCommand { get; }
    public RelayCommand SetCustomCoverCommand { get; }
    public RelayCommand LoadMoreVideosCommand { get; }
    public RelayCommand CutCommand { get; }
    public AsyncRelayCommand PasteCommand { get; }

    public MainViewModel()
    {
        ChooseFolderCommand = new RelayCommand(_ => ChooseFolder());
        RescanCommand = new AsyncRelayCommand(_ => ScanCurrentFolderAsync(), _ => Directory.Exists(CurrentFolder));
        AddTagCommand = new RelayCommand(_ => AddTag(TagKind.Normal));
        AddActorCommand = new RelayCommand(_ => AddTag(TagKind.Actor));
        DeleteTagCommand = new RelayCommand(DeleteTag, parameter => parameter is SelectableTagViewModel);
        EditTagCommand = new RelayCommand(EditTag, parameter => parameter is SelectableTagViewModel);
        ConfigureToolsCommand = new RelayCommand(_ => ConfigureTools());
        ConfigureStorageCommand = new RelayCommand(_ => ConfigureStorage());
        GenerateSelectedThumbnailCommand = new AsyncRelayCommand(_ => GenerateSelectedThumbnailAsync(), _ => SelectedVideo is not null);
        OpenSelectedVideoCommand = new RelayCommand(_ => OpenSelectedVideo(), _ => SelectedVideo is not null);
        DeleteSelectedVideoCommand = new AsyncRelayCommand(_ => DeleteSelectedVideoAsync(), _ => SelectedVideo is not null);
        SetCustomCoverCommand = new RelayCommand(_ => SetCustomCover(), _ => SelectedVideo is not null);
        LoadMoreVideosCommand = new RelayCommand(_ => LoadMoreVideos(), _ => DisplayedVideos.Count < _currentDisplaySource.Count);
        CutCommand = new RelayCommand(_ => CutSelectedVideos(), _ => _allVideos.Any(v => v.IsSelected));
        PasteCommand = new AsyncRelayCommand(_ => PasteVideosAsync(), _ => _cutVideos.Count > 0 && SelectedFolder is not null);
    }

    public string? CurrentFolder
    {
        get => _currentFolder;
        private set
        {
            if (SetField(ref _currentFolder, value))
            {
                RescanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public FolderNode? FolderRoot
    {
        get => _folderRoot;
        private set => SetField(ref _folderRoot, value);
    }

    public FolderNode? SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetField(ref _selectedFolder, value))
            {
                _ = RefreshDisplayedVideosAsync();
                SyncRightChecks();
            }
        }
    }

    public VideoItem? SelectedVideo
    {
        get => _selectedVideo;
        set
        {
            if (EqualityComparer<VideoItem?>.Default.Equals(_selectedVideo, value))
            {
                return;
            }

            _selectedVideo = value;
            OnPropertyChanged();

            SyncRightChecks();
            GenerateSelectedThumbnailCommand.RaiseCanExecuteChanged();
            OpenSelectedVideoCommand.RaiseCanExecuteChanged();
            DeleteSelectedVideoCommand.RaiseCanExecuteChanged();
            SetCustomCoverCommand.RaiseCanExecuteChanged();
            CutCommand.RaiseCanExecuteChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public int ScanCount
    {
        get => _scanCount;
        private set => SetField(ref _scanCount, value);
    }

    public int ThumbnailCompleted
    {
        get => _thumbnailCompleted;
        private set => SetField(ref _thumbnailCompleted, value);
    }

    public int ThumbnailTotal
    {
        get => _thumbnailTotal;
        private set => SetField(ref _thumbnailTotal, value);
    }

    public bool IsStartPageVisible
    {
        get => _isStartPageVisible;
        private set => SetField(ref _isStartPageVisible, value);
    }

    public string DisplayedCountText => $"已加载 {DisplayedVideos.Count} / {_currentDisplaySource.Count} 个视频";

    public IReadOnlyList<SortModeOption> SortModeOptions { get; } =
    [
        new(VideoSortMode.ModifiedDesc, "按修改时间（新→旧）"),
        new(VideoSortMode.ModifiedAsc, "按修改时间（旧→新）"),
        new(VideoSortMode.SizeDesc, "按文件大小（大→小）"),
        new(VideoSortMode.SizeAsc, "按文件大小（小→大）"),
    ];

    public VideoSortMode SortMode
    {
        get => _state.SortMode;
        set
        {
            if (_state.SortMode == value)
            {
                return;
            }

            _state.SortMode = value;
            OnPropertyChanged();
            _ = SaveAsync();
            _ = RefreshDisplayedVideosAsync();
        }
    }

    public async Task InitializeAsync()
    {
        _settings = await _settingsStore.LoadAsync();
        ApplyStorageFolder(GetStorageFolder());
        _state = await _store.LoadAsync();
        RefreshTagRows();
        if (Directory.Exists(_state.LastFolder))
        {
            CurrentFolder = _state.LastFolder;
            IsStartPageVisible = false;
            await ScanCurrentFolderAsync();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void ChooseFolder()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择要管理的视频文件夹",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(CurrentFolder) ? CurrentFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            CurrentFolder = dialog.SelectedPath;
            _state.LastFolder = dialog.SelectedPath;
            IsStartPageVisible = false;
            _ = SaveAsync();
            RescanCommand.Execute(null);
        }
    }

    private async Task ScanCurrentFolderAsync()
    {
        if (!Directory.Exists(CurrentFolder))
        {
            return;
        }

        ScanCount = 0;
        ThumbnailCompleted = 0;
        ThumbnailTotal = 0;
        StatusText = "正在扫描视频文件...";
        var progress = new Progress<ScanProgress>(p =>
        {
            ScanCount = p.Count;
            StatusText = $"已扫描 {p.Count} 个视频";
        });

        _allVideos = await _scanner.ScanAsync(CurrentFolder!, _state, progress, _shutdown.Token);
        FolderRoot = VideoScanner.BuildFolderTree(CurrentFolder!, _allVideos);
        FolderTreeRoots.Clear();
        if (FolderRoot is not null)
        {
            FolderTreeRoots.Add(FolderRoot);
        }

        SelectedFolder = FolderRoot;
        StatusText = $"扫描完成，共 {ScanCount} 个视频";
        _ = GenerateThumbnailsAsync();
    }

    private async Task GenerateThumbnailsAsync()
    {
        var missing = _allVideos.Count(video => string.IsNullOrWhiteSpace(video.ThumbnailPath) || !File.Exists(video.ThumbnailPath));
        if (missing == 0)
        {
            return;
        }

        ThumbnailTotal = missing;
        ThumbnailCompleted = 0;
        var progress = new Progress<ThumbnailProgress>(p =>
        {
            ThumbnailCompleted = p.Completed;
            ThumbnailTotal = p.Total;
            StatusText = $"正在生成封面 {p.Completed}/{p.Total}";
        });

        await _thumbnailService.GenerateMissingAsync(_allVideos, _state, progress, _shutdown.Token);
        await SaveAsync();
        StatusText = "封面生成完成";
    }

    private async Task GenerateSelectedThumbnailAsync()
    {
        if (SelectedVideo is null)
        {
            return;
        }

        var result = await _thumbnailService.GenerateForVideoAsync(SelectedVideo, _state, _shutdown.Token);
        await SaveAsync();
        if (!result.Success)
        {
            System.Windows.MessageBox.Show(result.Error ?? "生成封面失败。", "生成封面失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AddTag(TagKind kind)
    {
        var dialog = new Views.TagEditorDialog(kind) { Owner = System.Windows.Application.Current.MainWindow };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _state.Tags.Add(new TagDefinition
        {
            Name = dialog.TagName,
            Color = dialog.SelectedColor,
            Kind = kind,
            SortOrder = GetNextSortOrder(kind)
        });

        RefreshTagRows();
        _ = SaveAsync();
    }

    private void DeleteTag(object? parameter)
    {
        if (parameter is not SelectableTagViewModel row)
        {
            return;
        }

        if (System.Windows.MessageBox.Show($"删除标签“{row.Name}”？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        _state.Tags.RemoveAll(tag => tag.Id == row.Id);
        foreach (var metadata in _state.Videos.Values)
        {
            metadata.TagIds.Remove(row.Id);
            metadata.ActorIds.Remove(row.Id);
        }

        foreach (var video in _allVideos)
        {
            video.TagIds.Remove(row.Id);
            video.ActorIds.Remove(row.Id);
            video.RefreshTags();
        }

        RefreshTagRows();
        _ = RefreshDisplayedVideosAsync();
        _ = SaveAsync();
    }

    private void EditTag(object? parameter)
    {
        if (parameter is not SelectableTagViewModel row)
        {
            return;
        }

        var dialog = new Views.TagEditorDialog(row.Kind, row.Name, row.Color) { Owner = System.Windows.Application.Current.MainWindow };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        row.Definition.Name = dialog.TagName;
        row.Definition.Color = dialog.SelectedColor;
        RefreshTagRows();
        foreach (var video in _allVideos)
        {
            video.RefreshTags();
        }

        _ = RefreshDisplayedVideosAsync();
        _ = SaveAsync();
    }

    private void ConfigureTools()
    {
        var dialog = new Views.ToolPathDialog(_state.FfmpegPath, _state.FfprobePath) { Owner = System.Windows.Application.Current.MainWindow };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _state.FfmpegPath = dialog.FfmpegPath;
        _state.FfprobePath = dialog.FfprobePath;
        _ = SaveAsync();
    }

    private async void ConfigureStorage()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择保存 video-tags.json 和 thumbs 的文件夹",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(GetStorageFolder()) ? GetStorageFolder() : AppContext.BaseDirectory
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        _settings.StorageFolder = dialog.SelectedPath;
        ApplyStorageFolder(dialog.SelectedPath);
        await _settingsStore.SaveAsync(_settings);
        if (File.Exists(_store.StatePath))
        {
            _state = await _store.LoadAsync();
            RefreshTagRows();
            if (Directory.Exists(_state.LastFolder))
            {
                CurrentFolder = _state.LastFolder;
                IsStartPageVisible = false;
                await ScanCurrentFolderAsync();
            }
        }
        else
        {
            await SaveAsync();
        }

        StatusText = $"存储目录已设置为：{dialog.SelectedPath}";
    }

    private string GetStorageFolder() => string.IsNullOrWhiteSpace(_settings.StorageFolder) ? AppContext.BaseDirectory : _settings.StorageFolder;

    private void ApplyStorageFolder(string storageFolder)
    {
        _store.StorageFolder = storageFolder;
        _scanner.SetStorageFolder(storageFolder);
        _thumbnailService.SetStorageFolder(storageFolder);
    }

    private void OpenSelectedVideo()
    {
        if (SelectedVideo is null)
        {
            return;
        }

        try
        {
            _fileOpenService.Open(SelectedVideo.Path);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task DeleteSelectedVideoAsync()
    {
        if (SelectedVideo is null)
        {
            return;
        }

        var video = SelectedVideo;
        if (System.Windows.MessageBox.Show($"确定删除文件？\n{video.Path}", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _fileDeleteService.Delete(video.Path);
            _allVideos.Remove(video);
            _state.Videos.Remove(video.Path);
            _state.ThumbnailCache.Remove(video.Path);
            SelectedVideo = null;
            FolderRoot = CurrentFolder is null ? null : VideoScanner.BuildFolderTree(CurrentFolder, _allVideos);
            FolderTreeRoots.Clear();
            if (FolderRoot is not null)
            {
                FolderTreeRoots.Add(FolderRoot);
            }

            _ = RefreshDisplayedVideosAsync();
            await SaveAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "删除失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SetCustomCover()
    {
        if (SelectedVideo is null)
        {
            return;
        }

        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "选择视频封面图片",
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.webp|所有文件|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        SelectedVideo.CustomCoverPath = dialog.FileName;
        SaveVideoMetadata(SelectedVideo);
        _ = SaveAsync();
    }

    private void RefreshTagRows()
    {
        AllTagDefinitions.Clear();
        foreach (var tag in _state.Tags.OrderBy(tag => tag.Kind).ThenBy(tag => tag.SortOrder).ThenBy(tag => tag.Name))
        {
            AllTagDefinitions.Add(tag);
        }

        ReplaceRows(TagRows, _state.Tags.Where(tag => tag.Kind == TagKind.Normal).OrderBy(tag => tag.Name), OnRightTagChanged);
        ReplaceRows(ActorRows, _state.Tags.Where(tag => tag.Kind == TagKind.Actor).OrderBy(tag => tag.SortOrder).ThenBy(tag => tag.Name), OnRightTagChanged);
        ReplaceRows(FilterTagRows, _state.Tags.Where(tag => tag.Kind == TagKind.Normal).OrderBy(tag => tag.Name), OnFilterTagChanged);
        ReplaceRows(FilterActorRows, _state.Tags.Where(tag => tag.Kind == TagKind.Actor).OrderBy(tag => tag.SortOrder).ThenBy(tag => tag.Name), OnFilterTagChanged);
        SyncRightChecks();
    }

    private static void ReplaceRows(ObservableCollection<SelectableTagViewModel> rows, IEnumerable<TagDefinition> tags, EventHandler handler)
    {
        foreach (var row in rows)
        {
            row.CheckedChanged -= handler;
        }

        rows.Clear();
        foreach (var tag in tags)
        {
            var row = new SelectableTagViewModel { Definition = tag };
            row.CheckedChanged += handler;
            rows.Add(row);
        }
    }

    private int GetNextSortOrder(TagKind kind)
    {
        var existing = _state.Tags.Where(tag => tag.Kind == kind).ToList();
        return existing.Count == 0 ? 0 : existing.Max(tag => tag.SortOrder) + 1;
    }

    public void MoveTag(SelectableTagViewModel source, SelectableTagViewModel target)
    {
        if (source.Id == target.Id || source.Kind != target.Kind)
        {
            return;
        }

        var ordered = _state.Tags
            .Where(tag => tag.Kind == source.Kind)
            .OrderBy(tag => tag.SortOrder)
            .ThenBy(tag => tag.Name)
            .ToList();
        var sourceTag = ordered.FirstOrDefault(tag => tag.Id == source.Id);
        var targetTag = ordered.FirstOrDefault(tag => tag.Id == target.Id);
        if (sourceTag is null || targetTag is null)
        {
            return;
        }

        ordered.Remove(sourceTag);
        ordered.Insert(ordered.IndexOf(targetTag), sourceTag);
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].SortOrder = index;
        }

        var checkedFilterTags = FilterTagRows.Where(row => row.IsChecked).Select(row => row.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var checkedFilterActors = FilterActorRows.Where(row => row.IsChecked).Select(row => row.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        RefreshTagRows();
        RestoreChecks(FilterTagRows, checkedFilterTags);
        RestoreChecks(FilterActorRows, checkedFilterActors);
        _ = SaveAsync();
    }

    private static void RestoreChecks(IEnumerable<SelectableTagViewModel> rows, HashSet<string> checkedIds)
    {
        foreach (var row in rows)
        {
            row.IsChecked = checkedIds.Contains(row.Id);
        }
    }

    public void SelectFolder(FolderNode folder)
    {
        ClearFilterChecks();
        SelectedFolder = folder;
        SelectedVideo = null;
        PasteCommand.RaiseCanExecuteChanged();
    }

    public void SelectSingleVideo(VideoItem video)
    {
        foreach (var v in _allVideos.Where(v => v.IsSelected && v != video))
        {
            v.IsSelected = false;
        }

        video.IsSelected = true;
        SelectedVideo = video;
        CutCommand.RaiseCanExecuteChanged();
    }

    public void ToggleVideoSelection(VideoItem video)
    {
        video.IsSelected = !video.IsSelected;
        SelectedVideo = video.IsSelected ? video : _allVideos.FirstOrDefault(v => v.IsSelected);
        CutCommand.RaiseCanExecuteChanged();
    }

    private void ClearSelection()
    {
        foreach (var v in _allVideos.Where(v => v.IsSelected))
        {
            v.IsSelected = false;
        }

        CutCommand.RaiseCanExecuteChanged();
    }

    private void CutSelectedVideos()
    {
        _cutVideos = _allVideos.Where(v => v.IsSelected).ToList();
        PasteCommand.RaiseCanExecuteChanged();
        StatusText = $"已剪切 {_cutVideos.Count} 个视频";
    }

    private async Task PasteVideosAsync()
    {
        if (_cutVideos.Count == 0 || SelectedFolder is null)
        {
            return;
        }

        var targetFolder = SelectedFolder.Path;
        var moved = 0;
        var skipped = new List<string>();

        foreach (var video in _cutVideos)
        {
            var fileName = System.IO.Path.GetFileName(video.Path);
            var destination = System.IO.Path.Combine(targetFolder, fileName);

            if (string.Equals(video.Path, destination, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (File.Exists(destination))
            {
                skipped.Add(fileName);
                continue;
            }

            try
            {
                File.Move(video.Path, destination);
            }
            catch (Exception ex)
            {
                skipped.Add($"{fileName} ({ex.Message})");
                continue;
            }

            var oldPath = video.Path;
            if (_state.Videos.TryGetValue(oldPath, out var metadata))
            {
                _state.Videos.Remove(oldPath);
                metadata.Path = destination;
                _state.Videos[destination] = metadata;
            }

            if (_state.ThumbnailCache.TryGetValue(oldPath, out var cache))
            {
                _state.ThumbnailCache.Remove(oldPath);
                cache.VideoPath = destination;
                _state.ThumbnailCache[destination] = cache;
            }

            _allVideos.Remove(video);
            var newVideo = new VideoItem
            {
                Path = destination,
                ModifiedAt = video.ModifiedAt,
                Size = video.Size
            };
            newVideo.TagIds.AddRange(video.TagIds);
            newVideo.ActorIds.AddRange(video.ActorIds);
            newVideo.ThumbnailPath = video.ThumbnailPath;
            newVideo.CustomCoverPath = video.CustomCoverPath;
            newVideo.Duration = video.Duration;
            _allVideos.Add(newVideo);
            moved++;
        }

        _cutVideos.Clear();
        ClearSelection();
        SelectedVideo = null;
        PasteCommand.RaiseCanExecuteChanged();

        _ = RefreshDisplayedVideosAsync();
        await SaveAsync();

        if (skipped.Count > 0)
        {
            System.Windows.MessageBox.Show($"已移动 {moved} 个文件，{skipped.Count} 个跳过：\n{string.Join("\n", skipped.Take(10))}", "粘贴完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            StatusText = $"已移动 {moved} 个视频到 {System.IO.Path.GetFileName(targetFolder)}";
        }
    }

    private void ClearFilterChecks()
    {
        _suppressFilterChanged = true;
        try
        {
            foreach (var row in FilterTagRows.Concat(FilterActorRows))
            {
                row.IsChecked = false;
            }
        }
        finally
        {
            _suppressFilterChanged = false;
        }
    }

    private void OnFilterTagChanged(object? sender, EventArgs e)
    {
        if (_suppressFilterChanged)
        {
            return;
        }

        _ = RefreshDisplayedVideosAsync();
    }

    private void OnRightTagChanged(object? sender, EventArgs e)
    {
        if (_suppressRightTagChanged || sender is not SelectableTagViewModel row)
        {
            return;
        }

        var targets = GetTagTargets().ToList();
        if (targets.Count == 0)
        {
            return;
        }

        if (SelectedVideo is null && SelectedFolder is not null && !ConfirmFolderTagChange(row, targets.Count))
        {
            _suppressRightTagChanged = true;
            row.IsChecked = !row.IsChecked;
            _suppressRightTagChanged = false;
            return;
        }

        foreach (var video in targets)
        {
            var ids = row.Kind == TagKind.Actor ? video.ActorIds : video.TagIds;
            if (row.IsChecked && !ids.Contains(row.Id))
            {
                ids.Add(row.Id);
            }
            else if (!row.IsChecked)
            {
                ids.Remove(row.Id);
            }

            video.RefreshTags();
            SaveVideoMetadata(video);
        }

        _ = SaveAsync();
    }

    private bool ConfirmFolderTagChange(SelectableTagViewModel row, int targetCount)
    {
        var kind = row.Kind == TagKind.Actor ? "演员" : "标签";
        var operation = row.IsChecked ? "添加" : "移除";
        var folder = SelectedFolder?.Path ?? string.Empty;
        var message = $"确定要给文件夹下的 {targetCount} 个视频{operation}{kind}“{row.Name}”吗？\n\n{folder}";
        return System.Windows.MessageBox.Show(message, "确认批量修改", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private IEnumerable<VideoItem> GetTagTargets()
    {
        if (SelectedVideo is not null)
        {
            yield return SelectedVideo;
            yield break;
        }

        if (SelectedFolder is null)
        {
            yield break;
        }

        foreach (var video in _allVideos.Where(video => IsUnderFolder(video, SelectedFolder.Path)))
        {
            yield return video;
        }
    }

    private void SyncRightChecks()
    {
        var target = SelectedVideo;
        foreach (var row in TagRows.Concat(ActorRows))
        {
            row.CheckedChanged -= OnRightTagChanged;
            row.IsChecked = target is not null && (row.Kind == TagKind.Actor ? target.ActorIds : target.TagIds).Contains(row.Id);
            row.CheckedChanged += OnRightTagChanged;
        }
    }

    private async Task RefreshDisplayedVideosAsync()
    {
        _backgroundLoadCts?.Cancel();
        _backgroundLoadCts?.Dispose();
        _backgroundLoadCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        var version = Interlocked.Increment(ref _currentDisplayVersion);
        var token = _backgroundLoadCts.Token;

        var selectedNormal = FilterTagRows.Where(row => row.IsChecked).Select(row => row.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedActors = FilterActorRows.Where(row => row.IsChecked).Select(row => row.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var folderPath = SelectedFolder?.Path;
        var allVideos = _allVideos;

        _currentDisplaySource = [];
        DisplayedVideos.Clear();
        OnPropertyChanged(nameof(DisplayedCountText));
        LoadMoreVideosCommand.RaiseCanExecuteChanged();
        StatusText = "正在加载视频...";

        List<VideoItem> source;
        try
        {
            source = await Task.Run(() =>
            {
                var result = new List<VideoItem>();
                var restrictToFolder = !string.IsNullOrWhiteSpace(folderPath) && selectedNormal.Count == 0 && selectedActors.Count == 0;
                foreach (var video in allVideos)
                {
                    token.ThrowIfCancellationRequested();
                    if (restrictToFolder && !IsUnderFolderFast(video, folderPath!))
                    {
                        continue;
                    }

                    if (selectedNormal.Count > 0 && !selectedNormal.All(id => video.TagIds.Contains(id, StringComparer.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    if (selectedActors.Count > 0 && !selectedActors.Any(id => video.ActorIds.Contains(id, StringComparer.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    result.Add(video);
                }

                token.ThrowIfCancellationRequested();
                return ApplySort(result).ToList();
            }, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_currentDisplayVersion != version || token.IsCancellationRequested)
        {
            return;
        }

        _currentDisplaySource = source;
        LoadMoreVideos();
        OnPropertyChanged(nameof(DisplayedCountText));
        StatusText = $"已匹配 {source.Count} 个视频";
    }

    private void LoadMoreVideos()
    {
        var start = DisplayedVideos.Count;
        var end = Math.Min(start + DisplayPageSize, _currentDisplaySource.Count);
        for (var index = start; index < end; index++)
        {
            DisplayedVideos.Add(_currentDisplaySource[index]);
        }

        LoadMoreVideosCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(DisplayedCountText));
    }

    private static bool IsUnderFolderFast(VideoItem video, string folder)
    {
        var normalizedFolder = folder.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        return string.Equals(video.Folder, normalizedFolder, StringComparison.OrdinalIgnoreCase)
            || video.Folder.StartsWith(normalizedFolder + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || video.Folder.StartsWith(normalizedFolder + System.IO.Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnderFolder(VideoItem video, string folder) => IsUnderFolderFast(video, folder);

    private void SaveVideoMetadata(VideoItem video)
    {
        _state.Videos[video.Path] = new VideoMetadata
        {
            Path = video.Path,
            CustomCoverPath = video.CustomCoverPath,
            TagIds = video.TagIds.ToList(),
            ActorIds = video.ActorIds.ToList()
        };
    }

    private Task SaveAsync() => _store.SaveAsync(_state);

    private IEnumerable<VideoItem> ApplySort(IEnumerable<VideoItem> query) => _state.SortMode switch
    {
        VideoSortMode.ModifiedAsc => query.OrderBy(video => video.ModifiedAt),
        VideoSortMode.SizeDesc => query.OrderByDescending(video => video.Size),
        VideoSortMode.SizeAsc => query.OrderBy(video => video.Size),
        _ => query.OrderByDescending(video => video.ModifiedAt),
    };

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record SortModeOption(VideoSortMode Mode, string Label);
