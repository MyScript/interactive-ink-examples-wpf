// Copyright @ MyScript. All rights reserved.

using MyScript.IInk.Graphics;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MyScript.IInk.UIReferenceImplementation
{

    public class RendererListener : IRendererListener
    {
        private EditorUserControl _ucEditor;

        public RendererListener(EditorUserControl ucEditor)
        {
            _ucEditor = ucEditor;
        }

        public void ViewTransformChanged(Renderer renderer)
        {
            if (_ucEditor.SmartGuideEnabled && _ucEditor.smartGuide != null)
            {
                var dispatcher = _ucEditor.Dispatcher;
                dispatcher.BeginInvoke(new Action(() => { _ucEditor.smartGuide.OnTransformChanged(); }));
            }
        }
    }

    public class EditorListener : IEditorListener
    {
        private EditorUserControl _ucEditor;

        public EditorListener(EditorUserControl ucEditor)
        {
            _ucEditor = ucEditor;
        }

        public void PartChanged(Editor editor)
        {
            if (_ucEditor.SmartGuideEnabled && _ucEditor.smartGuide != null)
            {
                var dispatcher = _ucEditor.Dispatcher;
                dispatcher.BeginInvoke(new Action(() => { _ucEditor.smartGuide.OnPartChanged(); }));
            }
        }

        public void ContentChanged(Editor editor, string[] blockIds)
        {
            if (_ucEditor.SmartGuideEnabled && _ucEditor.smartGuide != null)
            {
                var dispatcher = _ucEditor.Dispatcher;
                dispatcher.BeginInvoke(new Action(() => { _ucEditor.smartGuide.OnContentChanged(blockIds); }));
            }
        }

        public void SelectionChanged(Editor editor)
        {
            if (_ucEditor.SmartGuideEnabled && _ucEditor.smartGuide != null)
            {
                using (var selection = editor.GetSelection())
                {
                    var mode = editor.GetSelectionMode();
                    var blockIds = editor.GetIntersectingBlocks(selection);
                    var dispatcher = _ucEditor.Dispatcher;
                    dispatcher.BeginInvoke(new Action(() => { _ucEditor.smartGuide.OnSelectionChanged(blockIds, mode); }));
                }
            }
        }

        public void ActiveBlockChanged(Editor editor, string blockId)
        {
            if (_ucEditor.SmartGuideEnabled && _ucEditor.smartGuide != null)
            {
                var dispatcher = _ucEditor.Dispatcher;
                dispatcher.BeginInvoke(new Action(() => { _ucEditor.smartGuide.OnActiveBlockChanged(blockId); }));
            }
        }

        public void OnError(Editor editor, string blockId, EditorError error, string message)
        {
            MessageBox.Show(message, "Error " + error, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


    /// <summary>
    /// Interaction logic for EditorControl.xaml
    /// </summary>
    public sealed partial class EditorUserControl : UserControl, IRenderTarget
    {
        private Engine _engine;
        private Editor _editor;
        private Renderer _renderer;
        private ToolController _toolController;
        private ImageLoader _loader;
        private bool _smartGuideEnabled = true;
        private float _pixelDensity = 1.0f;

        public Engine Engine
        {
            get
            {
                return _engine;
            }

            set
            {
                _engine = value;
            }
        }

        public Editor Editor
        {
            get
            {
                return _editor;
            }
        }

        public Renderer Renderer
        {
            get
            {
                return _renderer;
            }
        }

        public ToolController ToolController
        {
            get
            {
                return _toolController;
            }
        }

        public ImageLoader ImageLoader
        {
            get
            {
                return _loader;
            }
        }

        public SmartGuideUserControl SmartGuide
        {
            get
            {
                return smartGuide;
            }
        }

        public bool SmartGuideEnabled
        {
            get
            {
                return _smartGuideEnabled;
            }

            set
            {
                EnableSmartGuide(value);
            }
        }

        public void SetInputTool(PointerTool pointerTool)
        {
            ToolController.SetToolForType(PointerType.PEN,   pointerTool);
            ToolController.SetToolForType(PointerType.MOUSE, pointerTool);
            if (!_activePen)
                ToolController.SetToolForType(PointerType.TOUCH, pointerTool);
        }

        public void SetActivePen(bool enabled)
        {
            _activePen = enabled;
            if (enabled)
                ToolController.SetToolForType(PointerType.TOUCH, PointerTool.HAND);
            else
                ToolController.SetToolForType(PointerType.TOUCH, ToolController.GetToolForType(PointerType.PEN));
        }

        public void SetToolStyle(PointerTool pointerTool, string style)
        {
            ToolController.SetToolStyle(pointerTool, style);
        }

        private bool _activePen = true;
        private PointerType _inputType = (PointerType)(-1);
        private int _inputDeviceId = -1;
        private bool _onScroll = false;
        private Graphics.Point _lastPointerPosition;

        public EditorUserControl()
        {
            InitializeComponent();
        }

        public void Initialize(Window window)
        {
            float pixelsPerDip = (float)DisplayResolution.GetPixelsPerDip(window);
            Vector rawDpi = DisplayResolution.GetRawDpi(window);
            Vector effectiveDpi = DisplayResolution.GetEffectiveDpi(window);
            float dpiX = (float)effectiveDpi.X;
            float dpiY = (float)effectiveDpi.Y;
            _pixelDensity = (float)rawDpi.Y / dpiY;

            _renderer = _engine.CreateRenderer(dpiX, dpiY, this);
            _renderer.AddListener(new RendererListener(this));

            renderLayer.Renderer = _renderer;

            _toolController = _engine.CreateToolController();

            _editor = _engine.CreateEditor(Renderer, ToolController);
            _editor.SetViewSize((int)Math.Round(renderLayer.ActualWidth), (int)Math.Round(renderLayer.ActualHeight));
            _editor.SetFontMetricsProvider(new FontMetricsProvider(dpiX, dpiY, pixelsPerDip));
            _editor.AddListener(new EditorListener(this));

            // see https://developer.myscript.com/docs/interactive-ink/latest/reference/styling for styling reference
            _editor.Theme =
                "glyph {" +
                "  font-family: MyScriptInter;" +
                "}" +
                ".math {" +
                "  font-family: STIX;" +
                "}" +
                ".math-variable {" +
                "  font-style: italic;" +
                "};";

            smartGuide.Editor = _editor;

            var tempFolder = _engine.Configuration.GetString("content-package.temp-folder");
            _loader = new ImageLoader(_editor, tempFolder);

            renderLayer.ImageLoader = _loader;

            float verticalMarginPX = 60;
            float horizontalMarginPX = 40;
            float verticalMarginMM = 25.4f * verticalMarginPX / dpiY;
            float horizontalMarginMM = 25.4f * horizontalMarginPX / dpiX;
            _engine.Configuration.SetNumber("text.margin.top", verticalMarginMM);
            _engine.Configuration.SetNumber("text.margin.left", horizontalMarginMM);
            _engine.Configuration.SetNumber("text.margin.right", horizontalMarginMM);
            _engine.Configuration.SetNumber("math.margin.top", verticalMarginMM);
            _engine.Configuration.SetNumber("math.margin.bottom", verticalMarginMM);
            _engine.Configuration.SetNumber("math.margin.left", horizontalMarginMM);
            _engine.Configuration.SetNumber("math.margin.right", horizontalMarginMM);
        }

        public void Closing()
        {
            smartGuide?.Closing();
        }

        /// <summary>Helper</summary>
        [Flags]
        public enum ContextualActions
        {
            NONE              = 0,
            ADD_BLOCK         = 1 << 0,     /// Add block. See <c>Editor.GetSupportedAddBlockTypes</c>.
            REMOVE            = 1 << 1,     /// Remove selection.
            CONVERT           = 1 << 2,     /// Convert. See <c>Editor.GetSupportedTargetConversionStates</c>.
            COPY              = 1 << 3,     /// Copy selection.
            OFFICE_CLIPBOARD  = 1 << 4,     /// Copy selection to Microsoft Office clipboard.
            PASTE             = 1 << 5,     /// Paste.
            IMPORT            = 1 << 6,     /// Import. See <c>Editor.GetSupportedImportMimeTypes</c>.
            EXPORT            = 1 << 7,     /// Export. See <c>Editor.GetSupportedExportMimeTypes</c>.
            FORMAT_TEXT       = 1 << 8,     /// Change Text blocks format.
            SELECTION_MODE    = 1 << 9,     /// Change selection mode.
            SELECTION_TYPE    = 1 << 10     /// Change selection type.
        }

        public ContextualActions GetAvailableActions(ContentBlock contentBlock)
        {
            if (contentBlock == null)
                return ContextualActions.NONE;

            var part = _editor.Part;
            if (part == null)
                return ContextualActions.NONE;

            var actions = ContextualActions.NONE;

            using (var rootBlock = _editor.GetRootBlock())
            {
                var isRoot = contentBlock.Id == rootBlock.Id;
                if (!isRoot && (contentBlock.Type == "Container"))
                    return ContextualActions.NONE;

                var onRawContent   = part.Type == "Raw Content";
                var onTextDocument = part.Type == "Text Document";

                var isEmpty = _editor.IsEmpty(contentBlock);

                var supportedBlocks  = _editor.SupportedAddBlockTypes;
                var supportedExports = _editor.GetSupportedExportMimeTypes(onRawContent ? rootBlock : contentBlock);
                var supportedImports = _editor.GetSupportedImportMimeTypes(contentBlock);
                var supportedStates  = _editor.GetSupportedTargetConversionStates(contentBlock);
                var supportedFormats = _editor.GetSupportedTextFormats(contentBlock);
                var supportedModes   = _editor.GetAvailableSelectionModes();
                var supportedTypes   = _editor.GetAvailableSelectionTypes(contentBlock);

                var hasBlocks  = (supportedBlocks  != null) && supportedBlocks.Any();
                var hasExports = (supportedExports != null) && supportedExports.Any();
                var hasImports = (supportedImports != null) && supportedImports.Any();
                var hasStates  = (supportedStates  != null) && supportedStates.Any();
                var hasFormats = (supportedFormats != null) && supportedFormats.Any();
                var hasModes   = (supportedModes   != null) && supportedModes.Any();
                var hasTypes   = (supportedTypes   != null) && supportedTypes.Any();

                if (hasBlocks && (!onTextDocument || isRoot))
                    actions |= ContextualActions.ADD_BLOCK;
                if (!isRoot)
                    actions |= ContextualActions.REMOVE;
                if (hasStates && !isEmpty)
                    actions |= ContextualActions.CONVERT;
                if (!onTextDocument || !isRoot)
                    actions |= ContextualActions.COPY;
                if (hasExports && supportedExports.Contains(MimeType.OFFICE_CLIPBOARD))
                    actions |= ContextualActions.OFFICE_CLIPBOARD;
                if (isRoot)
                    actions |= ContextualActions.PASTE;
                if (hasImports)
                    actions |= ContextualActions.IMPORT;
                if (hasExports)
                    actions |= ContextualActions.EXPORT;
                if (hasFormats)
                    actions |= ContextualActions.FORMAT_TEXT;
                if (hasModes)
                    actions |= ContextualActions.SELECTION_MODE;
                if (hasTypes)
                    actions |= ContextualActions.SELECTION_TYPE;
            }

            return actions;
        }

        public ContextualActions GetAvailableActions(ContentSelection contentSelection)
        {
            if (contentSelection == null || _editor.IsEmpty(contentSelection))
                return ContextualActions.NONE;

            var part = _editor.Part;
            if (part == null)
                return ContextualActions.NONE;

            var actions = ContextualActions.NONE;

            var supportedExports = _editor.GetSupportedExportMimeTypes(contentSelection);
            var supportedStates  = _editor.GetSupportedTargetConversionStates(contentSelection);
            var supportedFormats = _editor.GetSupportedTextFormats(contentSelection);
            var supportedModes   = _editor.GetAvailableSelectionModes();
            var supportedTypes   = _editor.GetAvailableSelectionTypes(contentSelection);

            var hasExports = (supportedExports != null) && supportedExports.Any();
            var hasStates  = (supportedStates  != null) && supportedStates.Any();
            var hasFormats = (supportedFormats != null) && supportedFormats.Any();
            var hasModes   = (supportedModes   != null) && supportedModes.Any();
            var hasTypes   = (supportedTypes   != null) && supportedTypes.Any();

            // Erase
            actions |= ContextualActions.REMOVE;
            if (hasStates)
                actions |= ContextualActions.CONVERT;
            // Copy
            actions |= ContextualActions.COPY;
            if (hasExports && supportedExports.Contains(MimeType.OFFICE_CLIPBOARD))
                actions |= ContextualActions.OFFICE_CLIPBOARD;
            if (hasExports)
                actions |= ContextualActions.EXPORT;
            if (hasFormats)
                actions |= ContextualActions.FORMAT_TEXT;
            if (hasModes)
                actions |= ContextualActions.SELECTION_MODE;
            if (hasTypes)
                actions |= ContextualActions.SELECTION_TYPE;

            return actions;
        }

        /// <summary>Retranscribe the size changed event to editor</summary>
        private void Control_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Editor != null)
                _editor.SetViewSize((int)Math.Round(e.NewSize.Width), (int)Math.Round(e.NewSize.Height));

            ((LayerControl)(sender)).InvalidateVisual();
        }

        /// <summary>Force inks layer to be redrawn</summary>
        public void Invalidate(Renderer renderer, LayerType layers)
        {
            renderLayer.Update();
        }

        /// <summary>Force inks layer to be redrawn</summary>
        public void Invalidate(Renderer renderer, int x, int y, int width, int height, LayerType layers)
        {
            if (width > 0 && height > 0)
            {
                renderLayer.Update();
            }
        }

        public float GetPixelDensity()
        {
            return _pixelDensity;
        }

        private void EnableSmartGuide(bool enable)
        {
            if (_smartGuideEnabled == enable)
                return;

            _smartGuideEnabled = enable;

            if (!_smartGuideEnabled && smartGuide != null)
                smartGuide.Visibility = Visibility.Hidden;
        }

        private System.Windows.Point GetPosition(InputEventArgs e)
        {
            if (e is MouseEventArgs)
                return ((MouseEventArgs)e).GetPosition(this);
            else if (e is StylusEventArgs)
                return ((StylusEventArgs)e).GetPosition(this);
            else if (e is TouchEventArgs)
                return ((TouchEventArgs)e).GetTouchPoint(this).Position;
            else
                return new System.Windows.Point();
        }

        private System.Int64 GetTimestamp(InputEventArgs e)
        {
            return -1;
        }

        private float GetForce(InputEventArgs e)
        {
            if (e is StylusEventArgs)
            {
                var points = ((StylusEventArgs)e).GetStylusPoints(this);
                return ((points.Count > 0) ? points[0].PressureFactor : 0);
            }

            return 0;
        }

        private PointerType GetPointerType(InputEventArgs e)
        {
            if (e is StylusEventArgs)
                return PointerType.PEN;
            else if (e is MouseEventArgs)
                return PointerType.MOUSE;
            else if (e is TouchEventArgs)
                return PointerType.TOUCH;

            return PointerType.CUSTOM_1;
        }

        private int GetPointerId(InputEventArgs e)
        {
            if (e is StylusEventArgs)
                return (int)PointerType.PEN;
            else if (e is MouseEventArgs)
                return (int)PointerType.MOUSE;
            else if (e is TouchEventArgs)
                return (int)PointerType.TOUCH;

            return (int)PointerType.CUSTOM_1;
        }

        private bool HasPart()
        {
            return (_editor != null) && (_editor.Part != null);
        }

        /// <summary>Retranscribe pointer event to editor</summary>
        public void OnPointerDown(InputEventArgs e)
        {
            var p = GetPosition(e);

            _lastPointerPosition = new Graphics.Point((float)p.X, (float)p.Y);
            _onScroll = false;

            try
            {
                _editor.PointerDown((float)p.X, (float)p.Y, GetTimestamp(e), GetForce(e), GetPointerType(e), GetPointerId(e));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Retranscribe pointer event to editor</summary>
        public void OnPointerMove(InputEventArgs e)
        {
            var p = GetPosition(e);
            var pointerType = GetPointerType(e);
            var pointerId = GetPointerId(e);
            var previousPosition = _lastPointerPosition;

            _lastPointerPosition = new Graphics.Point((float)p.X, (float)p.Y);

            var pointerTool = _editor.ToolController.GetToolForType(pointerType);
            if (!_onScroll && (pointerTool == PointerTool.HAND))
            {
                float deltaMin = 3.0f;
                float deltaX = _lastPointerPosition.X - previousPosition.X;
                float deltaY = _lastPointerPosition.Y - previousPosition.Y;

                _onScroll = _editor.IsScrollAllowed() && ((System.Math.Abs(deltaX) > deltaMin) || (System.Math.Abs(deltaY) > deltaMin));

                if (_onScroll)
                {
                    // Entering scrolling mode, cancel previous pointerDown event
                    _editor.PointerCancel(pointerId);
                }
            }

            if (_onScroll)
            {
                // Scroll the view
                float deltaX = _lastPointerPosition.X - previousPosition.X;
                float deltaY = _lastPointerPosition.Y - previousPosition.Y;
                Scroll(-deltaX, -deltaY);
            }
            else if (e is StylusEventArgs)
            {
                var pointList = ((StylusEventArgs)e).GetStylusPoints(this);
                if (pointList.Count > 0)
                {
                    var events = new PointerEvent[pointList.Count];
                    for (int i = 0; i < pointList.Count; ++i)
                    {
                        var p_ = pointList[i].ToPoint();
                        events[i] = new PointerEvent(PointerEventType.MOVE, (float)p_.X, (float)p_.Y, GetTimestamp(e), GetForce(e), pointerType, pointerId);
                    }

                    // Send pointer move events to the editor
                    try
                    {
                        _editor.PointerEvents(events);
                    }
                    catch
                    {
                        // Don't show error for every move event
                    }
                }
            }
            else
            {
                // Send pointer move event to the editor
                try
                {
                    _editor.PointerMove((float)p.X, (float)p.Y, GetTimestamp(e), GetForce(e), pointerType, pointerId);
                }
                catch
                {
                    // Don't show error for every move event
                }
            }
        }

        /// <summary>Retranscribe pointer event to editor</summary>
        public void OnPointerUp(InputEventArgs e)
        {
            var p = GetPosition(e);
            var previousPosition = _lastPointerPosition;

            _lastPointerPosition = new Graphics.Point((float)p.X, (float)p.Y);

            if (_onScroll)
            {
                // Scroll the view
                float deltaX = _lastPointerPosition.X - previousPosition.X;
                float deltaY = _lastPointerPosition.Y - previousPosition.Y;
                Scroll(-deltaX, -deltaY);

                // Exiting scrolling mode
                _onScroll = false;
            }
            else
            {
                // Send pointer up event to the editor
                try
                {
                    _editor.PointerUp((float)p.X, (float)p.Y, GetTimestamp(e), GetForce(e), GetPointerType(e), GetPointerId(e));
                }
                catch
                {
                    // Don't show error for up event
                }
            }
        }

        /// <summary>Retranscribe touch event to editor</summary>
        private void renderLayer_TouchDown(object sender, TouchEventArgs e)
        {
            if (!HasPart())
                return;

            if (_inputType != (PointerType)(-1))
                return;

            if (_inputDeviceId != -1)
                return;

            // Capture the touch device so all touch input is routed to this control.
            e.TouchDevice?.Capture(sender as UIElement, CaptureMode.SubTree);

            _inputType = PointerType.TOUCH;
            _inputDeviceId = e.TouchDevice.Id;

            OnPointerDown(e);

            e.Handled = true;
        }

        /// <summary>Retranscribe touch event to editor</summary>
        private void renderLayer_TouchMove(object sender, TouchEventArgs e)
        {
            if (!HasPart())
                return;

            if (_inputType != PointerType.TOUCH)
                return;

            if (_inputDeviceId != e.TouchDevice.Id)
                return;

            OnPointerMove(e);

            e.Handled = true;
        }

        /// <summary>Retranscribe touch event to editor</summary>
        private void renderLayer_TouchUp(object sender, TouchEventArgs e)
        {
            if (!HasPart())
                return;

            if (_inputType != PointerType.TOUCH)
                return;

            if (_inputDeviceId != e.TouchDevice.Id)
                return;

            OnPointerUp(e);

            _inputType = (PointerType)(-1);
            _inputDeviceId = -1;

            e.TouchDevice?.Capture(null);

            e.Handled = true;
        }

        private bool IsStylusTipDown(StylusEventArgs e)
        {
            // Check that we only have the stylus tip down, without any other button
            bool ok = false;

            for (int i = 0; i < e.StylusDevice.StylusButtons.Count; ++i)
            {
                var button = e.StylusDevice.StylusButtons[i];
                var down = (button.StylusButtonState == StylusButtonState.Down);

                if (button.Guid == StylusPointProperties.TipButton.Id)
                    ok =  down;
                else if (down)
                    return false;
            }

            return ok;
        }

        /// <summary>Retranscribe stylus event to editor</summary>
        private void renderLayer_StylusDown(object sender, StylusDownEventArgs e)
        {
            if (!HasPart())
                return;

            if (_inputType != (PointerType)(-1))
                return;

            if (_inputDeviceId != -1)
                return;

            if (!IsStylusTipDown(e))
                return;

            if ( (e.StylusDevice.TabletDevice != null) && (e.StylusDevice.TabletDevice.Type != TabletDeviceType.Stylus) )
                return; // Ignore if not generated by a stylus

            // Capture the stylus so all stylus input is routed to this control.
            e.StylusDevice?.Capture(sender as UIElement, CaptureMode.SubTree);

            _inputType = PointerType.PEN;
            _inputDeviceId = e.StylusDevice.Id;

            OnPointerDown(e);

            e.Handled = true;
        }

        /// <summary>Retranscribe stylus event to editor</summary>
        private void renderLayer_StylusMove(object sender, StylusEventArgs e)
        {
            if (!HasPart())
                return;

            if (_inputType != PointerType.PEN)
                return;

            if (_inputDeviceId != e.StylusDevice.Id)
                return;

            if (e.InAir)
                return; // Ignore stylus move when the pointing device is up

            if ( (e.StylusDevice.TabletDevice != null) && (e.StylusDevice.TabletDevice.Type != TabletDeviceType.Stylus) )
                return; // Ignore if not generated by a stylus

            OnPointerMove(e);

            e.Handled = true;
        }

        /// <summary>Retranscribe stylus event to editor</summary>
        private void renderLayer_StylusUp(object sender, StylusEventArgs e)
        {
            if (!HasPart())
                return;

            if (_inputType != PointerType.PEN)
                return;

            if ( (e.StylusDevice.TabletDevice != null) && (e.StylusDevice.TabletDevice.Type != TabletDeviceType.Stylus) )
                return; // Ignore if not generated by a stylus

            OnPointerUp(e);

            _inputType = (PointerType)(-1);
            _inputDeviceId = -1;

            e.StylusDevice?.Capture(null);

            e.Handled = true;
        }

        /// <summary>Retranscribe mouse event to editor</summary>
        private void renderLayer_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!HasPart())
                return;

            if (e.StylusDevice != null)
            {
                // Cancel the sampling if the event is sent by a long press with a stylus
                _editor.PointerCancel((int)(_inputType));
                _inputType = (PointerType)(-1);
                _inputDeviceId = -1;
            }
        }

        /// <summary>Retranscribe mouse event to editor</summary>
        private void renderLayer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!HasPart())
                return;

            if (_inputType != (PointerType)(-1))
                return;

            if (_inputDeviceId != -1)
                return;

            if (e.StylusDevice != null)
                return; // Ignore if not generated by a mouse

            // Capture the mouse so all mouse input is routed to this control.
            e.MouseDevice?.Capture(sender as UIElement, CaptureMode.SubTree);

            _inputType = PointerType.MOUSE;
            _inputDeviceId = -1;

            OnPointerDown(e);

            e.Handled = true;
        }

        /// <summary>Retranscribe mouse event to editor</summary>
        private void renderLayer_MouseMove(object sender, MouseEventArgs e)
        {
            if (!HasPart())
                return;

            if (_inputType != PointerType.MOUSE)
                return;

            if (_inputDeviceId != -1)
                return;

            if (e.StylusDevice != null)
                return; // Ignore if not generated by a mouse

            OnPointerMove(e);

            e.Handled = true;
        }

        /// <summary>Retranscribe mouse event to editor</summary>
        private void renderLayer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!HasPart())
                return;

            if (_inputType != PointerType.MOUSE)
                return;

            if (_inputDeviceId != -1)
                return;

            if (e.StylusDevice != null)
                return; // Ignore if not generated by a mouse

            OnPointerUp(e);

            _inputType = (PointerType)(-1);
            _inputDeviceId = -1;

            e.MouseDevice?.Capture(null);

            e.Handled = true;
        }

        /// <summary>Retranscribe mouse event to editor</summary>
        private void renderLayer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!HasPart())
                return;

            if (e.StylusDevice != null)
                return; // Ignore if not generated by a mouse

            const int WHEEL_DELTA = Mouse.MouseWheelDeltaForOneLine;    // 120
            var controlDown = ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control);
            var shiftDown = ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift);
            var wheelDelta = e.Delta / WHEEL_DELTA;

            if (controlDown)
            {
                if (wheelDelta > 0)
                    ZoomIn((uint)wheelDelta);
                else if (wheelDelta < 0)
                    ZoomOut((uint)(-wheelDelta));
            }
            else
            {
                const int SCROLL_SPEED = 100;
                float delta = (float)(-SCROLL_SPEED * wheelDelta);
                float deltaX = shiftDown ? delta : 0.0f;
                float deltaY = shiftDown ? 0.0f : delta;

                Scroll(deltaX, deltaY);
            }

            e.Handled = true;
        }

        public void ResetView(bool forceInvalidate)
        {
            if (!(_editor?.Renderer is Renderer _renderer) || !HasPart())
                return;

            // Reset view offset and scale
            _renderer.ViewScale = 1;
            _renderer.ViewOffset = new MyScript.IInk.Graphics.Point(0, 0);

            // Get new view transform (keep only scale and offset)
            var tr = _renderer.GetViewTransform();
            tr = new Graphics.Transform(tr.XX, tr.YX, 0, tr.XY, tr.YY, 0);

            // Compute new view offset
            var offset = new MyScript.IInk.Graphics.Point(0, 0);

            if (_editor.Part.Type == "Raw Content")
            {
                // Center view on the center of content for "Raw Content" parts
                var contentBox = _editor.GetRootBlock().Box;
                var contentCenter = new MyScript.IInk.Graphics.Point(contentBox.X + (contentBox.Width * 0.5f), contentBox.Y + (contentBox.Height * 0.5f));

                // From model coordinates to view coordinates
                contentCenter = tr.Apply(contentCenter.X, contentCenter.Y);

                var viewCenter = new MyScript.IInk.Graphics.Point(_editor.ViewWidth * 0.5f, _editor.ViewHeight * 0.5f);
                offset.X = contentCenter.X - viewCenter.X;
                offset.Y = contentCenter.Y - viewCenter.Y;
            }
            else
            {
                // Move the origin to the top-left corner of the page for other types of parts
                var boxV = _editor.Part.ViewBox;

                offset.X = boxV.X;
                offset.Y = boxV.Y;

                // From model coordinates to view coordinates
                offset = tr.Apply(offset.X, offset.Y);
            }

            // Set new view offset
            _renderer.ViewOffset = offset;

            if (forceInvalidate)
                Invalidate(_renderer, LayerType.LayerType_ALL);
        }

        public void ZoomIn(uint delta)
        {
            _renderer.Zoom((float)delta * (110.0f / 100.0f));
            Invalidate(_renderer, LayerType.LayerType_ALL);
        }

        public void ZoomOut(uint delta)
        {
            _renderer.Zoom((float)delta * (100.0f / 110.0f));
            Invalidate(_renderer, LayerType.LayerType_ALL);
        }

        private void Scroll(float deltaX, float deltaY)
        {
            var oldOffset = _renderer.ViewOffset;
            var newOffset = new MyScript.IInk.Graphics.Point(oldOffset.X + deltaX, oldOffset.Y + deltaY);

            _renderer.ViewOffset = newOffset;
            Invalidate(_renderer, LayerType.LayerType_ALL);
        }

        public bool SupportsOffscreenRendering()
        {
            return false;
        }

        public uint CreateOffscreenRenderSurface(int width, int height, bool alphaMask)
        {
            throw new NotImplementedException();
        }

        public void ReleaseOffscreenRenderSurface(uint surfaceId)
        {
            throw new NotImplementedException();
        }

        public ICanvas CreateOffscreenRenderCanvas(uint surfaceId)
        {
            throw new NotImplementedException();
        }
    }
}
