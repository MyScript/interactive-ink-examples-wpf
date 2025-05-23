// Copyright @ MyScript. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

using MyScript.IInk.UIReferenceImplementation;
using AvailableActions = MyScript.IInk.UIReferenceImplementation.EditorUserControl.ContextualActions;

namespace MyScript.IInk.Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string _configurationDirectory = "configurations";
        private const string _defaultConfiguration   = "interactivity.json";

        private Engine _engine;
        private Editor _editor => UcEditor.Editor;

        private Graphics.Point _lastPointerPosition;
        private IContentSelection _lastContentSelection;

        private int _filenameIndex;
        private string _packageName;

        private string _penWidth;
        private string _penColor;
        private string _highlighterWidth;
        private string _highlighterColor;

        private const float  PenThinWidth   = 0.25F;
        private const float  PenMediumWidth = 0.65F;        // default (in mm)
        private const float  PenLargeWidth  = 1.65F;
        private const string PenBlackColor  = "#000000";    // default
        private const string PenRedColor    = "#EA4335";
        private const string PenGreenColor  = "#34A853";
        private const string PenBlueColor   = "#4285F4";

        private const float  HighlighterThinWidth   = 1.67F;
        private const float  HighlighterMediumWidth = 5.0F;         // default (in mm)
        private const float  HighlighterLargeWidth  = 15.0F;
        private const string HighlighterYellowColor = "#FBBC05";    // default
        private const string HighlighterRedColor    = "#EA4335";
        private const string HighlighterGreenColor  = "#34A853";
        private const string HighlighterBlueColor   = "#4285F4";

        public MainWindow()
        {
            this._filenameIndex = 0;
            this._packageName = "";

            InitializeComponent();

            TypeOfContentDialog.AddNewPart += TypeOfContentDialog_AddNewPart;
            TypeOfContentDialog.SetParent(TypeOfContentDialogParent);

            this.Closing += Window_Closing;

            // Add keyboard shortcuts
            {
                var undoCmd = new System.Windows.Input.RoutedCommand();
                var undoGst = new System.Windows.Input.KeyGesture(  System.Windows.Input.Key.Z,
                                                                    System.Windows.Input.ModifierKeys.Control);

                var redoCmd = new System.Windows.Input.RoutedCommand();
                var redoGstZ = new System.Windows.Input.KeyGesture( System.Windows.Input.Key.Z,
                                                                    System.Windows.Input.ModifierKeys.Control|System.Windows.Input.ModifierKeys.Shift);
                var redoGstY = new System.Windows.Input.KeyGesture( System.Windows.Input.Key.Y,
                                                                    System.Windows.Input.ModifierKeys.Control);

                undoCmd.InputGestures.Add(undoGst);
                redoCmd.InputGestures.Add(redoGstZ);
                redoCmd.InputGestures.Add(redoGstY);

                CommandBindings.Add(new System.Windows.Input.CommandBinding(undoCmd, (e, s) => { _editor?.Undo(); }));
                CommandBindings.Add(new System.Windows.Input.CommandBinding(redoCmd, (e, s) => { _editor?.Redo(); }));
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_editor != null)
            {
                var part = _editor.Part;
                var package = part?.Package;
                package?.Save();

                _editor.Part = null;

                part?.Dispose();
                package?.Dispose();

                _editor.Dispose();
            }

            UcEditor?.Closing();
        }

        private void ResetSelection()
        {
            _lastContentSelection?.Dispose();
            _lastContentSelection = null;
        }

        private void EnableStrokePrediction(bool enable, uint durationMs = 16)
        {
            _engine.Configuration.SetBoolean("renderer.prediction.enable", enable);
            _engine.Configuration.SetNumber("renderer.prediction.duration", durationMs);
        }

        private void SetMaxRecognitionThreadCount(uint threadCount)
        {
            _engine.Configuration.SetNumber("max-recognition-thread-count", threadCount);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize Interactive Ink runtime environment
                _engine = Engine.Create(MyScript.Certificate.MyCertificate.Bytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            // Folders "conf" and "resources" are currently parts of the layout
            // (for each conf/res file of the project => properties => "Build Action = content")
            string[] confDirs = new string[1];
            confDirs[0] = "conf";
            _engine.Configuration.SetStringArray("configuration-manager.search-path", confDirs);

            var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var tempFolder =  Path.Combine(localFolder, "MyScript", "tmp");
            _engine.Configuration.SetString("content-package.temp-folder", tempFolder);

            EnableStrokePrediction(true, 16);

            // Configure multithreading for text recognition
            SetMaxRecognitionThreadCount(1);

            // Initialize the editor with the engine
            FontMetricsProvider.Initialize();
            UcEditor.Engine = _engine;
            UcEditor.Initialize(this);
            UcEditor.SmartGuide.MoreClicked += ShowSmartGuideMenu;

            // Set default tool/mode/styles
            SetInputTool(PointerTool.PEN);
            ActivePen_Click(ActivePen, null);
            PenWidth_Clicked(PenMedium, null);
            PenColor_Clicked(PenBlack, null);
            HighlighterWidth_Clicked(HighlighterMedium, null);
            HighlighterColor_Clicked(HighlighterYellow, null);

            NewFile();
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            _editor.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            _editor.Redo();
        }

        private void Previous_Click(object sender, RoutedEventArgs e)
        {
            var part = _editor.Part;

            if (part != null)
            {
                var package = part.Package;
                var index = package.IndexOfPart(part);

                if (index > 0)
                {
                    ResetSelection();
                    SetPart(null);

                    while (--index >= 0)
                    {
                        ContentPart newPart = null;

                        try
                        {
                            // Select new part
                            newPart = part.Package.GetPart(index);
                            SetPart(newPart);
                            Type.Text = _packageName + " - " + newPart.Type;
                            part.Dispose();
                            break;
                        }
                        catch
                        {
                            // Cannot set this part, try the previous one
                            SetPart(null);
                            Type.Text = "";
                            newPart?.Dispose();
                        }
                    }

                    if (index < 0)
                    {
                        // Restore current part if none can be set
                        SetPart(part);
                        Type.Text = _packageName + " - " + part.Type;
                    }

                    // Reset viewing parameters
                    UcEditor.ResetView(false);
                }
            }
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            var part = _editor.Part;

            if (part != null)
            {
                var package = part.Package;
                var count = package.PartCount;
                var index = package.IndexOfPart(part);

                if (index < count - 1)
                {
                    ResetSelection();
                    SetPart(null);

                    while (++index < count)
                    {
                        ContentPart newPart = null;

                        try
                        {
                            // Select new part
                            newPart = part.Package.GetPart(index);
                            SetPart(newPart);
                            Type.Text = _packageName + " - " + newPart.Type;
                            part.Dispose();
                            break;
                        }
                        catch
                        {
                            // Cannot set this part, try the next one
                            SetPart(null);
                            Type.Text = "";
                            newPart?.Dispose();
                        }
                    }

                    if (index >= count)
                    {
                        // Restore current part if none can be set
                        SetPart(part);
                        Type.Text = _packageName + " - " + part.Type;
                    }

                    // Reset viewing parameters
                    UcEditor.ResetView(false);
                }
            }
        }

        private void NewPart_Click(object sender, RoutedEventArgs e)
        {
            if (_editor.Part != null)
                NewPart();
            else
                NewFile();
        }

        private void SavePackage()
        {
            var part = _editor.Part;
            var package = part?.Package;
            package?.Save();
        }

        private void ClosePackage()
        {
            var part = _editor.Part;
            var package = part?.Package;
            SetPart(null);
            part?.Dispose();
            package?.Dispose();
            Type.Text = "";
        }

        Tuple<string, string> SplitPartTypeAndProfile(string partTypeWithProfile)
        {
            var profileStart = partTypeWithProfile.LastIndexOf("(");
            string partType;
            string profile;

            if (profileStart < 0)
            {
                partType = partTypeWithProfile;
                profile = string.Empty;
            }
            else
            {
                partType = partTypeWithProfile.Substring(0, profileStart);
                profile = partTypeWithProfile.Substring(profileStart + 1, partTypeWithProfile.Length - profileStart - 2);
            }

            return new Tuple<string, string>(partType.Trim(), profile.Trim());
        }

        private void TypeOfContentDialog_AddNewPart(string newPartType, bool newPackage)
        {
            (var partType, var profile) = SplitPartTypeAndProfile(newPartType);

            if (String.IsNullOrEmpty(partType))
                return;

            ResetSelection();

            if (!newPackage && (_editor.Part != null))
            {
                var previousPart = _editor.Part;
                var package = previousPart.Package;

                try
                {
                    SetPart(null);

                    var part = package.CreatePart(partType);

                    SetConfigurationProfile(part, profile);
                    SetPart(part);
                    Type.Text = _packageName + " - " + part.Type;

                    previousPart.Dispose();
                }
                catch (Exception ex)
                {
                    SetPart(previousPart);
                    Type.Text = _packageName + " - " + _editor.Part.Type;
                    MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                try
                {
                    // Save and close current package
                    SavePackage();
                    ClosePackage();

                    // Create package and part
                    var packageName = MakeUntitledFilename();
                    var package = _engine.CreatePackage(packageName);
                    var part = package.CreatePart(partType);

                    SetConfigurationProfile(part, profile);
                    SetPart(part);

                    _packageName = System.IO.Path.GetFileName(packageName);
                    Type.Text = _packageName + " - " + part.Type;
                }
                catch (Exception ex)
                {
                    ClosePackage();
                    MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // Reset viewing parameters
            UcEditor.ResetView(false);
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            _editor.Clear();
        }

        private void Convert_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var supportedStates = _editor.GetSupportedTargetConversionStates(null);

                if ( (supportedStates != null) && (supportedStates.Count() > 0) )
                    _editor.Convert(null, supportedStates[0]);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetZoom_Click(object sender, RoutedEventArgs e)
        {
            UcEditor.ResetView(true);
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            UcEditor.ZoomOut(1);
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            UcEditor.ZoomIn(1);
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            string filePath = null;
            string fileName = null;

            // Show save dialog
            {
                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();

                dlg.FileName = "Interactive Ink Document"; // Default file name
                dlg.DefaultExt = ".iink"; // Default file extension
                dlg.Filter = ".iink|*.iink"; // Filter files by extension

                bool? result = dlg.ShowDialog();

                if ((bool)result)
                {
                    filePath = dlg.FileName;
                    fileName = dlg.SafeFileName;
                }
            }

            if (!String.IsNullOrEmpty(filePath))
            {
                var part = _editor.Part;
                if (part == null)
                    return;

                try
                {
                    // Save Package with new name
                    part.Package.SaveAs(filePath);

                    // Update internals
                    _packageName = fileName;
                    Type.Text = _packageName + " - " + part.Type;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var part = _editor.Part;
                var package = part?.Package;
                package?.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            string filePath = null;
            string fileName = null;

            // Show open dialog
            {
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

                dlg.DefaultExt = ".iink"; // Default file extension
                dlg.Filter = ".iink|*.iink"; // Filter files by extension

                Nullable<bool> result = dlg.ShowDialog();

                if (result == true)
                {
                    filePath = dlg.FileName;
                    fileName = dlg.SafeFileName;
                }
            }

            if (!String.IsNullOrEmpty(filePath))
            {
                ResetSelection();

                try
                {
                    // Save and close current package
                    SavePackage();
                    ClosePackage();

                    // Open package and select first part
                    var package = _engine.OpenPackage(filePath);
                    var part = package.GetPart(0);
                    SetPart(part);
                    _packageName = fileName;
                    Type.Text = _packageName + " - " + part.Type;
                }
                catch (Exception ex)
                {
                    ClosePackage();
                    MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                // Reset viewing parameters
                UcEditor.ResetView(false);
            }
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            NewFile();
        }

        private void SetConfigurationProfile(ContentPart part, string profile)
        {
            var metadata = part.Metadata;
            metadata.SetString("configuration-profile", profile);
            part.Metadata = metadata;
        }

        private string ReadConfigurationFile(string profile)
        {
            try
            {
                using (var reader = new StreamReader(Path.Combine(_configurationDirectory, profile)))
                {
                    return reader.ReadToEnd();
                }
            }
            catch
            {
                return String.Empty;
            }
        }

        private void SetPart(ContentPart part)
        {
            _editor.Configuration.Reset();

            if (part != null)
            {
                // retrieve the configuration profile from the part metadata
                var metadata = part.Metadata;
                string configurationFile = _defaultConfiguration;
                var profile = metadata.GetString("configuration-profile", "");

                if (!String.IsNullOrEmpty(profile))
                    configurationFile = part.Type + "/" + profile + ".json";

                // update Editor configuration accordingly
                var configuration = ReadConfigurationFile(configurationFile);
                if (!String.IsNullOrEmpty(configuration))
                    _editor.Configuration.Inject(configuration);
            }

            _editor.Part = part;
        }

        string[] GetSupportedPartTypesAndProfile()
        {
            List<string> choices = new List<string>();

            foreach (var partType in _engine.SupportedPartTypes)
            {
                // Part with default configuration
                choices.Add(partType);

                // Check the configurations listed in "configurations/" directory for this part type
                var directoryPath = Path.Combine(_configurationDirectory, partType);
                if (Directory.Exists(directoryPath))
                {
                    var fileList = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);
                    foreach (var file in fileList)
                    {
                        var filename = Path.GetFileNameWithoutExtension(file);
                        choices.Add(partType + " (" + filename + ")");
                    }
                }
            }

            return choices.ToArray();
        }

        private void NewFile()
        {
            var supportedPartTypes = GetSupportedPartTypesAndProfile();
            if (supportedPartTypes.Length > 0)
            {
                bool cancelable = _editor.Part != null;
                TypeOfContentDialog.ShowHandlerDialog(supportedPartTypes, true, cancelable);
            }
        }

        private void NewPart()
        {
            var supportedPartTypes = GetSupportedPartTypesAndProfile();
            if (supportedPartTypes.Length > 0)
                TypeOfContentDialog.ShowHandlerDialog(supportedPartTypes, false, true);
        }

        private string MakeUntitledFilename()
        {
            var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var tempFolder = _engine.Configuration.GetString("content-package.temp-folder");
            string fileName;
            string folderName;

            do
            {
                string baseName = "File" + (++_filenameIndex) + ".iink";
                fileName = System.IO.Path.Combine(localFolder, "MyScript", baseName);
                var tempName = baseName + "-file";
                folderName = System.IO.Path.Combine(tempFolder, tempName);
            }
            while (System.IO.File.Exists(fileName) || System.IO.File.Exists(folderName));

            return fileName;
        }

        private void ShowBlockContextMenu()
        {
            var contentBlock = _lastContentSelection as ContentBlock;

            var contextMenu = new ContextMenu();

            var availableActions = UcEditor.GetAvailableActions(contentBlock);

            if (availableActions.HasFlag(AvailableActions.ADD_BLOCK))
            {
                var supportedBlocks = _editor.SupportedAddBlockTypes;
                supportedBlocks = supportedBlocks.Where(o => o != "Placeholder").ToArray();

                MenuItem addItem = new MenuItem { Header = "Add..." };
                for (int i = 0; i < supportedBlocks.Count(); ++i)
                {
                    MenuItem addBlockItem = new MenuItem { Header = "Add " + supportedBlocks[i], Tag = supportedBlocks[i] };
                    if (supportedBlocks[i] == "Image")
                        addBlockItem.Click += AddImage;
                    else
                        addBlockItem.Click += AddBlock;
                    addItem.Items.Add(addBlockItem);
                }
                if (!addItem.Items.IsEmpty)
                    contextMenu.Items.Add(addItem);
            }

            if (availableActions.HasFlag(AvailableActions.REMOVE))
            {
                MenuItem removeItem = new MenuItem { Header = "Remove" };
                removeItem.Click += Remove;
                contextMenu.Items.Add(removeItem);
            }

            if (availableActions.HasFlag(AvailableActions.CONVERT))
            {
                MenuItem convertItem = new MenuItem { Header = "Convert" };
                convertItem.Click += ConvertSelection;
                contextMenu.Items.Add(convertItem);
            }

            if ( availableActions.HasFlag(AvailableActions.COPY)
              || availableActions.HasFlag(AvailableActions.OFFICE_CLIPBOARD)
              || availableActions.HasFlag(AvailableActions.PASTE) )
            {
                MenuItem copyPasteItem = new MenuItem { Header = "Copy/Paste..." };
                contextMenu.Items.Add(copyPasteItem);

                {
                    MenuItem copyItem = new MenuItem {  Header = "Copy", IsEnabled = availableActions.HasFlag(AvailableActions.COPY) };
                    copyItem.Click += Copy;
                    copyPasteItem.Items.Add(copyItem);
                }
                {
                    MenuItem clipboardItem = new MenuItem { Header = "Copy To Clipboard (Microsoft Office)",
                        IsEnabled = availableActions.HasFlag(AvailableActions.OFFICE_CLIPBOARD) };
                    clipboardItem.Click += CopyToClipboard;
                    copyPasteItem.Items.Add(clipboardItem);
                }
                {
                    MenuItem pasteItem = new MenuItem { Header = "Paste", IsEnabled = availableActions.HasFlag(AvailableActions.PASTE) };
                    pasteItem.Click += Paste;
                    copyPasteItem.Items.Add(pasteItem);
                }
            }

            if ( availableActions.HasFlag(AvailableActions.IMPORT)
              || availableActions.HasFlag(AvailableActions.EXPORT) )
            {
                MenuItem importExportItem = new MenuItem { Header = "Import/Export..." };
                contextMenu.Items.Add(importExportItem);

                {
                    MenuItem importItem = new MenuItem { Header = "Import", IsEnabled = availableActions.HasFlag(AvailableActions.IMPORT) };
                    importItem.Click += Import;
                    importExportItem.Items.Add(importItem);
                }
                {
                    MenuItem exportItem = new MenuItem { Header = "Export", IsEnabled = availableActions.HasFlag(AvailableActions.EXPORT) };
                    exportItem.Click += Export;
                    importExportItem.Items.Add(exportItem);
                }
            }

            if (availableActions.HasFlag(AvailableActions.FORMAT_TEXT))
            {
                var supportedFormats = _editor.GetSupportedTextFormats(contentBlock);

                MenuItem formatMenuItem = new MenuItem { Header = "Format..." };
                contextMenu.Items.Add(formatMenuItem);

                if (supportedFormats.Contains(TextFormat.H1))
                {
                    MenuItem h1Item = new MenuItem { Header = "H1" };
                    h1Item.Click += FormatH1;
                    formatMenuItem.Items.Add(h1Item);
                }
                if (supportedFormats.Contains(TextFormat.H2))
                {
                    MenuItem h2Item = new MenuItem { Header = "H2" };
                    h2Item.Click += FormatH2;
                    formatMenuItem.Items.Add(h2Item);
                }
                if (supportedFormats.Contains(TextFormat.PARAGRAPH))
                {
                    MenuItem pItem = new MenuItem { Header = "P" };
                    pItem.Click += FormatP;
                    formatMenuItem.Items.Add(pItem);
                }
                if (supportedFormats.Contains(TextFormat.LIST_BULLET))
                {
                    MenuItem pItem = new MenuItem { Header = "Bullet list" };
                    pItem.Click += FormatBulletList;
                    formatMenuItem.Items.Add(pItem);
                }
                if (supportedFormats.Contains(TextFormat.LIST_CHECKBOX))
                {
                    MenuItem pItem = new MenuItem { Header = "Checkbox list" };
                    pItem.Click += FormatCheckboxList;
                    formatMenuItem.Items.Add(pItem);
                }
                if (supportedFormats.Contains(TextFormat.LIST_NUMBERED))
                {
                    MenuItem pItem = new MenuItem { Header = "Numbered list" };
                    pItem.Click += FormatNumberedList;
                    formatMenuItem.Items.Add(pItem);
                }
            }

            if (!_editor.IsEmpty(contentBlock))
            {
                IndentationLevels indentLevels = _editor.GetIndentationLevels(contentBlock);

                bool indentable = (int)indentLevels.Low < (int)indentLevels.Max - 1;
                bool deindentable = indentLevels.High > 0;

                if (indentable || deindentable)
                {
                    MenuItem indentMenuItem = new MenuItem { Header = "Indentation..." };
                    contextMenu.Items.Add(indentMenuItem);
                    if (indentable)
                    {
                        MenuItem pItem = new MenuItem { Header = "Increase" };
                        pItem.Click += IncreaseIndentation;
                        indentMenuItem.Items.Add(pItem);
                    }
                    if (deindentable)
                    {
                        MenuItem pItem = new MenuItem { Header = "Decrease" };
                        pItem.Click += DecreaseIndentation;
                        indentMenuItem.Items.Add(pItem);
                    }
                }
            }

            if (availableActions.HasFlag(AvailableActions.SELECTION_MODE))
            {
                var supportedModes = _editor.GetAvailableSelectionModes();

                MenuItem modeMenuItem = new MenuItem { Header = "Selection mode..." };
                contextMenu.Items.Add(modeMenuItem);

                if (supportedModes.Contains(ContentSelectionMode.LASSO))
                {
                    MenuItem lassoItem = new MenuItem { Header = "Lasso" };
                    lassoItem.Click += SelectModeLasso;
                    modeMenuItem.Items.Add(lassoItem);
                }
                if (supportedModes.Contains(ContentSelectionMode.ITEM))
                {
                    MenuItem itemItem = new MenuItem { Header = "Item" };
                    itemItem.Click += SelectModeItem;
                    modeMenuItem.Items.Add(itemItem);
                }
                if (supportedModes.Contains(ContentSelectionMode.RESIZE))
                {
                    MenuItem resizeItem = new MenuItem { Header = "Resize" };
                    resizeItem.Click += SelectModeResize;
                    modeMenuItem.Items.Add(resizeItem);
                }
            }

            if (availableActions.HasFlag(AvailableActions.SELECTION_TYPE))
            {
                var supportedTypes = _editor.GetAvailableSelectionTypes(contentBlock);

                MenuItem typeMenuItem = new MenuItem { Header = "Selection type..." };
                contextMenu.Items.Add(typeMenuItem);

                if (supportedTypes.Contains("Text"))
                {
                    MenuItem textItem = new MenuItem { Header = "Text" };
                    textItem.Click += SelectTypeText;
                    typeMenuItem.Items.Add(textItem);
                    MenuItem textSBItem = new MenuItem { Header = "Text - Single Block" };
                    textSBItem.Click += SelectTypeTextSingleBlock;
                    typeMenuItem.Items.Add(textSBItem);
                }
                if (supportedTypes.Contains("Math"))
                {
                    MenuItem mathItem = new MenuItem { Header = "Math" };
                    mathItem.Click += SelectTypeMath;
                    typeMenuItem.Items.Add(mathItem);
                    MenuItem mathSBItem = new MenuItem { Header = "Math - Single Block" };
                    mathSBItem.Click += SelectTypeMathSingleBlock;
                    typeMenuItem.Items.Add(mathSBItem);
                }
            }

            if (contextMenu.Items.Count > 0)
            {
                this.ContextMenu = contextMenu;
                this.ContextMenu.IsOpen = true;
            }
        }

        private void ShowSelectionContextMenu()
        {
            var contentSelection = _lastContentSelection as ContentSelection;

            var contextMenu = new ContextMenu();

            var availableActions = UcEditor.GetAvailableActions(contentSelection);

            if (availableActions.HasFlag(AvailableActions.REMOVE))
            {
                MenuItem eraseItem = new MenuItem { Header = "Erase" };
                eraseItem.Click += Remove;
                contextMenu.Items.Add(eraseItem);
            }

            if (availableActions.HasFlag(AvailableActions.CONVERT))
            {
                MenuItem convertItem = new MenuItem { Header = "Convert" };
                convertItem.Click += ConvertSelection;
                contextMenu.Items.Add(convertItem);
            }

            if (availableActions.HasFlag(AvailableActions.COPY))
            {
                MenuItem copyMenuItem = new MenuItem { Header = "Copy..." };
                contextMenu.Items.Add(copyMenuItem);

                // Copy
                {
                    MenuItem copyItem = new MenuItem { Header = "Copy" };
                    copyItem.Click += Copy;
                    copyMenuItem.Items.Add(copyItem);
                }
                // Clipboard
                {
                    MenuItem clipboardItem = new MenuItem { Header = "Copy To Clipboard (Microsoft Office)",
                        IsEnabled = availableActions.HasFlag(AvailableActions.OFFICE_CLIPBOARD) };
                    clipboardItem.Click += CopyToClipboard;
                    copyMenuItem.Items.Add(clipboardItem);
                }
            }

            if (availableActions.HasFlag(AvailableActions.EXPORT))
            {
                MenuItem exportItem = new MenuItem { Header = "Export" };
                exportItem.Click += Export;
                contextMenu.Items.Add(exportItem);
            }

            if (availableActions.HasFlag(AvailableActions.FORMAT_TEXT))
            {
                var supportedFormats = _editor.GetSupportedTextFormats(contentSelection);

                MenuItem formatMenuItem = new MenuItem { Header = "Format..." };
                contextMenu.Items.Add(formatMenuItem);

                if (supportedFormats.Contains(TextFormat.H1))
                {
                    MenuItem h1Item = new MenuItem { Header = "H1" };
                    h1Item.Click += FormatH1;
                    formatMenuItem.Items.Add(h1Item);
                }
                if (supportedFormats.Contains(TextFormat.H2))
                {
                    MenuItem h2Item = new MenuItem { Header = "H2" };
                    h2Item.Click += FormatH2;
                    formatMenuItem.Items.Add(h2Item);
                }
                if (supportedFormats.Contains(TextFormat.PARAGRAPH))
                {
                    MenuItem pItem = new MenuItem { Header = "P" };
                    pItem.Click += FormatP;
                    formatMenuItem.Items.Add(pItem);
                }
                if (supportedFormats.Contains(TextFormat.LIST_BULLET))
                {
                    MenuItem pItem = new MenuItem { Header = "Bullet list" };
                    pItem.Click += FormatBulletList;
                    formatMenuItem.Items.Add(pItem);
                }
                if (supportedFormats.Contains(TextFormat.LIST_CHECKBOX))
                {
                    MenuItem pItem = new MenuItem { Header = "Checkbox list" };
                    pItem.Click += FormatCheckboxList;
                    formatMenuItem.Items.Add(pItem);
                }
                if (supportedFormats.Contains(TextFormat.LIST_NUMBERED))
                {
                    MenuItem pItem = new MenuItem { Header = "Numbered list" };
                    pItem.Click += FormatNumberedList;
                    formatMenuItem.Items.Add(pItem);
                }
            }

            if (!_editor.IsEmpty(contentSelection))
            {
                IndentationLevels indentLevels = _editor.GetIndentationLevels(contentSelection);

                bool indentable = (int)indentLevels.Low < (int)indentLevels.Max - 1;
                bool deindentable = indentLevels.High > 0;

                if (indentable || deindentable)
                {
                    MenuItem indentMenuItem = new MenuItem { Header = "Indentation..." };
                    contextMenu.Items.Add(indentMenuItem);
                    if (indentable)
                    {
                        MenuItem pItem = new MenuItem { Header = "Increase" };
                        pItem.Click += IncreaseIndentation;
                        indentMenuItem.Items.Add(pItem);
                    }
                    if (deindentable)
                    {
                        MenuItem pItem = new MenuItem { Header = "Decrease" };
                        pItem.Click += DecreaseIndentation;
                        indentMenuItem.Items.Add(pItem);
                    }
                }
            }

            if (availableActions.HasFlag(AvailableActions.SELECTION_MODE))
            {
                var supportedModes = _editor.GetAvailableSelectionModes();

                MenuItem modeMenuItem = new MenuItem { Header = "Selection mode..." };
                contextMenu.Items.Add(modeMenuItem);

                if (supportedModes.Contains(ContentSelectionMode.LASSO))
                {
                    MenuItem lassoItem = new MenuItem { Header = "Lasso" };
                    lassoItem.Click += SelectModeLasso;
                    modeMenuItem.Items.Add(lassoItem);
                }
                if (supportedModes.Contains(ContentSelectionMode.ITEM))
                {
                    MenuItem itemItem = new MenuItem { Header = "Item" };
                    itemItem.Click += SelectModeItem;
                    modeMenuItem.Items.Add(itemItem);
                }
                if (supportedModes.Contains(ContentSelectionMode.RESIZE))
                {
                    MenuItem resizeItem = new MenuItem { Header = "Resize" };
                    resizeItem.Click += SelectModeResize;
                    modeMenuItem.Items.Add(resizeItem);
                }
            }

            if (availableActions.HasFlag(AvailableActions.SELECTION_TYPE))
            {
                var supportedTypes = _editor.GetAvailableSelectionTypes(contentSelection);

                MenuItem typeMenuItem = new MenuItem { Header = "Selection type..." };
                contextMenu.Items.Add(typeMenuItem);

                if (supportedTypes.Contains("Text"))
                {
                    MenuItem textItem = new MenuItem { Header = "Text" };
                    textItem.Click += SelectTypeText;
                    typeMenuItem.Items.Add(textItem);
                    MenuItem textSBItem = new MenuItem { Header = "Text - Single Block" };
                    textSBItem.Click += SelectTypeTextSingleBlock;
                    typeMenuItem.Items.Add(textSBItem);
                }
                if (supportedTypes.Contains("Math"))
                {
                    MenuItem mathItem = new MenuItem { Header = "Math" };
                    mathItem.Click += SelectTypeMath;
                    typeMenuItem.Items.Add(mathItem);
                    MenuItem mathSBItem = new MenuItem { Header = "Math - Single Block" };
                    mathSBItem.Click += SelectTypeMathSingleBlock;
                    typeMenuItem.Items.Add(mathSBItem);
                }
            }

            if (contextMenu.Items.Count > 0)
            {
                this.ContextMenu = contextMenu;
                this.ContextMenu.IsOpen = true;
            }
        }

        private void UcEditor_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(UcEditor);
            _lastPointerPosition = new Graphics.Point((float)pos.X, (float)pos.Y);

            ResetSelection();
            _lastContentSelection = _editor.HitSelection(_lastPointerPosition.X, _lastPointerPosition.Y);
            if (_lastContentSelection != null)
            {
                ShowSelectionContextMenu();
                e.Handled = true;
            }
            else
            {
                var contentBlock = _editor.HitBlock(_lastPointerPosition.X, _lastPointerPosition.Y);

                if ((contentBlock == null) || (contentBlock.Type == "Container"))
                {
                    contentBlock?.Dispose();
                    contentBlock = _editor.GetRootBlock();
                }
                _lastContentSelection = contentBlock;

                if (_lastContentSelection != null)
                {
                    ShowBlockContextMenu();
                    e.Handled = true;
                }
            }
        }

        private void ShowSmartGuideMenu(Point globalPos)
        {
            ResetSelection();
            _lastContentSelection = UcEditor.SmartGuide.ContentBlock?.ShallowCopy();

            ShowBlockContextMenu();
        }

        private void ConvertSelection(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastContentSelection != null)
                {
                    var supportedStates = _editor.GetSupportedTargetConversionStates(_lastContentSelection);

                    if ((supportedStates != null) && (supportedStates.Count() > 0))
                        _editor.Convert(_lastContentSelection, supportedStates[0]);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddBlock(object sender, RoutedEventArgs e)
        {
            try
            {
              // Uses Id as block type
              var blockType = ((MenuItem)(sender)).Tag.ToString();
              var mimeTypes = _editor.GetSupportedAddBlockDataMimeTypes(blockType);
              var useDialog = (mimeTypes != null) && (mimeTypes.Count() > 0);

              if (!useDialog)
              {
                  _editor.AddBlock(_lastPointerPosition.X, _lastPointerPosition.Y, blockType);
              }
              else
              {
                ImportDialog importDialog = new ImportDialog(this, "Add Content Block", mimeTypes);

                if (importDialog.ShowDialog() == true)
                {
                    var idx = importDialog.SelectedMimeType;
                    var data = importDialog.ResultText;

                    if ( (idx >= 0) && (idx < mimeTypes.Count()) && (String.IsNullOrWhiteSpace(data) == false) )
                    {
                        _editor.AddBlock(_lastPointerPosition.X, _lastPointerPosition.Y, blockType, mimeTypes[idx], data);
                    }
                }
              }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddImage(object sender, RoutedEventArgs e)
        {
            try
            {
                // Format filter as "name|*extension1;*extension2;..."
                MimeType[] mimeTypes = { MimeType.JPEG, MimeType.PNG, MimeType.GIF };
                List<string[]> extensionTypes = new List<string[]>(3);
                foreach (MimeType mimeType in mimeTypes)
                {
                    var extensions = MimeTypeF.GetFileExtensions(mimeType).Split(',');
                    extensionTypes.Add(extensions);
                }
                string filter = "Image|";
                foreach (var extensionList in extensionTypes)
                {
                    foreach (string extension in extensionList)
                        filter += "*" + extension + ";";
                }
                filter = filter.Remove(filter.Length - 1);

                // Show open image dialog
                var dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.Title = "Please select an image file";
                dialog.Filter = filter;

                bool? result = dialog.ShowDialog();
                if (result == true)
                {
                    string fileName = dialog.FileName;
                    string fileExtension = Path.GetExtension(fileName);

                    // Find mime type from file extension
                    MimeType mimeType = (MimeType)(-1);
                    for (int i = 0; i < extensionTypes.Count && mimeType == (MimeType)(-1); ++i)
                    {
                        var extensionType = extensionTypes[i];
                        foreach (var extension in extensionType)
                        {
                            if (fileExtension.Equals(extension, StringComparison.OrdinalIgnoreCase))
                            {
                                mimeType = mimeTypes[i];
                                break;
                            }
                        }
                    }
                    if (mimeType == (MimeType)(-1))
                        throw new Exception("AddImage: error identifying mime type from file extension");

                    _editor.AddImage(_lastPointerPosition.X, _lastPointerPosition.Y, fileName, mimeType);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Remove(object sender, RoutedEventArgs e)
        {
            try
            {
                var contentBlock = _lastContentSelection as ContentBlock;
                if (contentBlock != null)
                {
                    _editor.Erase(contentBlock);
                    contentBlock.Dispose();
                }
                else if (_lastContentSelection != null)
                {
                    var contentSelection = _lastContentSelection as ContentSelection;
                    _editor.Erase(contentSelection);
                    contentSelection.Dispose();
                }
                _lastContentSelection = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Copy(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastContentSelection != null)
                    _editor.Copy(_lastContentSelection);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Paste(object sender, RoutedEventArgs e)
        {
            try
            {
                _editor.Paste(_lastPointerPosition.X, _lastPointerPosition.Y);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Import(object sender, RoutedEventArgs e)
        {
            var part = _editor.Part;
            if (part == null)
                return;

            if (_lastContentSelection == null)
                return;

            var mimeTypes = _editor.GetSupportedImportMimeTypes(_lastContentSelection);

            if (mimeTypes == null)
                return;

            if (mimeTypes.Count() == 0)
                return;

            ImportDialog importDialog = new ImportDialog(this, "Import", mimeTypes);

            if (importDialog.ShowDialog() == true)
            {
                var idx = importDialog.SelectedMimeType;
                var data = importDialog.ResultText;

                if ( (idx >= 0) && (idx < mimeTypes.Count()) && (String.IsNullOrWhiteSpace(data) == false) )
                {
                    try
                    {
                        _editor.Import_(mimeTypes[idx], data, _lastContentSelection);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void Export(object sender, RoutedEventArgs e)
        {
            var part = _editor.Part;
            if (part == null)
                return;

            using (var rootBlock = _editor.GetRootBlock())
            {
                var onRawContent = part.Type == "Raw Content";
                var contentBlock = _lastContentSelection as ContentBlock;
                IContentSelection contentSelection = (contentBlock != null) ? (onRawContent ? rootBlock : contentBlock)
                    : _lastContentSelection;

                if (contentSelection == null)
                    return;

                var mimeTypes = _editor.GetSupportedExportMimeTypes(contentSelection);

                if (mimeTypes == null)
                    return;

                if (mimeTypes.Count() == 0)
                    return;

                string filterList = "";

                for (int i = 0; i < mimeTypes.Count(); ++i)
                {
                    // format filter as "name|*extension1;*extension2;..."
                    var extensions = MimeTypeF.GetFileExtensions(mimeTypes[i]).Split(',');
                    string filter = MimeTypeF.GetName(mimeTypes[i]) + "|";

                    for (int j = 0; j < extensions.Count(); ++j)
                    {
                        if (j > 0)
                            filter += ";";

                        filter += "*" + extensions[j];
                    }

                    if (i > 0)
                        filterList += "|";

                    filterList += filter;
                }

                // Show save dialog
                {
                    Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();

                    dlg.FileName = "Interactive Ink Document"; // Default file name
                    dlg.DefaultExt = String.Empty; // Default file extension
                    dlg.Filter = filterList; // Filter files by extension

                    bool? result = dlg.ShowDialog();

                    if ((bool)result)
                    {
                        var filePath = dlg.FileName;
                        var filterIndex = dlg.FilterIndex - 1;
                        var extensions = MimeTypeF.GetFileExtensions(mimeTypes[filterIndex]).Split(',');

                        if (extensions.Count() > 0)
                        {
                            int ext;

                            for (ext = 0; ext < extensions.Count(); ++ext)
                            {
                                if (filePath.EndsWith(extensions[ext], StringComparison.OrdinalIgnoreCase))
                                    break;
                            }

                            if (ext >= extensions.Count())
                                filePath += extensions[0];

                            try
                            {
                                var imagePainter = new ImagePainter();

                                imagePainter.ImageLoader = UcEditor.ImageLoader;

                                _editor.WaitForIdle();
                                _editor.Export_(contentSelection, filePath, imagePainter);

                                System.Diagnostics.Process.Start(filePath);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
            }
        }

        private void CopyToClipboard(object sender, RoutedEventArgs e)
        {
            try
            {
                MimeType[] mimeTypes = null;

                if (_lastContentSelection != null)
                    mimeTypes = _editor.GetSupportedExportMimeTypes(_lastContentSelection);

                if (mimeTypes != null && mimeTypes.Contains(MimeType.OFFICE_CLIPBOARD))
                {
                    // export block to a file
                    var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var clipboardPath =  Path.Combine(localFolder, "MyScript", "tmp/clipboard.gvml");
                    var imagePainter = new ImagePainter();

                    imagePainter.ImageLoader = UcEditor.ImageLoader;

                    _editor.Export_(_lastContentSelection, clipboardPath.ToString(), MimeType.OFFICE_CLIPBOARD, imagePainter);

                    // read back exported data
                    var clipboardData = File.ReadAllBytes(clipboardPath);
                    var clipboardStream = new MemoryStream(clipboardData);

                    // store the data into clipboard
                    Clipboard.SetData(MimeTypeF.GetTypeName(MimeType.OFFICE_CLIPBOARD), clipboardStream);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FormatH1(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastContentSelection != null)
                    _editor.SetTextFormat(_lastContentSelection, TextFormat.H1);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FormatH2(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastContentSelection != null)
                    _editor.SetTextFormat(_lastContentSelection, TextFormat.H2);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FormatP(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastContentSelection != null)
                    _editor.SetTextFormat(_lastContentSelection, TextFormat.PARAGRAPH);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FormatBulletList(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastContentSelection != null)
                    _editor.SetTextFormat(_lastContentSelection, TextFormat.LIST_BULLET);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FormatCheckboxList(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastContentSelection != null)
                    _editor.SetTextFormat(_lastContentSelection, TextFormat.LIST_CHECKBOX);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FormatNumberedList(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastContentSelection != null)
                    _editor.SetTextFormat(_lastContentSelection, TextFormat.LIST_NUMBERED);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void IncreaseIndentation(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastContentSelection != null)
                    _editor.Indent(_lastContentSelection, 1);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void DecreaseIndentation(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastContentSelection != null)
                    _editor.Indent(_lastContentSelection, -1);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectModeLasso(object sender, RoutedEventArgs e)
        {
            try
            {
                _editor.SetSelectionMode(ContentSelectionMode.LASSO);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectModeItem(object sender, RoutedEventArgs e)
        {
            try
            {
                _editor.SetSelectionMode(ContentSelectionMode.ITEM);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectModeResize(object sender, RoutedEventArgs e)
        {
            try
            {
                _editor.SetSelectionMode(ContentSelectionMode.RESIZE);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectTypeText(object sender, RoutedEventArgs e)
        {
            try
            {
                _editor.SetSelectionType(_lastContentSelection, "Text", false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectTypeTextSingleBlock(object sender, RoutedEventArgs e)
        {
            try
            {
                _editor.SetSelectionType(_lastContentSelection, "Text", true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectTypeMath(object sender, RoutedEventArgs e)
        {
            try
            {
                _editor.SetSelectionType(_lastContentSelection, "Math", false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SelectTypeMathSingleBlock(object sender, RoutedEventArgs e)
        {
            try
            {
                _editor.SetSelectionType(_lastContentSelection, "Math", true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetInputTool(PointerTool pointerTool)
        {
            UcEditor.SetInputTool(pointerTool);

            Pen.IsChecked         = (pointerTool == PointerTool.PEN);
            Hand.IsChecked        = (pointerTool == PointerTool.HAND);
            Eraser.IsChecked      = (pointerTool == PointerTool.ERASER);
            Selector.IsChecked    = (pointerTool == PointerTool.SELECTOR);
            Highlighter.IsChecked = (pointerTool == PointerTool.HIGHLIGHTER);
        }

        private void Pen_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;

            if ((bool)toggleButton.IsChecked)
            {
                SetInputTool(PointerTool.PEN);
            }
        }

        private void Hand_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;

            if ((bool)toggleButton.IsChecked)
            {
                SetInputTool(PointerTool.HAND);
            }
        }

        private void Eraser_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;

            if ((bool)toggleButton.IsChecked)
            {
                SetInputTool(PointerTool.ERASER);
            }
        }

        private void Selector_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;

            if ((bool)toggleButton.IsChecked)
            {
                SetInputTool(PointerTool.SELECTOR);
            }
        }

        private void Highlighter_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;

            if ((bool)toggleButton.IsChecked)
            {
                SetInputTool(PointerTool.HIGHLIGHTER);
            }
        }

        private void ActivePen_Click(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            bool enabled = (bool)checkBox.IsChecked;
            if (enabled)
            {
                if ((bool)Hand.IsChecked)
                    SetInputTool(PointerTool.PEN);
                Hand.IsEnabled = false;
                Hand.Foreground = System.Windows.Media.Brushes.Gray;
            }
            else
            {
                Hand.IsEnabled = true;
                Hand.Foreground = System.Windows.Media.Brushes.Black;
            }
            UcEditor.SetActivePen(enabled);
        }

        private void ApplyToolStyle(PointerTool pointerTool)
        {
            if (pointerTool == PointerTool.PEN)
            {
                string newStyle = (String.IsNullOrEmpty(_penWidth) ? _penWidth : "-myscript-pen-width: " + _penWidth + "; ")
                                + (String.IsNullOrEmpty(_penColor) ? _penColor : "color: " + _penColor + "; ");
                UcEditor.SetToolStyle(PointerTool.PEN, newStyle);
            }
            else if (pointerTool == PointerTool.HIGHLIGHTER)
            {
                string newStyle = (String.IsNullOrEmpty(_highlighterWidth) ? _highlighterWidth : "-myscript-pen-width: " + _highlighterWidth + "; ")
                                + (String.IsNullOrEmpty(_highlighterColor) ? _highlighterColor : "color: " + _highlighterColor + "; ");
                UcEditor.SetToolStyle(PointerTool.HIGHLIGHTER, newStyle);
            }
        }

        private void PenWidth_Clicked(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem.IsChecked)
            {
                if (menuItem == PenThin)
                {
                    PenMedium.IsChecked = false;
                    PenLarge.IsChecked = false;
                    _penWidth = PenThinWidth.ToString(CultureInfo.InvariantCulture);
                }
                else if (menuItem == PenLarge)
                {
                    PenThin.IsChecked = false;
                    PenMedium.IsChecked = false;
                    _penWidth = PenLargeWidth.ToString(CultureInfo.InvariantCulture);
                }
                else  // PenMedium
                {
                    PenThin.IsChecked = false;
                    PenLarge.IsChecked = false;
                    _penWidth = PenMediumWidth.ToString(CultureInfo.InvariantCulture);
                }
                ApplyToolStyle(PointerTool.PEN);
            }
            else
            {
                menuItem.IsChecked = true;
            }
        }

        private void PenColor_Clicked(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem.IsChecked)
            {
                if (menuItem == PenRed)
                {
                    PenBlack.IsChecked = false;
                    PenGreen.IsChecked = false;
                    PenBlue.IsChecked = false;
                    _penColor = PenRedColor;
                }
                else if (menuItem == PenGreen)
                {
                    PenBlack.IsChecked = false;
                    PenRed.IsChecked = false;
                    PenBlue.IsChecked = false;
                    _penColor = PenGreenColor;
                }
                else if (menuItem == PenBlue)
                {
                    PenBlack.IsChecked = false;
                    PenRed.IsChecked = false;
                    PenGreen.IsChecked = false;
                    _penColor = PenBlueColor;
                }
                else // PenBlack
                {
                    PenRed.IsChecked = false;
                    PenGreen.IsChecked = false;
                    PenBlue.IsChecked = false;
                    _penColor = PenBlackColor;
                }
                ApplyToolStyle(PointerTool.PEN);
            }
            else
            {
                menuItem.IsChecked = true;
            }
        }

        private void HighlighterWidth_Clicked(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem.IsChecked)
            {
                if (menuItem == HighlighterThin)
                {
                    HighlighterMedium.IsChecked = false;
                    HighlighterLarge.IsChecked = false;
                    _highlighterWidth = HighlighterThinWidth.ToString(CultureInfo.InvariantCulture);
                }
                else if (menuItem == HighlighterLarge)
                {
                    HighlighterThin.IsChecked = false;
                    HighlighterMedium.IsChecked = false;
                    _highlighterWidth = HighlighterLargeWidth.ToString(CultureInfo.InvariantCulture);
                }
                else  // HighlighterMedium
                {
                    HighlighterThin.IsChecked = false;
                    HighlighterLarge.IsChecked = false;
                    _highlighterWidth = HighlighterMediumWidth.ToString(CultureInfo.InvariantCulture);
                }
                ApplyToolStyle(PointerTool.HIGHLIGHTER);
            }
            else
            {
                menuItem.IsChecked = true;
            }
        }

        private void HighlighterColor_Clicked(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem.IsChecked)
            {
                if (menuItem == HighlighterRed)
                {
                    HighlighterYellow.IsChecked = false;
                    HighlighterGreen.IsChecked = false;
                    HighlighterBlue.IsChecked = false;
                    _highlighterColor = HighlighterRedColor;
                }
                else if (menuItem == HighlighterGreen)
                {
                    HighlighterYellow.IsChecked = false;
                    HighlighterRed.IsChecked = false;
                    HighlighterBlue.IsChecked = false;
                    _highlighterColor = HighlighterGreenColor;
                }
                else if (menuItem == HighlighterBlue)
                {
                    HighlighterYellow.IsChecked = false;
                    HighlighterRed.IsChecked = false;
                    HighlighterGreen.IsChecked = false;
                    _highlighterColor = HighlighterBlueColor;
                }
                else // HighlighterYellow
                {
                    HighlighterRed.IsChecked = false;
                    HighlighterGreen.IsChecked = false;
                    HighlighterBlue.IsChecked = false;
                    _highlighterColor = HighlighterYellowColor;
                }
                ApplyToolStyle(PointerTool.HIGHLIGHTER);
            }
            else
            {
                menuItem.IsChecked = true;
            }
        }

        private void SmartGuideEnabled_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            UcEditor.SmartGuideEnabled = (bool)menuItem.IsChecked;
        }
    }
}
