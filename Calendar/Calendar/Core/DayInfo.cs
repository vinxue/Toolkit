namespace Calendar.Core
{
    public sealed record DayInfo(
        DateOnly Date,
        bool IsToday,
        bool IsCurrentMonth,
        string SubText,
        bool IsHighlight,
        string AutomationName)
    {
        public const int GridRows = 6;
        public const int GridColumns = 7;

        private static readonly string[] WeekdayNames = ["星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"];

        public int Day => Date.Day;

        public static string WeekdayName(DateOnly date) => WeekdayNames[(int)date.DayOfWeek];

        public static DateOnly GridStart(int year, int month)
        {
            var first = new DateOnly(year, month, 1);
            return first.AddDays(-(int)first.DayOfWeek);
        }

        public static DayInfo Create(DateOnly date, int displayMonth, DateOnly today)
        {
            var lunar = ChineseCalendar.GetLunar(date);
            var festival = ChineseCalendar.GetFestival(date, lunar);
            var term = ChineseCalendar.GetSolarTerm(date);

            string subText = festival
                ?? term
                ?? (lunar is null ? "" : lunar.Day == 1 ? lunar.MonthName : lunar.DayName);

            string name = $"{date.Year}年{date.Month}月{date.Day}日 {WeekdayName(date)}";
            if (lunar is not null)
            {
                name += $" 农历{lunar.MonthName}{lunar.DayName}";
            }

            if (festival is not null)
            {
                name += $" {festival}";
            }

            if (term is not null)
            {
                name += $" {term}";
            }

            name += $" 第{WeekNumber.Of(date)}周";

            return new DayInfo(date, date == today, date.Month == displayMonth, subText, festival is not null || term is not null, name);
        }
    }
}
