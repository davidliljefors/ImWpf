using System.IO;

namespace ImWpf;

public class FileCollector
{
	public static List<(string, string)> GetAllFilesInDirectory(string directory)
	{
		var folder = new DirectoryInfo(directory);
		var files = from file in folder.EnumerateFiles("*.*", SearchOption.AllDirectories)
					select (file.DirectoryName, file.Name);

		return files.ToList();
	}

	public static void FilterBySubstring(List<(string, string)> inputList, string searchString, ref List<(string, string)> results)
    {
        results.Clear();

        // Split the search string on spaces
        var searchTerms = searchString.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var item in inputList)
        {
            bool isMatch = true;

            // Check if each search term is a substring in the item
            foreach (var term in searchTerms)
            {
                if (!item.Item2.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    isMatch = false;
                    break;
                }
            }

            if (isMatch)
            {
                results.Add(item);
            }
        }
    }
}
