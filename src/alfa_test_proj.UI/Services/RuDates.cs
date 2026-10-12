namespace alfa_test_proj.UI.Services;

public static class RuDates
{
    private static readonly string[] MonthsGenitive =
    [
        "января", "февраля", "марта", "апреля", "мая", "июня",
        "июля", "августа", "сентября", "октября", "ноября", "декабря",
    ];

    private static readonly string[] Weekdays =
        ["воскресенье", "понедельник", "вторник", "среда", "четверг", "пятница", "суббота"];
    
    public static string DayMonth(DateOnly date) => $"{date.Day} {MonthsGenitive[date.Month - 1]}";
    
    public static string Weekday(DateOnly date) => Weekdays[(int)date.DayOfWeek];
    
    public static string Long(DateOnly date) => $"{DayMonth(date)} {date.Year}";
}
