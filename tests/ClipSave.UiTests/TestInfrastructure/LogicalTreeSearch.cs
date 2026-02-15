using System.Windows;

namespace ClipSave.UiTests;

internal static class LogicalTreeSearch
{
    public static IReadOnlyList<T> FindDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        ArgumentNullException.ThrowIfNull(root);

        var results = new List<T>();
        Traverse(root, results);
        return results;
    }

    private static void Traverse<T>(DependencyObject parent, List<T> results)
        where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is not DependencyObject dependencyObject)
            {
                continue;
            }

            if (dependencyObject is T typed)
            {
                results.Add(typed);
            }

            Traverse(dependencyObject, results);
        }
    }
}
