
using System.Linq;

internal static class ExTools
{
    public static string trimStart(this string target, string pattern)
    {
        if (!string.IsNullOrWhiteSpace(target))
        {
            while(target.Contains(pattern))
            {
                target = target.Substring(target.IndexOf(pattern) + pattern.Length);
            }
        }


        return target;
    }
    public static string trimEnd(this string target, string pattern)
    {
        target = new string(target.Reverse().ToArray());
        pattern = new string(pattern.Reverse().ToArray());
        if (!string.IsNullOrWhiteSpace(target))
        {
            while(target.Contains(pattern))
            {
                target = target.Substring(target.IndexOf(pattern) + pattern.Length);
            }
        }


        return new(target.Reverse().ToArray());
    }
    public static string trim(this string target, string patternStart, string patternEnd)
    {
        return target.trimStart(patternStart).trimEnd(patternEnd);
    }

    public static string trimStart(this string target, params char[] pattern)
    {
        return trimStart(target, new string(pattern));
    }
    public static string trimEnd(this string target, params char[] pattern)
    {
        return trimEnd(target, new string(pattern));
    }
}
