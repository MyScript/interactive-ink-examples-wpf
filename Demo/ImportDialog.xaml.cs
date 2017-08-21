// Copyright MyScript. All right reserved.

using System.Windows;

namespace MyScript.IInk.Demo
{
    /// <summary>
    /// Interaction logic for ImportDialog.xaml
    /// </summary>
    partial class ImportDialog : Window
    {

        public ImportDialog(Window parent, string title, MimeType[] mimeTypes)
        {
            InitializeComponent();

            Owner = parent;
            Title = title;
            ResultTextBox.Text = "";

            MimeTypeComboBox.Items.Clear();
            foreach (var mimeType in mimeTypes)
                MimeTypeComboBox.Items.Add(MimeTypeF.GetTypeName(mimeType));

            MimeTypeComboBox.SelectedIndex = 0;
        }

        public string ResultText
        {
            get { return ResultTextBox.Text; }
            set { ResultTextBox.Text = value; }
        }

        public int SelectedMimeType
        {
            get { return MimeTypeComboBox.SelectedIndex; }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
