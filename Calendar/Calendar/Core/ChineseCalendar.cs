using System.Globalization;

namespace Calendar.Core
{
    public sealed record LunarDate(string YearName, string Zodiac, string MonthName, string DayName, int Month, int Day, bool IsLeapMonth);

    // Fully offline: lunar dates from System.Globalization, solar terms computed astronomically.
    public static class ChineseCalendar
    {
        public static readonly DateOnly MinDate = new(1901, 2, 19);
        public static readonly DateOnly MaxDate = new(2101, 1, 28);

        private static readonly ChineseLunisolarCalendar Lunisolar = new();

        private const string Stems = "甲乙丙丁戊己庚辛壬癸";
        private const string Branches = "子丑寅卯辰巳午未申酉戌亥";
        private const string Zodiacs = "鼠牛虎兔龙蛇马羊猴鸡狗猪";
        private static readonly string[] MonthNames = ["正", "二", "三", "四", "五", "六", "七", "八", "九", "十", "冬", "腊"];
        private static readonly string[] DayNames =
        [
            "初一", "初二", "初三", "初四", "初五", "初六", "初七", "初八", "初九", "初十",
            "十一", "十二", "十三", "十四", "十五", "十六", "十七", "十八", "十九", "二十",
            "廿一", "廿二", "廿三", "廿四", "廿五", "廿六", "廿七", "廿八", "廿九", "三十"
        ];

        // Index = ecliptic longitude / 15 degrees.
        private static readonly string[] TermNames =
        [
            "春分", "清明", "谷雨", "立夏", "小满", "芒种", "夏至", "小暑", "大暑", "立秋", "处暑", "白露",
            "秋分", "寒露", "霜降", "立冬", "小雪", "大雪", "冬至", "小寒", "大寒", "立春", "雨水", "惊蛰"
        ];

        private static readonly Dictionary<(int Month, int Day), string> Festivals = new()
        {
            [(1, 1)] = "春节",
            [(1, 15)] = "元宵",
            [(2, 2)] = "龙抬头",
            [(5, 5)] = "端午",
            [(7, 7)] = "七夕",
            [(7, 15)] = "中元",
            [(8, 15)] = "中秋",
            [(9, 9)] = "重阳",
            [(10, 15)] = "下元",
            [(12, 8)] = "腊八",
            [(12, 23)] = "小年",
        };

        private static readonly Dictionary<int, DateOnly[]> TermDateCache = [];

        public static LunarDate? GetLunar(DateOnly date)
        {
            if (date < MinDate || date > MaxDate)
            {
                return null;
            }

            var dt = date.ToDateTime(TimeOnly.MinValue);
            int index = Lunisolar.GetSexagenaryYear(dt) - 1;
            int lunarYear = Lunisolar.GetYear(dt);
            int era = Lunisolar.GetEra(dt);
            int rawMonth = Lunisolar.GetMonth(dt);
            int leapIndex = Lunisolar.GetLeapMonth(lunarYear, era);

            // GetMonth counts the leap month as its own index, so shift months after it back.
            bool isLeap = leapIndex > 0 && rawMonth == leapIndex;
            int month = leapIndex > 0 && rawMonth >= leapIndex ? rawMonth - 1 : rawMonth;
            int day = Lunisolar.GetDayOfMonth(dt);

            return new LunarDate(
                $"{Stems[index % 10]}{Branches[index % 12]}年",
                Zodiacs[index % 12].ToString(),
                (isLeap ? "闰" : "") + MonthNames[month - 1] + "月",
                DayNames[day - 1],
                month,
                day,
                isLeap);
        }

        public static string? GetFestival(DateOnly date, LunarDate? lunar)
        {
            if (lunar is null)
            {
                return null;
            }

            if (!lunar.IsLeapMonth && Festivals.TryGetValue((lunar.Month, lunar.Day), out var name))
            {
                return name;
            }

            // New Year's Eve is the last day of the 12th month, which has 29 or 30 days.
            if (date < MaxDate && GetLunar(date.AddDays(1)) is { Month: 1, Day: 1, IsLeapMonth: false })
            {
                return "除夕";
            }

            return null;
        }

        public static string? GetSolarTerm(DateOnly date)
        {
            int index = Array.IndexOf(TermDates(date.Year), date);
            return index < 0 ? null : TermNames[index];
        }

        // Index = ecliptic longitude / 15 degrees (0 = March equinox, 21 = Lichun).
        public static DateOnly TermDate(int year, int termIndex) => TermDates(year)[termIndex];

        private static DateOnly[] TermDates(int year)
        {
            if (!TermDateCache.TryGetValue(year, out var dates))
            {
                dates = new DateOnly[24];
                for (int i = 0; i < dates.Length; i++)
                {
                    dates[i] = ComputeTermDate(year, i);
                }

                TermDateCache[year] = dates;
            }

            return dates;
        }

        // Date of the term in China Standard Time; Meeus low-precision solar longitude (~15 min).
        private static DateOnly ComputeTermDate(int year, int index)
        {
            double target = index * 15.0;
            double offset = target >= 285 ? target - 360 : target;

            // March equinox of 2000 was JD 2451623.81 (UT).
            double jde = 2451623.81 + (year - 2000) * 365.2422 + offset / 360.0 * 365.2422;
            for (int i = 0; i < 6; i++)
            {
                double diff = ApparentLongitude(jde) - target;
                diff -= 360.0 * Math.Round(diff / 360.0);
                jde -= diff / 0.9856;
            }

            const double deltaT = 69.0;
            double jdUtc = jde - deltaT / 86400.0;
            var beijing = DateTime.FromOADate(jdUtc - 2415018.5).AddHours(8);
            return DateOnly.FromDateTime(beijing);
        }

        private static double ApparentLongitude(double jde)
        {
            double t = (jde - 2451545.0) / 36525.0;
            double l0 = 280.46646 + 36000.76983 * t + 0.0003032 * t * t;
            double m = (357.52911 + 35999.05029 * t - 0.0001537 * t * t) * Math.PI / 180.0;
            double c = (1.914602 - 0.004817 * t - 0.000014 * t * t) * Math.Sin(m)
                     + (0.019993 - 0.000101 * t) * Math.Sin(2 * m)
                     + 0.000289 * Math.Sin(3 * m);
            double omega = (125.04 - 1934.136 * t) * Math.PI / 180.0;
            double lambda = l0 + c - 0.00569 - 0.00478 * Math.Sin(omega);
            return lambda - 360.0 * Math.Floor(lambda / 360.0);
        }
    }
}
