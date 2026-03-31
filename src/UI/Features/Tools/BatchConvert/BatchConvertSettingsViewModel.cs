using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.VobSub.Ocr.Service;
using Nikse.SubtitleEdit.Features.Ocr;
using Nikse.SubtitleEdit.Features.Ocr.Engines;
using Nikse.SubtitleEdit.Features.Shared;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.Logic.Media;
using Nikse.SubtitleEdit.Logic.Ocr;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Tools.BatchConvert;

public partial class BatchConvertSettingsViewModel : ObservableObject
{
    [ObservableProperty] private bool _useSourceFolder;
    [ObservableProperty] private bool _useOutputFolder;
    [ObservableProperty] private string _outputFolder;
    [ObservableProperty] private bool _overwrite;
    [ObservableProperty] private ObservableCollection<string> _targetEncodings;
    [ObservableProperty] private string? _selectedTargetEncoding;

    [ObservableProperty] private ObservableCollection<OcrEngineItem> _ocrEngines;
    [ObservableProperty] private OcrEngineItem? _selectedOcrEngine;

    [ObservableProperty] private ObservableCollection<string> _languagePostFixes;
    [ObservableProperty] private string? _selectedLanguagePostFix;

    // Tesseract
    [ObservableProperty] private ObservableCollection<TesseractDictionary> _tesseractDictionaryItems;
    [ObservableProperty] private TesseractDictionary? _selectedTesseractDictionaryItem;

    // Paddle OCR
    [ObservableProperty] private ObservableCollection<OcrLanguage2> _paddleOcrLanguages;
    [ObservableProperty] private OcrLanguage2? _selectedPaddleOcrLanguage;

    // Ollama
    [ObservableProperty] private ObservableCollection<string> _ollamaLanguages;
    [ObservableProperty] private string? _selectedOllamaLanguage;
    [ObservableProperty] private string _ollamaModel;
    [ObservableProperty] private string _ollamaUrl;

    // Google Vision
    [ObservableProperty] private ObservableCollection<OcrLanguage> _googleVisionLanguages;
    [ObservableProperty] private OcrLanguage? _selectedGoogleVisionLanguage;
    [ObservableProperty] private string _googleVisionApiKey;

    // Mistral
    [ObservableProperty] private string _mistralApiKey;

    // Google Lens (Standalone + Sharp share language list)
    [ObservableProperty] private ObservableCollection<OcrLanguage2> _googleLensLanguages;
    [ObservableProperty] private OcrLanguage2? _selectedGoogleLensLanguage;

    // Binary image compare
    [ObservableProperty] private ObservableCollection<string> _imageCompareDatabases;
    [ObservableProperty] private string? _selectedImageCompareDatabase;

    // Visibility flags
    [ObservableProperty] bool _isOcrLanguageVisible;
    [ObservableProperty] bool _isTesseractOcrVisible;
    [ObservableProperty] bool _isPaddleOCrVisible;
    [ObservableProperty] bool _isOllamaVisible;
    [ObservableProperty] bool _isGoogleVisionVisible;
    [ObservableProperty] bool _isMistralVisible;
    [ObservableProperty] bool _isGoogleLensVisible;
    [ObservableProperty] bool _isBinaryImageCompareVisible;

    public Window? Window { get; set; }

    public bool OkPressed { get; private set; }

    private readonly IFolderHelper _folderHelper;

    public BatchConvertSettingsViewModel(IFolderHelper folderHelper)
    {
        var encodings = EncodingHelper.GetEncodings().Select(p => p.DisplayName).ToList();
        TargetEncodings = new ObservableCollection<string>(encodings);

        // Use the same engine list as the main OCR window
        OcrEngines = new ObservableCollection<OcrEngineItem>(OcrEngineItem.GetOcrEngines());

        LanguagePostFixes = new ObservableCollection<string>()
        {
            Se.Language.General.NoLanguageCode,
            Se.Language.General.TwoLetterLanguageCode,
            Se.Language.General.ThreeLetterLanguageCode,
        };
        SelectedLanguagePostFix = Se.Settings.Tools.BatchConvert.LanguagePostFix;
        if (SelectedLanguagePostFix == null)
        {
            SelectedLanguagePostFix = LanguagePostFixes[1];
        }

        // Paddle
        PaddleOcrLanguages = new ObservableCollection<OcrLanguage2>(PaddleOcr.GetLanguages().OrderBy(p => p.ToString()));

        // Tesseract
        TesseractDictionaryItems = new ObservableCollection<TesseractDictionary>();

        // Ollama
        OllamaLanguages = new ObservableCollection<string>(
            Iso639Dash2LanguageCode.List.Select(p => p.EnglishName).OrderBy(p => p));
        OllamaModel = Se.Settings.Ocr.OllamaModel;
        OllamaUrl = Se.Settings.Ocr.OllamaUrl;

        // Google Vision
        GoogleVisionApiKey = string.Empty;
        GoogleVisionLanguages = new ObservableCollection<OcrLanguage>(GoogleVisionOcr.GetLanguages().OrderBy(p => p.ToString()));

        // Mistral
        MistralApiKey = string.Empty;

        // Google Lens
        GoogleLensLanguages = new ObservableCollection<OcrLanguage2>(GoogleLensOcr.GetLanguages().OrderBy(p => p.ToString()));

        // Binary image compare
        ImageCompareDatabases = new ObservableCollection<string>(BinaryOcrDb.GetDatabases());

        _folderHelper = folderHelper;

        OutputFolder = string.Empty;
        LoadActiveTesseractDictionaries();
        LoadSettings();
        OnOcrEngineChanged();
    }

    private void LoadActiveTesseractDictionaries()
    {
        TesseractDictionaryItems.Clear();

        var folder = Se.TesseractModelFolder;
        if (!Directory.Exists(folder))
        {
            return;
        }

        var allDictionaries = TesseractDictionary.List();
        var items = new List<TesseractDictionary>();
        foreach (var file in Directory.GetFiles(folder, "*.traineddata"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name == "osd")
            {
                continue;
            }

            var dictionary = allDictionaries.FirstOrDefault(p => p.Code == name);
            if (dictionary != null)
            {
                items.Add(dictionary);
            }
            else
            {
                items.Add(new TesseractDictionary { Code = name, Name = name, Url = string.Empty });
            }
        }

        TesseractDictionaryItems.AddRange(items.OrderBy(p => p.ToString()));
    }

    private void LoadSettings()
    {
        UseSourceFolder = Se.Settings.Tools.BatchConvert.SaveInSourceFolder;
        UseOutputFolder = !UseSourceFolder;
        OutputFolder = Se.Settings.Tools.BatchConvert.OutputFolder;
        Overwrite = Se.Settings.Tools.BatchConvert.Overwrite;
        SelectedTargetEncoding = Se.Settings.Tools.BatchConvert.TargetEncoding;

        var savedEngineName = Se.Settings.Tools.BatchConvert.OcrEngine;
        SelectedOcrEngine = OcrEngines.FirstOrDefault(p => p.Name == savedEngineName) ?? OcrEngines.First();

        // Ollama
        OllamaModel = Se.Settings.Tools.BatchConvert.OllamaModel;
        OllamaUrl = Se.Settings.Tools.BatchConvert.OllamaUrl;
        SelectedOllamaLanguage = OllamaLanguages.FirstOrDefault(p => p == Se.Settings.Tools.BatchConvert.OllamaLanguage)
                                 ?? OllamaLanguages.FirstOrDefault(p => p == "English");

        // Google Vision
        GoogleVisionApiKey = Se.Settings.Tools.BatchConvert.GoogleVisionApiKey;
        SelectedGoogleVisionLanguage = GoogleVisionLanguages.FirstOrDefault(p => p.Code == Se.Settings.Tools.BatchConvert.GoogleVisionLanguage)
                                       ?? GoogleVisionLanguages.FirstOrDefault();

        // Mistral
        MistralApiKey = Se.Settings.Tools.BatchConvert.MistralApiKey;

        // Google Lens
        SelectedGoogleLensLanguage = GoogleLensLanguages.FirstOrDefault(p => p.Code == Se.Settings.Tools.BatchConvert.GoogleLensLanguage)
                                     ?? GoogleLensLanguages.FirstOrDefault(p => p.Code == "en")
                                     ?? GoogleLensLanguages.FirstOrDefault();

        // Binary image compare
        SelectedImageCompareDatabase = ImageCompareDatabases.FirstOrDefault(p => p == Se.Settings.Tools.BatchConvert.ImageCompareDatabase)
                                       ?? ImageCompareDatabases.FirstOrDefault();
    }

    private void SaveSettings()
    {
        Se.Settings.Tools.BatchConvert.SaveInSourceFolder = !UseOutputFolder;
        Se.Settings.Tools.BatchConvert.OutputFolder = OutputFolder;
        Se.Settings.Tools.BatchConvert.Overwrite = Overwrite;
        Se.Settings.Tools.BatchConvert.TargetEncoding = SelectedTargetEncoding ?? TargetEncodings.First();
        Se.Settings.Tools.BatchConvert.LanguagePostFix = SelectedLanguagePostFix ?? Se.Language.General.TwoLetterLanguageCode;
        Se.Settings.Tools.BatchConvert.OcrEngine = SelectedOcrEngine?.Name ?? "nOcr";

        if (SelectedOcrEngine?.EngineType == OcrEngineType.Tesseract)
        {
            Se.Settings.Tools.BatchConvert.TesseractLanguage = SelectedTesseractDictionaryItem?.Code ?? "eng";
        }

        if (SelectedOcrEngine?.EngineType == OcrEngineType.PaddleOcrStandalone ||
            SelectedOcrEngine?.EngineType == OcrEngineType.PaddleOcrPython)
        {
            Se.Settings.Tools.BatchConvert.PaddleLanguage = SelectedPaddleOcrLanguage?.Code ?? "en";
        }

        if (SelectedOcrEngine?.EngineType == OcrEngineType.Ollama)
        {
            Se.Settings.Tools.BatchConvert.OllamaLanguage = SelectedOllamaLanguage ?? "English";
            Se.Settings.Tools.BatchConvert.OllamaModel = OllamaModel;
            Se.Settings.Tools.BatchConvert.OllamaUrl = OllamaUrl;
        }

        if (SelectedOcrEngine?.EngineType == OcrEngineType.GoogleVision)
        {
            Se.Settings.Tools.BatchConvert.GoogleVisionApiKey = GoogleVisionApiKey;
            Se.Settings.Tools.BatchConvert.GoogleVisionLanguage = SelectedGoogleVisionLanguage?.Code ?? "en";
        }

        if (SelectedOcrEngine?.EngineType == OcrEngineType.Mistral)
        {
            Se.Settings.Tools.BatchConvert.MistralApiKey = MistralApiKey;
        }

        if (SelectedOcrEngine?.EngineType == OcrEngineType.GoogleLens ||
            SelectedOcrEngine?.EngineType == OcrEngineType.GoogleLensSharp)
        {
            Se.Settings.Tools.BatchConvert.GoogleLensLanguage = SelectedGoogleLensLanguage?.Code ?? "en";
        }

        if (SelectedOcrEngine?.EngineType == OcrEngineType.BinaryImageCompare)
        {
            Se.Settings.Tools.BatchConvert.ImageCompareDatabase = SelectedImageCompareDatabase ?? string.Empty;
        }

        Se.SaveSettings();
    }

    [RelayCommand]
    private async Task Ok()
    {
        if (UseOutputFolder && string.IsNullOrWhiteSpace(OutputFolder))
        {
            await MessageBox.Show(Window!, "Error",
                "Please select output folder", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        SaveSettings();
        OkPressed = true;
        Window?.Close();
    }

    [RelayCommand]
    private async Task BrowseOutputFolder()
    {
        var folder = await _folderHelper.PickFolderAsync(Window!, "Select output folder");
        if (!string.IsNullOrEmpty(folder))
        {
            OutputFolder = folder;
            UseOutputFolder = true;
            UseSourceFolder = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        Window?.Close();
    }

    internal void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Window?.Close();
        }
    }

    internal void OnOcrEngineChanged()
    {
        var engine = SelectedOcrEngine;
        if (engine == null)
        {
            IsOcrLanguageVisible = false;
            IsTesseractOcrVisible = false;
            IsPaddleOCrVisible = false;
            IsOllamaVisible = false;
            IsGoogleVisionVisible = false;
            IsMistralVisible = false;
            IsGoogleLensVisible = false;
            IsBinaryImageCompareVisible = false;
            return;
        }

        var et = engine.EngineType;

        IsOcrLanguageVisible = et != OcrEngineType.nOcr && et != OcrEngineType.BinaryImageCompare;
        IsTesseractOcrVisible = et == OcrEngineType.Tesseract;
        IsPaddleOCrVisible = et == OcrEngineType.PaddleOcrStandalone || et == OcrEngineType.PaddleOcrPython;
        IsOllamaVisible = et == OcrEngineType.Ollama;
        IsGoogleVisionVisible = et == OcrEngineType.GoogleVision;
        IsMistralVisible = et == OcrEngineType.Mistral;
        IsGoogleLensVisible = et == OcrEngineType.GoogleLens || et == OcrEngineType.GoogleLensSharp;
        IsBinaryImageCompareVisible = et == OcrEngineType.BinaryImageCompare;

        if (et == OcrEngineType.Tesseract)
        {
            SelectedTesseractDictionaryItem = TesseractDictionaryItems
                .FirstOrDefault(p => p.Code == Se.Settings.Tools.BatchConvert.TesseractLanguage)
                ?? TesseractDictionaryItems.FirstOrDefault();
        }

        if (et == OcrEngineType.PaddleOcrStandalone || et == OcrEngineType.PaddleOcrPython)
        {
            SelectedPaddleOcrLanguage = PaddleOcrLanguages
                .FirstOrDefault(p => p.Code == Se.Settings.Tools.BatchConvert.PaddleLanguage)
                ?? PaddleOcrLanguages.FirstOrDefault();
        }

        if (et == OcrEngineType.Ollama)
        {
            SelectedOllamaLanguage = OllamaLanguages.FirstOrDefault(p => p == Se.Settings.Tools.BatchConvert.OllamaLanguage)
                                     ?? OllamaLanguages.FirstOrDefault(p => p == "English");
        }

        if (et == OcrEngineType.GoogleVision)
        {
            SelectedGoogleVisionLanguage = GoogleVisionLanguages
                .FirstOrDefault(p => p.Code == Se.Settings.Tools.BatchConvert.GoogleVisionLanguage)
                ?? GoogleVisionLanguages.FirstOrDefault();
        }

        if (et == OcrEngineType.GoogleLens || et == OcrEngineType.GoogleLensSharp)
        {
            SelectedGoogleLensLanguage = GoogleLensLanguages
                .FirstOrDefault(p => p.Code == Se.Settings.Tools.BatchConvert.GoogleLensLanguage)
                ?? GoogleLensLanguages.FirstOrDefault(p => p.Code == "en")
                ?? GoogleLensLanguages.FirstOrDefault();
        }

        if (et == OcrEngineType.BinaryImageCompare)
        {
            SelectedImageCompareDatabase = ImageCompareDatabases
                .FirstOrDefault(p => p == Se.Settings.Tools.BatchConvert.ImageCompareDatabase)
                ?? ImageCompareDatabases.FirstOrDefault();
        }
    }
}
