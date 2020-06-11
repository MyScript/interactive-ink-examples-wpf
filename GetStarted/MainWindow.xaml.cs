// Copyright MyScript. All right reserved.

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using MyScript.IInk.UIReferenceImplementation;

namespace MyScript.IInk.GetStarted
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Defines the type of content (possible values are: "Text Document", "Text", "Diagram", "Math", "Drawing" and "Raw Content")
        private const string PART_TYPE = "Text Document";

        private Engine _engine;
        private Editor _editor => UcEditor.Editor;

        public MainWindow()
        {
            InitializeComponent();
            this.Closing += Window_Closing;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_editor != null)
            {
                var part = _editor.Part;
                var package = part?.Package;

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

            // Force pointer to be a pen, for an automatic detection, set InputMode to AUTO
            SetInputMode(InputMode.PEN);

            NewFile();
         }

        private void EditUndo_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            _editor.Undo();
        }

        private void EditRedo_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            _editor.Redo();
        }

        private void EditClear_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            _editor.Clear();
        }

        private void EditConvert_MenuItem_Click(object sender, RoutedEventArgs e)
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

        public void NewFile()
        {
            // Close current package
            if (_editor.Part != null)
            {
                var part = _editor.Part;
                var package = part?.Package;
                _editor.Part = null;
                part?.Dispose();
                package?.Dispose();
            }

            // Create package and part
            {
                var packageName = MakeUntitledFilename();
                var package = _engine.CreatePackage(packageName);
                var part = package.CreatePart(PART_TYPE);
                _editor.Part = part;
                Type.Text = "Type: " + PART_TYPE;
            }
        }

        private string MakeUntitledFilename()
        {
            var localFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            int num = 0;
            string name;

            do
            {
                string baseName = "File" + (++num) + ".iink";
                name = System.IO.Path.Combine(localFolder, "MyScript", baseName);
            }
            while (System.IO.File.Exists(name));

            return name;
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
    }
}
