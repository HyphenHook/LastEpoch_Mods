namespace LastEpoch_Hud.Scripts.Core.Diagnostics;

/// <summary>Builds the lines the error log writes.</summary>
public static class ErrorLogText
{
    private const string NoDetails = "(no details)";

    public static string Occurrence(string context, string details)
    {
        return context + ": " + OrPlaceholder(details);
    }

    public static string Summary(string context, string details, int count)
    {
        string times = count == 1 ? "1 time" : count + " times";
        return context + ": " + OrPlaceholder(details) + " (" + times + " since last report)";
    }

    /// <summary>Type name plus the first message line; Il2CppException messages carry the whole Il2Cpp stack.</summary>
    public static string ExceptionSummary(string typeName, string message)
    {
        string firstLine = FirstLine(message);
        if (string.IsNullOrEmpty(typeName))
        {
            return firstLine;
        }

        return firstLine.Length == 0 ? typeName : typeName + ": " + firstLine;
    }

    /// <summary>Full line for the first report (repeats == 0), summary line afterwards.</summary>
    public static string Line(
        string context,
        int repeats,
        string fullDetails,
        string summaryDetails
    )
    {
        if (repeats == 0)
        {
            return Occurrence(context, fullDetails);
        }

        return Summary(context, summaryDetails, repeats);
    }

    private static string OrPlaceholder(string details)
    {
        return string.IsNullOrEmpty(details) ? NoDetails : details;
    }

    private static string FirstLine(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        int end = message.IndexOfAny(new[] { '\r', '\n' });
        return end < 0 ? message : message.Substring(0, end);
    }
}
