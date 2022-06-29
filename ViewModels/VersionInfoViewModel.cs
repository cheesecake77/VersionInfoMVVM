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
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Run = DocumentFormat.OpenXml.Wordprocessing.Run;
using Text = DocumentFormat.OpenXml.Wordprocessing.Text;
using DocumentFormat.OpenXml.Spreadsheet;

namespace VersionInfoMVVM.ViewModels 
{
    // AFTERWARDS: всплывающие окна с предложением сохранить после Create, Exit и Open
    // AFTERWARDS: написать unit test'ы
    // AFTERWARDS: рефакторинг
    // AFTERWARDS: реализовать MVVM-friendly диалоги
    // TODO: добавить проверки для работы с файлами
    public class VersionInfoViewModel : ViewModelBase
    {
        //Флаги
        bool stopRunning = false, running = false;

        //Свойства для работы с данными
        string? currentFile;
        public string? CurrentFile { get => currentFile; set => this.RaiseAndSetIfChanged(ref currentFile, value); }

        ObservableCollection<BaseDescription>? fileData;
        public ObservableCollection<BaseDescription>? FileData { get => fileData; set => this.RaiseAndSetIfChanged(ref fileData, value); }
        ObservableCollection<string>? directoryData;
        public ObservableCollection<string>? DirectoryData { get => directoryData; set => this.RaiseAndSetIfChanged(ref directoryData, value); }
        ObservableCollection<BaseDescription>? savedFileData;
        public ObservableCollection<BaseDescription>? SavedFileData { get => savedFileData; set => savedFileData = value; }

        //Свойства для работы с интерфейсом
        string? selectedItem;
        public string? FolderListBoxItem { get => selectedItem; set => this.RaiseAndSetIfChanged(ref selectedItem, value); }

        string? statusBarText;
        public string? StatusBarText { get => statusBarText; set => this.RaiseAndSetIfChanged(ref statusBarText, value); }

        string? updateButtonText;
        public string? UpdateButtonText { get => updateButtonText; set => this.RaiseAndSetIfChanged(ref updateButtonText, value); }

        string? checkButtonText;
        public string? CheckButtonText { get => checkButtonText; set => this.RaiseAndSetIfChanged(ref checkButtonText, value); }


        public VersionInfoViewModel(DataUnit data)
        {
            //Инициализация
            FileData = new ObservableCollection<BaseDescription>();
            DirectoryData = new ObservableCollection<string>();
            StatusBarText = "Готово";
            UpdateButtonText = "Обновить";
            CheckButtonText = "Сравнить";

            if (data.directoryData != null) DirectoryData = data.directoryData;
            if (data.fileData != null) FileData = data.fileData;


            //Определение обработчкиов меню
            OnOpenItem = ReactiveCommand.Create(() =>
            {
                var d = new OpenFileDialog { Title = "Открыть файл..." };
                d.Filters.Add(new FileDialogFilter() { Name = "XML-документ", Extensions = { "xml" } });
                var res = d.ShowAsync(App.MainWindow).Result;
                if (res != null)
                {
                    CurrentFile = res[0];
                    var serializer = new XmlSerializer(typeof(DataUnit));
                    using var reader = new StreamReader(res[0]);
                    var input = (DataUnit?)serializer.Deserialize(reader);
                    if (input != null)
                    {
                        DirectoryData = input.directoryData;
                        FileData = new ObservableCollection<BaseDescription>(input.fileData);
                        SavedFileData = new ObservableCollection<BaseDescription>(input.fileData);
                    }

                }
            });
            OnCreateItem = ReactiveCommand.Create(() =>
            {
                if (FileData != null && DirectoryData != null)
                {
                    FileData.Clear();
                    DirectoryData.Clear();
                    if (CurrentFile != null) CurrentFile = null;
                }
            });
            OnSaveItem = ReactiveCommand.Create(async () =>
            {
                if (CurrentFile is String currentfile)
                {
                    var serializer = new XmlSerializer(typeof(DataUnit));
                    DataUnit output = new() { fileData = FileData, directoryData = DirectoryData };
                    using var writer = new StreamWriter(CurrentFile);
                    serializer.Serialize(writer, output);
                }

                if (CurrentFile is null)
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

            //Определение обработчиков подменю
            ExportToTXT = ReactiveCommand.Create(async() => {
                var d = new SaveFileDialog();
                d.Filters.Add(new FileDialogFilter() { Name = "Текстовый документ", Extensions = { "txt" } });
                var res = await d.ShowAsync(App.MainWindow);
                if (res != null)
                {
                    using (var writer = new StreamWriter(res, false))
                    {
                        var timeFormat = "dd-MM-yyyy HH:mm:ss";
                        int nameLength = FileData.Max(f => f.Name.Length);
                        int stateLength = 8;
                        int versionLength = FileData.OfType<FileDescription>().Max(f => f.Version.Length);
                        int timeLength = timeFormat.Length;
                        int sizeLength = FileData.OfType<FileDescription>().Max(f => f.Size.ToString().Length);
                        int hashSize = 32;

                        var fileFormat = string.Format("{{0, -{0}}} {{1,-{1}}} {{2,-{2}}} " +
                            "{{3,-{3}}} {{4,{4}}} {{5,-{5}}}", nameLength, stateLength, versionLength, timeLength, sizeLength, hashSize);

                        var headerFormat = string.Format("{{0, -{0}}} {{1,-{1}}} {{2,-{2}}} " +
                            "{{3,-{3}}} {{4,-{4}}} {{5,-{5}}}", nameLength, stateLength, versionLength, timeLength, sizeLength, hashSize);

                        writer.WriteLine(String.Format(headerFormat, "Имя", "Состояние", "Версия", "Время", "Размер", "Хэш"));
                        for (int i = 0; i < FileData.Count; i++)
                        {
                            if (FileData[i] is DirectoryDescription)
                            {
                                writer.WriteLine();
                                writer.WriteLine($"{FileData[i].Name}");
                            }

                            if (FileData[i] is FileDescription fd)
                            {
                                writer.WriteLine(String.Format(fileFormat, fd.Name, fd.FileState, fd.Version, fd.Time.ToString(timeFormat), fd.Size, fd.Hash));
                            }
                        }
                    }
                }
            });
            ExportToCSV = ReactiveCommand.Create(async () => {
                var timeFormat = "dd-MM-yyyy HH:mm:ss";
                var d = new SaveFileDialog();
                d.Filters.Add(new FileDialogFilter() { Name = "CSV - файл", Extensions = { "csv" } });
                var res = await d.ShowAsync(App.MainWindow);
                if (res != null)
                {
                    using (var writer = new StreamWriter(res, false, Encoding.UTF8))
                    {
                        writer.WriteLine($"{"Name"};{"FileState"};{"Version"};{"Time"};{"Size"};{"Hash"};");
                        for (int i = 0; i < FileData.Count; i++)
                        {
                            if (FileData[i] is DirectoryDescription dd) writer.WriteLine(dd.Name + ";");
                            if (FileData[i] is FileDescription fd) writer.WriteLine($"{fd.Name};{fd.FileState};{fd.Version};{fd.Time.ToString(timeFormat)};{fd.Size};{fd.Hash};");
                        }
                    }
                }
            });
            ExportToDOCX = ReactiveCommand.Create(async () => {
                // HIGHPRIORITY: исправить запись в docx
                var d = new SaveFileDialog();
                d.Filters.Add(new FileDialogFilter() { Name = "Документы Word", Extensions = { "docx" } });
                var res = await d.ShowAsync(App.MainWindow);
                if (res != null)
                {
                    using (var doc = WordprocessingDocument.Create(res, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
                    {
                        var timeFormat = "dd-MM-yyyy HH:mm:ss";
                        int nameLength = 0;
                        int stateLength = 8;
                        int versionLength = FileData.OfType<FileDescription>().Max(f => f.Version.Length);
                        int timeLength = timeFormat.Length;
                        int sizeLength = FileData.OfType<FileDescription>().Max(f => f.Size.ToString().Length);
                        int hashSize = 32;

                        var fileFormat = string.Format("{{0, -{0}}} {{1,-{1}}} {{2,-{2}}} " +
                            "{{3,-{3}}} {{4,-{4}}} {{5,-{5}}}", nameLength, stateLength, versionLength, timeLength, sizeLength, hashSize);

                        var headerFormat = string.Format("{{0, -{0}}} {{1,-{1}}} {{2,-{2}}} " +
                            "{{3,-{3}}} {{4,-{4}}} {{5,-{5}}}", nameLength, stateLength, versionLength, timeLength, sizeLength, hashSize);

                        MainDocumentPart mainPart = doc.AddMainDocumentPart();
                        mainPart.Document = new Document();
                        Body body = mainPart.Document.AppendChild(new Body());
                        for (int i = 0; i < FileData.Count; i++)
                        {
                            Paragraph para = body.AppendChild(new Paragraph());
                            Run run = para.AppendChild(new Run());
                            if (FileData[i] is DirectoryDescription dd) run.AppendChild(new Text($"{dd.Name}"));
                            if (FileData[i] is FileDescription fd) run.AppendChild(new Text(String.Format(fileFormat, fd.Name, 
                                fd.FileState, fd.Version, fd.Time.ToString(timeFormat), fd.Size, fd.Hash)));
                            
                        }
                        doc.Save();
                        doc.Close();
                    }
                }
            });
            ExportToXLSX = ReactiveCommand.Create(async () => {
                var d = new SaveFileDialog();
                d.Filters.Add(new FileDialogFilter() { Name = "Документы Excel", Extensions = { "xlsx" } });
                var res = await d.ShowAsync(App.MainWindow);
                if (res != null)
                {
                    using (var doc = SpreadsheetDocument.Create(res, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook))
                    {
                        var timeFormat = "dd-MM-yyyy HH:mm:ss";
                        int nameLength = FileData.Max(f => f.Name.Length);
                        int stateLength = 10;
                        int versionLength = FileData.OfType<FileDescription>().Max(f => f.Version.Length)  > "Версия".Length + 1 ? FileData.OfType<FileDescription>().Max(f => f.Version.Length) : "Версия".Length + 1;
                        int timeLength = timeFormat.Length;
                        int sizeLength = FileData.OfType<FileDescription>().Max(f => f.Size.ToString().Length) > "Размер".Length ? FileData.OfType<FileDescription>().Max(f => f.Size.ToString().Length) : "Размер".Length + 1;
                        int hashLength = 34;
                        List<int> ColumnsWidths = new() { nameLength, stateLength, versionLength, timeLength, sizeLength, hashLength };

                        WorkbookPart workbookpart = doc.AddWorkbookPart();
                        workbookpart.Workbook = new Workbook();
                        WorksheetPart worksheetPart = workbookpart.AddNewPart<WorksheetPart>();
                        worksheetPart.Worksheet = new Worksheet(new SheetData());
                        Sheets sheets = doc.WorkbookPart.Workbook.AppendChild<Sheets>(new Sheets());
                        Sheet sheet = new Sheet()
                        {
                            Id = doc.WorkbookPart.GetIdOfPart(worksheetPart),
                            SheetId = 1,
                            Name = "Список файлов"
                        };
                        sheets.Append(sheet);

                        Worksheet worksheet = new Worksheet();
                        SheetViews sheetViews = new SheetViews();
                        SheetView sheetView = new SheetView() { TabSelected = true, WorkbookViewId = 0U };
                        sheetViews.Append(sheetView);
                        SheetFormatProperties sheetFormatProperties = new SheetFormatProperties() { DefaultRowHeight = 15D, DyDescent = 0.25D };

                        DocumentFormat.OpenXml.Spreadsheet.Columns columns = new DocumentFormat.OpenXml.Spreadsheet.Columns();

                        for (int i = 0; i < ColumnsWidths.Count; i++)
                        {
                            columns.Append(new DocumentFormat.OpenXml.Spreadsheet.Column() { Min = (uint)i + 2, Max = (uint)i + 2, Width = ColumnsWidths[i] }); 
                        }

                        SheetData sheetData = new SheetData();
                        var header = new Row() { RowIndex = 1 };
                        header.Append(new Cell());
                        header.Append(new Cell()
                        {
                            CellValue = new CellValue("Имя"),
                            DataType = new DocumentFormat.OpenXml.EnumValue<CellValues>(CellValues.String)
                        });
                        header.Append(new Cell() 
                        {
                            CellValue = new CellValue("Состояние"),
                            DataType = new DocumentFormat.OpenXml.EnumValue<CellValues>(CellValues.String)
                        });
                        header.Append(new Cell()
                        {
                            CellValue = new CellValue("Версия"),
                            DataType = new DocumentFormat.OpenXml.EnumValue<CellValues>(CellValues.String)
                        });
                        header.Append(new Cell()
                        {
                            CellValue = new CellValue("Время"),
                            DataType = new DocumentFormat.OpenXml.EnumValue<CellValues>(CellValues.String)
                        });
                        header.Append(new Cell()
                        {
                            CellValue = new CellValue("Размер"),
                            DataType = new DocumentFormat.OpenXml.EnumValue<CellValues>(CellValues.String)
                        });
                        header.Append(new Cell()
                        {
                            CellValue = new CellValue("Хэш"),
                            DataType = new DocumentFormat.OpenXml.EnumValue<CellValues>(CellValues.String)
                        });
                        sheetData.Append(header);

                        for (int r = 0; r < FileData.Count; r++)
                        {
                            var row = new Row { RowIndex = (uint)r + 2 };
                            row.Append(new Cell());
                            if (FileData[r] is DirectoryDescription)
                            {
                                row.Append(new Cell()
                                {
                                    CellValue = new CellValue(FileData[r].Name),
                                    DataType = new DocumentFormat.OpenXml.EnumValue<CellValues>(CellValues.String)
                                });
                            }
                            else if (FileData[r] is FileDescription fd)
                            {
                                row.Append(new Cell()
                                {
                                    CellValue = new CellValue(fd.Name),
                                    DataType = new DocumentFormat.OpenXml.EnumValue<CellValues>(CellValues.String)
                                }); ;
                                row.Append(new Cell()
                                {
                                    CellValue = new CellValue(fd.FileState.ToString()),
                                    DataType = new DocumentFormat.OpenXml.EnumValue<CellValues>(CellValues.String)
                                });
                                row.Append(new Cell()
                                {
                                    CellValue = new CellValue(fd.Version),
                                    DataType = new DocumentFormat.OpenXml.EnumValue<CellValues>(CellValues.String)
                                }); ;
                                // TODO: проверить, можно ли записывать время как string
                                row.Append(new Cell()
                                {
                                    CellValue = new CellValue(fd.Time.ToString(timeFormat)),
                                    DataType = new DocumentFormat.OpenXml.EnumValue<CellValues>(CellValues.String)
                                });
                                row.Append(new Cell()
                                {
                                    CellValue = new CellValue((double)fd.Size),
                                    DataType = new DocumentFormat.OpenXml.EnumValue<CellValues>(CellValues.Number)
                                });
                                row.Append(new Cell()
                                {
                                    CellValue = new CellValue(fd.Hash),
                                    DataType = new DocumentFormat.OpenXml.EnumValue<CellValues>(CellValues.String)
                                });
                            }
                            sheetData.Append(row);
                        }
                        worksheet.Append(sheetViews);
                        worksheet.Append(sheetFormatProperties);
                        worksheet.Append(columns);
                        worksheet.Append(sheetData);

                        worksheetPart.Worksheet = worksheet;
                        doc.Close();

                    }

                }
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
                if (FolderListBoxItem is not String folder)
                {
                    return;
                }
                DirectoryData.Remove(folder);
                foreach (var file in FileData.Where(f => f.Path.StartsWith(folder)).ToList()) FileData.Remove(file);
                SavedFileData = new ObservableCollection<BaseDescription>(FileData);
            });
            OnUpdateButton = ReactiveCommand.Create(() =>
            {
                if (running)
                {
                    stopRunning = true;
                    running = false;
                    statusBarText = "Готово";
                    UpdateButtonText = "Обновить";
                    return;
                }
                UpdateButtonText = "Отмена";

                if (FileData != null) FileData.Clear();
                var flist = new ObservableCollection<BaseDescription>();
                stopRunning = false;
                running = true;
                Task.Run(() =>
                {
                    if (DirectoryData != null)
                        foreach (var d in DirectoryData)
                        {
                            if (stopRunning) throw new Exception();
                            FindFiles(d, flist);
                        }
                }).ContinueWith(t => {
                    StatusBarText = "Готово";
                    UpdateButtonText = "Обновить";
                    running = false;
                    if (t.IsFaulted)
                    {
                        StatusBarText = "Update canceled";
                        stopRunning = false;
                        running = false;
                    }
                    FileData = new ObservableCollection<BaseDescription>(flist);
                    SavedFileData = new ObservableCollection<BaseDescription>(flist);
                });
            });
            OnCheckButton = ReactiveCommand.Create(() =>
            {
                if (running)
                {
                    stopRunning = true;
                    running = false;
                    statusBarText = "Готово";
                    CheckButtonText = "Сравнить";
                    return;
                }
                CheckButtonText = "Отмена";

                //Сохранение текущего состояния
                var oldFileList = new ObservableCollection<BaseDescription>(FileData);

                //Обновляем данные
                if (FileData != null) FileData.Clear();
                var flist = new ObservableCollection<BaseDescription>();
                stopRunning = false;
                running = true;
                _ = Task.Run(() =>
                {
                    if (DirectoryData != null)
                        foreach (var d in DirectoryData)
                        {
                            if (stopRunning) throw new Exception();
                            FindFiles(d, flist);
                        }
                }).ContinueWith(t =>
                {
                    StatusBarText = "Готово";
                    CheckButtonText = "Сравнить";
                    running = false;
                    if (t.IsFaulted)
                    {
                        StatusBarText = "Canceled";
                        stopRunning = false;
                        running = false;
                    }

                    FileData = flist;

                    var newFileList = new ObservableCollection<BaseDescription>(FileData);

                    //Сравниваем старые и новые данные, изменеяем НОВЫЕ данные 
                    foreach (var file in newFileList.OfType<FileDescription>())
                    {
                        var found = (FileDescription?)oldFileList.FirstOrDefault(f => f.Path == file.Path);
                        if (found == null) file.FileState = FileState.Added;
                    }

                    foreach (var file in oldFileList)
                    {
                        if (file is DirectoryDescription dir)
                        {
                            var found = newFileList.FirstOrDefault(f => f.Path == dir.Path);
                            if (found is null) newFileList.Add(dir);
                        }

                        if (file is FileDescription fl)
                        {
                            var found = newFileList.FirstOrDefault(f => f.Path == fl.Path);
                            if (found is null)
                            {
                                fl.FileState = FileState.Deleted;
                                newFileList.Add(fl);
                            }
                        }
                    }
                    foreach (var file in newFileList.OfType<FileDescription>())
                    {
                        var found = oldFileList.FirstOrDefault(f => f.Path == file.Path);
                        if (found is not null && !file.Equals((FileDescription)found)) file.FileState = FileState.Modified;
                    }
                    //Запись данных
                    FileData = new ObservableCollection<BaseDescription>(newFileList.OrderBy(f => f.Path));
                    savedFileData = new ObservableCollection<BaseDescription>(FileData);
                    RemoveEmptyFolders();
                });
            });

            //Определение обработчиков RadioButton'ов
            OnAllRadioButton = ReactiveCommand.Create(() => {
                if (SavedFileData is not null) FileData = new ObservableCollection<BaseDescription>(SavedFileData);
            });
            OnAddedRadioButton = ReactiveCommand.Create(() => {
                if (FileData is null) return;
                if (SavedFileData is null) return;
                FileData.Clear();
                foreach (var file in SavedFileData)
                {
                    if (file is DirectoryDescription) FileData.Add(file);
                    if (file is FileDescription fd)
                        if (fd.FileState == FileState.Added) FileData.Add(fd);
                }
                RemoveEmptyFolders();
            });
            OnDeletedRadioButton = ReactiveCommand.Create(() => {
                if (FileData is null) return;
                if (SavedFileData is null) return;
                FileData.Clear();
                foreach (var file in SavedFileData)
                {
                    if (file is DirectoryDescription) FileData.Add(file);
                    if (file is FileDescription fd)
                        if (fd.FileState == FileState.Deleted) FileData.Add(fd);
                }
                RemoveEmptyFolders();
            });
            OnModifiedRadioButton = ReactiveCommand.Create(() => {
                if (FileData is null) return;
                if (SavedFileData is null) return;
                FileData.Clear();
                foreach (var file in SavedFileData)
                {
                    if (file is DirectoryDescription) FileData.Add(file);
                    if (file is FileDescription fd)
                        if (fd.FileState == FileState.Modified) FileData.Add(fd);
                }
                RemoveEmptyFolders();
            });

        }
        //Объвление обработчиков меню
        public ReactiveCommand<Unit, Unit> OnOpenItem { get; }
        public ReactiveCommand<Unit, Unit> OnCreateItem { get; }
        public ReactiveCommand<Unit, Task> OnSaveItem { get; }
        public ReactiveCommand<Unit, Task> OnSaveAsItem { get; }
        public ReactiveCommand<Unit, Unit> OnCloseItem { get; }

        //Объвление обработчиков подменю
        public ReactiveCommand<Unit, Task> ExportToTXT { get; }
        public ReactiveCommand<Unit, Task> ExportToCSV { get; }
        public ReactiveCommand<Unit, Task> ExportToDOCX { get; }
        public ReactiveCommand<Unit, Task> ExportToXLSX { get; }

        //Объявление обработчиков кнопок
        public ReactiveCommand<Unit, Task> OnAddButton { get; }
        public ReactiveCommand<Unit, Unit> OnDeleteButton { get; }
        public ReactiveCommand<Unit, Unit> OnCheckButton { get; }
        public ReactiveCommand<Unit, Unit> OnUpdateButton { get; }

        //Объявление обработчиков RadioButton'ов
        public ReactiveCommand<Unit, Unit> OnAllRadioButton { get; }
        public ReactiveCommand<Unit, Unit> OnAddedRadioButton { get; }
        public ReactiveCommand<Unit, Unit> OnDeletedRadioButton { get; }
        public ReactiveCommand<Unit, Unit> OnModifiedRadioButton { get; }


        //Вспомогательные методы
        private void FindFiles(string directory, ObservableCollection<BaseDescription> files)
        {
            StatusBarText = $"Загрузка файлов из директории {directory}";
            var flist = Directory.GetFiles(directory);
            if (flist.Count() > 0)
            {
                files.Add(new DirectoryDescription() { Path = directory });
                foreach (string f in flist)
                {
                    if (stopRunning) throw new Exception();
                    var file = new FileDescription() { Path = f };
                    files.Add(file.FillProperties());

                }
            }
            var dlist = Directory.GetDirectories(directory);
            if (dlist != null)
                foreach (var d in dlist)
                {
                    if (stopRunning) throw new Exception();
                    FindFiles(d, files);
                }
        }
        private void RemoveEmptyFolders()
        {
            foreach (var f in FileData.OfType<DirectoryDescription>().ToList()) 
            {
                if (FileData.IndexOf(f) == FileData.Count - 1)
                {
                    FileData.Remove(f);
                    return;
                }

                if (FileData[FileData.IndexOf(f)+1] is DirectoryDescription) FileData.Remove(f);
            }
        }
    }

    //Конвертеры
    public class FileStateConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is FileState fileState)
            {
                if (targetType.IsAssignableTo(typeof(string)))
                    switch (fileState)
                    {
                        case FileState.Ok:
                            return "Ок";

                        case FileState.Deleted:
                            return "Удален";

                        case FileState.Modified:
                            return "Изменен";

                        case FileState.Added:
                            return "Добавлен";

                        default:
                            throw new ArgumentOutOfRangeException("Неккоректный FileState", nameof(fileState));
                    }

                if (targetType.IsAssignableTo(typeof(IImage)))
                {
                    var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                    string? assemblyName = Assembly.GetEntryAssembly().GetName().Name;
                    if (assemblyName is null) throw new Exception("Ошибка при определении имени сборки");
                    if (assets is null) throw new Exception("Не найдены assets");
                    switch (fileState) 
                    {
                        case FileState.Ok:
                            return new Bitmap(assets.Open(new Uri($"avares://{assemblyName}/Assets/ok.png")));

                        case FileState.Deleted:
                            return new Bitmap(assets.Open(new Uri($"avares://{assemblyName}/Assets/deleted.png")));

                        case FileState.Modified:
                            return new Bitmap(assets.Open(new Uri($"avares://{assemblyName}/Assets/modified.png")));

                        case FileState.Added:
                            return new Bitmap(assets.Open(new Uri($"avares://{assemblyName}/Assets/added.png")));

                        default:
                            return new Bitmap(assets.Open(new Uri($"avares://{assemblyName}/Assets/exception.png")));
                    }
                }
            }
            throw new ArgumentException("Передан аргумент не являщийся FileState", nameof(fileState));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value;
        }
    }

    public class IsDirectoryFontConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isDirectory)
            {
                if (isDirectory) return FontWeight.SemiBold;
                return FontWeight.Normal;
            }
            throw new ArgumentException("Передан аргумент не являющийся bool", nameof(isDirectory)); ;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {

            return value;
        }
    }

}
