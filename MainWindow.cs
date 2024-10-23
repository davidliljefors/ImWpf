using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Example;
using System.IO.Hashing;
using System.Runtime.InteropServices;

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

		ref struct WidgetHolder
		{
			public ContentControl control;
			public u64 lastState;
		}

		private const int kLineHeight = 16;
		private ScrollViewer m_scrollView;
		private Canvas m_canvas;
		private Action m_redraw;

		private Dictionary<u64, WidgetHolder> m_data;
		private u64 m_rebuildState = 0;
		
		public WidgetLayout(Window root, Action redraw)
		{
			// Scroll Viewer
			m_scrollView = new ScrollViewer();
			m_scrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

			m_canvas = new Canvas();
			m_scrollView.Content = m_canvas;

			// Add the scroll viewer to the window
			root.Content = m_scrollView;

			m_redraw = redraw;
		}
	
		public void Begin()
		{
			m_rebuildState = 0;
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

		public void Button(string label, Action onClicked, [CallerLineNumber] u64 lineNum = 0, [CallerFilePath] string caller = "")
		{
			u64 callerHash = XXH3String(caller, lineNum);
			m_rebuildState = XXH3Value(ref callerHash, m_rebuildState);
			
			if(m_data.ContainsKey(callerHash))
			{
				return;
			}
		
			WidgetHolder holder = new();
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
				layout.Begin();
				
				for (int i = 0; i< m_entities.Length; ++i)
				{
					m_entities[i].position = layout.EditVec3("Entity pos", m_entities[i].position, (_)=>{});
				}

				layout.End();
			}
		}

		static Func<int, int, int> GetFunc()
		{
			return (a, b) => a+b+1;
		}

		[STAThread]
		public static void Main()
		{
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

			controlWindow.AddButton("Add widget", () =>
			{
				//layout.AddWidget(new Widget { Name = nextButton });
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