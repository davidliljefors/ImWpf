using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;

namespace ImWpf;
using u64 = UInt64;

internal static class XxHash
{
	private static u64 RefHash<T>(ref T value, u64 seed) where T : struct
	{
		var sizeofT = Marshal.SizeOf<T>();
		var valueSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, sizeofT / 8));
		return XxHash3.HashToUInt64(valueSpan, (long)seed);
	}

	public static u64 ValueHash<T>(T value, u64 seed) where T : struct
	{
		var sizeofT = Marshal.SizeOf<T>();
		var valueSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, sizeofT / 8));
		return XxHash3.HashToUInt64(valueSpan, (long)seed);
	}

	public static u64 StringHash(string str, u64 seed = 0)
	{
		return XxHash3.HashToUInt64(MemoryMarshal.AsBytes(str.AsSpan()), (long)seed);
	}
}

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
	enum WidgetType
	{
		None,
		Button,
		Label,
		Textbox,
		TextBlock,
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
	static public bool kEnableCulling = true;
	private const int kPoolSize = 32;

	private double m_cursorX;
	private double m_cursorY;

	private double m_lastHeight;
	private bool m_editFlag = false;

	private Dictionary<u64, WidgetHolder> m_lastWidgets = new();
	private Dictionary<u64, WidgetHolder> m_currentWidgets = new();

	private readonly ScrollViewer m_scrollView;
	private readonly Canvas m_canvas;
	private readonly Stack<Button> m_buttonPool = new Stack<Button>(kPoolSize);
	private readonly Stack<Label> m_labelPool = new Stack<Label>(kPoolSize);
	private readonly Stack<TextBox> m_textBoxPool = new Stack<TextBox>(kPoolSize);
	private readonly Stack<TextBlock> m_textBlockPool = new Stack<TextBlock>(kPoolSize);

	private readonly ContentControl m_root;
	private Action m_redraw;
	private u64 m_stateHash = 0;

	private Button AllocateButton()
	{
		if (m_buttonPool.TryPop(out var button))
		{
			return button;
		}

		Button newButton = new Button();
		newButton.Content = new TextBlock();
		return newButton;
	}


	private Label AllocateLabel()
	{
		if (m_labelPool.TryPop(out var label))
		{
			return label;
		}

		return new Label();
	}

	private TextBox AllocateTextBox()
	{
		if (m_textBoxPool.TryPop(out var textBox))
		{
			return textBox;
		}
		return new TextBox();
	}

	private TextBlock AllocateTextBlock()
	{
		if (m_textBlockPool.TryPop(out var textBlock))
		{
			return textBlock;
		}
		return new TextBlock();
	}

	private void ReturnWidget(WidgetHolder holder)
	{
		switch (holder.type)
		{
			case WidgetType.None:
				break;
			case WidgetType.Button:
				Button button = (Button)holder.widget;
				button.Click -= Button_Click;
				if (m_buttonPool.Count < kPoolSize)
					m_buttonPool.Push(button);

				break;
			case WidgetType.Label:
				if (m_labelPool.Count < kPoolSize)
					m_labelPool.Push((Label)holder.widget);
				break;
			case WidgetType.Textbox:
				TextBox textbox = (TextBox)holder.widget;
				textbox.PreviewKeyDown -= Textbox_PreviewKeyDown;
				textbox.TextChanged -= TextBox_TextChanged;

				if (m_textBoxPool.Count < kPoolSize)
					m_textBoxPool.Push(textbox);
				break;
			case WidgetType.TextBlock:
				TextBlock textBlock = (TextBlock)holder.widget;
				if (m_textBlockPool.Count < kPoolSize)
					m_textBlockPool.Push(textBlock);
				break;
		}
	}

	[Flags]
	public enum Flag
	{
		None = 0,
		TakeFocus = 1
	}

	private Flag m_flags = Flag.None;

	public void PushFocusFlag()
	{
		m_flags |= Flag.TakeFocus;
	}

	public void PopFocusFlag()
	{
		m_flags = m_flags & (~Flag.TakeFocus);
	}

	public WidgetLayout(ContentControl root)
	{
		m_redraw = () => { };
		m_root = root;
		m_scrollView = new ScrollViewer();
		m_scrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
		m_scrollView.PreviewKeyDown += M_scrollView_PreviewKeyDown;
		KeyboardNavigation.SetDirectionalNavigation(m_scrollView, KeyboardNavigationMode.None);
		m_canvas = new Canvas
		{
			Width = 400,
			Height = 400,
		};

		m_scrollView.Content = m_canvas;
		m_root.Content = m_scrollView;

		CompositionTarget.Rendering += Render;
	}

	private void M_scrollView_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		var focused = Keyboard.FocusedElement as UIElement;
		if (e.Key == Key.Up)
		{
			e.Handled = true;
			focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Up));
		}
		else if (e.Key == Key.Down)
		{
			e.Handled = true;
			focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Down));
		}
		else if (e.Key == Key.PageDown || e.Key == Key.PageUp)
		{
			e.Handled = true;
		}

		m_editFlag = true;
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

		if (m_canvas.Height < m_scrollView.ActualHeight)
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
			if (m_root.ActualHeight > 0 && m_root.ActualWidth > 0)
			{
				Begin();
				m_redraw();
				End();
			}
		}
	}

	private Rect m_viewportRect = Rect.Empty;

	public void Begin()
	{
		m_canvas.Visibility = Visibility.Hidden;
		m_stateHash = 0;
		m_cursorX = kMargin;
		m_cursorY = kMargin;

		m_viewportRect = new Rect(0, 0, m_scrollView.ActualWidth, m_scrollView.ActualHeight + kLineHeight * 4);
		double offsetX = m_scrollView.HorizontalOffset;
		double offsetY = m_scrollView.VerticalOffset - kLineHeight * 2;
		m_viewportRect.X += offsetX;
		m_viewportRect.Y += offsetY;
	}

	public void End()
	{
		m_cursorY += kLineHeight;

		m_lastHeight = m_cursorY;

		foreach (var value in m_lastWidgets)
		{
			m_canvas.Children.Remove(value.Value.widget);
			ReturnWidget(value.Value);
		}
		m_lastWidgets.Clear();

		(m_lastWidgets, m_currentWidgets) = (m_currentWidgets, m_lastWidgets);

		m_canvas.Visibility = Visibility.Visible;
	}

	public void Button(string label, Action onClicked, Layout layout, [CallerLineNumber] int lineNum = 0, [CallerFilePath] string caller = "")
	{
		m_stateHash = XxHash.StringHash(caller, m_stateHash);
		m_stateHash = XxHash.ValueHash(m_stateHash, (u64)lineNum);

		var (buttonVisible, buttonRect) = GetWidgetRectAndMoveCursor(layout, ref m_cursorX, ref m_cursorY, m_canvas.Width);
		if (buttonVisible)
		{
			var (reused, holder) = GetOrCreateButton(m_stateHash, label, onClicked);
			ApplyWidgetRect(holder.widget, buttonRect);
		}
	}

	public void TextBlock(string label, Layout layout, [CallerLineNumber] int lineNum = 0, [CallerFilePath] string caller = "")
	{
		m_stateHash = XxHash.StringHash(caller, m_stateHash);
		m_stateHash = XxHash.ValueHash(m_stateHash, (u64)lineNum);

		var (buttonVisible, buttonRect) = GetWidgetRectAndMoveCursor(layout, ref m_cursorX, ref m_cursorY, m_canvas.Width);
		if (buttonVisible)
		{
			var (reused, holder) = GetOrCreateTextBlock(m_stateHash, label);
			ApplyWidgetRect(holder.widget, buttonRect);
		}
	}

	public void Label(string label, Layout layout, [CallerLineNumber] int lineNum = 0, [CallerFilePath] string caller = "")
	{
		m_stateHash = XxHash.StringHash(caller, m_stateHash);
		m_stateHash = XxHash.ValueHash(m_stateHash, (u64)lineNum);

		var (labelVisible, labelRect) = GetWidgetRectAndMoveCursor(layout, ref m_cursorX, ref m_cursorY, m_canvas.Width);
		if (labelVisible)
			ApplyWidgetRect(GetOrCreateWidget<Label>(m_stateHash, label).Item2.widget, labelRect);
	}

	public void EditText(string label, string content, Layout layout, Action<string> onEdit, Action onAccept, int lineNum = 0, [CallerFilePath] string caller = "")
	{
		m_stateHash = XxHash.StringHash(caller, m_stateHash);
		m_stateHash = XxHash.ValueHash(m_stateHash, (u64)lineNum);

		var (labelVisible, labelRect) = GetWidgetRectAndMoveCursor(Layout.RelativeWidth(0.3, true), ref m_cursorX, ref m_cursorY, m_canvas.Width);
		if (labelVisible)
			ApplyWidgetRect(GetOrCreateWidget<Label>(m_stateHash, label).Item2.widget, labelRect);

		m_stateHash = XxHash.ValueHash(m_stateHash, m_stateHash);
		var (contentVisible, contentRect) = GetWidgetRectAndMoveCursor(layout, ref m_cursorX, ref m_cursorY, m_canvas.Width);
		if (contentVisible)
		{
			var (reusedTextbox, contentHolder) = GetOrCreateTextbox(m_stateHash, content, onEdit, onAccept);
			if ((m_flags & Flag.TakeFocus) == Flag.TakeFocus)
			{
				contentHolder.widget.Focus();
			}
			ApplyWidgetRect(contentHolder.widget, contentRect);
		}
	}

	private (bool, WidgetHolder) GetOrCreateButton(u64 hash, string content, Action onClicked)
	{
		if (FindWidgetHolder<Button>(hash, out WidgetHolder holder))
		{
			Button old = (Button)holder.widget;
			if (!string.Equals(old.Content as string, content))
			{
				((TextBlock)old.Content).Text = content;
				holder.OnClicked = onClicked;
			}
			return (true, holder);
		}

		Button widget = AllocateButton();
		widget.Click += Button_Click;
		((TextBlock)widget.Content).Text = content;

		var newHolder = new WidgetHolder { lastState = hash, widget = widget, type = WidgetType.Button, OnClicked = onClicked };
		m_canvas.Children.Add(widget);
		m_currentWidgets.Add(hash, newHolder);

		return (false, newHolder);
	}

	private (bool, WidgetHolder) GetOrCreateTextBlock(u64 hash, string content)
	{
		if (FindWidgetHolder<TextBlock>(hash, out WidgetHolder holder))
		{
			TextBlock old = (TextBlock)holder.widget;
			if (!string.Equals(old.Text as string, content))
			{
				old.Text = content;
			}
			return (true, holder);
		}

		TextBlock widget = AllocateTextBlock();
		widget.Text = content;

		var newHolder = new WidgetHolder { lastState = hash, widget = widget, type = WidgetType.TextBlock };
		m_canvas.Children.Add(widget);
		m_currentWidgets.Add(hash, newHolder);

		return (false, newHolder);
	}

	private (bool, WidgetHolder) GetOrCreateWidget<T>(u64 hash, string content) where T : ContentControl, new()
	{
		if (FindWidgetHolder<T>(hash, out WidgetHolder holder))
		{
			T old = (T)holder.widget;
			if (!Equals(old.Content, content))
			{
				old.Content = content;
			}
			return (true, holder);
		}

		T widget = new T();
		widget.Content = content;

		var newHolder = new WidgetHolder { lastState = hash, widget = widget, type = GetTypeFromWidget<T>() };
		m_canvas.Children.Add(widget);
		m_currentWidgets.Add(hash, newHolder);

		return (false, newHolder);
	}

	private (bool, WidgetHolder) GetOrCreateTextbox(u64 hash, string text, Action<string> onEdit, Action onAccept)
	{
		if (FindWidgetHolder<TextBox>(hash, out var holder))
		{
			TextBox old = (TextBox)holder.widget;
			if (!Equals(old.Text, text))
			{
				old.Text = text;
			}
			return (true, holder);
		}

		TextBox widget = AllocateTextBox();
		widget.Text = text;
		widget.VerticalContentAlignment = VerticalAlignment.Center;

		widget.TextChanged += TextBox_TextChanged;
		widget.PreviewKeyDown += Textbox_PreviewKeyDown;

		var newHolder = new WidgetHolder { lastState = hash, widget = widget, OnTextEdit = onEdit, OnClicked = onAccept };
		m_canvas.Children.Add(widget);
		m_currentWidgets.Add(hash, newHolder);

		return (false, newHolder);
	}

	private void Textbox_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Enter)
		{
			foreach (var widget in m_lastWidgets)
			{
				if (widget.Value.widget == sender)
				{
					widget.Value.OnClicked();
				}
			}
			m_editFlag = true;
		}

		if (e.Key == Key.Up)
		{
			e.Handled = false;
		}
		else if (e.Key == Key.Down)
		{
			e.Handled = false;
		}
	}

	private void Button_Click(object sender, RoutedEventArgs e)
	{
		foreach (var widget in m_lastWidgets)
		{
			if (widget.Value.widget == sender)
			{
				widget.Value.OnClicked();
			}
		}
		m_editFlag = true;
	}

	private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		foreach (var widget in m_lastWidgets)
		{
			if (widget.Value.widget == sender)
			{
				var tb = e.OriginalSource as TextBox;
				widget.Value.OnTextEdit(tb.Text);
			}
		}

		m_editFlag = true;
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

		Rect rect = new Rect();

		rect.X = desiredX;
		rect.Y = desiredY;
		rect.Width = desiredWith;
		rect.Height = kLineHeight;

		if (!layout.nextOnSameLine)
		{
			curX = kMargin;
			curY += kLineHeight;
		}

		if (CullElement(ref rect, ref m_viewportRect) && kEnableCulling)
		{
			return (false, rect);
		}

		return (true, rect);

	}

	private void HideCulledWidget(u64 hash)
	{
		if (m_lastWidgets.TryGetValue(hash, out var reusedHolder))
		{
			reusedHolder.widget.Visibility = Visibility.Hidden;
			m_canvas.Children.Remove(reusedHolder.widget);
		}
	}

	private void ApplyWidgetRect(FrameworkElement widget, Rect rect)
	{
		if (widget.Visibility == Visibility.Hidden)
		{
			m_canvas.Children.Add(widget);
			widget.Visibility = Visibility.Visible;
		}

		widget.Height = rect.Height - kMargin;
		widget.Width = rect.Width;

		Canvas.SetTop(widget, rect.Y);
		Canvas.SetLeft(widget, rect.X);
	}

	private bool FindWidgetHolder<T>(u64 hash, out WidgetHolder holder) where T : FrameworkElement, new()
	{
		if (m_lastWidgets.TryGetValue(hash, out var reusedHolder))
		{
			holder = reusedHolder;
			m_lastWidgets.Remove(hash);
			m_currentWidgets.Add(hash, reusedHolder);
			return true;
		}

		holder = null;
		return false;
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

	private static bool CullElement(ref Rect rect, ref Rect viewportRect)
	{
		return !viewportRect.IntersectsWith(rect);
	}
}
