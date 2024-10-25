using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Example;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Security.Cryptography.Xml;

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

	static class GcUtils	
	{
		public static void DumpStats()
		{
			// Get the current generation
			int gen0 = GC.CollectionCount(0);
			int gen1 = GC.CollectionCount(1);
			int gen2 = GC.CollectionCount(2);

			// Get the total memory used
			long totalMemory = GC.GetTotalMemory(false);

			Console.WriteLine("GC Statistics:");
			Console.WriteLine($"  Collections (Gen 0): {gen0}");
			Console.WriteLine($"  Collections (Gen 1): {gen1}");
			Console.WriteLine($"  Collections (Gen 2): {gen2}");
			Console.WriteLine($"  Total Memory Used: {totalMemory / (1024)} KB");
			Console.WriteLine();
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

	public class ControlWindow
	{
		struct ButtonData
		{
			public string Label;
			public Action OnClick;
			public Button Button;
		}

		struct EditTextData
		{
			public Action<string> OnEdit;
			public TextBox TextBox;
		}

		private Window m_root;
		private StackPanel m_stackPanel;
		private Dictionary<Button, ButtonData> m_buttons = new();
		private Dictionary<TextBox, EditTextData> m_text = new();

		public ControlWindow(Window root)
		{
			m_root = root;

			m_root.Width = 300;
			m_root.Height = 400;

			var scrollViewer = new ScrollViewer();
			scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
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

			EditTextData data = new();
			data.OnEdit = onEdit;
			data.TextBox = text;

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
		private static u64 XXH3Value<T>(ref T value, u64 seed) where T : struct
		{
			var sizeofT =  Marshal.SizeOf<T>();
			var valueSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, sizeofT));
			return XxHash3.HashToUInt64(valueSpan, (long)seed);
		}

		private static u64 XXH3String(string str, u64 seed = 0)
		{
			return XxHash3.HashToUInt64(MemoryMarshal.AsBytes(str.AsSpan()), (long)seed);
		}

		private void SetElementPositionAndMoveCursor(Layout layout, Control control, ref double curX, ref double curY, double lineWidth)
		{
			Canvas.SetLeft(control, curX);
			Canvas.SetTop(control, curY);
			control.Height = kLineHeight - kMargin;

			if(layout.absolute)
			{
				control.Width = layout.width;
				curX += control.Width + kMargin;
			}
			if(!layout.absolute)
			{
				double remainingWidth = lineWidth - curX - kMargin/2;
				control.Width = remainingWidth * layout.width;
				curX += control.Width + kMargin;
			}

			if(!layout.nextOnSameLine)
			{
				curX = kMargin;
				curY += kLineHeight;
			}
		}

		struct WidgetHolder
		{
			public ContentControl control;
			public u64 lastState;
		}

		private const double kLineHeight = 32;
		private const double kMargin = 4;

		private double m_cursorX;
		private double m_cursorY;

		private ScrollViewer m_scrollView;
		private Canvas m_canvas;
		private Action m_redraw;

		private readonly Dictionary<u64, WidgetHolder> m_data = new();
		private u64 m_rebuildState = 0;
		
		public WidgetLayout(Window root, Action redraw)
		{
			// Scroll Viewer
			m_redraw = redraw;
			m_scrollView = new ScrollViewer();
			m_scrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

			m_canvas = new Canvas
            {
                Background = System.Windows.Media.Brushes.DimGray
            };

			m_scrollView.SizeChanged += (s, e) =>
            {
                m_canvas.Width = m_scrollView.ActualWidth;
                m_canvas.Height = m_scrollView.ActualHeight;
				m_redraw.Invoke();
            };

			m_scrollView.Content = m_canvas;

			// Add the scroll viewer to the window
			root.Content = m_scrollView;

		}
	
		public void Begin()
		{
			m_rebuildState = 0;
			m_cursorX = kMargin;
			m_cursorY = kMargin;
			m_canvas.Children.Clear();
		}

		public void End()
		{

		}

		public Vec3 EditVec3(string label, Vec3 value, Action<Vec3> onEdit, [CallerLineNumber] u64 lineNum = 0, [CallerFilePath] string caller = "")
		{
			// u64 callerHash = XXH3String(caller, lineNum);

			// Button control = GetOrCreateWidget<Button>

			return value;
		}

		public void Button(string label, Action onClicked, Layout layout, [CallerLineNumber] u64 lineNum = 0, [CallerFilePath] string caller = "")
		{
			u64 callerHash = XXH3String(caller, lineNum);
			m_rebuildState = XXH3Value(ref callerHash, m_rebuildState);
			
		
			var button = new Button();
			// WidgetHolder holder = new();
			// holder.control = button;

			button.Content = label;

			SetElementPositionAndMoveCursor(layout, button, ref m_cursorX, ref m_cursorY, m_canvas.Width);
			
			m_canvas.Children.Add(button);
		}

		public void Label(string label, Layout layout)
		{
			var labelWidget = new Label();
			labelWidget.Content = label;

			SetElementPositionAndMoveCursor(layout, labelWidget, ref m_cursorX, ref m_cursorY, m_canvas.Width);
			
			m_canvas.Children.Add(labelWidget);
		}

		private void NextLine()
		{
			m_cursorY += kLineHeight;
			m_cursorX = kMargin;
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

		private WidgetHolder GetOrCreateWidget<T>(u64 hash) where T : ContentControl, new()
		{
			if(m_data.TryGetValue(hash, out WidgetHolder holder))
			{
				return holder;
			}

			WidgetHolder newHolder = new();

			m_data.Add(hash, newHolder);

			return newHolder;
		}

		private u64 ItemHash(string callerAssembly, int callerLine)
		{
			u64 hash = 0;
			return hash;
		}
	}

	public class ExampleApp
	{
		public class UpdateLoop
		{
			public WidgetLayout layout;
			private Entity[] m_entities = new Entity[10];

			public void Redraw()
			{
				GcUtils.DumpStats();

				layout.Begin();
				
				layout.Button("Testbutton line 1", ()=>{}, new());
				layout.Button("Testbutton line 2", ()=>{}, new());
				layout.Button("Testbutton line 3", ()=>{}, new());

				layout.Label("Position", Layout.FixedWidth(60, true));
				layout.Button("TestSameLine 2", ()=>{}, Layout.RelativeWidth(0.5, true));
				layout.Button("TestSameLine 3", ()=>{}, new());

				layout.Button("TestNextLine 1", ()=>{}, new());

				layout.End();
			}
		}

		static Func<int, int, int> GetFunc()
		{
			return (a, b) => a+b+1;
		}



		[DllImport("kernel32.dll")]
    	private static extern bool AllocConsole();

		[STAThread]
		public static void Main()
		{
			AllocConsole();
			var lambda = (int a, int b) => a+b;

			Func<int, int, int> func1 = lambda;
			Func<int, int, int> func2 = GetFunc();

			bool same1 = lambda.Target == func1.Target;
			bool same2 = lambda.Target == func2.Target;

			Application app = new Application();
			Window rootWindow = new();
			UpdateLoop updateLoop = new();
			updateLoop.layout = new(rootWindow, updateLoop.Redraw);

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