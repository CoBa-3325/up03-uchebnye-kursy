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
using System.Xml;
using Microsoft.Win32;
using WpfApp1.Services;

namespace WpfApp1
{
    public partial class AdminWindow : Window
    {
        private int currentUserId;
        private string currentFio;
        private List<UserRow> allUsers = new List<UserRow>();
        private List<UserRow> filteredUsers = new List<UserRow>();

        public AdminWindow(int id, string fio)
        {
            InitializeComponent();
            currentUserId = id;
            currentFio = fio;
            InfoLBL.Text = "Администратор: " + fio;
            LoadUsers();
            ApplyUserFilter();
            LoadGruppy();
            LoadDisc();
            LoadPrepUsers();
            LoadPrep();
            LoadKursCombo();
            LoadKurs();
        }

        private void LoadUsers()
        {
            allUsers.Clear();
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            string sql = "SELECT [polzovatel_id], [login], [rol], [aktivnost], [data_sozdaniya] FROM [23_09Polzovateli] ORDER BY [polzovatel_id]";
            SqlCommand cmd = new SqlCommand(sql, conn);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                UserRow row = new UserRow();
                row.Id = Convert.ToInt32(reader["polzovatel_id"]);
                row.Login = reader["login"].ToString();
                row.Rol = reader["rol"].ToString();
                row.Aktivnost = Convert.ToBoolean(reader["aktivnost"]);
                row.DataSozdaniya = reader["data_sozdaniya"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["data_sozdaniya"]);
                allUsers.Add(row);
            }
            reader.Close();
            conn.Close();
        }

        private void UserFilterChanged(object sender, TextChangedEventArgs e) { ApplyUserFilter(); }

        private void ApplyUserFilter()
        {
            if (allUsers == null) return;
            filteredUsers = allUsers.ToList();
            string q = SearchBOX.Text.Trim().ToLower();
            if (q != "")
                filteredUsers = filteredUsers.Where(x => x.Login.ToLower().Contains(q)).ToList();
            UsersGRD.ItemsSource = null;
            UsersGRD.ItemsSource = filteredUsers;
            InfoLBL.Text = "Администратор: " + currentFio + " | Записей: " + filteredUsers.Count;
            UpdateUserActionButtons();
        }

        // ------------------------------------------------------------------
        // Управление пользователями: блокировка и удаление учётных записей
        // ------------------------------------------------------------------

        private void UsersGRD_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUserActionButtons();
        }

        /// <summary>
        /// Приводит кнопки управления пользователем в состояние,
        /// соответствующее выбранной строке таблицы.
        /// </summary>
        private void UpdateUserActionButtons()
        {
            if (ToggleBlockBTN == null || DelUserBTN == null) return;

            UserRow row = UsersGRD.SelectedItem as UserRow;
            if (row == null)
            {
                ToggleBlockBTN.Content = "Заблокировать";
                ToggleBlockBTN.IsEnabled = false;
                DelUserBTN.IsEnabled = false;
                return;
            }

            ToggleBlockBTN.Content = row.Aktivnost ? "Заблокировать" : "Разблокировать";
            ToggleBlockBTN.IsEnabled = true;
            DelUserBTN.IsEnabled = true;
        }

        /// <summary>
        /// Блокирует или разблокирует учётную запись выбранного пользователя.
        /// Блокировка закрывает пользователю вход в систему
        /// (проверка выполняется в MainWindow при авторизации).
        /// </summary>
        private void ToggleBlockBTN_Click(object sender, RoutedEventArgs e)
        {
            UserRow row = UsersGRD.SelectedItem as UserRow;
            if (row == null) { MessageBox.Show("Выберите пользователя"); return; }

            if (row.Id == currentUserId)
            {
                MessageBox.Show("Нельзя заблокировать собственную учётную запись");
                return;
            }

            bool newState = !row.Aktivnost;

            // Нельзя заблокировать единственного активного администратора,
            // иначе в систему нельзя будет войти.
            if (row.Rol == "admin" && row.Aktivnost && CountOtherActiveAdmins(row.Id) == 0)
            {
                MessageBox.Show("Это единственный активный администратор.\n" +
                                "Заблокировать его нельзя — сначала создайте другого администратора.");
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(MainWindow.ConnectionString))
                {
                    conn.Open();
                    SqlCommand cmd = new SqlCommand("UPDATE [23_09Polzovateli] SET [aktivnost]=@a WHERE [polzovatel_id]=@id", conn);
                    cmd.Parameters.AddWithValue("@a", newState);
                    cmd.Parameters.AddWithValue("@id", row.Id);
                    cmd.ExecuteNonQuery();
                }

                LoadUsers();
                ApplyUserFilter();
                UpdateUserActionButtons();

                MessageBox.Show("Пользователь «" + row.Login + "» " +
                                (newState ? "разблокирован." : "заблокирован."));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        /// <summary>Количество активных администраторов, кроме указанного пользователя.</summary>
        private int CountOtherActiveAdmins(int exceptUserId)
        {
            using (SqlConnection conn = new SqlConnection(MainWindow.ConnectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM [23_09Polzovateli] WHERE [rol]='admin' AND [aktivnost]=1 AND [polzovatel_id]<>@id", conn);
                cmd.Parameters.AddWithValue("@id", exceptUserId);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        /// <summary>
        /// Удаляет учётную запись выбранного пользователя вместе со связанными
        /// данными. Удаление выполняется одной транзакцией в порядке,
        /// соответствующем внешним ключам (FK_Ocenki_Vypolneniya,
        /// FK_Vypolneniya_Ucheniki, FK_ZapisNaKursy_Ucheniki,
        /// FK_Ucheniki_Polzovateli, FK_Prepodavateli_Polzovateli).
        /// </summary>
        private void DelUserBTN_Click(object sender, RoutedEventArgs e)
        {
            UserRow row = UsersGRD.SelectedItem as UserRow;
            if (row == null) { MessageBox.Show("Выберите пользователя"); return; }

            if (row.Id == currentUserId)
            {
                MessageBox.Show("Нельзя удалить собственную учётную запись");
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(MainWindow.ConnectionString))
                {
                    conn.Open();

                    // Преподавателя с курсами удалить нельзя: с курсами связаны
                    // задания, ответы учеников и оценки.
                    object prepIdObj = ExecScalar(conn, null,
                        "SELECT [prepodavatel_id] FROM [23_09Prepodavateli] WHERE [polzovatel_id]=@id", row.Id);
                    if (prepIdObj != null)
                    {
                        int prepId = Convert.ToInt32(prepIdObj);
                        int kursCount = Convert.ToInt32(ExecScalar(conn, null,
                            "SELECT COUNT(*) FROM [23_09Kursy] WHERE [prepodavatel_id]=@id", prepId));
                        if (kursCount > 0)
                        {
                            MessageBox.Show("У преподавателя есть курсы, удалить нельзя.\n" +
                                            "Сначала удалите его курсы или закрепите их за другим преподавателем.");
                            return;
                        }
                    }

                    MessageBoxResult answer = MessageBox.Show(
                        "Удалить пользователя «" + row.Login + "» (" + row.RolRu + ")?\n\n" +
                        "Вместе с учётной записью будут удалены его записи, ответы и оценки.\n" +
                        "Действие необратимо.",
                        "Удаление пользователя", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (answer != MessageBoxResult.Yes) return;

                    SqlTransaction tx = conn.BeginTransaction();
                    try
                    {
                        object uchenikIdObj = ExecScalar(conn, tx,
                            "SELECT [uchenik_id] FROM [23_09Ucheniki] WHERE [polzovatel_id]=@id", row.Id);
                        if (uchenikIdObj != null)
                        {
                            int uchenikId = Convert.ToInt32(uchenikIdObj);
                            ExecNonQuery(conn, tx,
                                "DELETE FROM [23_09Ocenki] WHERE [vypolnenie_id] IN " +
                                "(SELECT [vypolnenie_id] FROM [23_09Vypolneniya] WHERE [uchenik_id]=@id)", uchenikId);
                            ExecNonQuery(conn, tx, "DELETE FROM [23_09Vypolneniya] WHERE [uchenik_id]=@id", uchenikId);
                            ExecNonQuery(conn, tx, "DELETE FROM [23_09ZapisNaKursy] WHERE [uchenik_id]=@id", uchenikId);
                            ExecNonQuery(conn, tx, "DELETE FROM [23_09Ucheniki] WHERE [polzovatel_id]=@id", row.Id);
                        }

                        if (prepIdObj != null)
                        {
                            ExecNonQuery(conn, tx, "DELETE FROM [23_09Prepodavateli] WHERE [polzovatel_id]=@id", row.Id);
                        }

                        ExecNonQuery(conn, tx, "DELETE FROM [23_09Polzovateli] WHERE [polzovatel_id]=@id", row.Id);
                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }

                LoadUsers();
                ApplyUserFilter();
                UpdateUserActionButtons();

                MessageBox.Show("Пользователь «" + row.Login + "» удалён.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка удаления: " + ex.Message);
            }
        }

        /// <summary>Выполняет запрос и возвращает первое значение первой строки.</summary>
        private static object ExecScalar(SqlConnection conn, SqlTransaction tx, string sql, params object[] args)
        {
            using (SqlCommand cmd = CreateCommand(conn, tx, sql, args))
            {
                return cmd.ExecuteScalar();
            }
        }

        /// <summary>Выполняет запрос, изменяющий данные.</summary>
        private static int ExecNonQuery(SqlConnection conn, SqlTransaction tx, string sql, params object[] args)
        {
            using (SqlCommand cmd = CreateCommand(conn, tx, sql, args))
            {
                return cmd.ExecuteNonQuery();
            }
        }

        private static SqlCommand CreateCommand(SqlConnection conn, SqlTransaction tx, string sql, object[] args)
        {
            SqlCommand cmd = (tx == null) ? new SqlCommand(sql, conn) : new SqlCommand(sql, conn, tx);
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    cmd.Parameters.AddWithValue("@p" + i, args[i]);
                }
            }
            return cmd;
        }

        private void LoadGruppy()
        {
            List<GruppaRow> list = new List<GruppaRow>();
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand cmd = new SqlCommand("SELECT [gruppa_id], [nomer_klassa], [bukva_klassa] FROM [23_09Gruppy] ORDER BY [nomer_klassa], [bukva_klassa]", conn);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                GruppaRow row = new GruppaRow();
                row.Id = Convert.ToInt32(reader["gruppa_id"]);
                row.NomerKlassa = Convert.ToInt32(reader["nomer_klassa"]);
                row.BukvaKlassa = reader["bukva_klassa"].ToString();
                list.Add(row);
            }
            reader.Close();
            conn.Close();
            GruppyGRD.ItemsSource = list;
        }

        private void AddGruppaBTN_Click(object sender, RoutedEventArgs e)
        {
            string nomerStr = GruppaNomerBOX.Text.Trim();
            string bukva = GruppaBukvaBOX.Text.Trim();
            int nomer;
            if (!int.TryParse(nomerStr, out nomer) || bukva == "")
            {
                MessageBox.Show("Введите номер класса (число) и букву");
                return;
            }
            // Ограничение CHECK в таблице 23_09Gruppy: 1 <= [nomer_klassa] <= 11
            if (nomer < 1 || nomer > 11)
            {
                MessageBox.Show("Номер класса должен быть от 1 до 11");
                return;
            }
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand cmd = new SqlCommand("INSERT INTO [23_09Gruppy] ([nomer_klassa], [bukva_klassa]) VALUES (@n, @b)", conn);
            cmd.Parameters.AddWithValue("@n", nomer);
            cmd.Parameters.AddWithValue("@b", bukva);
            cmd.ExecuteNonQuery();
            conn.Close();
            GruppaNomerBOX.Text = "";
            GruppaBukvaBOX.Text = "";
            LoadGruppy();
        }

        private void DelGruppaBTN_Click(object sender, RoutedEventArgs e)
        {
            GruppaRow row = GruppyGRD.SelectedItem as GruppaRow;
            if (row == null) { MessageBox.Show("Выберите группу"); return; }
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand check = new SqlCommand("SELECT COUNT(*) FROM [23_09Ucheniki] WHERE [gruppa_id]=@id", conn);
            check.Parameters.AddWithValue("@id", row.Id);
            if (Convert.ToInt32(check.ExecuteScalar()) > 0)
            {
                conn.Close();
                MessageBox.Show("В группе есть ученики, удалить нельзя");
                return;
            }
            SqlCommand cmd = new SqlCommand("DELETE FROM [23_09Gruppy] WHERE [gruppa_id]=@id", conn);
            cmd.Parameters.AddWithValue("@id", row.Id);
            cmd.ExecuteNonQuery();
            conn.Close();
            LoadGruppy();
        }

        private void LoadDisc()
        {
            List<DiscRow> list = new List<DiscRow>();
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand cmd = new SqlCommand("SELECT [disciplina_id], [nazvanie], [opisanie] FROM [23_09Discipliny] ORDER BY [nazvanie]", conn);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                DiscRow row = new DiscRow();
                row.Id = Convert.ToInt32(reader["disciplina_id"]);
                row.Nazvanie = reader["nazvanie"].ToString();
                row.Opisanie = reader["opisanie"] == DBNull.Value ? "" : reader["opisanie"].ToString();
                list.Add(row);
            }
            reader.Close();
            conn.Close();
            DiscGRD.ItemsSource = list;
        }

        private void AddDiscBTN_Click(object sender, RoutedEventArgs e)
        {
            string nazv = DiscNazvBOX.Text.Trim();
            string opis = DiscOpisBOX.Text.Trim();
            if (nazv == "") { MessageBox.Show("Введите название"); return; }
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand cmd = new SqlCommand("INSERT INTO [23_09Discipliny] ([nazvanie], [opisanie]) VALUES (@n, @o)", conn);
            cmd.Parameters.AddWithValue("@n", nazv);
            cmd.Parameters.AddWithValue("@o", opis);
            cmd.ExecuteNonQuery();
            conn.Close();
            DiscNazvBOX.Text = "";
            DiscOpisBOX.Text = "";
            LoadDisc();
            LoadKursCombo();
        }

        private void DelDiscBTN_Click(object sender, RoutedEventArgs e)
        {
            DiscRow row = DiscGRD.SelectedItem as DiscRow;
            if (row == null) { MessageBox.Show("Выберите дисциплину"); return; }
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand check = new SqlCommand("SELECT COUNT(*) FROM [23_09Kursy] WHERE [disciplina_id]=@id", conn);
            check.Parameters.AddWithValue("@id", row.Id);
            if (Convert.ToInt32(check.ExecuteScalar()) > 0)
            {
                conn.Close();
                MessageBox.Show("По дисциплине есть курсы, удалить нельзя");
                return;
            }
            SqlCommand cmd = new SqlCommand("DELETE FROM [23_09Discipliny] WHERE [disciplina_id]=@id", conn);
            cmd.Parameters.AddWithValue("@id", row.Id);
            cmd.ExecuteNonQuery();
            conn.Close();
            LoadDisc();
            LoadKursCombo();
        }

        private void LoadPrepUsers()
        {
            PrepUserCMB.Items.Clear();
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand cmd = new SqlCommand("SELECT [polzovatel_id], [login] FROM [23_09Polzovateli] WHERE [rol]='teacher' AND [polzovatel_id] NOT IN (SELECT [polzovatel_id] FROM [23_09Prepodavateli])", conn);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = reader["login"].ToString();
                item.Tag = reader["polzovatel_id"].ToString();
                PrepUserCMB.Items.Add(item);
            }
            reader.Close();
            conn.Close();
        }

        private void LoadPrep()
        {
            List<PrepRow> list = new List<PrepRow>();
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            string sql = "SELECT p.[prepodavatel_id], p.[polzovatel_id], p.[fio], u.[login] FROM [23_09Prepodavateli] p INNER JOIN [23_09Polzovateli] u ON p.[polzovatel_id]=u.[polzovatel_id] ORDER BY p.[prepodavatel_id]";
            SqlCommand cmd = new SqlCommand(sql, conn);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                PrepRow row = new PrepRow();
                row.Id = Convert.ToInt32(reader["prepodavatel_id"]);
                row.PolzovatelId = Convert.ToInt32(reader["polzovatel_id"]);
                row.Fio = reader["fio"].ToString();
                row.Login = reader["login"].ToString();
                list.Add(row);
            }
            reader.Close();
            conn.Close();
            PrepGRD.ItemsSource = list;
        }

        private void AddPrepBTN_Click(object sender, RoutedEventArgs e)
        {
            if (PrepUserCMB.SelectedItem == null) { MessageBox.Show("Выберите пользователя"); return; }
            string fio = PrepFioBOX.Text.Trim();
            if (fio == "") { MessageBox.Show("Введите ФИО"); return; }
            int polzId = int.Parse(((ComboBoxItem)PrepUserCMB.SelectedItem).Tag.ToString());
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand cmd = new SqlCommand("INSERT INTO [23_09Prepodavateli] ([polzovatel_id], [fio]) VALUES (@p, @f)", conn);
            cmd.Parameters.AddWithValue("@p", polzId);
            cmd.Parameters.AddWithValue("@f", fio);
            cmd.ExecuteNonQuery();
            conn.Close();
            PrepFioBOX.Text = "";
            LoadPrepUsers();
            LoadPrep();
            LoadKursCombo();
        }

        private void DelPrepBTN_Click(object sender, RoutedEventArgs e)
        {
            PrepRow row = PrepGRD.SelectedItem as PrepRow;
            if (row == null) { MessageBox.Show("Выберите преподавателя"); return; }
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand check = new SqlCommand("SELECT COUNT(*) FROM [23_09Kursy] WHERE [prepodavatel_id]=@id", conn);
            check.Parameters.AddWithValue("@id", row.Id);
            if (Convert.ToInt32(check.ExecuteScalar()) > 0)
            {
                conn.Close();
                MessageBox.Show("У преподавателя есть курсы, удалить нельзя");
                return;
            }
            SqlCommand cmd = new SqlCommand("DELETE FROM [23_09Prepodavateli] WHERE [prepodavatel_id]=@id", conn);
            cmd.Parameters.AddWithValue("@id", row.Id);
            cmd.ExecuteNonQuery();
            conn.Close();
            LoadPrepUsers();
            LoadPrep();
            LoadKursCombo();
        }

        private void LoadKursCombo()
        {
            KursDiscCMB.Items.Clear();
            KursPrepCMB.Items.Clear();
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand cmd = new SqlCommand("SELECT [disciplina_id], [nazvanie] FROM [23_09Discipliny] ORDER BY [nazvanie]", conn);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = reader["nazvanie"].ToString();
                item.Tag = reader["disciplina_id"].ToString();
                KursDiscCMB.Items.Add(item);
            }
            reader.Close();
            SqlCommand cmd2 = new SqlCommand("SELECT [prepodavatel_id], [fio] FROM [23_09Prepodavateli] ORDER BY [fio]", conn);
            SqlDataReader reader2 = cmd2.ExecuteReader();
            while (reader2.Read())
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = reader2["fio"].ToString();
                item.Tag = reader2["prepodavatel_id"].ToString();
                KursPrepCMB.Items.Add(item);
            }
            reader2.Close();
            conn.Close();
        }

        private void ChooseImageBTN_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Изображения (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";
            if (dlg.ShowDialog() == true)
            {
                KursImageBOX.Text = dlg.FileName;
            }
        }

        private void LoadKurs()
        {
            List<KursRow> list = new List<KursRow>();
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            string sql = "SELECT k.[kurs_id], k.[nazvanie], d.[nazvanie] AS DiscName, p.[fio] AS PrepName, k.[data_sozdaniya] FROM [23_09Kursy] k LEFT JOIN [23_09Discipliny] d ON k.[disciplina_id]=d.[disciplina_id] LEFT JOIN [23_09Prepodavateli] p ON k.[prepodavatel_id]=p.[prepodavatel_id] ORDER BY k.[kurs_id] DESC";
            SqlCommand cmd = new SqlCommand(sql, conn);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                KursRow row = new KursRow();
                row.Id = Convert.ToInt32(reader["kurs_id"]);
                row.Nazvanie = reader["nazvanie"].ToString();
                row.DiscName = reader["DiscName"] == DBNull.Value ? "" : reader["DiscName"].ToString();
                row.PrepName = reader["PrepName"] == DBNull.Value ? "" : reader["PrepName"].ToString();
                row.DataSozdaniya = reader["data_sozdaniya"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["data_sozdaniya"]);
                list.Add(row);
            }
            reader.Close();
            conn.Close();
            KursGRD.ItemsSource = list;
        }

        private void AddKursBTN_Click(object sender, RoutedEventArgs e)
        {
            string nazv = KursNazvBOX.Text.Trim();
            string opis = KursOpisBOX.Text.Trim();
            string image = KursImageBOX.Text.Trim();
            if (nazv == "") { MessageBox.Show("Введите название курса"); return; }
            if (KursDiscCMB.SelectedItem == null) { MessageBox.Show("Выберите дисциплину"); return; }
            if (KursPrepCMB.SelectedItem == null) { MessageBox.Show("Выберите преподавателя"); return; }
            int discId = int.Parse(((ComboBoxItem)KursDiscCMB.SelectedItem).Tag.ToString());
            int prepId = int.Parse(((ComboBoxItem)KursPrepCMB.SelectedItem).Tag.ToString());
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand cmd = new SqlCommand("INSERT INTO [23_09Kursy] ([disciplina_id], [prepodavatel_id], [nazvanie], [opisanie], [put_k_izobrazheniyu]) VALUES (@d, @p, @n, @o, @i)", conn);
            cmd.Parameters.AddWithValue("@d", discId);
            cmd.Parameters.AddWithValue("@p", prepId);
            cmd.Parameters.AddWithValue("@n", nazv);
            cmd.Parameters.AddWithValue("@o", opis);
            cmd.Parameters.AddWithValue("@i", image);
            cmd.ExecuteNonQuery();
            conn.Close();
            KursNazvBOX.Text = "";
            KursOpisBOX.Text = "";
            KursImageBOX.Text = "";
            LoadKurs();
        }

        private void DelKursBTN_Click(object sender, RoutedEventArgs e)
        {
            KursRow row = KursGRD.SelectedItem as KursRow;
            if (row == null) { MessageBox.Show("Выберите курс"); return; }
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand check = new SqlCommand("SELECT COUNT(*) FROM [23_09ZapisNaKursy] WHERE [kurs_id]=@id", conn);
            check.Parameters.AddWithValue("@id", row.Id);
            if (Convert.ToInt32(check.ExecuteScalar()) > 0)
            {
                conn.Close();
                MessageBox.Show("На курс записаны ученики, удалить нельзя");
                return;
            }
            SqlCommand check2 = new SqlCommand("SELECT COUNT(*) FROM [23_09Zadaniya] WHERE [kurs_id]=@id", conn);
            check2.Parameters.AddWithValue("@id", row.Id);
            if (Convert.ToInt32(check2.ExecuteScalar()) > 0)
            {
                conn.Close();
                MessageBox.Show("В курсе есть задания, удалить нельзя");
                return;
            }
            SqlCommand cmd = new SqlCommand("DELETE FROM [23_09Kursy] WHERE [kurs_id]=@id", conn);
            cmd.Parameters.AddWithValue("@id", row.Id);
            cmd.ExecuteNonQuery();
            conn.Close();
            LoadKurs();
        }

        private void ImportUsersBTN_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "XML файлы (*.xml)|*.xml";
            if (dlg.ShowDialog() != true) return;
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(dlg.FileName);

                int count = 0;
                int skipped = 0;
                string errors = "";

                using (SqlConnection conn = new SqlConnection(MainWindow.ConnectionString))
                {
                    conn.Open();
                    SqlTransaction tx = conn.BeginTransaction();
                    try
                    {
                        int index = 0;
                        foreach (XmlNode node in doc.SelectNodes("//User"))
                        {
                            index++;
                            // Точка сохранения позволяет откатить только текущую запись,
                            // не прерывая импорт остальных.
                            string savepoint = "sp_user_" + index;
                            using (SqlCommand spCmd = new SqlCommand("SAVE TRAN " + savepoint, conn, tx))
                            {
                                spCmd.ExecuteNonQuery();
                            }

                            string login = node["login"] != null ? node["login"].InnerText.Trim() : "";
                            try
                            {
                                string error = ImportUserRecord(conn, tx, node);
                                if (error == null) count++;
                                else { skipped++; errors += login + " - " + error + "; "; }
                            }
                            catch (Exception ex)
                            {
                                using (SqlCommand rb = new SqlCommand("ROLLBACK TRAN " + savepoint, conn, tx))
                                {
                                    rb.ExecuteNonQuery();
                                }
                                skipped++;
                                errors += login + " - " + ex.Message + "; ";
                            }
                        }
                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }

                ImportUsersInfo.Text = "Импортировано пользователей: " + count +
                                       (skipped > 0 ? ", пропущено: " + skipped : "");
                if (errors != "")
                {
                    MessageBox.Show("Импорт завершён с ошибками:\n\n" + errors);
                }

                LoadUsers();
                ApplyUserFilter();
                LoadPrepUsers();
                LoadPrep();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка импорта: " + ex.Message); }
        }

        /// <summary>
        /// Добавляет одного пользователя из XML-файла.
        /// Возвращает null при успехе либо описание причины пропуска записи.
        /// </summary>
        private string ImportUserRecord(SqlConnection conn, SqlTransaction tx, XmlNode node)
        {
            string login = node["login"] != null ? node["login"].InnerText.Trim() : "";
            string parol = node["parol"] != null ? node["parol"].InnerText : "";
            string rol = node["rol"] != null ? node["rol"].InnerText.Trim().ToLower() : "student";
            string fio = node["fio"] != null ? node["fio"].InnerText.Trim() : "";
            string gruppaId = node["gruppa_id"] != null ? node["gruppa_id"].InnerText.Trim() : "";

            if (login == "" || parol == "") return "пустой логин или пароль";
            if (rol != "admin" && rol != "teacher" && rol != "student")
                return "неизвестная роль '" + rol + "'";
            if (fio == "") fio = login;

            int existing = Convert.ToInt32(ExecScalar(conn, tx,
                "SELECT COUNT(*) FROM [23_09Polzovateli] WHERE [login]=@l", login));
            if (existing > 0) return "логин уже существует";

            int gruppaIdValue = 0;
            if (rol == "student")
            {
                if (gruppaId == "") return "не указана группа";
                if (!int.TryParse(gruppaId, out gruppaIdValue)) return "некорректный идентификатор группы";

                int gruppCount = Convert.ToInt32(ExecScalar(conn, tx,
                    "SELECT COUNT(*) FROM [23_09Gruppy] WHERE [gruppa_id]=@g", gruppaIdValue));
                if (gruppCount == 0) return "группа с идентификатором " + gruppaIdValue + " не найдена";
            }

            int newId = Convert.ToInt32(ExecScalar(conn, tx,
                "INSERT INTO [23_09Polzovateli] ([login], [parol], [rol]) VALUES (@l, @p, @r); SELECT SCOPE_IDENTITY();",
                login, PasswordHasher.Hash(parol), rol));

            if (rol == "student")
            {
                ExecNonQuery(conn, tx,
                    "INSERT INTO [23_09Ucheniki] ([polzovatel_id], [gruppa_id], [fio]) VALUES (@u, @g, @f)",
                    newId, gruppaIdValue, fio);
            }
            else if (rol == "teacher")
            {
                ExecNonQuery(conn, tx,
                    "INSERT INTO [23_09Prepodavateli] ([polzovatel_id], [fio]) VALUES (@u, @f)",
                    newId, fio);
            }
            return null;
        }

        private void ImportCoursesBTN_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "XML файлы (*.xml)|*.xml";
            if (dlg.ShowDialog() != true) return;
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(dlg.FileName);

                int count = 0;
                int skipped = 0;
                string errors = "";

                using (SqlConnection conn = new SqlConnection(MainWindow.ConnectionString))
                {
                    conn.Open();
                    SqlTransaction tx = conn.BeginTransaction();
                    try
                    {
                        int index = 0;
                        foreach (XmlNode node in doc.SelectNodes("//Course"))
                        {
                            index++;
                            string savepoint = "sp_course_" + index;
                            using (SqlCommand spCmd = new SqlCommand("SAVE TRAN " + savepoint, conn, tx))
                            {
                                spCmd.ExecuteNonQuery();
                            }

                            string nazv = node["nazvanie"] != null ? node["nazvanie"].InnerText.Trim() : "";
                            try
                            {
                                string error = ImportCourseRecord(conn, tx, node);
                                if (error == null) count++;
                                else { skipped++; errors += nazv + " - " + error + "; "; }
                            }
                            catch (Exception ex)
                            {
                                using (SqlCommand rb = new SqlCommand("ROLLBACK TRAN " + savepoint, conn, tx))
                                {
                                    rb.ExecuteNonQuery();
                                }
                                skipped++;
                                errors += nazv + " - " + ex.Message + "; ";
                            }
                        }
                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }

                ImportCoursesInfo.Text = "Импортировано курсов: " + count +
                                         (skipped > 0 ? ", пропущено: " + skipped : "");
                if (errors != "")
                {
                    MessageBox.Show("Импорт завершён с ошибками:\n\n" + errors);
                }

                LoadKurs();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка импорта: " + ex.Message); }
        }

        /// <summary>
        /// Добавляет один курс из XML-файла.
        /// Возвращает null при успехе либо описание причины пропуска записи.
        /// </summary>
        private string ImportCourseRecord(SqlConnection conn, SqlTransaction tx, XmlNode node)
        {
            string nazv = node["nazvanie"] != null ? node["nazvanie"].InnerText.Trim() : "";
            string discIdStr = node["disciplina_id"] != null ? node["disciplina_id"].InnerText.Trim() : "";
            string prepIdStr = node["prepodavatel_id"] != null ? node["prepodavatel_id"].InnerText.Trim() : "";
            string opis = node["opisanie"] != null ? node["opisanie"].InnerText.Trim() : "";

            if (nazv == "") return "не указано название";

            int discId, prepId;
            if (!int.TryParse(discIdStr, out discId)) return "некорректный идентификатор дисциплины";
            if (!int.TryParse(prepIdStr, out prepId)) return "некорректный идентификатор преподавателя";

            int discCount = Convert.ToInt32(ExecScalar(conn, tx,
                "SELECT COUNT(*) FROM [23_09Discipliny] WHERE [disciplina_id]=@d", discId));
            if (discCount == 0) return "дисциплина с идентификатором " + discId + " не найдена";

            int prepCount = Convert.ToInt32(ExecScalar(conn, tx,
                "SELECT COUNT(*) FROM [23_09Prepodavateli] WHERE [prepodavatel_id]=@p", prepId));
            if (prepCount == 0) return "преподаватель с идентификатором " + prepId + " не найден";

            ExecNonQuery(conn, tx,
                "INSERT INTO [23_09Kursy] ([disciplina_id], [prepodavatel_id], [nazvanie], [opisanie]) VALUES (@d, @p, @n, @o)",
                discId, prepId, nazv, opis);
            return null;
        }

        private void ExitBTN_Click(object sender, RoutedEventArgs e)
        {
            MainWindow MW = new MainWindow();
            MW.Show();
            this.Close();
        }
    }

    public class UserRow
    {
        public int Id { get; set; }
        public string Login { get; set; }
        public string Rol { get; set; }
        public bool Aktivnost { get; set; }
        public DateTime? DataSozdaniya { get; set; }
        public string RolRu
        {
            get
            {
                if (Rol == "admin") return "Администратор";
                if (Rol == "teacher") return "Учитель";
                if (Rol == "student") return "Ученик";
                return Rol;
            }
        }
        public string StatusText { get { return Aktivnost ? "Активен" : "Заблокирован"; } }
    }

    public class GruppaRow
    {
        public int Id { get; set; }
        public int NomerKlassa { get; set; }
        public string BukvaKlassa { get; set; }
    }

    public class DiscRow
    {
        public int Id { get; set; }
        public string Nazvanie { get; set; }
        public string Opisanie { get; set; }
    }

    public class PrepRow
    {
        public int Id { get; set; }
        public int PolzovatelId { get; set; }
        public string Fio { get; set; }
        public string Login { get; set; }
    }

    public class KursRow
    {
        public int Id { get; set; }
        public string Nazvanie { get; set; }
        public string DiscName { get; set; }
        public string PrepName { get; set; }
        public DateTime? DataSozdaniya { get; set; }
    }
}