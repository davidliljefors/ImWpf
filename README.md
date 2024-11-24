# ImWpf

If you think xaml is hard and get discombobulated from all the OnPropertyChanged
this is for you

## Example usage (MainWindow.cs)
```cs

public void Redraw()
{
    if (m_hLastSearch != XxHash.StringHash(m_searchPattern))
    {
        FileCollector.FilterBySubstring(m_paths, m_searchPattern, ref m_results);
        m_hLastSearch = XxHash.StringHash(m_searchPattern);
    }
    
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

```
