// Copyright @ MyScript. All rights reserved.

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using MyScript.IInk.UIReferenceImplementation;

namespace MyScript.IInk.Demo
{
    /// <summary>
    /// Interaction logic for TypeOfContentDialog.xaml
    /// </summary>
    public partial class TypeOfContentDialog : UserControl
    {
        public TypeOfContentDialog()
        {
            InitializeComponent();
            Visibility = Visibility.Hidden;
        }

        private string _result = string.Empty;
        private bool _newPackage = true;
        private bool _cancelable = false;
        private UIElement _parent;
        public delegate void AddNewPartHandler(string partType, bool newPackage);
        public event AddNewPartHandler AddNewPart;

        public string ShowHandlerDialog(string[] supportedPartTypes, bool newPackage, bool cancelable)
        {
            Visibility = Visibility.Visible;

            _parent.IsEnabled = false;
            _newPackage = newPackage;
            _cancelable = cancelable;

            Close.IsEnabled = _cancelable;
            Close.Visibility = _cancelable ? Visibility.Visible : Visibility.Hidden;

            List<Button> buttons = new List<Button>();

            if (Types.Children.Count == 0)
            {
                foreach (string type in supportedPartTypes)
                {
                    var button = new Button()
                    {
                        Margin = new Thickness(5),
                        Content = type,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        Style = (Style)FindResource("CustomButton")
                    };

                    button.Click += PartType_Click;
                    Types.Children.Add(button);
                }
            }

            return string.Empty;
        }

        private void HideHandlerDialog()
        {
            Visibility = Visibility.Hidden;
            _parent.IsEnabled = true;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _result = ((Button)(sender)).Content.ToString();
            HideHandlerDialog();
        }

        private void PartType_Click(object sender, RoutedEventArgs e)
        {
            _result = ((Button)(sender)).Content.ToString();
            AddNewPart?.Invoke(_result, _newPackage);
            HideHandlerDialog();
        }

        internal void SetParent(UIElement modalDialogParent)
        {
            _parent = modalDialogParent;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (_cancelable)
            {
                AddNewPart?.Invoke(string.Empty, _newPackage);
                HideHandlerDialog();
            }
        }
    }
}
