using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

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
		private Label label1;

		public class Widget
		{
			public string Name { get; set; }
		}

		public WidgetLayout(Window root)
		{
			// Scroll Viewer
			var scrollViewer = new ScrollViewer();
			scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

			// StackPanel to contain the widgets
			widgetPanel = new StackPanel();
			scrollViewer.Content = widgetPanel;

			// Add the scroll viewer to the window
			root.Content = scrollViewer;

			// Test: Add a few widgets
			AddWidget(new Widget { Name = "Widget 1" });
			AddWidget(new Widget { Name = "Widget 2" });
			AddWidget(new Widget { Name = "Widget 3" });
		}

		private List<Widget> widgets = new();
		private StackPanel widgetPanel;

		public void AddWidget(Widget widget)
		{
			widgets.Add(widget);
			RedrawWidgets();
		}

		public void RemoveWidget(string name)
		{
			var widget = widgets.Find((w)=>w.Name == name);
			if(widget!= null)
			{
				widgets.Remove(widget);
			}
			RedrawWidgets();
		}

		private void RedrawWidgets()
		{
			widgetPanel.Children.Clear(); // Clear existing widgets

			foreach (var widget in widgets)
			{
				// Create a label to represent the widget content
				var label = new Label
				{
					Content = widget.Name,
					Margin = new Thickness(5)
				};

				// Create a border for the widget
				var border = new Border
				{
					BorderBrush = Brushes.Black,
					BorderThickness = new Thickness(1),
					Margin = new Thickness(5),
					Padding = new Thickness(5),
					Child = label
				};

				// Add the border-wrapped widget to the panel
				widgetPanel.Children.Add(border);
			}
		}

		void button1_Click(object sender, RoutedEventArgs e)
		{
			label1.Content = "Hello WPF!";
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
				layout.AddWidget(new Widget { Name = nextButton });
			});
			controlWindow.AddButton("Remove Widget", () =>
			{
				layout.RemoveWidget(nextButton);
			});
			controlWindow.AddText((string val) =>
			{
				nextButton = val;
			});


			controlWindow.Show();

			rootWindow.Width = 600;
			rootWindow.Height = 800;
			app.Run(rootWindow);

		}
	}
}