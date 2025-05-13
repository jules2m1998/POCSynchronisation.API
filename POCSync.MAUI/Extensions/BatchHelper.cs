namespace POCSync.MAUI.Extensions;

public static class BatchHelper
{
    /// <summary>
    /// Generates batches of a specified size from a collection using yield.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <param name="collection">The collection to batch.</param>
    /// <param name="batchSize">The size of each batch.</param>
    /// <returns>An IEnumerable of List of T, where each inner list is a batch.</returns>
    /// <exception cref="ArgumentNullException">Thrown if collection is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if batchSize is not positive.</exception>
    public static IEnumerable<List<T>> Batch<T>(this IEnumerable<T> collection, int batchSize)
    {
        ArgumentNullException.ThrowIfNull(collection);

        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be a positive number.");
        }

        var currentBatch = new List<T>();
        foreach (var item in collection)
        {
            currentBatch.Add(item);
            if (currentBatch.Count == batchSize)
            {
                yield return currentBatch;
                currentBatch = new List<T>();
            }
        }

        // Return the last partial batch if any
        if (currentBatch.Count > 0)
        {
            yield return currentBatch;
        }
    }
}
