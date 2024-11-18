using System;
using System.Collections.Generic;
using System.IO;

public class FileCollector
{
    public static List<string> GetAllFilePaths(string rootPath)
    {
        var filePaths = new List<string>();
        var directories = new List<string>();

        // Start with the root directory
        directories.Add(rootPath);

        while (directories.Count > 0)
        {
            // Pop a directory from the stack
            int last = directories.Count-1;
            var currentDir = directories[last];
            directories.RemoveAt(last);

            try
            {
                // Collect all files in the current directory
                filePaths.AddRange(Directory.GetFiles(currentDir));
                directories.AddRange(Directory.GetDirectories(currentDir));
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories/files that can't be accessed
                Console.WriteLine($"Access denied to directory: {currentDir}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in directory: {currentDir}. Exception: {ex.Message}");
            }
        }

        return filePaths;
    }

    public static void FilterBySubstring(List<string> inputList, string searchString, ref List<String> results)
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
                if (!item.Contains(term, StringComparison.OrdinalIgnoreCase))
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
