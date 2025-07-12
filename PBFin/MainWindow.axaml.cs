using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using Bitmap = Avalonia.Media.Imaging.Bitmap;
using Color = System.Drawing.Color;

namespace PBFin;

public partial class MainWindow : Window
{
    public ObservableCollection<ButtonData> ButtonDataList { get; } = new ObservableCollection<ButtonData> { };
    public ObservableCollection<DirectoryData> DirectoryList { get; } = new ObservableCollection<DirectoryData> { };

    
    private Button _openDirectoryButton;
    private LibVLC _mainLibVLC { get; set; }
    private MediaPlayer MainMediaPlayer { get; set; }
    private int _currentSong;
    private bool _isPlaying = false;
    private bool _isShuffling = false;
    private List<String> SongsInDirectory { get; set; } = new List<string>();
    private MediaList SongsList { get; set; }
    
    #region Init And Constructor

    public MainWindow()
    {
        InitializeComponent();
        InitMediaPlayer();
        this.DataContext = this;


        LoadInitialPersistentData();
        LoadInitialSettings();
        if (DirectoryList.Count > 0)
        {
            SongsInDirectory = Directory.GetFiles(DirectoryList[0].DirectoryPath).ToList();
            CreateSongList();
        }
    }

    private void LoadInitialSettings()
    {
        if (!Directory.Exists("./Settings/"))
        {
            VolumeSlider.Value = 50;
            return;
        }
        VolumeSlider.Value = double.Parse(File.ReadAllBytes("./Settings/Volume.txt"));
    }

    private void LoadInitialPersistentData()
    {
        //load directories
        if (!Directory.Exists("./SongsDirectories/"))
        {
            return;
        }
        DirectoryList.Clear();
        List<string> directories = File.ReadAllLines(@"./SongsDirectories/directories.txt").ToList();
        foreach (string directory in directories)
        {
            DirectoryList.Add(new  DirectoryData {DirectoryText = directory, DirectoryPath = directory});
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        _openDirectoryButton = this.Find<Button>("OpenDirectoryButton");
        _openDirectoryButton.Click += OnNewDirectory;
        base.OnApplyTemplate(e);
    }

    private void InitMediaPlayer()
    {
        _mainLibVLC = new(enableDebugLogs: true);
        
        MainMediaPlayer = new(_mainLibVLC);
        
        MainMediaPlayer.TimeChanged += MediaPlayer_TimeChanged;

        MainMediaPlayer.EndReached += OnSongEnd;

        SongsList = new MediaList(_mainLibVLC);
        
        SongsList.ItemAdded += ListMedia;
    }

    

    #endregion

    #region Continuity

    private void OnSongEnd(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(PlayNextSong);
    }

    private void PlayNextSong()
    {
        _isPlaying = false;
        if (_isShuffling)
        {
            int random =  new Random().Next(0, SongsInDirectory.Count);
            _currentSong = random;
            MainMediaPlayer.Media = SongsList[_currentSong];
            MainMediaPlayer.Play();
            _isShuffling = true;
            _isPlaying = true;
        }
        else if (!_isShuffling)
        {
            if (_currentSong < SongsInDirectory.Count - 1)
            {
                _currentSong++;
                MainMediaPlayer.Media = SongsList[_currentSong];
                MainMediaPlayer.Play();
                _isPlaying = true;
            
            }
            else
            {
                _currentSong = 0;
                MainMediaPlayer.Media = SongsList[_currentSong];
                MainMediaPlayer.Play();
                _isPlaying = true;
            }
        }
    }

    #endregion

    #region PlayPause

    public void OnPlay(object sender, RoutedEventArgs args)
    {
        PauseButtonImage.Source = new Bitmap(AssetLoader.Open(new Uri("avares://PBFin/Assets/pauseButton.png")));
        MainMediaPlayer.Media = SongsList[_currentSong];
        MainMediaPlayer.Play();
        _isPlaying = true;
    }
    private void OnPause(object? sender, RoutedEventArgs e)
    {

        if (_isPlaying)
        {
            PauseButtonImage.Source= new Bitmap(AssetLoader.Open(new Uri("avares://PBFin/Assets/playButton.png")));

            MainMediaPlayer.Pause();
            _isPlaying = false;
        }
        else
        {
            PauseButtonImage.Source= new Bitmap(AssetLoader.Open(new Uri("avares://PBFin/Assets/pauseButton.png")));

            MainMediaPlayer.Play();
            _isPlaying = true;
        }
    }

    #endregion
    
    #region TimeStampHandling

    private void MediaPlayer_TimeChanged(object sender, MediaPlayerTimeChangedEventArgs e)
    {
        Dispatcher.UIThread.Invoke(
            new Action(
                () =>
                {
                    if (_isPlaying)
                    {
                        TimeSpan currentTime = TimeSpan.FromSeconds(MainMediaPlayer.Time/1000.0);
                        TimeSpan totalTime = TimeSpan.FromSeconds(MainMediaPlayer.Length / 1000.0);
                        
                        string currentTimeString = currentTime.ToString(@"mm\:ss");
                        string totalTimeString = totalTime.ToString(@"mm\:ss");
                        
                        PlaybackStatus.Text = $"{Path.GetFileNameWithoutExtension(SongsInDirectory[_currentSong])}\n{currentTimeString} / {totalTimeString}";
                        ProgressSlider.ValueChanged -= OnTimeStampChanged;
                        ProgressSlider.Value = MainMediaPlayer.Time * 100.0 / MainMediaPlayer.Length;
                        ProgressSlider.ValueChanged += OnTimeStampChanged;
                    }
                }
            )
        );
    }
    private void OnTimeStampChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        double fraction = e.NewValue / 100.0;        
        MainMediaPlayer.Time = (long)(fraction * MainMediaPlayer.Length);    
    }

    #endregion

    #region VolumeHandling

    private void UpdateVolume(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (!Directory.Exists("./Settings/"))
        {
            Directory.CreateDirectory("./Settings/");
        }

        if (!File.Exists("./Settings/Volume.txt"))
        {
            File.Create("./Settings/Volume.txt").Close();
        }
        MainMediaPlayer.Volume = (int)e.NewValue;
        File.WriteAllText("./Settings/Volume.txt", MainMediaPlayer.Volume.ToString());
    }

    #endregion

    #region DirectoryHandling
    
    private async void OnNewDirectory(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        var resultDirectory = await dialog.ShowAsync(this);
        //var resultDirectory = @"\\192.168.129.29/mnt/DataStore/share/Emiliano";
        if (resultDirectory != null)
        {
            SongsInDirectory = Directory.GetFiles(resultDirectory).ToList();
            CreateSongList();
            foreach (var directoryData in DirectoryList)
            {
                if (directoryData.DirectoryPath == resultDirectory)
                {
                    return;
                }
            }
            SaveDirectory(resultDirectory);
        }
    }

    private async void SaveDirectory(string resultDirectory)
    {
        if (!Directory.Exists("./SongsDirectories/"))
        {
            DirectoryList.Add(new  DirectoryData{ DirectoryPath = resultDirectory, DirectoryText = resultDirectory});
            Directory.CreateDirectory("./SongsDirectories/");
        }

        if (!File.Exists("./SongsDirectories/directories.txt"))
        {
            File.Create("./SongsDirectories/directories.txt").Close();
        }

        using (StreamWriter writer = new("./SongsDirectories/directories.txt",  append:true))
        {
            SaveToStreamAsync(resultDirectory, writer);
        }
        
        LoadInitialPersistentData();
    }
    private void SaveToStreamAsync(string data, StreamWriter writer)
    {
        writer.WriteLine(data);
        writer.Close();
    }
    
    private void LoadDirectory(object? sender, RoutedEventArgs e)
    {
        _isPlaying = false;
        if (sender is Button button && button.Tag is string Path)
        {
            SongsInDirectory = Directory.GetFiles(Path).ToList();
            CreateSongList();
        }
    }

    private void CreateSongList()
    {
        for (int i = SongsList.Count-1; i >= 0; i--)
        {
            SongsList.RemoveIndex(i);
        }
        foreach (var song in SongsInDirectory)
        {
            SongsList.AddMedia(new Media(_mainLibVLC, song));
        }
    }

    private void ListMedia(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(CreateButtonList);
    }
    private void UpdateSongList(object? sender, TextChangedEventArgs e)
    {
        if (SearchBox.Text == "")
        {
            CreateButtonList();
        }
        else
        {
            CreateFilteredButtonList();
        }
    }

    private void CreateFilteredButtonList()
    {
        ButtonDataList.Clear();
        for (int i = 0 ; i < SongsInDirectory.Count; i++)
        {
            if (Path.GetFileNameWithoutExtension(SongsInDirectory[i]).ToLower().Contains(SearchBox.Text.ToLower()))
            {
                ButtonDataList.Add(new  ButtonData{ButtonText = Path.GetFileNameWithoutExtension(SongsInDirectory[i]), Tag = i});
            }
        }
    }

    private void CreateButtonList()
    {
        ButtonDataList.Clear();
        for (int i = 0 ; i < SongsInDirectory.Count; i++)
        {
            ButtonDataList.Add(new  ButtonData{ButtonText = Path.GetFileNameWithoutExtension(SongsInDirectory[i]), Tag = i});
        }
    }

    private void PlayFromlist(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int position)
        {
            _isPlaying = true;
            _currentSong=position;
            OnPlay(sender, e);
        }
    }

    #endregion

    #region SongNextPrevious
    private void OnPrevious(object? sender, RoutedEventArgs e)
    {
        if (DirectoryList.Count == 0)
        {
            return;
        }
        if (!_isShuffling)
        {
            if (_currentSong >0)
            {
                _currentSong--;
                OnPlay(sender, e);
            }
            else
            {
                _currentSong = SongsInDirectory.Count - 1;
                OnPlay(sender, e);
            }
        }
        else if (_isShuffling)
        {
            int random = new Random().Next(0, SongsInDirectory.Count);
            _currentSong = random;
            OnPlay(sender, e);
        }
    }

    private void OnNext(object? sender, RoutedEventArgs e)
    {
        if (DirectoryList.Count == 0)
        {
            return;
        }
        if (!_isShuffling)
        {
            if (_currentSong < SongsInDirectory.Count - 1)
            {
                _currentSong++;
                OnPlay(sender, e);
            }
            else
            {
                _currentSong = 0;
                OnPlay(sender, e);
            }
        }
        else if (_isShuffling)
        {
            int random = new Random().Next(0, SongsInDirectory.Count);
            _currentSong = random;
            OnPlay(sender, e);
        }
        
    }
    #endregion

    #region Shuffle

    private void OnShuffle(object? sender, RoutedEventArgs e)
    {
        if (!_isShuffling)
        {
            _isShuffling = true;
            ShuffleButtonImage.Source = new Bitmap(AssetLoader.Open(new Uri("avares://PBFin/Assets/shuffleButton.png")));
        } else if (_isShuffling)
        {
            _isShuffling = false;
            ShuffleButtonImage.Source = new Bitmap(AssetLoader.Open(new Uri("avares://PBFin/Assets/noShuffleButton.png")));
        }
    }

    #endregion

    private void SetBorder(object? sender, PointerEventArgs e)
    {
        if (sender is Border border && border.Tag is int position)
        {
            border.BorderThickness = new Thickness(5);
        }
    }

    private void UnsetBorder(object? sender, PointerEventArgs e)
    {
        if (sender is Border border && border.Tag is int position)
        {
            border.BorderThickness = new Thickness(0);
        }
    }
}