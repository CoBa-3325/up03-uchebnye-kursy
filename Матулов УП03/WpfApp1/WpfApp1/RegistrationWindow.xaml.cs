using System;
using System.Collections.Generic;
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
using WpfApp1.Services;

namespace WpfApp1
{
    public partial class RegistrationWindow : Window
    {
        public RegistrationWindow()
        {
            InitializeComponent();
            RolCMB.SelectedIndex = 0;
            LoadGroups();
        }

        private void LoadGroups()
        {
            GroupCMB.Items.Clear();
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand cmd = new SqlCommand("SELECT [gruppa_id], [nomer_klassa], [bukva_klassa] FROM [23_09Gruppy] ORDER BY [nomer_klassa], [bukva_klassa]", conn);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = reader["nomer_klassa"].ToString() + reader["bukva_klassa"].ToString();
                item.Tag = reader["gruppa_id"].ToString();
                GroupCMB.Items.Add(item);
            }
            reader.Close();
            conn.Close();
            if (GroupCMB.Items.Count > 0) GroupCMB.SelectedIndex = 0;
        }

        private void RolCMB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lblGroup == null || GroupCMB == null) return;
            string tag = ((ComboBoxItem)RolCMB.SelectedItem).Tag.ToString();
            if (tag == "student")
            {
                lblGroup.Visibility = Visibility.Visible;
                GroupCMB.Visibility = Visibility.Visible;
            }
            else
            {
                lblGroup.Visibility = Visibility.Collapsed;
                GroupCMB.Visibility = Visibility.Collapsed;
            }
        }

        private void btnRegistration_Click(object sender, RoutedEventArgs e)
        {
            string login = txtLogin.Text.Trim();
            string password = txtPassword.Password;
            string password2 = txtPassword2.Password;
            string fullName = txtFullName.Text.Trim();
            // Телефон и email пока не сохраняются: в таблице 23_09Polzovateli
            // нет соответствующих колонок. Требуется изменение схемы БД.
            string phone = txtPhone.Text.Trim();
            string email = txtEmail.Text.Trim();

            if (RolCMB.SelectedItem == null) { MessageBox.Show("Выберите роль"); return; }
            string rol = ((ComboBoxItem)RolCMB.SelectedItem).Tag.ToString();

            if (login == "" || password == "" || fullName == "")
            {
                MessageBox.Show("Заполните обязательные поля");
                return;
            }
            if (password.Length < 5)
            {
                MessageBox.Show("Пароль должен быть не менее 5 символов");
                return;
            }
            if (password != password2)
            {
                MessageBox.Show("Пароли не совпадают");
                return;
            }

            // Группа обязательна для ученика. Проверяем ДО создания учётной записи,
            // иначе в базе остаётся пользователь без привязки к группе.
            int gruppaId = 0;
            if (rol == "student")
            {
                if (GroupCMB.SelectedItem == null)
                {
                    MessageBox.Show("Выберите группу");
                    return;
                }
                gruppaId = int.Parse(((ComboBoxItem)GroupCMB.SelectedItem).Tag.ToString());
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(MainWindow.ConnectionString))
                {
                    conn.Open();

                    SqlCommand check = new SqlCommand("SELECT COUNT(*) FROM [23_09Polzovateli] WHERE [login]=@login", conn);
                    check.Parameters.AddWithValue("@login", login);
                    if (Convert.ToInt32(check.ExecuteScalar()) > 0)
                    {
                        MessageBox.Show("Такой логин уже есть");
                        return;
                    }

                    // Регистрация выполняется одной транзакцией: запись пользователя
                    // и связанная с ней запись ученика/преподавателя создаются вместе.
                    SqlTransaction tx = conn.BeginTransaction();
                    try
                    {
                        SqlCommand insUser = new SqlCommand("INSERT INTO [23_09Polzovateli] ([login], [parol], [rol]) VALUES (@l, @p, @r); SELECT SCOPE_IDENTITY();", conn, tx);
                        insUser.Parameters.AddWithValue("@l", login);
                        insUser.Parameters.AddWithValue("@p", PasswordHasher.Hash(password));
                        insUser.Parameters.AddWithValue("@r", rol);
                        int newUserId = Convert.ToInt32(insUser.ExecuteScalar());

                        if (rol == "student")
                        {
                            SqlCommand insUch = new SqlCommand("INSERT INTO [23_09Ucheniki] ([polzovatel_id], [gruppa_id], [fio]) VALUES (@u, @g, @f)", conn, tx);
                            insUch.Parameters.AddWithValue("@u", newUserId);
                            insUch.Parameters.AddWithValue("@g", gruppaId);
                            insUch.Parameters.AddWithValue("@f", fullName);
                            insUch.ExecuteNonQuery();
                        }
                        else if (rol == "teacher")
                        {
                            SqlCommand insPrep = new SqlCommand("INSERT INTO [23_09Prepodavateli] ([polzovatel_id], [fio]) VALUES (@u, @f)", conn, tx);
                            insPrep.Parameters.AddWithValue("@u", newUserId);
                            insPrep.Parameters.AddWithValue("@f", fullName);
                            insPrep.ExecuteNonQuery();
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }

                MessageBox.Show("Регистрация прошла успешно. Теперь войдите под своим логином.");
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}