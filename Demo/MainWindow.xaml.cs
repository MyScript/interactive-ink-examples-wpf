// Copyright MyScript. All right reserved.

using System;
using System.ComponentModel;
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

            // Initialize the editor with the engine
            UcEditor.Engine = _engine;
            UcEditor.Initialize(this);
            UcEditor.SmartGuide.MoreClicked += ShowSmartGuideMenu;

            // Force pointer to be a pen, for an automatic detection, set InputMode to AUTO
            SetInputMode(InputMode.PEN);

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
                var index = part.Package.IndexOfPart(part);

                if (index > 0)
                {
                    // Reset viewing parameters
                    UcEditor.ResetView(false);

                    // Select new part
                    _lastSelectedBlock?.Dispose();
                    _lastSelectedBlock = null;

                    _editor.Part = null;

                    var newPart = part.Package.GetPart(index - 1);
                    _editor.Part = newPart;
                    Type.Text = _packageName + " - " + newPart.Type;

                    part.Dispose();
                }
            }
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            var part = _editor.Part;

            if (part != null)
            {
                var index = part.Package.IndexOfPart(part);

                if (index < part.Package.PartCount - 1)
                {
                    // Reset viewing parameters
                    UcEditor.ResetView(false);

                    // Select new part
                    _lastSelectedBlock?.Dispose();
                    _lastSelectedBlock = null;

                    _editor.Part = null;

                    var newPart = part.Package.GetPart(index + 1);
                    _editor.Part = newPart;
                    Type.Text = _packageName + " - " + newPart.Type;

                    part.Dispose();
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

        private void TypeOfContentDialog_AddNewPart(string newPartType, bool newPackage)
        {
            if (newPartType != string.Empty)
            {
                // Reset viewing parameters
                UcEditor.ResetView(false);

                _lastSelectedBlock?.Dispose();
                _lastSelectedBlock = null;

                if (!newPackage && (_editor.Part != null))
                {
                    var package = _editor.Part.Package;

                    _editor.Part.Dispose();
                    _editor.Part = null;

                    var part = package.CreatePart(newPartType);
                    _editor.Part = part;
                    Type.Text = _packageName + " - " + part.Type;
                }
                else
                {
                    // Close current package
                    if (_editor.Part != null)
                    {
                        var part = _editor.Part;
                        var package = part?.Package;
                        package?.Save();
                        _editor.Part = null;
                        part?.Dispose();
                        package?.Dispose();
                    }

                    // Create package and part
                    {
                        var packageName = MakeUntitledFilename();
                        var package = _engine.CreatePackage(packageName);
                        var part = package.CreatePart(newPartType);

                        _editor.Part = part;
                        _packageName = System.IO.Path.GetFileName(packageName);
                        Type.Text = _packageName + " - " + part.Type;
                    }
                }
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

                part.Package.SaveAs(filePath);
                _packageName = fileName;
                Type.Text = _packageName + " - " + part.Type;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var part = _editor.Part;

            if (part == null)
                return;

            part.Package.Save();
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
                // Reset viewing parameters
                UcEditor.ResetView(false);

                _lastSelectedBlock?.Dispose();
                _lastSelectedBlock = null;

                // Close current package
                if (_editor.Part != null)
                {
                    var part = _editor.Part;
                    var package = part?.Package;
                    package?.Save();
                    _editor.Part = null;
                    part?.Dispose();
                    package?.Dispose();
                }

                // Open package and select first part
                {
                    var package = _engine.OpenPackage(filePath);
                    var part = package.GetPart(0);
                    _editor.Part = part;
                    _packageName = fileName;
                    Type.Text = _packageName + " - " + part.Type;
                }
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
                var displayCopy     = (onTextDocument ? !isRoot : !onRawContent);
                var displayPaste    = hasTypes && isRoot;
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

        private void SetInputMode(InputMode inputMode)
        {
            UcEditor.InputMode = inputMode;
            Auto.IsChecked = (inputMode == InputMode.AUTO);
            Touch.IsChecked = (inputMode == InputMode.TOUCH);
            Pen.IsChecked = (inputMode == InputMode.PEN);
        }

        private void Pen_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;

            if ((bool)toggleButton.IsChecked)
            {
                SetInputMode(InputMode.PEN);
            }
        }

        private void Touch_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;

            if ((bool)toggleButton.IsChecked)
            {
                SetInputMode(InputMode.TOUCH);
            }
        }

        private void Auto_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggleButton = sender as ToggleButton;

            if ((bool)toggleButton.IsChecked)
            {
                SetInputMode(InputMode.AUTO);
            }
        }

        private void More_Open(object sender, EventArgs e)
        {
            var box = sender as ComboBox;
            box.SelectedIndex = -1;
        }

        private void SmartGuideEnabled_Click(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            UcEditor.SmartGuideEnabled = (bool)checkBox.IsChecked;
        }
    }
}

