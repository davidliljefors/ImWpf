using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Example;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Security.Cryptography.Xml;
using System.Windows.Shapes;

namespace ImWpf;
using u64 = UInt64;

public class WidgetLayout
{
    private const bool kEnableCulling = true;

    private struct WidgetHolder
    {
        public FrameworkElement widget;
        public u64 lastState;

        public Action<string> OnTextEdit;
    }

    private struct AddedWidget
    {
        public WidgetHolder widgetHolder;
        public int index;
    }

    private const double kLineHeight = 32;
    private const double kMargin = 4;

    private double m_cursorX;
    private double m_cursorY;

    private readonly ScrollViewer m_scrollView;
    private readonly Canvas m_canvas;
    private readonly List<WidgetHolder> m_lastWidgets = new();
    private readonly List<AddedWidget> m_addedWidgets = new();
    private int m_index;
    private int m_consumeIndex;
    private double m_lastHeight;
    private bool m_editFlag = false;

    private Action m_redraw;
    private Window m_root;
    private u64 m_rebuildState = 0;

    public WidgetLayout(Window root)
    {
        m_redraw = () => { };
        m_root = root;
        // Scroll Viewer
        m_scrollView = new ScrollViewer();
        m_scrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

        m_canvas = new Canvas
        {
            Width = 400,
            Height = 400,
        };

        m_scrollView.Content = m_canvas;

        // Add the scroll viewer to the window
        m_root.Content = m_scrollView;

        CompositionTarget.Rendering += Render;
    }

    public void BindRedrawFunc(Action redraw)
    {
        m_redraw = redraw;
    }

    private double m_lastVerticalOffset = 0.0;

    private record struct RenderState
    {
        public double WindowSizeX;
        public double WindowSizeY;
        public double ScrollOffsetY;
    }

    private RenderState m_lastState = new RenderState();

    private void Render(object? _, EventArgs __)
    {
        bool needUpdate = false;

        RenderState state = new RenderState
        {
            ScrollOffsetY = m_scrollView.VerticalOffset,
            WindowSizeX = m_root.Width,
            WindowSizeY = m_root.Height,
        };

        var prevScrollbar = m_scrollView.VerticalScrollBarVisibility;

        if (m_lastState != state || m_editFlag)
        {
            m_lastState = state;
            needUpdate = true;
            m_editFlag = false;
        }

        if (Math.Abs(m_canvas.Height - m_lastHeight) > 0.1)
        {
            if (m_scrollView.ViewportHeight < m_lastHeight)
            {
                m_scrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
            }
            else
            {
                m_scrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            }

            m_canvas.Height = Math.Max(m_lastHeight, m_scrollView.ActualHeight);
            needUpdate = true;
        }

        if (Math.Abs(m_scrollView.VerticalOffset - m_lastVerticalOffset) > 0.1)
        {
            needUpdate = true;
            m_lastVerticalOffset = m_scrollView.VerticalOffset;
        }

        if (Math.Abs(m_canvas.Width - m_scrollView.ViewportWidth) > 0.1)
        {
            m_canvas.Width = m_scrollView.ViewportWidth;
            needUpdate = true;
        }

        if (prevScrollbar != m_scrollView.VerticalScrollBarVisibility)
        {
            if (prevScrollbar == ScrollBarVisibility.Hidden)
            {
                m_canvas.Width -= SystemParameters.VerticalScrollBarWidth;
            }
            if (prevScrollbar == ScrollBarVisibility.Visible)
            {
                m_canvas.Width += SystemParameters.VerticalScrollBarWidth;
            }
        }

        if (needUpdate)
        {
            m_redraw();
        }
    }

    public void Begin()
    {
        culled = 0;
        drawn = 0;
        m_canvas.Visibility = Visibility.Hidden;
        m_rebuildState = 0;
        m_cursorX = kMargin;
        m_cursorY = kMargin;

        m_index = 0;
        m_consumeIndex = 0;
    }

    public int Reused = 0;
    public int Created = 0;

    private int culled = 0;
    private int drawn = 0;

    private static bool CullElement(Rect rect, ScrollViewer sv, Canvas canvas)
    {
        var viewportRect = new Rect(0, 0, sv.ActualWidth, sv.ActualHeight);

        double offsetX = sv.HorizontalOffset;
        double offsetY = sv.VerticalOffset;

        viewportRect.X += offsetX;
        viewportRect.Y += offsetY;

        return !viewportRect.IntersectsWith(rect); ;
    }

    public void End()
    {
        Reused = m_lastWidgets.Count;
        Created = m_addedWidgets.Count;

        m_cursorY += kLineHeight;

        foreach (AddedWidget widget in m_addedWidgets)
        {
            m_lastWidgets.Insert(widget.index, widget.widgetHolder);
        }

        var lastIndex = m_index-1;

        m_lastHeight = m_cursorY;
        m_addedWidgets.Clear();

        for(int i = m_lastWidgets.Count-1; i > lastIndex; --i)
        {
            m_canvas.Children.Remove(m_lastWidgets[i].widget);
            m_lastWidgets.RemoveAt(i);
        }

        if(lastIndex < m_lastWidgets.Count-1)
        {
            m_lastWidgets.RemoveRange(lastIndex, m_lastWidgets.Count-1);
        }

        m_canvas.Visibility = Visibility.Visible;
    }

    public Vec3 EditVec3(string label, Vec3 value, Action<Vec3> onEdit, [CallerLineNumber] int lineNum = 0, [CallerFilePath] string caller = "")
    {
        u64 callerStateHash = XXH3String(caller, (u64)lineNum);
        u64 hLabel = XXH3Value(callerStateHash, m_rebuildState);
        u64 hText1 = XXH3Value(0xBEEF, hLabel);
        u64 hText2 = XXH3Value(0xDEAD, hText1);
        u64 hText3 = XXH3Value(0xF0F0, hText2);
        m_rebuildState = hText3;

        var labelWidget = GetOrCreateWidget<Button>(hLabel, label);
        SetElementPositionAndMoveCursor(Layout.RelativeWidth(0.2, true), labelWidget, ref m_cursorX, ref m_cursorY, m_canvas.Width);

        var editX = GetOrCreateWidget<Button>(hText1, value.x.ToString("F2"));
        SetElementPositionAndMoveCursor(Layout.RelativeWidth(0.333, true), editX, ref m_cursorX, ref m_cursorY, m_canvas.Width);

        var editY = GetOrCreateWidget<Button>(hText2, value.y.ToString("F2"));
        SetElementPositionAndMoveCursor(Layout.RelativeWidth(0.50, true), editY, ref m_cursorX, ref m_cursorY, m_canvas.Width);

        var editZ = GetOrCreateWidget<Button>(hText3, value.z.ToString("F2"));
        SetElementPositionAndMoveCursor(Layout.RelativeWidth(1.0, false), editZ, ref m_cursorX, ref m_cursorY, m_canvas.Width);

        return value;
    }

    public void Button(string label, Action onClicked, Layout layout, [CallerLineNumber] int lineNum = 0, [CallerFilePath] string caller = "")
    {
        u64 callerHash = XXH3String(caller, (u64)lineNum);
        m_rebuildState = XXH3Value(ref callerHash, m_rebuildState);

        Button button = GetOrCreateWidget<Button>(callerHash, label);
        SetElementPositionAndMoveCursor(layout, button, ref m_cursorX, ref m_cursorY, m_canvas.Width);
    }

    public void Label(string label, Layout layout, [CallerLineNumber] int lineNum = 0, [CallerFilePath] string caller = "")
    {
        u64 callerHash = XXH3String(caller, (u64)lineNum);
        m_rebuildState = XXH3Value(ref callerHash, m_rebuildState);

        Label textLabel = GetOrCreateWidget<Label>(callerHash, label);
        if (!string.Equals((string)textLabel.Content, label))
        {
            textLabel.Content = label;
        }
        SetElementPositionAndMoveCursor(layout, textLabel, ref m_cursorX, ref m_cursorY, m_canvas.Width);
    }

    public void Text(string label)
    {

    }

    public void EditText(string label, string content, Layout layout, Action<string> onEdit, [CallerLineNumber] int lineNum = 0, [CallerFilePath] string caller = "")
    {   
        u64 callerStateHash = XXH3String(caller, (u64)lineNum);
        u64 hLabel = XXH3Value(callerStateHash, m_rebuildState);
        u64 hText1 = XXH3Value(0xEAEA, hLabel);
        m_rebuildState = XXH3Value(ref callerStateHash, m_rebuildState);

        var labelWidget = GetOrCreateWidget<Button>(hLabel, label);
        SetElementPositionAndMoveCursor(Layout.RelativeWidth(0.2, true), labelWidget, ref m_cursorX, ref m_cursorY, m_canvas.Width);
        
        TextBox textBox = GetOrCreateTextbox(hText1, content, onEdit);
        SetElementPositionAndMoveCursor(layout, textBox, ref m_cursorX, ref m_cursorY, m_canvas.Width);
    }

    private void OnEdit()
    {

    }

    private T GetOrCreateWidget<T>(u64 hash, string content) where T : ContentControl, new()
    {
        if (m_consumeIndex < m_lastWidgets.Count)
        {
            if (m_lastWidgets[m_consumeIndex].lastState == hash)
            {
                var read = m_consumeIndex;
                m_consumeIndex++;
                m_index++;
                return (T)m_lastWidgets[read].widget;
            }
        }

        T widget = new T
        {
            Content = content
        };

        var addedWidget = new AddedWidget();
        addedWidget.index = m_index;
        addedWidget.widgetHolder = new WidgetHolder { lastState = hash, widget = widget };
        m_index++;

        m_addedWidgets.Add(addedWidget);
        m_canvas.Children.Add(widget);

        return widget;
    }

    private Rectangle GetOrCreateRect(u64 hash, string _)
    {
        if (m_consumeIndex < m_lastWidgets.Count)
        {
            if (m_lastWidgets[m_consumeIndex].lastState == hash)
            {
                var read = m_consumeIndex;
                m_consumeIndex++;
                m_index++;
                return (Rectangle)m_lastWidgets[read].widget;
            }
        }

        Rectangle widget = new Rectangle
        {
            Fill = Brushes.Blue
        };

        var addedWidget = new AddedWidget();
        addedWidget.index = m_index;
        addedWidget.widgetHolder = new WidgetHolder { lastState = hash, widget = widget };
        m_index++;

        m_addedWidgets.Add(addedWidget);
        m_canvas.Children.Add(widget);

        return widget;
    }

    private TextBox GetOrCreateTextbox(u64 hash, string content, Action<string> onEdit)
    {
        if (m_consumeIndex < m_lastWidgets.Count)
        {
            if (m_lastWidgets[m_consumeIndex].lastState == hash)
            {
                var read = m_consumeIndex;
                m_consumeIndex++;
                m_index++;
                return (TextBox)m_lastWidgets[read].widget;
            }
        }

        TextBox widget = new TextBox
        {
            Text = content,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        widget.TextChanged += TextBox_TextChanged;

        var addedWidget = new AddedWidget();
        addedWidget.index = m_index;
        addedWidget.widgetHolder = new WidgetHolder { lastState = hash, widget = widget, OnTextEdit = onEdit };
        m_index++;

        m_addedWidgets.Add(addedWidget);
        m_canvas.Children.Add(widget);

        return widget;
    }

    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        foreach(var widget in m_lastWidgets)
        {
            if(widget.widget == sender)
            {
                var tb = e.OriginalSource as TextBox;
                widget.OnTextEdit(tb.Text);
            }
        }

        m_editFlag = true;
    }

    private void UpdateHashState(u64 nextHash)
    {

    }

    private static u64 XXH3Value<T>(ref T value, u64 seed) where T : struct
    {
        var sizeofT = Marshal.SizeOf<T>();
        var valueSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, sizeofT / 8));
        return XxHash3.HashToUInt64(valueSpan, (long)seed);
    }

    private static u64 XXH3Value<T>(T value, u64 seed) where T : struct
    {
        var sizeofT = Marshal.SizeOf<T>();
        var valueSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, sizeofT / 8));
        return XxHash3.HashToUInt64(valueSpan, (long)seed);
    }

    private static u64 XXH3String(string str, u64 seed = 0)
    {
        return XxHash3.HashToUInt64(MemoryMarshal.AsBytes(str.AsSpan()), (long)seed);
    }

    private void SetElementPositionAndMoveCursor(Layout layout, FrameworkElement widget, ref double curX, ref double curY, double lineWidth)
    {
        double desiredWith = 0;
        double desiredX = curX;
        double desiredY = curY;
        if (layout.absolute)
        {
            desiredWith = layout.width;
        }
        else
        {
            double remainingWidth = Math.Max(lineWidth - curX - kMargin / 2, 0);
            desiredWith = remainingWidth * layout.width;
        }

        curX += desiredWith + kMargin;
        if (widget.Height != Math.Max(kLineHeight - kMargin, 0))
            widget.Height = Math.Max(kLineHeight - kMargin, 0);


        Rect rect = new();

        rect.X = desiredX;
        rect.Y = desiredY;
        rect.Width = desiredWith;
        rect.Height = kLineHeight;

        if (!layout.nextOnSameLine)
        {
            curX = kMargin;
            curY += kLineHeight;
        }

        if (CullElement(rect, m_scrollView, m_canvas) && kEnableCulling)
        {
            ++culled;
            if (widget.Visibility == Visibility.Visible)
            {
                widget.Visibility = Visibility.Hidden;
                m_canvas.Children.Remove(widget);
            }
            return;
        }

        ++drawn;

        if (widget.Visibility == Visibility.Hidden)
        {
            m_canvas.Children.Add(widget);
            widget.Visibility = Visibility.Visible;
        }

        widget.Width = desiredWith;
        Canvas.SetTop(widget, desiredY);
        Canvas.SetLeft(widget, desiredX);
    }
}
