using System;
using System.Collections.Generic;
using System.Json;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MyScript.IInk.UIReferenceImplementation
{
    /// <summary>
    /// Interaction logic for SmartGuideUserControl.xaml
    /// </summary>
    public sealed partial class SmartGuideUserControl : UserControl
    {
        private class Word
        {
            public string Label;
            public bool Updated;
            public List<string> Candidates;
        };

        private const int SMART_GUIDE_FADE_OUT_DELAY_WRITE_IN_DIAGRAM_DEFAULT   = 3000;
        private const int SMART_GUIDE_FADE_OUT_DELAY_WRITE_OTHER_DEFAULT        = 0;
        private const int SMART_GUIDE_FADE_OUT_DELAY_OTHER_DEFAULT              = 0;
        private const int SMART_GUIDE_HIGHLIGHT_REMOVAL_DELAY_DEFAULT           = 2000;

        private const int SMART_GUIDE_CLICK_TIMING  = 300;  // timing in ms
        private const int SMART_GUIDE_CLICK_DXY     = 5;    // distance in px

        private Color SMART_GUIDE_CONTROL_COLOR         = Color.FromArgb(0xFF, 0x95, 0x9D, 0xA6);
        private Color SMART_GUIDE_TEXT_DEFAULT_COLOR    = Color.FromArgb(0xFF, 0xBF, 0xBF, 0xBF);
        private Color SMART_GUIDE_TEXT_HIGHLIGHT_COLOR  = Colors.Black;

        private enum UpdateCause
        {
            Visual,     /**< A visual change occurred. */
            Edit,       /**< An edit occurred (writing or editing gesture). */
            Selection,  /**< The selection changed. */
            View        /**< View parameters changed (scroll or zoom). */
        };

        private enum TextBlockStyle
        {
            H1,
            H2,
            H3,
            NORMAL
        };

        public delegate void MoreClickedHandler(Point globalPos);

        private Editor _editor;

        public event MoreClickedHandler MoreClicked;

        private ContentBlock _activeBlock;
        private ContentBlock _selectedBlock;

        private ContentBlock _currentBlock;
        private List<Word> _currentWords;

        private ContentBlock _previousBlock;
        private List<Word> _previousWords;

        private bool _pointerDown;
        private int _pointerDownT;
        private Point _pointerDownP;
        private Point _pointerMoveP;

        private DispatcherTimer _timer1;
        private DispatcherTimer _timer2;
        private int fadeOutWriteInDiagramDelay;
        private int fadeOutWriteDelay;
        private int fadeOutOtherDelay;
        private int removeHighlightDelay;

        public Editor Editor
        {
            get { return _editor; }
            set { SetEditor(value); }
        }

        public ContentBlock ContentBlock
        {
            get { return _currentBlock; }
        }

        public SmartGuideUserControl()
        {
            InitializeComponent();
            Initialize();

            _currentWords = new List<Word>();
            _previousWords = new List<Word>();

            _pointerDown = false;
            _pointerDownT = 0;
            _pointerDownP = new Point();
            _pointerMoveP = new Point();

            _timer1 = new DispatcherTimer();
            _timer1.Tick += new EventHandler(onTimeout1);
            _timer2 = new DispatcherTimer();
            _timer2.Tick += new EventHandler(onTimeout2);
        }

        private void Initialize()
        {
            this.Visibility = Visibility.Hidden;

            // Required for positionning using margins
            this.HorizontalAlignment = HorizontalAlignment.Left;
            this.VerticalAlignment = VerticalAlignment.Top;

            // Iput events
            this.MouseEnter += OnMouseEnterEvent;
            this.MouseLeave += OnMouseLeaveEvent;
            this.StylusEnter += OnStylusEnterEvent;
            this.StylusLeave += OnStylusLeaveEvent;
            this.TouchEnter += OnTouchEnterEvent;
            this.TouchLeave += OnTouchLeaveEvent;

            // Input buttons events: use preview events because of tunneling/bubbling/swallowing behavior
            this.PreviewMouseDown += MousePressEvent;
            this.PreviewMouseUp +=  MouseReleaseEvent;
            this.PreviewStylusDown += StylusPressEvent;
            this.PreviewStylusUp +=  StylusReleaseEvent;
            this.PreviewTouchDown += TouchPressEvent;
            this.PreviewTouchUp +=  TouchReleaseEvent;
            this.MouseMove += MouseMoveEvent_;
            this.StylusMove += StylusMoveEvent_;
            this.TouchMove += TouchMoveEvent_;

            // Sub-items
            styleItem.IsEnabled = false;
            moreItem.IsEnabled = false;

            textItem.IsEnabled = false;
            textItem.Children.Clear();

            scrollItem.IsEnabled = true;
            scrollItem.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scrollItem.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
            scrollItem.IsDeferredScrollingEnabled = false;
        }

        private void SetEditor(Editor editor)
        {
            _editor = editor;

            Configuration configuration = _editor.Engine.Configuration;
            fadeOutWriteInDiagramDelay = (int)configuration.GetNumber("smart-guide.fade-out-delay.write-in-diagram", SMART_GUIDE_FADE_OUT_DELAY_WRITE_IN_DIAGRAM_DEFAULT);
            fadeOutWriteDelay = (int)configuration.GetNumber("smart-guide.fade-out-delay.write", SMART_GUIDE_FADE_OUT_DELAY_WRITE_OTHER_DEFAULT);
            fadeOutOtherDelay = (int)configuration.GetNumber("smart-guide.fade-out-delay.other", SMART_GUIDE_FADE_OUT_DELAY_OTHER_DEFAULT);
            removeHighlightDelay = (int)configuration.GetNumber("smart-guide.highlight-removal-delay", SMART_GUIDE_HIGHLIGHT_REMOVAL_DELAY_DEFAULT);
        }

        private static List<Word> CloneWords(List<Word> from)
        {
            if (from == null)
                return null;

            List<Word> to = new List<Word>();

            foreach (var word in from)
            {
                var word_ = new Word();

                word_.Label = word.Label;
                word_.Updated = word.Updated;
                word_.Candidates = null;

                if (word.Candidates != null)
                {
                    word_.Candidates = new List<string>();
                    foreach (var candidate in word_.Candidates)
                        word_.Candidates.Add(candidate);
                }

                to.Add(word_);
            }

            return to;
        }

        private void onTimeout1(object sender, EventArgs e)
        {
            this.Visibility = Visibility.Hidden;
            _timer1.Stop();
        }

        private void onTimeout2(object sender, EventArgs e)
        {
            foreach (var child in textItem.Children)
            {
                var label = child as Label;
                label.Foreground = new SolidColorBrush(SMART_GUIDE_TEXT_DEFAULT_COLOR);
            }

            _timer2.Stop();
        }

        private void BackupData()
        {
            _previousBlock = _currentBlock;
            _previousWords = CloneWords(_currentWords);
        }

        private void UpdateData()
        {
            if (_currentBlock == null)
            {
                _currentWords.Clear();
            }
            else if (_currentBlock.IsValid())
            {
                string jiixStr;

                try
                {
                    jiixStr = _editor.Export_(_currentBlock, MimeType.JIIX);
                }
                catch
                {
                    // when processing is ongoing, export may fail : ignore
                    return;
                }

                var words = new List<Word>();
                var jiix = JsonValue.Parse(jiixStr) as JsonObject;
                var jiixWords = (JsonArray)jiix["words"];
                foreach (var jiixWord_ in jiixWords)
                {
                    var jiixWord = (JsonObject)jiixWord_;

                    var label = (string)jiixWord["label"];

                    var candidates = new List<string>();
                    JsonValue jiixCandidates_;
                    if (jiixWord.TryGetValue("candidates", out jiixCandidates_))
                    {
                        var jiixCandidates = (JsonArray)jiixCandidates_;
                        foreach (var jiixCandidate_ in jiixCandidates)
                            candidates.Add((string)jiixCandidate_);
                    }

                    words.Add(new Word() { Label = label, Candidates = candidates, Updated = false });
                }

                _currentWords = words;

                if ((_previousBlock != null) && (_currentBlock.Id == _previousBlock.Id))
                {
                    ComputeTextDifferences(_previousWords, _currentWords);
                }
                else
                {
                    var count = _currentWords.Count;
                    for (var c = 0; c < count; ++c)
                    {
                        var word = _currentWords[c];
                        word.Updated = false;
                        _currentWords[c] = word;
                    }
                }
            }
        }

        private void ResetWidgets()
        {
            Visibility = Visibility.Hidden;
            Margin = new Thickness(0, 0, 0, 0);
            textItem.Children.Clear();
            SetTextBlockStyle(TextBlockStyle.NORMAL);
        }

        private static void GetBlockPadding(ContentBlock block, out float paddingLeft, out float paddingRight)
        {
            paddingLeft = 0.0f;
            paddingRight = 0.0f;
            if (!string.IsNullOrEmpty(block.Attributes))
            {
                var attributes = JsonValue.Parse(block.Attributes) as JsonObject;
                JsonValue padding_;
                if (attributes.TryGetValue("padding", out padding_))
                {
                    var padding = padding_ as JsonObject;
                    paddingLeft = (float)(double)padding["left"];
                    paddingRight = (float)(double)padding["right"];
                }
            }
        }

        void SetTextBlockStyle(TextBlockStyle textBlockStyle)
        {
            switch (textBlockStyle)
            {
                case TextBlockStyle.H1:
                    styleItem.Content = "H1";
                    styleItem.BorderBrush = Brushes.Black;
                    styleItem.Background = Brushes.Black;
                    styleItem.Foreground = Brushes.White;
                    break;

                case TextBlockStyle.H2:
                    styleItem.Content = "H2";
                    styleItem.BorderBrush = new SolidColorBrush(SMART_GUIDE_CONTROL_COLOR);
                    styleItem.Background = new SolidColorBrush(SMART_GUIDE_CONTROL_COLOR);
                    styleItem.Foreground = Brushes.White;
                    break;

                case TextBlockStyle.H3:
                    styleItem.Content = "H3";
                    styleItem.BorderBrush = new SolidColorBrush(SMART_GUIDE_CONTROL_COLOR);
                    styleItem.Background = new SolidColorBrush(SMART_GUIDE_CONTROL_COLOR);
                    styleItem.Foreground = Brushes.White;
                    break;

                case TextBlockStyle.NORMAL:
                default:
                    styleItem.Content = "¶";
                    styleItem.BorderBrush = new SolidColorBrush(SMART_GUIDE_CONTROL_COLOR);
                    styleItem.Background = Brushes.White;
                    styleItem.Foreground = new SolidColorBrush(SMART_GUIDE_CONTROL_COLOR);
                    break;
            }
        }

        private void UpdateWidgets(UpdateCause cause)
        {
            _timer1.Stop();
            if (_currentBlock != null)
            {
                // Update size and position
                var rectangle = _currentBlock.Box;
                float paddingLeft, paddingRight;
                GetBlockPadding(_currentBlock, out paddingLeft, out paddingRight);
                var transform = _editor.Renderer.GetViewTransform();
                var topLeft = transform.Apply(rectangle.X + paddingLeft, rectangle.Y);
                var topRight = transform.Apply(rectangle.X + rectangle.Width - paddingRight, rectangle.Y);
                var x = topLeft.X;
                var y = topLeft.Y;
                var width = topRight.X - topLeft.X;

                Width = width;
                Margin = new Thickness(Math.Floor(x), Math.Floor(y - ActualHeight), 0, 0);

                // Update text
                Label lastUpdatedItem = null;
                {
                    textItem.Children.Clear();

                    foreach (var word in _currentWords)
                    {
                        var label = word.Label;
                        label = label.Replace('\n', ' ');

                        var item = new Label{
                                                Content = label,
                                                HorizontalAlignment = HorizontalAlignment.Left,
                                                VerticalAlignment = VerticalAlignment.Stretch,
                                                HorizontalContentAlignment = HorizontalAlignment.Left,
                                                VerticalContentAlignment = VerticalAlignment.Center,
                                                Padding = new Thickness(0),
                                                BorderThickness = new Thickness(0),
                                                Margin = new Thickness(0),
                                                Background = Brushes.Transparent
                                             };

                        if (word.Updated)
                            item.Foreground = new SolidColorBrush(SMART_GUIDE_TEXT_HIGHLIGHT_COLOR);
                        else
                            item.Foreground = new SolidColorBrush(SMART_GUIDE_TEXT_DEFAULT_COLOR);

                        textItem.Children.Add(item);

                        if (word.Updated)
                            lastUpdatedItem = item;
                    }
                }

                // Set cursor position
                if (lastUpdatedItem != null)
                    lastUpdatedItem.BringIntoView();
                else
                    scrollItem.ScrollToLeftEnd();

                // Update Style item
                SetTextBlockStyle(TextBlockStyle.NORMAL);

                // Visibility/Fading
                {
                    var configuration = _editor.Engine.Configuration;
                    int delay = 0;

                    if (cause == UpdateCause.Edit)
                        delay = _currentBlock.Id.StartsWith("diagram/") ? fadeOutWriteInDiagramDelay : fadeOutWriteDelay;
                    else
                        delay = fadeOutOtherDelay;

                    if (delay > 0)
                    {
                        _timer1.Interval = TimeSpan.FromMilliseconds(delay);
                        _timer1.Start();
                    }

                    if (lastUpdatedItem != null)
                    {
                        _timer2.Interval = TimeSpan.FromMilliseconds(removeHighlightDelay);
                        _timer2.Start();
                    }

                    this.Visibility = Visibility.Visible;
                }
            }
            else
            {
                ResetWidgets();
            }
        }

        public void OnPartChanged()
        {
            BackupData();
            _currentBlock = _activeBlock = _selectedBlock = null;
            UpdateData();
            ResetWidgets();
        }

        public void OnContentChanged(string[] blockIds)
        {
            if (_editor == null)
            {
                ResetWidgets();
                return;
            }

            // The active block may have been removed then added again in which case
            // the old instance is invalid but can be restored by remapping the identifier
            if ( (_activeBlock != null) && !_activeBlock.IsValid())
            {
                _activeBlock = _editor.GetBlockById(_activeBlock.Id);
                if (_activeBlock == null)
                    ResetWidgets();
            }

            if (_activeBlock != null)
            {
                if (blockIds.Contains(_activeBlock.Id))
                {
                    _currentBlock = _activeBlock;
                    BackupData();
                    UpdateData();
                    UpdateWidgets(UpdateCause.Edit);
                }
            }
        }

        public void OnSelectionChanged(string[] blockIds)
        {
            ContentBlock block = null;

            foreach (var blockId in blockIds)
            {
                var block_ = _editor.GetBlockById(blockId);

                if ( (block_ != null) && (block_.Type == "Text") )
                {
                    block = block_;
                    break;
                }
            }

            bool selectionChanged = false;

            if ((block != null) && (_currentBlock != null))
                selectionChanged = (_currentBlock.Id != block.Id);
            else
                selectionChanged = (block != _currentBlock);

            if (selectionChanged)
            {
                if (_selectedBlock != null)
                {
                    BackupData();
                    _currentBlock = _selectedBlock;
                    UpdateData();
                    UpdateWidgets(UpdateCause.Selection);
                }
                else
                {
                    ResetWidgets();
                }
            }
        }

        public void OnActiveBlockChanged(string blockId)
        {
            _activeBlock = _editor.GetBlockById(blockId);

            if ( (_currentBlock != null) && (_activeBlock != null) && (_currentBlock.Id == _activeBlock.Id) )
                return; // selectionChanged already changed the active block

            BackupData();
            _currentBlock = _activeBlock;
            UpdateData();

            if (_currentBlock != null)
                UpdateWidgets(UpdateCause.Edit);
            else
                ResetWidgets();
        }

        public void OnTransformChanged()
        {
            UpdateWidgets(UpdateCause.View);
        }

        static private void ComputeTextDifferences(List<Word> s1, List<Word> s2)
        {
            var len1 = s1.Count;
            var len2 = s2.Count;

            uint[,] d = new uint[len1 + 1, len2 + 1];
            int i;
            int j;

            // Levenshtein distance algorithm at word level
            d[0,0] = 0;
            for(i = 1; i <= len1; ++i)
                d[i,0] = (uint)i;
            for(i = 1; i <= len2; ++i)
                d[0,i] = (uint)i;

            for(i = 1; i <= len1; ++i)
            {
                for(j = 1; j <= len2; ++j)
                {
                    var d_ = Math.Min(d[i - 1,j] + 1, d[i,j - 1] + 1);
                    d[i,j] = (uint)(Math.Min(d_ , d[i - 1,j - 1] + (s1[i - 1].Label == s2[j - 1].Label ? 0 : 1) ));
                }
            }

            // Backward traversal
            for (j = 0; j < len2; ++j)
            {
                var word = s2[j];
                word.Updated = true;
                s2[j] = word;
            }

            if ( (len1 > 0) && (len2 > 0) )
            {
                i = len1;
                j = len2;

                while (j > 0)
                {
                    int d01 = (int)d[i,j-1];
                    int d11 = (i > 0) ? (int)d[i-1,j-1] : -1;
                    int d10 = (i > 0) ? (int)d[i-1,j] : -1;

                    if ( (d11 >= 0) && (d11 <= d10) && (d11 <= d01) )
                    {
                        --i;
                        --j;
                    }
                    else if ( (d10 >= 0) && (d10 <= d11) && (d10 <= d01) )
                    {
                        --i;
                    }
                    else //if ( (d01 <= d11) && (d01 <= d10) )
                    {
                        --j;
                    }

                    if ( (i < len1) && (j < len2) )
                    {
                        var word = s2[j];
                        word.Updated = s1[i].Label != s2[j].Label;
                        s2[j] = word;
                    }
                }
            }
        }

        private void OnEnterEvent()
        {
            _pointerDown = false;
        }

        private void OnLeaveEvent()
        {
            _pointerDown = false;
        }

        private void OnMouseEnterEvent(object sender, MouseEventArgs e)
        {
            OnEnterEvent();
        }

        private void OnMouseLeaveEvent(object sender, MouseEventArgs e)
        {
            OnLeaveEvent();
        }

        private void OnStylusEnterEvent(object sender, StylusEventArgs e)
        {
            OnEnterEvent();
        }

        private void OnStylusLeaveEvent(object sender, StylusEventArgs e)
        {
            OnLeaveEvent();
        }

        private void OnTouchEnterEvent(object sender, TouchEventArgs e)
        {
            OnEnterEvent();
        }

        private void OnTouchLeaveEvent(object sender, TouchEventArgs e)
        {
            OnLeaveEvent();
        }

        private Point GetInputPosition(InputEventArgs e, IInputElement relativeTo)
        {
            if (e is MouseEventArgs)
                return ((MouseEventArgs)e).GetPosition(relativeTo);
            else if (e is StylusEventArgs)
                return ((StylusEventArgs)e).GetPosition(relativeTo);
            else if (e is TouchEventArgs)
                return ((TouchEventArgs)e).GetTouchPoint(relativeTo).Position;

            return new Point();
        }

        private void OnPressEvent(object sender, InputEventArgs e)
        {
            _pointerDown = true;
            _pointerDownT = e.Timestamp;
            _pointerDownP = GetInputPosition(e, this);
            _pointerMoveP = _pointerDownP;
        }

        private void MousePressEvent(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (e.ChangedButton == MouseButton.Left)
                OnPressEvent(sender, e);
        }

        private void StylusPressEvent(object sender, StylusDownEventArgs e)
        {
            e.Handled = true;
            OnPressEvent(sender, e);
        }

        private void TouchPressEvent(object sender, TouchEventArgs e)
        {
            e.Handled = true;
            OnPressEvent(sender, e);
        }

        private void OnMoveEvent(object sender, InputEventArgs e)
        {
            if (!_pointerDown)
                return;

            var rectT = GetRectOfObject(textItem);
            var position = _pointerMoveP;

            _pointerMoveP = GetInputPosition(e, this);

            if (rectT.Contains(_pointerDownP) && rectT.Contains(_pointerMoveP))
            {
                var offset = _pointerMoveP.X - position.X;
                scrollItem.ScrollToHorizontalOffset(scrollItem.HorizontalOffset - offset);
            }
        }

        private void MouseMoveEvent_(object sender, MouseEventArgs e)
        {
            if (!_pointerDown)
                return;

            if (e.LeftButton == MouseButtonState.Released)
                return;

            e.Handled = true;
            OnMoveEvent(sender, e);
        }

        private void StylusMoveEvent_(object sender, StylusEventArgs e)
        {
            if (!_pointerDown)
                return;

            if (e.InAir)
                return;

            e.Handled = true;
            OnMoveEvent(sender, e);
        }

        private void TouchMoveEvent_(object sender, TouchEventArgs e)
        {
            if (!_pointerDown)
                return;

            e.Handled = true;
            OnMoveEvent(sender, e);
        }

        private Rect GetRectOfObject(FrameworkElement element)
        {
            var rect = new Rect(new Point(0, 0), element.RenderSize);
            return element.TransformToVisual(this).TransformBounds(rect);
        }

        private void OnReleaseEvent(object sender, InputEventArgs e)
        {
            if (!_pointerDown)
                return;

            var xy = GetInputPosition(e, this);

            _pointerDown = false;

            // Evaluate click
            var dx = (_pointerDownP.X - xy.X);
            var dy = (_pointerDownP.Y - xy.Y);
            var dxy = (dx * dx) + (dy * dy);
            var dt = e.Timestamp - _pointerDownT;

            if (dt <= SMART_GUIDE_CLICK_TIMING &&
                dxy <= SMART_GUIDE_CLICK_DXY * SMART_GUIDE_CLICK_DXY)
            {
                _timer1.Stop();

                if (GetRectOfObject(textItem).Contains(xy))
                {
                    int idx = 0;
                    foreach (var item_ in textItem.Children)
                    {
                        var item = item_ as Label;
                        var xy_ = GetInputPosition(e, item);

                        if ( (xy_.X >= 0) && (xy_.X < item.ActualWidth)
                            && (xy_.Y >= 0) && (xy_.Y < item.ActualHeight) )
                        {
                            OnWordClicked(item, idx, GetInputPosition(e, null));
                            break;
                        }

                        ++idx;
                    }
                }
                else if (GetRectOfObject(moreItem).Contains(xy))
                {
                    OnMoreClicked(GetInputPosition(e, null));
                }
            }
        }

        private void MouseReleaseEvent(object sender, MouseButtonEventArgs e)
        {
            if ( (e.ChangedButton == MouseButton.Left) && _pointerDown)
            {
                OnReleaseEvent(sender, e);
                e.Handled = true;
            }
        }

        private void StylusReleaseEvent(object sender, StylusEventArgs e)
        {
            if (_pointerDown)
            {
                OnReleaseEvent(sender, e);
                e.Handled = true;
            }
        }

        private void TouchReleaseEvent(object sender, TouchEventArgs e)
        {
            if (_pointerDown)
            {
                OnReleaseEvent(sender, e);
                e.Handled = true;
            }
        }

        private void OnMoreClicked(Point globalPos)
        {
            if (_currentBlock != null)
            {
                MoreClicked?.Invoke(globalPos);
            }
        }

        private void OnWordClicked(Label wordView, int wordIndex, Point globalPos)
        {
            if ((string)wordView.Content == " ")
                return;

            try
            {
                var word = _currentWords[wordIndex];

                var contextMenu = new ContextMenu();
                contextMenu.Tag = (object)wordIndex;
 
                foreach (var candidate in word.Candidates)
                {
                    MenuItem item = new MenuItem();

                    item.Header = candidate;
                    item.IsCheckable = true;
                    if (candidate == word.Label)
                    {
                        item.IsChecked = true;
                    }
                    else
                    {
                        item.Click += OnCandidateClicked;
                        item.Tag = wordView;
                    }

                    contextMenu.Items.Add(item);
                }

                if (contextMenu.Items.Count == 0)
                {
                    MenuItem item = new MenuItem();
                    item.Header = word.Label;
                    item.IsCheckable = true;
                    item.IsChecked = true;

                    contextMenu.Items.Add(item);
                }

                contextMenu.PlacementTarget = textItem;
                contextMenu.IsOpen = true;
            }
            catch
            {
            }
        }

        private void OnCandidateClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var item = sender as MenuItem;
                var menu = item.Parent as ContextMenu;
                var wordView = (Label)item.Tag;
                var wordIndex = (int)menu.Tag;
                var wordLabel = (string)item.Header;

                _currentWords[wordIndex].Label = wordLabel;
                wordView.Content = wordLabel;

                string jiixStr = _editor.Export_(_currentBlock, MimeType.JIIX);
                var jiix = JsonValue.Parse(jiixStr) as JsonObject;
                var jiixWords = (JsonArray)jiix["words"];
                var jiixWord = (JsonObject)jiixWords[wordIndex];
                jiixWord["label"] = wordLabel;
                jiixStr = jiix.ToString();
                _editor.Import_(MimeType.JIIX, jiixStr, _currentBlock);
            }
            catch
            {
            }
        }
    }
}
