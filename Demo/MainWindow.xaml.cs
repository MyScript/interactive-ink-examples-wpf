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
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_editor != null)
                _editor.Part = null;
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
                    var newPart = part.Package.GetPart(index - 1);
                    _editor.Part = newPart;
                    Type.Text = _packageName + " - " + newPart.Type;
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
                    var newPart = part.Package.GetPart(index + 1);
                    _editor.Part = newPart;
                    Type.Text = _packageName + " - " + newPart.Type;
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
                ContentPackage package;

                if (!newPackage && (_editor.Part != null))
                {
                    package = _editor.Part.Package;
                }
                else
                {
                    string packageName = MakeUntitledFilename();
                    package = _engine.CreatePackage(packageName);
                    _packageName = System.IO.Path.GetFileName(packageName);
                }

                _editor.Part = null;

                // Reset viewing parameters
                UcEditor.ResetView(false);

                var part = package.CreatePart(newPartType);
                _editor.Part = part;
                Type.Text = _packageName + " - " + part.Type;
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

                // Open package and select first part
                _editor.Part = null;
                var package = _engine.OpenPackage(filePath);
                var part = package.GetPart(0);
                _editor.Part = part;
                _packageName = fileName;
                Type.Text = _packageName + " - " + part.Type;
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
            string name;

            do
            {
                string baseName = "File" + (++_filenameIndex) + ".iink";
                name = System.IO.Path.Combine(localFolder, "MyScript", baseName);
            }
            while (System.IO.File.Exists(name));

            return name;
        }

        private void ShowContextMenu()
        {
            var contentBlock = _lastSelectedBlock;
            var supportedTypes = _editor.SupportedAddBlockTypes;
            var supportedExports = _editor.GetSupportedExportMimeTypes(contentBlock);
            var supportedImport = _editor.GetSupportedImportMimeTypes(contentBlock);

            var isContainer = contentBlock.Type == "Container";
            var isRoot = contentBlock.Id == _editor.GetRootBlock().Id;

            var displayConvert  = !isContainer && !_editor.IsEmpty(contentBlock);
            var displayAddBlock = supportedTypes != null && supportedTypes.Any() && isContainer;
            var displayAddImage = false; //supportedTypes != null && supportedTypes.Any() && isContainer;
            var displayRemove   = !isRoot && !isContainer;
            var displayCopy     = !isRoot && !isContainer;
            var displayPaste    = supportedTypes != null && supportedTypes.Any() && isContainer;
            var displayImport   = supportedImport != null && supportedImport.Any();
            var displayExport   = supportedExports != null && supportedExports.Any();
            var displayOfficeClipboard = (supportedExports != null) && supportedExports.Contains(MimeType.OFFICE_CLIPBOARD);

            var contextMenu = new ContextMenu();

            if (displayConvert)
            {
                MenuItem convertItem = new MenuItem();

                convertItem.Header = "Convert";
                convertItem.Click += ConvertBlock;
                contextMenu.Items.Add(convertItem);
            }

            if (displayAddBlock || displayAddImage)
            {
                MenuItem addItem = new MenuItem();

                addItem.Header = "Add...";
                contextMenu.Items.Add(addItem);

                if (displayAddBlock)
                {
                    for (int i = 0; i < supportedTypes.Count(); ++i)
                    {
                        MenuItem addBlockItem = new MenuItem();
                        addBlockItem.Header = "Add " + supportedTypes[i];
                        addBlockItem.Tag = supportedTypes[i];
                        addBlockItem.Click += AddBlock;

                        addItem.Items.Add(addBlockItem);
                    }
                }

                if (displayAddImage)
                {
                    MenuItem addImageItem = new MenuItem();

                    addImageItem.Header = "Add Image";
                    addImageItem.Click += AddImage;

                    addItem.Items.Add(addImageItem);
                }
            }

            if (displayCopy)
            {
                MenuItem copyItem = new MenuItem();

                copyItem.Header = "Copy";
                copyItem.Click += Copy;
                contextMenu.Items.Add(copyItem);
            }

            if (displayPaste)
            {
                MenuItem pasteItem = new MenuItem();

                pasteItem.Header = "Paste";
                pasteItem.Click += Paste;
                contextMenu.Items.Add(pasteItem);
            }

            if (displayRemove)
            {
                MenuItem removeItem = new MenuItem();

                removeItem.Header = "Remove";
                removeItem.Click += Remove;
                contextMenu.Items.Add(removeItem);
            }

            if (displayImport || displayExport)
            {
                MenuItem importExportItem = new MenuItem();

                importExportItem.Header = "Import/Export...";
                contextMenu.Items.Add(importExportItem);

                if (displayImport)
                {
                    MenuItem importItem = new MenuItem();

                    importItem.Header = "Import";
                    importItem.Click += Import;

                    importExportItem.Items.Add(importItem);
                }

                if (displayExport)
                {
                    MenuItem exportItem = new MenuItem();

                    exportItem.Header = "Export";
                    exportItem.Click += Export;

                    importExportItem.Items.Add(exportItem);
                }
            }

            if (displayOfficeClipboard)
            {
                MenuItem clipboardItem = new MenuItem();

                clipboardItem.Header = "Copy To Clipboard (Microsoft Office)";
                clipboardItem.Click += CopyToClipboard;
                contextMenu.Items.Add(clipboardItem);
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
            _lastSelectedBlock = _editor.HitBlock(_lastPointerPosition.X, _lastPointerPosition.Y);

            if (_lastSelectedBlock == null)
                _lastSelectedBlock = _editor.GetRootBlock();

            if (_lastSelectedBlock != null)
            {
                ShowContextMenu();
                e.Handled = true;
            }
        }

        private void ShowSmartGuideMenu(Point globalPos)
        {
            _lastSelectedBlock = UcEditor.SmartGuide.ContentBlock;

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
                    _editor.RemoveBlock(_lastSelectedBlock);
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
            _editor.Paste(_lastPointerPosition.X, _lastPointerPosition.Y);
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

            if (_lastSelectedBlock == null)
                return;

            var mimeTypes = _editor.GetSupportedExportMimeTypes(_lastSelectedBlock);

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
                            var drawer = new ImageDrawer(_editor.Renderer.DpiX, _editor.Renderer.DpiY);

                            drawer.ImageLoader = UcEditor.ImageLoader;

                            _editor.WaitForIdle();
                            _editor.Export_(_lastSelectedBlock, filePath, drawer);

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
                    var drawer = new ImageDrawer(_editor.Renderer.DpiX, _editor.Renderer.DpiY);

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
    }
}

