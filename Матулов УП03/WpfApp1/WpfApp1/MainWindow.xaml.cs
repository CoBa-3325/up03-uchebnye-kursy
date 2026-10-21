using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Data.SqlClient;
using System.Xml;
using WpfApp1.Services;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        /// <summary>Заглушка пароля в файле App.config, входящем в репозиторий.</summary>
        private const string PLACEHOLDER = "CHANGE_ME";

        private static string _connectionString;

        /// <summary>
        /// Получение строки подключения к базе данных.
        /// Параметры по умолчанию заданы в разделе connectionStrings файла App.config.
        /// Если в них оставлена заглушка CHANGE_ME, реальные параметры читаются
        /// из локального файла App.config.local, который не входит в репозиторий.
        /// </summary>
        private static string ResolveConnectionString()
        {
            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["DefaultConnection"];
            string result = settings == null ? null : settings.ConnectionString;

            if (string.IsNullOrWhiteSpace(result) || result.Contains(PLACEHOLDER))
            {
                string localPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App.config.local");
                if (File.Exists(localPath))
                {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(localPath);
                    XmlNode node = doc.SelectSingleNode(
                        "/configuration/connectionStrings/add[@name='DefaultConnection']/@connectionString");
                    if (node != null) result = node.Value;
                }
            }

            if (string.IsNullOrWhiteSpace(result) || result.Contains(PLACEHOLDER))
            {
                throw new ConfigurationErrorsException(
                    "Строка подключения к базе данных не задана. " +
                    "Укажите её в разделе connectionStrings файла WpfApp1.exe.config " +
                    "или в файле App.config.local рядом с программой.");
            }
            return result;
        }

        /// <summary>
        /// Строка подключения к базе данных SQL Server.
        /// Читается из раздела &lt;connectionStrings&gt; файла конфигурации
        /// (App.config при сборке, WpfApp1.exe.config при запуске),
        /// поэтому при смене сервера пересборка программы не требуется.
        /// </summary>
        public static string ConnectionString
        {
            get
            {
                if (_connectionString == null)
                {
                    _connectionString = ResolveConnectionString();
                }
                return _connectionString;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string login = txtLogin.Text.Trim();
            string password = txtPassword.Password;
            if (login == "" || password == "")
            {
                MessageBox.Show("Заполните логин и пароль");
                return;
            }
            try
            {
                SqlConnection conn = new SqlConnection(ConnectionString);
                conn.Open();
                SqlCommand cmd = new SqlCommand("SELECT [polzovatel_id], [parol], [rol], [aktivnost] FROM [23_09Polzovateli] WHERE [login]=@login", conn);
                cmd.Parameters.AddWithValue("@login", login);
                SqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    int id = Convert.ToInt32(reader["polzovatel_id"]);
                    string parol = reader["parol"].ToString();
                    string rol = reader["rol"].ToString();
                    bool aktivnost = Convert.ToBoolean(reader["aktivnost"]);
                    reader.Close();

                    if (!PasswordHasher.Verify(password, parol))
                    {
                        conn.Close();
                        MessageBox.Show("Неверный логин или пароль");
                        return;
                    }
                    // Ранее пароли хранились в открытом виде - переводим такой пароль в хеш.
                    if (!PasswordHasher.IsHashed(parol))
                    {
                        UpgradePasswordToHash(conn, id, password);
                    }
                    if (!aktivnost)
                    {
                        conn.Close();
                        MessageBox.Show("Учётная запись заблокирована");
                        return;
                    }

                    string fio = login;
                    if (rol == "teacher")
                    {
                        SqlCommand fioCmd = new SqlCommand("SELECT [fio] FROM [23_09Prepodavateli] WHERE [polzovatel_id]=@id", conn);
                        fioCmd.Parameters.AddWithValue("@id", id);
                        object res = fioCmd.ExecuteScalar();
                        if (res != null) fio = res.ToString();
                    }
                    else if (rol == "student")
                    {
                        SqlCommand fioCmd = new SqlCommand("SELECT [fio] FROM [23_09Ucheniki] WHERE [polzovatel_id]=@id", conn);
                        fioCmd.Parameters.AddWithValue("@id", id);
                        object res = fioCmd.ExecuteScalar();
                        if (res != null) fio = res.ToString();
                    }
                    conn.Close();

                    if (rol == "admin")
                    {
                        AdminWindow A = new AdminWindow(id, fio);
                        A.Show();
                        this.Close();
                    }
                    else if (rol == "teacher")
                    {
                        TeacherWindow T = new TeacherWindow(id, fio);
                        T.Show();
                        this.Close();
                    }
                    else if (rol == "student")
                    {
                        StudentWindow S = new StudentWindow(id, fio);
                        S.Show();
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("Неизвестная роль: " + rol);
                    }
                }
                else
                {
                    reader.Close();
                    conn.Close();
                    MessageBox.Show("Неверный логин или пароль");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        /// <summary>
        /// Переводит сохранённый в открытом виде пароль на хешированное хранение.
        /// Ошибка здесь не критична: авторизация к этому моменту уже выполнена.
        /// </summary>
        private static void UpgradePasswordToHash(SqlConnection conn, int userId, string password)
        {
            try
            {
                SqlCommand cmd = new SqlCommand("UPDATE [23_09Polzovateli] SET [parol]=@p WHERE [polzovatel_id]=@id", conn);
                cmd.Parameters.AddWithValue("@p", PasswordHasher.Hash(password));
                cmd.Parameters.AddWithValue("@id", userId);
                cmd.ExecuteNonQuery();
            }
            catch
            {
                // пользователь уже авторизован, сбой обновления на работу не влияет
            }
        }

        private void btnRegistration_Click(object sender, RoutedEventArgs e)
        {
            RegistrationWindow R = new RegistrationWindow();
            R.ShowDialog();
        }
    }
}