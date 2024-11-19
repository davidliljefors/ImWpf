using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Security.Cryptography.Xml;
using System.Windows.Shapes;
using Example;

namespace ImWpf;
using u64 = UInt64;

public struct Layout
{
	public double width = 1.0;
	public bool absolute = false;
	public bool nextOnSameLine = false;

    public Layout()
    {
		width = 1.0;
		absolute = false;
		nextOnSameLine = false;
    }

    public static Layout FixedWidth(double width, bool nextOnSameLine)
	{
		Layout size = new();
		size.width = width;
		size.absolute = true;
		size.nextOnSameLine = nextOnSameLine;
		return size;
	}

	public static Layout RelativeWidth(double width, bool nextOnSameLine)
	{
		Layout size = new();
		size.width = width;
		size.absolute = false;
		size.nextOnSameLine = nextOnSameLine;
		return size;
	}
}

public class WidgetLayout
{
    private const bool kEnableCulling = true;
    private const bool kAlwaysShowScrollbar = true;

    enum WidgetType
    {
		None,
		Button,
		Label,
		Textbox,

    }

    private class WidgetHolder
    {
        public FrameworkElement widget;
        public u64 lastState;
        public WidgetType type;
        public Action<string> OnTextEdit;
        public Action OnClicked;
    }


    private const double kLineHeight = 32;
    private const double kMargin = 4;

    private double m_cursorX;
    private double m_cursorY;

    private readonly ScrollViewer m_scrollView;
    public readonly Canvas m_canvas;
    private double m_lastHeight;
    private bool m_editFlag = false;

	//private List<Button> m_pooledButtons = new();
    //private List<Label> m_pooledLabels = new();

    private Dictionary<u64, WidgetHolder> m_lastWidgets = new();
    private Dictionary<u64, WidgetHolder> m_currentWidgets = new();


    private Action m_redraw;
    private ContentControl m_root;
    private u64 m_rebuildState = 0;


    public WidgetLayout(ContentControl root)
    {
        m_redraw = () => { };
        m_root = root;
        // Scroll Viewer
        m_scrollView = new ScrollViewer();
        m_scrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;

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

    private void Render(object _, EventArgs __)
    {
        bool needUpdate = false;

        RenderState state = new RenderState
        {
            ScrollOffsetY = m_scrollView.VerticalOffset,
            WindowSizeX = m_root.ActualWidth,
            WindowSizeY = m_root.ActualHeight,
        };


        if (m_lastState != state || m_editFlag)
        {
            m_lastState = state;
            needUpdate = true;
            m_editFlag = false;
        }

        if(m_canvas.Height < m_scrollView.ActualHeight)
        {
            m_canvas.Height = Math.Max(m_lastHeight, m_scrollView.ActualHeight);
            needUpdate = true;
        }

        if (Math.Abs(m_canvas.Height - m_lastHeight) > 0.1)
        {
            m_canvas.Height = Math.Max(m_lastHeight, m_scrollView.ActualHeight);
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

        

        if (needUpdate)
        {
            m_redraw();
        }
    }

    public void Begin()
    {
        m_canvas.Visibility = Visibility.Hidden;
        m_rebuildState = 0;
        m_cursorX = kMargin;
        m_cursorY = kMargin;
    }

    private static bool CullElement(Rect rect, ScrollViewer sv, Canvas canvas)
    {
        var viewportRect = new Rect(0, 0, sv.ActualWidth, sv.ActualHeight);

        double offsetX = sv.HorizontalOffset;
        double offsetY = sv.VerticalOffset;

        viewportRect.X += offsetX;
        viewportRect.Y += offsetY;

        return !viewportRect.IntersectsWith(rect);
    }

    public void End()
    {
        m_cursorY += kLineHeight;

        m_lastHeight = m_cursorY;

        foreach (var value in m_lastWidgets)
        {
			m_canvas.Children.Remove(value.Value.widget);
        }
        m_lastWidgets.Clear();

        (m_lastWidgets, m_currentWidgets) = (m_currentWidgets, m_lastWidgets);

        m_canvas.Visibility = Visibility.Visible;
    }

    public Vector3 EditVec3(string label, Vector3 value, Action<Vector3> onEdit, [CallerLineNumber] int lineNum = 0, [CallerFilePath] string caller = "")
    {
        u64 callerStateHash = XXH3String(caller, (u64)lineNum);
        u64 hLabel = XXH3Value(callerStateHash, m_rebuildState);
        u64 hText1 = XXH3Value(0xBEEF, hLabel);
        u64 hText2 = XXH3Value(0xDEAD, hText1);
        u64 hText3 = XXH3Value(0xF0F0, hText2);
        m_rebuildState = hText3;

        var (labelVisible, labelRect) = GetWidgetRectAndMoveCursor(Layout.RelativeWidth(0.2, true), ref m_cursorX, ref m_cursorY, m_canvas.Width);

        if (labelVisible == false)
	        HideCulledWidget(FindWidget<Button>(hLabel));
        else
	        ApplyWidgetRect(GetOrCreateWidget<Button>(hLabel, label).Item2.widget, labelRect);

		var (xVisible, xRect) = GetWidgetRectAndMoveCursor(Layout.RelativeWidth(0.333, true), ref m_cursorX, ref m_cursorY, m_canvas.Width);
		if (xVisible == false)
			HideCulledWidget(FindWidget<Button>(hText1));
		else
			ApplyWidgetRect(GetOrCreateWidget<Button>(hText1, value.X.ToString("F2")).Item2.widget, xRect);

		var (yVisible, yRect) = GetWidgetRectAndMoveCursor(Layout.RelativeWidth(0.5, true), ref m_cursorX, ref m_cursorY, m_canvas.Width);
		if (yVisible == false)
			HideCulledWidget(FindWidget<Button>(hText2));
		else
			ApplyWidgetRect(GetOrCreateWidget<Button>(hText2, value.X.ToString("F2")).Item2.widget, yRect);

		var (zVisible, zRect) = GetWidgetRectAndMoveCursor(Layout.RelativeWidth(1.0, false), ref m_cursorX, ref m_cursorY, m_canvas.Width);
		if (zVisible == false)
			HideCulledWidget(FindWidget<Button>(hText3));
		else
			ApplyWidgetRect(GetOrCreateWidget<Button>(hText3, value.X.ToString("F2")).Item2.widget, zRect);

        return value;
    }

    public void Button(string label, Action onClicked, Layout layout, [CallerLineNumber] int lineNum = 0, [CallerFilePath] string caller = "")
    {
        u64 hState = XXH3String(caller, m_rebuildState);
        hState = XXH3String(label, hState);
        hState = XXH3Value((u64)lineNum, hState);

        m_rebuildState = hState;

        var (buttonVisible, buttonRect) = GetWidgetRectAndMoveCursor(layout, ref m_cursorX, ref m_cursorY, m_canvas.Width);
        if (buttonVisible == false)
        {
	        HideCulledWidget(FindWidget<Button>(hState));
	        return;
        }

        var (reused, holder) = GetOrCreateButton(hState, label, onClicked);
		ApplyWidgetRect(holder.widget, buttonRect);
    }

    public void Label(string label, Layout layout, [CallerLineNumber] int lineNum = 0, [CallerFilePath] string caller = "")
    {
	    u64 callerHash = XXH3String(caller, (u64)lineNum);
	    u64 hLabel = XXH3Value(callerHash, m_rebuildState);
	    m_rebuildState = hLabel;

        var (labelVisible, labelRect) = GetWidgetRectAndMoveCursor(layout, ref m_cursorX, ref m_cursorY, m_canvas.Width);
        if (labelVisible == false)
	        HideCulledWidget(FindWidget<Label>(hLabel));
        else
	        ApplyWidgetRect(GetOrCreateWidget<Label>(hLabel, label).Item2.widget, labelRect);
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

        var (labelVisible, labelRect) = GetWidgetRectAndMoveCursor(Layout.RelativeWidth(0.3, true), ref m_cursorX, ref m_cursorY, m_canvas.Width);
        if (labelVisible == false)
			HideCulledWidget(FindWidget<Label>(hLabel));
        else
	        ApplyWidgetRect(GetOrCreateWidget<Label>(hLabel, label).Item2.widget, labelRect);


        var (contentVisible, contentRect) = GetWidgetRectAndMoveCursor(layout, ref m_cursorX, ref m_cursorY, m_canvas.Width);
        if (contentVisible == false)
        {
	        HideCulledWidget(FindWidget<TextBox>(hText1));
	        return;
        }


        var (reusedTextbox, contentHolder) = GetOrCreateTextbox(hText1, content, onEdit);
        ApplyWidgetRect(contentHolder.widget, contentRect);
	}

    private bool FindWidgetHolder<T>(u64 hash, out WidgetHolder holder) where T : FrameworkElement, new()
    {
		if(m_lastWidgets.TryGetValue(hash, out var reusedHolder))
		{
			holder = reusedHolder;
			m_lastWidgets.Remove(hash);
			m_currentWidgets.Add(hash, reusedHolder);
			return true;
		}

		holder = null;
	    return false;
    }

    private T FindWidget<T>(u64 hash) where T : FrameworkElement, new()
    {
	    if (FindWidgetHolder<T>(hash, out var holder))
	    {
		    return (T)holder.widget;
	    }

	    return null;
    }

    private (bool, WidgetHolder) GetOrCreateButton(u64 hash, string content, Action onClicked)
    {
	    if(FindWidgetHolder<Button>(hash, out WidgetHolder holder))
	    {
		    Button old = (Button)holder.widget;
		    if(!string.Equals(old.Content as string, content))
		    {
			    old.Content = content;
				holder.OnClicked = onClicked;
		    }
		    return (true, holder);
	    }

	    Button widget = new Button();
		widget.Click += Button_Click;
	    widget.Content = content;

		var newHolder = new WidgetHolder { lastState = hash, widget = widget, type = WidgetType.Button, OnClicked = onClicked};
	    m_canvas.Children.Add(widget);
		m_currentWidgets.Add(hash, newHolder);

	    return (false, newHolder);
    }

    private (bool, WidgetHolder) GetOrCreateWidget<T>(u64 hash, string content) where T : ContentControl, new()
    {
	    if(FindWidgetHolder<T>(hash, out WidgetHolder holder))
	    {
		    T old = (T)holder.widget;
			if(!Equals(old.Content,content))
			{
				old.Content = content;
			}
		    return (true, holder);
	    }

	    T widget = new T();
	    widget.Content = content;

	    var newHolder = new WidgetHolder { lastState = hash, widget = widget, type = GetTypeFromWidget<T>()};
        m_canvas.Children.Add(widget);
        m_currentWidgets.Add(hash, newHolder);

        return (false, newHolder);
    }

	//private Rectangle GetOrCreateRect(u64 hash, string _)
 //   {
 //       if (m_consumeIndex < m_lastWidgets.Count)
 //       {
 //           if (m_lastWidgets[m_consumeIndex].lastState == hash)
 //           {
 //               var read = m_consumeIndex;
 //               m_consumeIndex++;
 //               m_index++;
 //               return (Rectangle)m_lastWidgets[read].widget;
 //           }
 //       }

 //       Rectangle widget = new Rectangle
 //       {
 //           Fill = Brushes.Blue
 //       };

 //       var addedWidget = new AddedWidget();
 //       addedWidget.index = m_index;
 //       addedWidget.widgetHolder = new WidgetHolder { lastState = hash, widget = widget };
 //       m_index++;

 //       m_addedWidgets.Add(addedWidget);
 //       m_canvas.Children.Add(widget);

 //       return widget;
 //   }

    private (bool, WidgetHolder) GetOrCreateTextbox(u64 hash, string text, Action<string> onEdit)
    {
        if (FindWidgetHolder<TextBox>(hash, out var holder))
        {
	        TextBox old = (TextBox)holder.widget;
	        if(!Equals(old.Text,text))
	        {
		        old.Text = text;
	        }
	        return (true, holder);
        }

        TextBox widget = new TextBox
        {
            Text = text,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        widget.TextChanged += TextBox_TextChanged;

        var newHolder = new WidgetHolder { lastState = hash, widget = widget, OnTextEdit = onEdit };
        m_canvas.Children.Add(widget);
		m_currentWidgets.Add(hash, newHolder);

        return (false, newHolder);
    }

    
    private void Button_Click(object sender, RoutedEventArgs e)
    {
	    foreach(var widget in m_currentWidgets)
	    {
		    if(widget.Value.widget == sender)
		    {
			    widget.Value.OnClicked();
		    }
	    }
	    m_editFlag = true;
    }

    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        foreach(var widget in m_lastWidgets)
        {
	        if(widget.Value.widget == sender)
	        {
                var tb = e.OriginalSource as TextBox;
                widget.Value.OnTextEdit(tb.Text);
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
            if (widget.Visibility == Visibility.Visible)
            {
                widget.Visibility = Visibility.Hidden;
                m_canvas.Children.Remove(widget);
            }
            return;
        }

        if (widget.Visibility == Visibility.Hidden)
        {
            m_canvas.Children.Add(widget);
            widget.Visibility = Visibility.Visible;
        }

        widget.Width = desiredWith;
        Canvas.SetTop(widget, desiredY);
        Canvas.SetLeft(widget, desiredX);
    }

    private (bool visible, Rect rect) GetWidgetRectAndMoveCursor(Layout layout, ref double curX, ref double curY, double lineWidth)
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
		    return (false, rect);
	    }

	    return (true, rect);
	    
    }

    private void HideCulledWidget(FrameworkElement widget)
    {
	    if (widget == null)
		    return;

	    if (widget.Visibility == Visibility.Hidden)
		    return;

	    widget.Visibility = Visibility.Hidden;
	    m_canvas.Children.Remove(widget);
    }

	private void ApplyWidgetRect(FrameworkElement widget, Rect rect)
	{
		if (widget.Visibility == Visibility.Hidden)
		{
			m_canvas.Children.Add(widget);
			widget.Visibility = Visibility.Visible;
		}

		widget.Height = rect.Height-kMargin;
		//if (Math.Abs(rect.Height - widget.Height) > 0.01)

		widget.Width = rect.Width;
		//if (Math.Abs(rect.Width - widget.Width) > 0.01)

		Canvas.SetTop(widget, rect.Y);
		Canvas.SetLeft(widget, rect.X);
	}

	private void PoolWidget(WidgetHolder widget)
	{
		//if (widget.type == WidgetType.Button)
		{
			//widget.widget.Visibility = Visibility.Hidden;
			//m_pooledButtons.Add((Button)widget.widget);
		}
	}

	private object GetPooledWidget<T>()
	{
		return null;
	}

	private static WidgetType GetTypeFromWidget<T>()
	{
		if (typeof(T) == typeof(Button))
			return WidgetType.Button;

		if (typeof(T) == typeof(Label))
			return WidgetType.Label;

		if (typeof(T) == typeof(TextBox))
			return WidgetType.Textbox;

		return WidgetType.None;
	}

    public void MarkEdit()
    {
	    m_editFlag = true;
    }
}
