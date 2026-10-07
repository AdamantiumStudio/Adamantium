using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Adamantium.Fonts;

public static class TypefaceStore
{
    private static readonly Dictionary<string, Lazy<Typeface>> typefaceMap = new();
    private static readonly HashSet<string> loading = new();
    private static int loadedCount;

    /// <summary>Raised, on the worker that read it, when something read in the background is ready: a typeface asked for
    /// with <see cref="LoadInBackground"/>, or a face's character map (<see cref="FontFace.TryHasCharacter"/>). Text set
    /// without it is laid out again then.</summary>
    public static event Action Loaded;

    /// <summary>How many times <see cref="Loaded"/> has been raised, counted before each: read before asking for fonts,
    /// a different count afterwards means a font arrived meanwhile.</summary>
    public static int LoadedCount => Volatile.Read(ref loadedCount);

    /// <summary>The typeface of a file, or of a system font by name with <paramref name="isSystem"/>; each is parsed
    /// once and shared, whichever thread asks. Waits while it is parsed; files parse in parallel.</summary>
    public static Typeface GetTypeface(string path, bool isSystem = false)
    {
        return Entry(path, isSystem).Value;
    }

    /// <summary>The typeface of a file when it is already parsed; never waits.</summary>
    public static bool TryGetTypeface(string path, out Typeface typeface)
    {
        lock (typefaceMap)
        {
            if (typefaceMap.TryGetValue(path, out var entry) && entry.IsValueCreated)
            {
                typeface = entry.Value;
                return true;
            }
        }

        typeface = null;
        return false;
    }

    /// <summary>Parses the typeface of a file on a worker, once however often it is asked, and raises
    /// <see cref="Loaded"/> when it is ready.</summary>
    public static void LoadInBackground(string path)
    {
        lock (typefaceMap)
        {
            if ((typefaceMap.TryGetValue(path, out var entry) && entry.IsValueCreated) || !loading.Add(path))
            {
                return;
            }
        }

        Task.Run(() =>
        {
            Typeface typeface = null;
            try
            {
                typeface = GetTypeface(path);
            }
            catch (Exception)
            {
            }
            finally
            {
                lock (typefaceMap)
                {
                    loading.Remove(path);
                }
            }

            if (typeface != null)
            {
                NotifyLoaded();
            }
        });
    }

    internal static void NotifyLoaded()
    {
        Interlocked.Increment(ref loadedCount);
        Loaded?.Invoke();
    }

    private static Lazy<Typeface> Entry(string path, bool isSystem)
    {
        lock (typefaceMap)
        {
            if (!typefaceMap.TryGetValue(path, out var entry))
            {
                entry = new Lazy<Typeface>(() => isSystem ? Typeface.LoadSystemFont(path) : Typeface.LoadFont(path),
                    LazyThreadSafetyMode.ExecutionAndPublication);
                typefaceMap[path] = entry;
            }

            return entry;
        }
    }
}
