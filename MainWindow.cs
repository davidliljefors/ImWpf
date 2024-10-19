using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Example;

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
		const int kLineHeight = 16;
		ScrollViewer m_scrollView;
		StackPanel m_stackPanel;
		
		public WidgetLayout(Window root)
		{
			// Scroll Viewer
			m_scrollView = new ScrollViewer();
			m_scrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

			// StackPanel to contain the widgets
			m_stackPanel = new StackPanel();
			m_scrollView.Content = m_stackPanel;

			// Add the scroll viewer to the window
			root.Content = m_scrollView;
		}
	
		public void Begin()
		{

		}

		public void End()
		{

		}

		public void EditVec3(string label, ref Vec3 value, Action<Vec3> onEdit)
		{

		}

		public void Text(string label)
		{

		}

		public void EditText(string label, ref string value, Action<string> onEdit)
		{

		}
	}

	public class ExampleApp
	{
		public struct UpdateLoop
		{
			public WidgetLayout layout;
			private Entity m_dummy;

			public void Update()
			{

				layout.Begin();
				

				layout.End();
			}
		}
		static void TimerCallback(object? state)
		{
			if (state is UpdateLoop updateLoop)
			{
				updateLoop.Update();
			}
		}

		[STAThread]
		public static void Main()
		{
			string nextButton = "";

			Application app = new Application();
			Window rootWindow = new();
			WidgetLayout layout = new(rootWindow);


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
				nextButton = val;
			});

			UpdateLoop updateLoop = new();

			Timer timer = new Timer(new TimerCallback(TimerCallback), updateLoop, 1000, 1000 / 120);

			controlWindow.Show();

			rootWindow.Width = 600;
			rootWindow.Height = 800;
			app.Run(rootWindow);

		}
	}
}