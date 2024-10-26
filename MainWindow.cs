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

namespace Example
{
	public struct Vec3
	{
		public float x;
		public float y;
		public float z;
	}

	public struct Color
	{
		public float r;
		public float g;
		public float b;
	}

	public struct Entity
	{
		public string name;
		public Color color;
		public Vec3 position;
	}
}

namespace ImWpf
{
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

	public class ManualLayoutCanvas : Canvas
	{
		public Size size;
		protected override Size MeasureOverride(Size availableSize)
		{
			return availableSize;
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			double totalWidth = finalSize.Width;
			double totalHeight = finalSize.Height;

			// Cache values for arrangement
			double childWidth = totalWidth / Children.Count; // Example calculation

			// Calculate arrangement positions
			Rect[] arrangementRectangles = new Rect[Children.Count];

			for (int i = 0; i < Children.Count; i++)
			{
				arrangementRectangles[i] = new Rect(i * childWidth, 0, childWidth, totalHeight);
			}

			// Arrange children in a second pass
			for (int i = 0; i < Children.Count; i++)
			{
				UIElement child = Children[i];
				child.Arrange(arrangementRectangles[i]);
			}

			return finalSize;
		}

		private Rect GetChildBounds(UIElement child)
		{
			// Example: Retrieve manually calculated position and size
			double x = Canvas.GetLeft(child);  // Custom X position
			double y = Canvas.GetTop(child);   // Custom Y position
			double width = 500; // Custom Width (or any set value)
			double height = 500; // Custom Height (or any set value)

			// Return a Rect specifying position and size
			return new Rect(x, y, width, height);
		}
	}

	public class ControlWindow
	{
		private struct ButtonData
		{
			public string Label;
			public Action OnClick;
			public Button Button;
		}

		private struct EditTextData
		{
			public Action<string> OnEdit;
			public TextBox TextBox;
		}

		private readonly Window m_root;
		private readonly StackPanel m_stackPanel;
		private readonly Dictionary<Button, ButtonData> m_buttons = new();
		private readonly Dictionary<TextBox, EditTextData> m_text = new();

		public ControlWindow(Window root)
		{
			m_root = root;

			m_root.Width = 300;
			m_root.Height = 400;

			var scrollViewer = new ScrollViewer
			{
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto
			};

			m_root.Content = scrollViewer;

			// StackPanel to contain the widgets
			m_stackPanel = new StackPanel();
			scrollViewer.Content = m_stackPanel;
		}

		public void AddButton(string label, Action onClick)
		{
			Button button = new();

			ButtonData data = new ButtonData();
			data.Label = label;
			data.OnClick = onClick;
			data.Button = new Button();
			data.Button.Content = label;

			m_buttons.Add(data.Button, data);

			m_stackPanel.Children.Add(data.Button);

			data.Button.Click += Button_Click;
		}

		public void AddText(Action<string> onEdit)
		{
			TextBox text = new();

			EditTextData data = new()
			{
				OnEdit = onEdit,
				TextBox = text
			};

			m_stackPanel.Children.Add(data.TextBox);
			m_text.Add(data.TextBox, data);

			data.TextBox.LostFocus += TextBox_TextChanged;
		}

		private void TextBox_TextChanged(object sender, RoutedEventArgs e)
		{
			TextBox tb = (TextBox)sender;
			m_text[tb].OnEdit(tb.Text);
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			m_buttons[(Button)sender].OnClick();
		}

		public void Show()
		{
			m_root.Show();
		}
	}

	public class WidgetLayout
	{
		private const bool kEnableCulling = true;

		private struct WidgetHolder
		{
			public FrameworkElement widget;
			public u64 lastState;
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
		private readonly Dictionary<u64, WidgetHolder> m_data = new();
		private readonly Dictionary<u64, Control> m_widgets = new();
		private readonly List<WidgetHolder> m_lastWidgets = new();
		private readonly List<AddedWidget> m_addedWidgets = new();
		private int m_index;
		private int m_consumeIndex;
		private double m_lastHeight;
		
		private Action m_redraw;
		private Window m_root;
		private u64 m_rebuildState = 0;
		
		public WidgetLayout(Window root)
		{
			m_redraw = () =>{};
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

		private u64 m_lastSizeHash = 0;

		private void Render(object? _, EventArgs __)
		{
			bool needUpdate = false;
			var prevScrollbar = m_scrollView.VerticalScrollBarVisibility;
			//m_scrollView.Height = m_root.Height;

			if (Math.Abs(m_canvas.Height - m_scrollView.ViewportHeight) > 0.1)
			{
				if (m_scrollView.ViewportHeight < m_lastHeight)
				{
					m_scrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
				}
				else
				{
					m_scrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
				}

				m_canvas.Height = m_lastHeight;
				needUpdate = true;
			}

			if(Math.Abs(m_canvas.Width - m_scrollView.ViewportWidth) > 0.1)
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

			return !viewportRect.IntersectsWith(rect);;
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

			m_lastHeight = m_cursorY;
			m_addedWidgets.Clear();

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
			if(!string.Equals((string)textLabel.Content, label))
			{
				textLabel.Content = label;
			}
			SetElementPositionAndMoveCursor(layout, textLabel, ref m_cursorX, ref m_cursorY, m_canvas.Width);
		}

		public void Text(string label)
		{
			
		}

		public void EditText(string label, ref string value, Action<string> onEdit)
		{

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

		private TextBox GetOrCreateTextbox(u64 hash, string content)
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

			var addedWidget = new AddedWidget();
			addedWidget.index = m_index;
			addedWidget.widgetHolder = new WidgetHolder { lastState = hash, widget = widget };
			m_index++;

			m_addedWidgets.Add(addedWidget);
			m_canvas.Children.Add(widget);

			return widget;
		}

		private void UpdateHashState(u64 nextHash)
		{

		}

		private static u64 XXH3Value<T>(ref T value, u64 seed) where T : struct
		{
			var sizeofT =  Marshal.SizeOf<T>();
			var valueSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, sizeofT / 8));
			return XxHash3.HashToUInt64(valueSpan, (long)seed);
		}

		private static u64 XXH3Value<T>(T value, u64 seed) where T : struct
		{
			var sizeofT =  Marshal.SizeOf<T>();
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
			if(layout.absolute)
			{
				desiredWith = layout.width;
			}
			else
			{
				double remainingWidth = Math.Max(lineWidth - curX - kMargin/2, 0);
				desiredWith = remainingWidth * layout.width;
			}

			curX += desiredWith + kMargin;
			if(widget.Height != Math.Max(kLineHeight - kMargin, 0))
				widget.Height = Math.Max(kLineHeight - kMargin, 0);


			Rect rect = new();

			rect.X = desiredX;
			rect.Y = desiredY;
			rect.Width = desiredWith;
			rect.Height = kLineHeight;

			if(!layout.nextOnSameLine)
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

	public class ExampleApp
	{
		public class UpdateLoop(in WidgetLayout layout)
		{
			private readonly WidgetLayout m_layout = layout;
			private Entity[] m_entities = new Entity[10];
			private Vec3 vec = new();

			public void Redraw()
			{
				int gen0 = GC.CollectionCount(0);
				int gen1 = GC.CollectionCount(1);
				int gen2 = GC.CollectionCount(2);

				// Get the total memory used

				m_layout.Begin();

				long totalMemory = GC.GetTotalMemory(false);

				m_layout.Button("Testbutton line 1", ()=>{}, new Layout());
				m_layout.Button("Testbutton line 2", ()=>{}, new Layout());
				m_layout.Button("Testbutton line 3", () => { }, new Layout());

				//m_layout.Label("GC Statistics:", new Layout());
				//m_layout.Label($"  Collections (Gen 0): {gen0}", new Layout());
				//m_layout.Label($"  Collections (Gen 0): {gen1}", new Layout());
				//m_layout.Label($"  Collections (Gen 0): {gen2}", new Layout());
				//m_layout.Label($"  Total Memory Used: {totalMemory / (1024)} KB", new Layout());

				//m_layout.Label("Position", Layout.FixedWidth(60, true));
				//m_layout.Button("TestSameLine 2", () => { }, Layout.RelativeWidth(0.5, true));
				//m_layout.Button("TestSameLine 3", () => { }, new Layout());
				//m_layout.Button("TestNextLine 1", () => { }, new Layout());

				//m_layout.Label($"Reused {m_layout.Reused} and created {m_layout.Created}", new Layout());

				//vec.x += 1.23f;
				//vec.y += 3.2f;
				//vec.z += 123.0f;

				for (int i = 0; i < 10000; ++i)
				{
					m_layout.EditVec3("Position", vec, null);
				}

				m_layout.End();
			}
		}

		[DllImport("kernel32.dll")]
    	private static extern bool AllocConsole();

		[STAThread]
		public static void Main()
		{
			AllocConsole();
			Application app = new Application();

			var lunaTheme = new ResourceDictionary
			{
				Source = new Uri(
					"pack://application:,,,/PresentationFramework.Luna;component/themes/Luna.NormalColor.xaml",
					UriKind.Absolute)
			};
			app.Resources.MergedDictionaries.Clear();
			app.Resources.MergedDictionaries.Add(lunaTheme);

			Window rootWindow = new();
			WidgetLayout layout = new(rootWindow);
			UpdateLoop updateLoop = new(in layout);
			layout.BindRedrawFunc(updateLoop.Redraw);
			updateLoop.Redraw();

			ControlWindow controlWindow = new(new Window());

			controlWindow.AddButton("Redraw", () =>
			{
				updateLoop.Redraw();
			});

			controlWindow.AddButton("Remove Widget", () =>
			{
				//layout.RemoveWidget(nextButton);
			});

			controlWindow.AddText((string val) =>
			{
				//nextButton = val;
			});

			controlWindow.Show();

			rootWindow.Width = 600;
			rootWindow.Height = 800;
			app.Run(rootWindow);
		}
	}
}