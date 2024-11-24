using System.Diagnostics;
using System.Windows;
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
			private u64 m_hLastSearch = 0;

			public void CollectPaths()
			{
				m_paths = FileCollector.GetAllFilesInDirectory("C:\\dev");
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

			private static void LaunchVsCode(string path)
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "cmd.exe",
					Arguments = $"/c code \"{path}\"",
					UseShellExecute = false,
					CreateNoWindow = true
				});
			}

			public void Redraw()
			{
				if (m_hLastSearch != XxHash.StringHash(m_searchPattern))
				{
					FileCollector.FilterBySubstring(m_paths, m_searchPattern, ref m_results);
					m_hLastSearch = XxHash.StringHash(m_searchPattern);
				}
				//DrawGcStats();
				m_layout.Label("Quick Open in Vs Code", new Layout());
				m_layout.EditText($"Search {m_paths.Count} results...", m_searchPattern, new Layout(), (s) =>
					m_searchPattern = s, () =>
				{
					if (m_results.Count > 0)
					{
						LaunchVsCode(m_results[0].Item1 + '\\' + m_results[0].Item2);
					}
				});

				foreach (var path in m_results.Take(10000))
				{
					m_layout.Button(path.Item2, () =>
						{
							LaunchVsCode(path.Item1 + '\\' + path.Item2);
						}, new Layout());
				}
			}
		}

		[DllImport("kernel32.dll")]
    	private static extern bool AllocConsole();

		[STAThread]
		public static void Main()
		{
			//AllocConsole();
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
			updateLoop.CollectPaths();
			layout.BindRedrawFunc(updateLoop.Redraw);
			updateLoop.Redraw();
			rootWindow.Width = 600;
			rootWindow.Height = 800;
			app.Run(rootWindow);
		}
	}
}