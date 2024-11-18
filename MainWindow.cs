using System.Windows;
using System.Windows.Controls;
using Example;
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


	public class ExampleApp
	{
		public class UpdateLoop(in WidgetLayout layout)
		{
			private readonly WidgetLayout m_layout = layout;
			private Entity[] m_entities = new Entity[10];
			private Vec3 vec = new();
			private List<string> m_paths = new();
			private List<string> m_results = new();
			private string m_searchPattern = "";

			public void CollectPaths()
			{
				m_paths = FileCollector.GetAllFilePaths("C:\\dev\\");
			}

			public void DrawGcStats()
			{
				int gen0 = GC.CollectionCount(0);
				int gen1 = GC.CollectionCount(1);
				int gen2 = GC.CollectionCount(2);
				long totalMemory = GC.GetTotalMemory(false);

				m_layout.Button("Testbutton line 1", ()=>{}, new Layout());
				m_layout.Button("Testbutton line 2", ()=>{}, new Layout());
				m_layout.Button("Testbutton line 3", () => { }, new Layout());

				m_layout.Label("GC Statistics:", new Layout());
				m_layout.Label($"  Collections (Gen 0): {gen0}", new Layout());
				m_layout.Label($"  Collections (Gen 1): {gen1}", new Layout());
				m_layout.Label($"  Collections (Gen 2): {gen2}", new Layout());
				m_layout.Label($"  Total Memory Used: {totalMemory / (1024)} KB", new Layout());
			}

			public void Redraw()
			{
				m_layout.Begin();

				m_layout.EditText("Search...", m_searchPattern, new Layout(), (string s) => {
					m_searchPattern = s;
				});
				
				m_layout.Label($"Drawn {drawn} Culled {culled}", new Layout());
				
				m_layout.Label(m_searchPattern, new Layout());

				FileCollector.FilterBySubstring(m_paths, m_searchPattern, ref m_results);

				foreach(var path in m_results.Take(10))
				{
					m_layout.Button(path, ()=>{}, new Layout());
				}

				m_layout.End();
				culled = m_layout.culled;
				drawn = m_layout.drawn;
			}
			int culled = 0;
			int drawn = 0;
		}

		[DllImport("kernel32.dll")]
    	private static extern bool AllocConsole();

		[STAThread]
		public static void Main()
		{
			AllocConsole();
			Application app = new Application();

			// var lunaTheme = new ResourceDictionary
			// {
			// 	Source = new Uri(
			// 		"pack://application:,,,/PresentationFramework.Luna;component/themes/Luna.NormalColor.xaml",
			// 		UriKind.Absolute)
			// };
			// app.Resources.MergedDictionaries.Clear();
			// app.Resources.MergedDictionaries.Add(lunaTheme);

			Window rootWindow = new();
			WidgetLayout layout = new(rootWindow);
			UpdateLoop updateLoop = new(in layout);
			updateLoop.CollectPaths();
			layout.BindRedrawFunc(updateLoop.Redraw);
			updateLoop.Redraw();

			//ControlWindow controlWindow = new(new Window());

			// controlWindow.AddButton("Redraw", () =>
			// {
			// 	updateLoop.Redraw();
			// });

			// controlWindow.AddButton("Remove Widget", () =>
			// {
			// 	//layout.RemoveWidget(nextButton);
			// });

			// controlWindow.AddText((string val) =>
			// {
			// 	//nextButton = val;
			// });

			// controlWindow.Show();

			rootWindow.Width = 600;
			rootWindow.Height = 800;
			app.Run(rootWindow);
		}
	}
}