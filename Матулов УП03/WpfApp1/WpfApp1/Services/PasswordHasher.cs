using System;
using System.Security.Cryptography;

namespace WpfApp1.Services
{
    /// <summary>
    /// Хеширование и проверка паролей пользователей.
    /// Формат хранения в БД: PBKDF2$<итерации>$<соль base64>$<хеш base64>
    /// Пароли, сохранённые до перехода на хеширование (в открытом виде),
    /// распознаются по отсутствию префикса "PBKDF2$" и проверяются как есть;
    /// после успешного входа такой пароль автоматически переводится в хеш.
    /// </summary>
    public static class PasswordHasher
    {
        private const string Prefix = "PBKDF2$";
        private const int Iterations = 100000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        /// <summary>Формирует новый хеш пароля со случайной солью.</summary>
        public static string Hash(string password)
        {
            if (password == null) throw new ArgumentNullException("password");

            byte[] salt = new byte[SaltSize];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] hash = Derive(password, salt, Iterations);
            return Prefix + Iterations + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Проверяет соответствие введённого пароля сохранённому знаению.
        /// Поддерживает как хешированные, так и legacy-пароли в открытом виде.
        /// </summary>
        public static bool Verify(string password, string stored)
        {
            if (password == null || string.IsNullOrEmpty(stored)) return false;

            if (!IsHashed(stored))
            {
                // legacy: пароль хранился в открытом виде
                return string.Equals(password, stored, StringComparison.Ordinal);
            }

            string[] parts = stored.Split('$');
            if (parts.Length != 4) return false;

            int iterations;
            if (!int.TryParse(parts[1], out iterations) || iterations <= 0) return false;

            byte[] salt, expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] actual = Derive(password, salt, iterations);
            return FixedTimeEquals(expected, actual);
        }

        /// <summary>Признак того, что значение уже хранится в виде хеша.</summary>
        public static bool IsHashed(string stored)
        {
            return !string.IsNullOrEmpty(stored) && stored.StartsWith(Prefix, StringComparison.Ordinal);
        }

        private static byte[] Derive(string password, byte[] salt, int iterations)
        {
            using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(HashSize);
            }
        }

        /// <summary>Сравнение за постоянное время — защита от подбора по времени ответа.</summary>
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }
}
