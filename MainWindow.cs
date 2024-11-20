using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices;

namespace ImWpf
{
	using u64 = UInt64;

	public class ExampleApp
	{
		public class UpdateLoop(in WidgetLayout layout)
		{
			private readonly WidgetLayout m_layout = layout;
			private List<(string, string)> m_paths = new();
			private List<(string, string)> m_results = new();
			private string m_searchPattern = "";
			private bool m_bUseLambdas = false;

			public void CollectPaths()
			{
				m_paths = FileCollector.GetFiles2("C:\\dev");
			}

			public void DrawGcStats()
			{
				int gen0 = GC.CollectionCount(0);
				int gen1 = GC.CollectionCount(1);
				int gen2 = GC.CollectionCount(2);
				long totalMemory = GC.GetTotalMemory(false);

				m_layout.Label("GC Statistics:", new Layout());
				m_layout.Label($"  Collections (Gen 0): {gen0}", new Layout());
				m_layout.Label($"  Collections (Gen 1): {gen1}", new Layout());
				m_layout.Label($"  Collections (Gen 2): {gen2}", new Layout());
				m_layout.Label($"  Total Memory Used: {totalMemory / (1024)} KB", new Layout());
			}

			public void Redraw()
			{
				m_layout.Begin();
				DrawGcStats();

				m_layout.EditText("Search...", m_searchPattern, new Layout(), (string s) =>
				{
					m_searchPattern = s;
				}, () => {});

				m_layout.Button($"Lambdas in the buttons [{(m_bUseLambdas ? 'X' : ' ')}]",
					() => { m_bUseLambdas = !m_bUseLambdas; }, new Layout());

				m_layout.Label($"Found {m_results.Count} files", new Layout());

				m_layout.Label(m_searchPattern, new Layout());

				FileCollector.FilterBySubstring(m_paths, m_searchPattern, ref m_results);

				if (m_bUseLambdas)
				{
					foreach (var path in m_results.Take(10000))
					{
						m_layout.Button(path.Item2, () => { Console.WriteLine($"Open file {path.Item1 + '\\' + path.Item2}"); }, new Layout());
					}
				}
				else
				{
					foreach (var path in m_results.Take(10000))
					{
						m_layout.Button(path.Item2, () => { }, new Layout());
					}
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
			rootWindow.Width = 600;
			rootWindow.Height = 800;
			app.Run(rootWindow);
		}
	}
}