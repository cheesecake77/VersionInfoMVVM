using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Serialization;
using System.Reactive;
using ReactiveUI;
using VersionInfoMVVM.Models;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Threading;
using VersionInfoMVVM.Views;
using Avalonia.Data.Converters;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia;
using Avalonia.Platform;
using System.Reflection;

namespace VersionInfoMVVM.ViewModels 
{
    // TODO: всплывающие окна с предложением сохранить после Create, Exit и Open
    // AFTERWARDS: вынести стилизацию в отдельный файл
    // TODO: прерывание операций Сравнение и Обновление
    // AFTERWARDS: написать unit test'ы
    // AFTERWARDS: очистка кода
    // TODO: добавить состояния приложения (Ready, Updating и т. п.)
    // TODO: запись в txt, cvs, docx, xlsx
    // TODO: обработать отключение и включение интерфейса в зависимости от полей (currentFile is null, кнопка Save отключена и т.д.) 
    // TODO: сделать фильтрацию файлов с помощью radioButton'ов
    public class VersionInfoViewModel : ViewModelBase
    {
        private string? currentFile;
        public string CurrentFile
        {
            get => currentFile; set
            {
                this.RaiseAndSetIfChanged(ref currentFile, value);
            }
        }

        private ObservableCollection<BaseDescription>? fileData;
        public ObservableCollection<BaseDescription>? FileData { get => fileData; set => this.RaiseAndSetIfChanged(ref fileData, value); }
        private ObservableCollection<string>? directoryData;
        public ObservableCollection<string>? DirectoryData { get => directoryData; set => this.RaiseAndSetIfChanged(ref directoryData, value); }

        private string? selectedItem;
        public string FolderListBoxItem { get => selectedItem; set => this.RaiseAndSetIfChanged(ref selectedItem, value); }

        private string? statusBarText;
        public string? StatusBarText { get => statusBarText; set => this.RaiseAndSetIfChanged(ref statusBarText, value); }

        private AppMode appMode;
        public AppMode AppMode { get => appMode; set => this.RaiseAndSetIfChanged(ref appMode, value); }


        public VersionInfoViewModel(DataUnit data)
        {

            StatusBarText = "Готово";
            AppMode = AppMode.Ready;
            if (data.fileData != null && data.directoryData != null)
            {
                FileData = data.fileData;
                DirectoryData = data.directoryData;
            }
            else
            {
                FileData = new ObservableCollection<BaseDescription>();
                DirectoryData = new ObservableCollection<string>();
            }
            //Определение обработчкиов меню
            OnOpenItem = ReactiveCommand.Create(() =>
            {
                var d = new OpenFileDialog { Title = "Открыть файл..." };
                d.Filters.Add(new FileDialogFilter() { Name = "XML-документ", Extensions = { "xml" } });
                var res = d.ShowAsync(App.MainWindow).Result;
                if (res != null)
                {
                    currentFile = res[0];
                    var serializer = new XmlSerializer(typeof(DataUnit));
                    using var reader = new StreamReader(res[0]);
                    var input = (DataUnit?)serializer.Deserialize(reader);
                    if (input != null)
                    {
                        DirectoryData = input.directoryData;
                        FileData = input.fileData;
                        data.fileData = FileData;
                        data.directoryData = directoryData;
                    }

                }
            });
            OnCreateItem = ReactiveCommand.Create(() =>
            {
                if (FileData != null && DirectoryData != null)
                {
                    FileData.Clear();
                    DirectoryData.Clear();
                    if (currentFile != null) currentFile = null;
                    data.directoryData = DirectoryData;
                    data.fileData = FileData;
                }
            });
            OnSaveItem = ReactiveCommand.Create(() =>
            {
                if (currentFile != null)
                {
                    var serializer = new XmlSerializer(typeof(DataUnit));
                    DataUnit output = new() { fileData = FileData, directoryData = DirectoryData };
                    using var writer = new StreamWriter(currentFile);
                    serializer.Serialize(writer, output);
                }
            });
            OnSaveAsItem = ReactiveCommand.Create(async () =>
            {
                var d = new SaveFileDialog();
                d.Filters.Add(new FileDialogFilter() { Name = "XML-документ", Extensions = { "xml" } });
                var res = await d.ShowAsync(App.MainWindow);
                if (res != null)
                {
                    var serializer = new XmlSerializer(typeof(DataUnit));
                    DataUnit output = new() { fileData = FileData, directoryData = DirectoryData };
                    using var writer = new StreamWriter(res);
                    serializer.Serialize(writer, output);
                }
            });
            OnCloseItem = ReactiveCommand.Create(() =>
            {
                App.MainWindow.Close();
            });

            //Определение обработчиков кнопок
            OnAddButton = ReactiveCommand.Create(async () =>
            {
                var d = new OpenFolderDialog() { Title = "Добавление каталога" };
                var res = await d.ShowAsync(App.MainWindow);
                if (string.IsNullOrEmpty(res) || DirectoryData.Contains(res)) return;
                DirectoryData.Add(res);
            });
            OnDeleteButton = ReactiveCommand.Create(() =>
            {
                if (FolderListBoxItem == null) return;
                if (FolderListBoxItem is String folder) DirectoryData.Remove(folder);
            });
            OnUpdateButton = ReactiveCommand.Create(() =>
            {
                if (FileData != null) FileData.Clear();
                var flist = new ObservableCollection<BaseDescription>();
                Task.Run(() =>
                {
                    if (DirectoryData != null)
                        foreach (var d in DirectoryData)
                        {
                            Dispatcher.UIThread.InvokeAsync(() => StatusBarText = $"Загрузка файлов из директории {d}");
                            FindFiles(d, flist);
                        }
                }).ContinueWith(t => {
                    FileData = flist;
                    StatusBarText = "Готово";
                });

            });
            OnCheckButton = ReactiveCommand.Create(() => 
            {
                // TODO: проверить check, исправить ветку для измененных файлов, добавить обработку файлов с состоянием Unknown
                var newList = new ObservableCollection<BaseDescription>();
                _ = Task.Run(() =>
                {
                    if (DirectoryData != null)
                        foreach (var d in DirectoryData)
                        {
                            Dispatcher.UIThread.InvokeAsync(() => StatusBarText = $"Загрузка файлов из директории {d}");
                            FindFiles(d, newList);
                        }

                    var temp_list = new ObservableCollection<BaseDescription>(FileData);
                    foreach (FileDescription file in temp_list.Where(f => f is FileDescription))
                    {
                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            var found = newList.FirstOrDefault(t => t.Path == file.Path);
                            if (found == null)
                            {
                                file.FileState = FileState.Deleted;
                                FileData = temp_list;
                            }
                        });
                    }

                    foreach (FileDescription file in newList.Where(f => f is FileDescription))
                    {
                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            var found = FileData.FirstOrDefault(t => t.Path == file.Path);
                            if (found == null)
                            {
                                file.FileState = FileState.Added;
                                temp_list.Add(file);
                            }
                        });
                    }

                    foreach (FileDescription file in newList.Where(f => f is FileDescription))
                    {
                        var found = (FileDescription)temp_list.FirstOrDefault(t => t.Path == file.Path);
                        if (found != null)
                        {
                            var debug = found.Equals(file);
                            if (!found.Equals(file)) 
                            {
                                found.Time = file.Time;
                                found.Size = file.Size;
                                found.Hash = file.Hash;
                                found.Version = file.Version;
                                found.FileState = FileState.Modified;

                            }
                        }
                    }

                    FileData = temp_list;

                }).ContinueWith(t =>
                {
                    StatusBarText = "Готово";
                });

            });
            
        }
        //Объвление обработчиков меню
        public ReactiveCommand<Unit, Unit> OnOpenItem { get; }
        public ReactiveCommand<Unit, Unit> OnCreateItem { get; }
        public ReactiveCommand<Unit, Unit> OnSaveItem { get; }
        public ReactiveCommand<Unit, Task> OnSaveAsItem { get; }
        public ReactiveCommand<Unit, Unit> OnCloseItem { get; }

        //Объявление обработчиков кнопок
        public ReactiveCommand<Unit, Task> OnAddButton { get; }
        public ReactiveCommand<Unit, Unit> OnDeleteButton { get; }
        public ReactiveCommand<Unit, Unit> OnCheckButton { get; }
        public ReactiveCommand<Unit, Unit> OnUpdateButton { get; }
        

        //Вспомогательные методы
        private void FindFiles(string directory, ObservableCollection<BaseDescription> files)
        {

            files.Add(new DirectoryDescription() { Path = directory });
            var flist = Directory.GetFiles(directory);
            foreach (string f in flist)
            {
                var file = new FileDescription() { Path = f };
                files.Add(file.FillProperties());

            }
            var dlist = Directory.GetDirectories(directory);
            if (dlist != null)
                foreach (var d in dlist) FindFiles(d, files);
        }
    }
    // AFTERWARDS: объединить конвертеры FileState в один конвертер
    public class FileStateConverterToString : IValueConverter
    {
        public static readonly FileStateConverterToString Instance = new();

        public object? Convert(object? value, Type target, object? parameter, CultureInfo culture)
        {
            if (value is FileState fileState) 
            {
                switch (fileState)
                {
                    case FileState.Ok: 
                        return "Ок";

                    case FileState.Unknown:
                        return "Неизвестно";

                    case FileState.Deleted:
                        return "Удален";

                    case FileState.Modified:
                        return "Изменен";

                    case FileState.Added:
                        return "Добавлен";

                    default:
                        throw new ArgumentOutOfRangeException("Неккоректный FileState",nameof(fileState));
                } 
            }

            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value;
        }
    }
    public class FileStateConverterToImage : IValueConverter
    {
        public static readonly FileStateConverterToImage Instance = new();
        object? IValueConverter.Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
            string assemblyName = Assembly.GetEntryAssembly().GetName().Name;
            if (value is FileState fileState)
            switch (fileState)
                {
                    case FileState.Ok:
                        return new Bitmap(assets.Open(new Uri($"avares://{assemblyName}/Assets/ok.png")));

                    case FileState.Unknown:
                        return new Bitmap(assets.Open(new Uri($"avares://{assemblyName}/Assets/unknown.png")));

                    case FileState.Deleted:
                        return new Bitmap(assets.Open(new Uri($"avares://{assemblyName}/Assets/deleted.png")));

                    case FileState.Modified:
                        return new Bitmap(assets.Open(new Uri($"avares://{assemblyName}/Assets/modified.png")));

                    case FileState.Added:
                        return new Bitmap(assets.Open(new Uri($"avares://{assemblyName}/Assets/added.png")));

                    default:
                        return new Bitmap(assets.Open(new Uri($"avares://{assemblyName}/Assets/exception.png")));
                }
            return value;
        }

        object? IValueConverter.ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value;
        }
    }

    public class IsDirectoryFontConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isDirectory)
                if (isDirectory) return FontWeight.SemiBold;
            return FontWeight.Normal;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value;
        }
    }
    public enum AppMode
    {
        Ready,
        Updating,
        Checking,
    };
}
