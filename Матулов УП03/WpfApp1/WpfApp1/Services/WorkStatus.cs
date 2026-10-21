using System;

namespace WpfApp1.Services
{
    /// <summary>
    /// Статусы выполнения учебных работ.
    /// Значения соответствуют ограничению CHECK таблицы 23_09Vypolneniya:
    /// 'provereno', 'otpravleno', 'naznacheno'.
    /// Пользователю статусы показываются на русском языке (см. ToRu).
    /// </summary>
    public static class WorkStatus
    {
        /// <summary>Работа отправлена учителю, ожидает проверки.</summary>
        public const string Otpravleno = "otpravleno";

        /// <summary>Работа проверена, оценка выставлена.</summary>
        public const string Provereno = "provereno";

        /// <summary>Работа назначена, но ещё не отправлена.</summary>
        public const string Naznacheno = "naznacheno";

        /// <summary>Возвращает русское название статуса для интерфейса.</summary>
        public static string ToRu(string status)
        {
            if (status == null) return "";
            switch (status)
            {
                case Otpravleno: return "На проверку";
                case Provereno: return "Проверено";
                case Naznacheno: return "Назначено";
                default: return status;
            }
        }
    }
}
