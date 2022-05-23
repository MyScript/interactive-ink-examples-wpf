// Copyright @ MyScript. All rights reserved.

using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MyScript.IInk.UIReferenceImplementation;

namespace MyScript.IInk.Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Engine _engine;
        private Editor _editor => UcEditor.Editor;

        private Graphics.Point _lastPointerPosition;
        private ContentBlock _lastSelectedBlock;

        private int _filenameIndex;
        private string _packageName;

        private string _penWidth;
        private string _penColor;
        private string _highlighterWidth;
        private string _highlighterColor;

        private const float  PenMediumWidth = 0.625F;       // default (in mm)
        private const string PenBlackColor  = "#000000";    // default
        private const string PenRedColor    = "#EA4335";
        private const string PenGreenColor  = "#34A853";
        private const string PenBlueColor   = "#4285F4";

        private const float  HighlighterMediumWidth = 5.0F;         // default (in mm)
        private const string HighlighterYellowColor = "#FBBC0566";  // default
        private const string HighlighterRedColor    = "#EA433566";
        private const string HighlighterGreenColor  = "#34A85366";
        private const string HighlighterBlueColor   = "#4285F466";

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

        private void EnableRawContentConversion()
        {
            // Activate handwriting recognition for text and shapes
            _engine.Configuration.SetBoolean("raw-content.recognition.text", true);
            _engine.Configuration.SetBoolean("raw-content.recognition.shape", true);

            // Allow conversion of text, nodes and edges
            _engine.Configuration.SetBoolean("raw-content.convert.node", true);
            _engine.Configuration.SetBoolean("raw-content.convert.text", true);
            _engine.Configuration.SetBoolean("raw-content.convert.edge", true);

            // Allow converting shapes by holding the pen in position
            _engine.Configuration.SetBoolean("raw-content.convert.shape-on-hold", true);

            // Allow interactions
            _engine.Configuration.SetBoolean("raw-content.tap-interactions", true);
            _engine.Configuration.SetBoolean("raw-content.eraser.erase-precisely", false);

            // Show alignment guides and snap to them
            _engine.Configuration.SetBoolean("raw-content.guides.enable", true);
            _engine.Configuration.SetBoolean("raw-content.guides.snap", true);

            // Allow gesture detection
            var gestures = new string[] { "underline", "double-underline", "scratch-out", "join", "insert", "strike-through" };
            _engine.Configuration.SetStringArray("raw-content.pen.gestures", gestures);
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

            EnableRawContentConversion();

            // Initialize the editor with the engine
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
                    _lastSelectedBlock?.Dispose();
                    _lastSelectedBlock = null;
                    _editor.Part = null;

                    while (--index >= 0)
                    {
                        ContentPart newPart = null;

                        try
                        {
                            // Select new part
                            newPart = part.Package.GetPart(index);
                            _editor.Part = newPart;
                            Type.Text = _packageName + " - " + newPart.Type;
                            part.Dispose();
                            break;
                        }
                        catch
                        {
                            // Can't set this part, try the previous one
                            _editor.Part = null;
                            Type.Text = "";
                            newPart?.Dispose();
                        }
                    }

                    if (index < 0)
                    {
                        // Restore current part if none can be set
                        _editor.Part = part;
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
                    _lastSelectedBlock?.Dispose();
                    _lastSelectedBlock = null;
                    _editor.Part = null;

                    while (++index < count)
                    {
                        ContentPart newPart = null;

                        try
                        {
                            // Select new part
                            newPart = part.Package.GetPart(index);
                            _editor.Part = newPart;
                            Type.Text = _packageName + " - " + newPart.Type;
                            part.Dispose();
                            break;
                        }
                        catch
                        {
                            // Can't set this part, try the next one
                            _editor.Part = null;
                            Type.Text = "";
                            newPart?.Dispose();
                        }
                    }

                    if (index >= count)
                    {
                        // Restore current part if none can be set
                        _editor.Part = part;
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
            _editor.Part = null;
            part?.Dispose();
            package?.Dispose();
            Type.Text = "";
        }

        private void TypeOfContentDialog_AddNewPart(string newPartType, bool newPackage)
        {
            if (newPartType != string.Empty)
            {
                _lastSelectedBlock?.Dispose();
                _lastSelectedBlock = null;

                if (!newPackage && (_editor.Part != null))
                {
                    var previousPart = _editor.Part;
                    var package = previousPart.Package;

                    try
                    {
                        _editor.Part = null;

                        var part = package.CreatePart(newPartType);
                        _editor.Part = part;
                        Type.Text = _packageName + " - " + part.Type;

                        previousPart.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _editor.Part = previousPart;
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
                        var part = package.CreatePart(newPartType);
                        _editor.Part = part;
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

            if (!string.IsNullOrEmpty(filePath))
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

            if (!string.IsNullOrEmpty(filePath))
            {
                _lastSelectedBlock?.Dispose();
                _lastSelectedBlock = null;

                try
                {
                    // Save and close current package
                    SavePackage();
                    ClosePackage();

                    // Open package and select first part
                    var package = _engine.OpenPackage(filePath);
                    var part = package.GetPart(0);
                    _editor.Part = part;
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

        private void NewFile()
        {
            if (_engine.SupportedPartTypes.Length > 0)
            {
                bool cancelable = _editor.Part != null;
                TypeOfContentDialog.ShowHandlerDialog(_engine.SupportedPartTypes, true, cancelable);
            }
        }

        private void NewPart()
        {
            if (_engine.SupportedPartTypes.Length > 0)
            {
                TypeOfContentDialog.ShowHandlerDialog(_engine.SupportedPartTypes, false, true);
            }
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

        private void ShowContextMenu()
        {
            var part = _editor.Part;
            if (_editor.Part == null)
                return;

            using (var rootBlock = _editor.GetRootBlock())
            {
                var contentBlock = _lastSelectedBlock;
                if (contentBlock == null)
                    return;

                var isRoot = contentBlock.Id == rootBlock.Id;
                if (!isRoot && (contentBlock.Type == "Container") )
                    return;

                var onRawContent = part.Type == "Raw Content";
                var onTextDocument = part.Type == "Text Document";

                var isEmpty = _editor.IsEmpty(contentBlock);

                var supportedTypes = _editor.SupportedAddBlockTypes;
                var supportedExports = _editor.GetSupportedExportMimeTypes(onRawContent ? rootBlock : contentBlock);
                var supportedImports = _editor.GetSupportedImportMimeTypes(contentBlock);
                var supportedStates = _editor.GetSupportedTargetConversionStates(contentBlock);

                var hasTypes = (supportedTypes != null) && supportedTypes.Any();
                var hasExports = (supportedExports != null) && supportedExports.Any();
                var hasImports = (supportedImports != null) && supportedImports.Any();
                var hasStates = (supportedStates != null) && supportedStates.Any();

                var displayConvert  = hasStates && !isEmpty;
                var displayAddBlock = hasTypes && isRoot;
                var displayAddImage = false; // hasTypes && isRoot;
                var displayRemove   = !isRoot;
                var displayCopy     = !onTextDocument || !isRoot;
                var displayPaste    = isRoot;
                var displayImport   = hasImports;
                var displayExport   = hasExports;
                var displayClipboard = hasExports && supportedExports.Contains(MimeType.OFFICE_CLIPBOARD);

                var contextMenu = new ContextMenu();

                if (displayAddBlock || displayAddImage)
                {
                    MenuItem addItem = new MenuItem { Header = "Add..." };
                    contextMenu.Items.Add(addItem);

                    if (displayAddBlock)
                    {
                        for (int i = 0; i < supportedTypes.Count(); ++i)
                        {
                            MenuItem addBlockItem = new MenuItem { Header = "Add " + supportedTypes[i], Tag = supportedTypes[i] };
                            addBlockItem.Click += AddBlock;
                            addItem.Items.Add(addBlockItem);
                        }
                    }

                    if (displayAddImage)
                    {
                        MenuItem addImageItem = new MenuItem { Header = "Add Image" };
                        addImageItem.Click += AddImage;
                        addItem.Items.Add(addImageItem);
                    }
                }

                if (displayRemove)
                {
                    MenuItem removeItem = new MenuItem { Header = "Remove" };
                    removeItem.Click += Remove;
                    contextMenu.Items.Add(removeItem);
                }

                if (displayConvert)
                {
                    MenuItem convertItem = new MenuItem { Header = "Convert" };
                    convertItem.Click += ConvertBlock;
                    contextMenu.Items.Add(convertItem);
                }

                if (displayCopy || displayClipboard || displayPaste)
                {
                    MenuItem copyPasteItem = new MenuItem { Header = "Copy/Paste..." };
                    contextMenu.Items.Add(copyPasteItem);

                    //if (displayCopy)
                    {
                        MenuItem copyItem = new MenuItem {  Header = "Copy", IsEnabled = displayCopy };
                        copyItem.Click += Copy;
                        copyPasteItem.Items.Add(copyItem);
                    }

                    //if (displayClipboard)
                    {
                        MenuItem clipboardItem = new MenuItem { Header = "Copy To Clipboard (Microsoft Office)", IsEnabled = displayClipboard };
                        clipboardItem.Click += CopyToClipboard;
                        copyPasteItem.Items.Add(clipboardItem);
                    }

                    //if (displayPaste)
                    {
                        MenuItem pasteItem = new MenuItem { Header = "Paste", IsEnabled = displayPaste };
                        pasteItem.Click += Paste;
                        copyPasteItem.Items.Add(pasteItem);
                    }
                }

                if (displayImport || displayExport)
                {
                    MenuItem importExportItem = new MenuItem { Header = "Import/Export..." };
                    contextMenu.Items.Add(importExportItem);

                    //if (displayImport)
                    {
                        MenuItem importItem = new MenuItem { Header = "Import", IsEnabled = displayImport };
                        importItem.Click += Import;
                        importExportItem.Items.Add(importItem);
                    }

                    //if (displayExport)
                    {
                        MenuItem exportItem = new MenuItem { Header = "Export", IsEnabled = displayExport };
                        exportItem.Click += Export;
                        importExportItem.Items.Add(exportItem);
                    }
                }

                if (contextMenu.Items.Count > 0)
                {
                    this.ContextMenu = contextMenu;
                    this.ContextMenu.IsOpen = true;
                }
            }
        }

        private void UcEditor_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(UcEditor);

            _lastPointerPosition = new Graphics.Point((float)pos.X, (float)pos.Y);
            _lastSelectedBlock?.Dispose();
            _lastSelectedBlock = _editor.HitBlock(_lastPointerPosition.X, _lastPointerPosition.Y);

            if ( (_lastSelectedBlock == null) || (_lastSelectedBlock.Type == "Container") )
            {
                _lastSelectedBlock?.Dispose();
                _lastSelectedBlock = _editor.GetRootBlock();
            }

            if (_lastSelectedBlock != null)
            {
                ShowContextMenu();
                e.Handled = true;
            }
        }

        private void ShowSmartGuideMenu(Point globalPos)
        {
            _lastSelectedBlock?.Dispose();
            _lastSelectedBlock = UcEditor.SmartGuide.ContentBlock?.ShallowCopy();

            if (_lastSelectedBlock != null)
                ShowContextMenu();
        }

        private void ConvertBlock(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastSelectedBlock != null)
                {
                    var supportedStates = _editor.GetSupportedTargetConversionStates(_lastSelectedBlock);

                    if ( (supportedStates != null) && (supportedStates.Count() > 0) )
                        _editor.Convert(_lastSelectedBlock, supportedStates[0]);
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
            // TODO
        }

        private void Remove(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_lastSelectedBlock != null)
                {
                    _editor.RemoveBlock(_lastSelectedBlock);
                    _lastSelectedBlock.Dispose();
                    _lastSelectedBlock = null;
                }
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
                if (_lastSelectedBlock != null)
                    _editor.Copy(_lastSelectedBlock);
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

            if (_lastSelectedBlock == null)
                return;

            var mimeTypes = _editor.GetSupportedImportMimeTypes(_lastSelectedBlock);

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
                        _editor.Import_(mimeTypes[idx], data, _lastSelectedBlock);
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
                var contentBlock = onRawContent ? rootBlock : _lastSelectedBlock;

                if (contentBlock == null)
                    return;

                var mimeTypes = _editor.GetSupportedExportMimeTypes(contentBlock);

                if (mimeTypes == null)
                    return;

                if (mimeTypes.Count() == 0)
                    return;

                string filterList = "";

                for (int i = 0; i < mimeTypes.Count(); ++i)
                {
                    // format filter as "name|extension1;extension2;...;extensionX"
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
                    dlg.DefaultExt = System.String.Empty; // Default file extension
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
                                var drawer = new ImageDrawer();

                                drawer.ImageLoader = UcEditor.ImageLoader;

                                _editor.WaitForIdle();
                                _editor.Export_(contentBlock, filePath, drawer);

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

                if (_lastSelectedBlock != null)
                    mimeTypes = _editor.GetSupportedExportMimeTypes(_lastSelectedBlock);

                if (mimeTypes != null && mimeTypes.Contains(MimeType.OFFICE_CLIPBOARD))
                {
                    // export block to a file
                    var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var clipboardPath =  Path.Combine(localFolder, "MyScript", "tmp/clipboard.gvml");
                    var drawer = new ImageDrawer();

                    drawer.ImageLoader = UcEditor.ImageLoader;

                    _editor.Export_(_lastSelectedBlock, clipboardPath.ToString(), MimeType.OFFICE_CLIPBOARD, drawer);

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
                string newStyle = (string.IsNullOrEmpty(_penWidth) ? _penWidth : "-myscript-pen-width: " + _penWidth + "; ")
                                + (string.IsNullOrEmpty(_penColor) ? _penColor : "color: " + _penColor + "; ");
                UcEditor.SetToolStyle(PointerTool.PEN, newStyle);
            }
            else if (pointerTool == PointerTool.HIGHLIGHTER)
            {
                string newStyle = (string.IsNullOrEmpty(_highlighterWidth) ? _highlighterWidth : "-myscript-pen-width: " + _highlighterWidth + "; ")
                                + (string.IsNullOrEmpty(_highlighterColor) ? _highlighterColor : "color: " + _highlighterColor + "; ");
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
                    _penWidth = (PenMediumWidth / 3.0F).ToString(CultureInfo.InvariantCulture);
                }
                else if (menuItem == PenLarge)
                {
                    PenThin.IsChecked = false;
                    PenMedium.IsChecked = false;
                    _penWidth = (PenMediumWidth * 3.0F).ToString(CultureInfo.InvariantCulture);
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
                    _highlighterWidth = (HighlighterMediumWidth / 3.0F).ToString(CultureInfo.InvariantCulture);
                }
                else if (menuItem == HighlighterLarge)
                {
                    HighlighterThin.IsChecked = false;
                    HighlighterMedium.IsChecked = false;
                    _highlighterWidth = (HighlighterMediumWidth * 3.0F).ToString(CultureInfo.InvariantCulture);
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

