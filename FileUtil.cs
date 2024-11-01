using System;
using System.Collections.Generic;
using System.IO;

public class FileCollector
{
    public static List<string> GetAllFilePaths(string rootPath)
    {
        var filePaths = new List<string>();
        var directories = new Stack<string>();

        // Start with the root directory
        directories.Push(rootPath);

        while (directories.Count > 0)
        {
            // Pop a directory from the stack
            var currentDir = directories.Pop();

            try
            {
                // Collect all files in the current directory
                foreach (var file in Directory.EnumerateFiles(currentDir))
                {
                    filePaths.Add(file);
                }

                // Push all subdirectories onto the stack
                foreach (var dir in Directory.EnumerateDirectories(currentDir))
                {
                    directories.Push(dir);
                }
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
