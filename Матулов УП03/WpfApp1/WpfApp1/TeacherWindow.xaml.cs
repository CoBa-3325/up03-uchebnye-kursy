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
    public partial class TeacherWindow : Window
    {
        private int currentUserId;
        private string currentFio;
        private int prepodavatelId = 0;

        public TeacherWindow(int id, string fio)
        {
            InitializeComponent();
            currentUserId = id;
            currentFio = fio;
            InfoLBL.Text = "Учитель: " + fio;
            LoadPrepodavatelId();
            LoadMyKurs();
            LoadDiscCombo();
            LoadZadKursCombo();
            LoadVedKursCombo();
            LoadZad();
            LoadCheck();
        }

        /// <summary>Загружает справочник дисциплин в выпадающий список формы курса.</summary>
        private void LoadDiscCombo()
        {
            KursDiscCMB.Items.Clear();
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
            conn.Close();
            if (KursDiscCMB.Items.Count > 0) KursDiscCMB.SelectedIndex = 0;
        }

        /// <summary>Выбор файла изображения для курса.</summary>
        private void ChooseImageBTN_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Изображения (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";
            if (dlg.ShowDialog() == true)
            {
                KursImageBOX.Text = dlg.FileName;
            }
        }

        /// <summary>
        /// Создаёт учебный курс, закреплённый за текущим преподавателем.
        /// </summary>
        private void AddKursBTN_Click(object sender, RoutedEventArgs e)
        {
            if (prepodavatelId == 0) { MessageBox.Show("Учётная запись преподавателя не найдена"); return; }

            string nazv = KursNazvBOX.Text.Trim();
            string opis = KursOpisBOX.Text.Trim();
            string image = KursImageBOX.Text.Trim();

            if (nazv == "") { MessageBox.Show("Введите название курса"); return; }
            if (KursDiscCMB.SelectedItem == null) { MessageBox.Show("Выберите дисциплину"); return; }

            int discId = int.Parse(((ComboBoxItem)KursDiscCMB.SelectedItem).Tag.ToString());

            try
            {
                using (SqlConnection conn = new SqlConnection(MainWindow.ConnectionString))
                {
                    conn.Open();
                    SqlCommand cmd = new SqlCommand("INSERT INTO [23_09Kursy] ([disciplina_id], [prepodavatel_id], [nazvanie], [opisanie], [put_k_izobrazheniyu]) VALUES (@d, @p, @n, @o, @i)", conn);
                    cmd.Parameters.AddWithValue("@d", discId);
                    cmd.Parameters.AddWithValue("@p", prepodavatelId);
                    cmd.Parameters.AddWithValue("@n", nazv);
                    cmd.Parameters.AddWithValue("@o", opis);
                    cmd.Parameters.AddWithValue("@i", image);
                    cmd.ExecuteNonQuery();
                }

                KursNazvBOX.Text = "";
                KursOpisBOX.Text = "";
                KursImageBOX.Text = "";

                LoadMyKurs();
                LoadZadKursCombo();
                LoadVedKursCombo();
                MessageBox.Show("Курс создан");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        private void LoadPrepodavatelId()
        {
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand cmd = new SqlCommand("SELECT [prepodavatel_id] FROM [23_09Prepodavateli] WHERE [polzovatel_id]=@p", conn);
            cmd.Parameters.AddWithValue("@p", currentUserId);
            object res = cmd.ExecuteScalar();
            if (res != null) prepodavatelId = Convert.ToInt32(res);
            conn.Close();
        }

        private void LoadMyKurs()
        {
            List<MyKursRow> list = new List<MyKursRow>();
            if (prepodavatelId == 0) { MyKursGRD.ItemsSource = list; return; }
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            string sql = "SELECT k.[kurs_id], k.[nazvanie], k.[opisanie], d.[nazvanie] AS DiscName FROM [23_09Kursy] k LEFT JOIN [23_09Discipliny] d ON k.[disciplina_id]=d.[disciplina_id] WHERE k.[prepodavatel_id]=@p ORDER BY k.[kurs_id] DESC";
            SqlCommand cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@p", prepodavatelId);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                MyKursRow row = new MyKursRow();
                row.Id = Convert.ToInt32(reader["kurs_id"]);
                row.Nazvanie = reader["nazvanie"].ToString();
                row.Opisanie = reader["opisanie"] == DBNull.Value ? "" : reader["opisanie"].ToString();
                row.DiscName = reader["DiscName"] == DBNull.Value ? "" : reader["DiscName"].ToString();
                list.Add(row);
            }
            reader.Close();
            conn.Close();
            MyKursGRD.ItemsSource = list;
        }

        private void LoadZadKursCombo()
        {
            ZadKursCMB.Items.Clear();
            if (prepodavatelId == 0) return;
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand cmd = new SqlCommand("SELECT [kurs_id], [nazvanie] FROM [23_09Kursy] WHERE [prepodavatel_id]=@p", conn);
            cmd.Parameters.AddWithValue("@p", prepodavatelId);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = reader["nazvanie"].ToString();
                item.Tag = reader["kurs_id"].ToString();
                ZadKursCMB.Items.Add(item);
            }
            reader.Close();
            conn.Close();
            if (ZadKursCMB.Items.Count > 0) ZadKursCMB.SelectedIndex = 0;
        }

        private void LoadVedKursCombo()
        {
            VedKursCMB.Items.Clear();
            if (prepodavatelId == 0) return;
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand cmd = new SqlCommand("SELECT [kurs_id], [nazvanie] FROM [23_09Kursy] WHERE [prepodavatel_id]=@p", conn);
            cmd.Parameters.AddWithValue("@p", prepodavatelId);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = reader["nazvanie"].ToString();
                item.Tag = reader["kurs_id"].ToString();
                VedKursCMB.Items.Add(item);
            }
            reader.Close();
            conn.Close();
            if (VedKursCMB.Items.Count > 0) VedKursCMB.SelectedIndex = 0;
        }

        private void LoadZad()
        {
            List<ZadRow> list = new List<ZadRow>();
            if (prepodavatelId == 0) { ZadGRD.ItemsSource = list; return; }
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            string sql = "SELECT z.[zadanie_id], z.[nazvanie], z.[srok_sdachi], z.[maks_ball], k.[nazvanie] AS KursName FROM [23_09Zadaniya] z INNER JOIN [23_09Kursy] k ON z.[kurs_id]=k.[kurs_id] WHERE k.[prepodavatel_id]=@p ORDER BY z.[zadanie_id] DESC";
            SqlCommand cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@p", prepodavatelId);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ZadRow row = new ZadRow();
                row.Id = Convert.ToInt32(reader["zadanie_id"]);
                row.Nazvanie = reader["nazvanie"].ToString();
                row.KursName = reader["KursName"].ToString();
                row.SrokSdachi = reader["srok_sdachi"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["srok_sdachi"]);
                row.MaksBall = Convert.ToInt32(reader["maks_ball"]);
                list.Add(row);
            }
            reader.Close();
            conn.Close();
            ZadGRD.ItemsSource = list;
        }

        private void AddZadBTN_Click(object sender, RoutedEventArgs e)
        {
            if (ZadKursCMB.SelectedItem == null) { MessageBox.Show("Выберите курс"); return; }
            string nazv = ZadNazvBOX.Text.Trim();
            string opis = ZadOpisBOX.Text.Trim();
            if (nazv == "" || opis == "")
            {
                MessageBox.Show("Введите название и описание");
                return;
            }
            if (!ZadDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Выберите срок сдачи");
                return;
            }
            int ball;
            if (!int.TryParse(ZadBallBOX.Text.Trim(), out ball) || ball <= 0)
            {
                MessageBox.Show("Некорректный макс. балл");
                return;
            }
            int kursId = int.Parse(((ComboBoxItem)ZadKursCMB.SelectedItem).Tag.ToString());
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand cmd = new SqlCommand("INSERT INTO [23_09Zadaniya] ([kurs_id], [nazvanie], [opisanie], [srok_sdachi], [maks_ball]) VALUES (@k, @n, @o, @s, @b)", conn);
            cmd.Parameters.AddWithValue("@k", kursId);
            cmd.Parameters.AddWithValue("@n", nazv);
            cmd.Parameters.AddWithValue("@o", opis);
            cmd.Parameters.AddWithValue("@s", ZadDatePicker.SelectedDate.Value);
            cmd.Parameters.AddWithValue("@b", ball);
            cmd.ExecuteNonQuery();
            conn.Close();
            ZadNazvBOX.Text = "";
            ZadOpisBOX.Text = "";
            ZadBallBOX.Text = "100";
            LoadZad();
            MessageBox.Show("Задание создано");
        }

        private void LoadCheck()
        {
            List<CheckRow> list = new List<CheckRow>();
            if (prepodavatelId == 0) { CheckGRD.ItemsSource = list; return; }
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            string sql = "SELECT v.[vypolnenie_id], v.[otvet], v.[data_sdachi], v.[status], z.[nazvanie] AS ZadName, z.[maks_ball], k.[nazvanie] AS KursName, u.[fio] AS UchenikName FROM [23_09Vypolneniya] v INNER JOIN [23_09Zadaniya] z ON v.[zadanie_id]=z.[zadanie_id] INNER JOIN [23_09Kursy] k ON z.[kurs_id]=k.[kurs_id] INNER JOIN [23_09Ucheniki] u ON v.[uchenik_id]=u.[uchenik_id] WHERE k.[prepodavatel_id]=@p AND v.[status]=@s ORDER BY v.[data_sdachi]";
            SqlCommand cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@p", prepodavatelId);
            cmd.Parameters.AddWithValue("@s", WorkStatus.Otpravleno);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                CheckRow row = new CheckRow();
                row.Id = Convert.ToInt32(reader["vypolnenie_id"]);
                row.Otvet = reader["otvet"] == DBNull.Value ? "" : reader["otvet"].ToString();
                row.DataSdachi = reader["data_sdachi"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["data_sdachi"]);
                row.Status = reader["status"].ToString();
                row.ZadName = reader["ZadName"].ToString();
                row.KursName = reader["KursName"].ToString();
                row.UchenikName = reader["UchenikName"].ToString();
                row.MaksBall = reader["maks_ball"] == DBNull.Value ? 0 : Convert.ToInt32(reader["maks_ball"]);
                list.Add(row);
            }
            reader.Close();
            conn.Close();
            CheckGRD.ItemsSource = list;
        }

        private void SaveBallBTN_Click(object sender, RoutedEventArgs e)
        {
            CheckRow row = CheckGRD.SelectedItem as CheckRow;
            if (row == null) { MessageBox.Show("Выберите работу"); return; }
            int ball;
            if (!int.TryParse(BallBOX.Text.Trim(), out ball))
            {
                MessageBox.Show("Введите балл числом");
                return;
            }
            // Ограничения CHECK в таблице 23_09Ocenki: [ball] >= 0
            if (ball < 0)
            {
                MessageBox.Show("Балл не может быть отрицательным");
                return;
            }
            if (row.MaksBall > 0 && ball > row.MaksBall)
            {
                MessageBox.Show("Балл не может превышать максимальный балл по заданию (" + row.MaksBall + ")");
                return;
            }

            string comment = CommentBOX.Text.Trim();

            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();

            SqlCommand chk = new SqlCommand("SELECT COUNT(*) FROM [23_09Ocenki] WHERE [vypolnenie_id]=@v", conn);
            chk.Parameters.AddWithValue("@v", row.Id);
            if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
            {
                SqlCommand upd0 = new SqlCommand("UPDATE [23_09Ocenki] SET [ball]=@b, [kommentariy]=@k WHERE [vypolnenie_id]=@v", conn);
                upd0.Parameters.AddWithValue("@b", ball);
                upd0.Parameters.AddWithValue("@k", comment);
                upd0.Parameters.AddWithValue("@v", row.Id);
                upd0.ExecuteNonQuery();
            }
            else
            {
                SqlCommand ins = new SqlCommand("INSERT INTO [23_09Ocenki] ([vypolnenie_id], [ball], [kommentariy]) VALUES (@v, @b, @k)", conn);
                ins.Parameters.AddWithValue("@v", row.Id);
                ins.Parameters.AddWithValue("@b", ball);
                ins.Parameters.AddWithValue("@k", comment);
                ins.ExecuteNonQuery();
            }

            // Статус соответствует ограничению CHECK таблицы 23_09Vypolneniya
            SqlCommand upd = new SqlCommand("UPDATE [23_09Vypolneniya] SET [status]=@s WHERE [vypolnenie_id]=@v", conn);
            upd.Parameters.AddWithValue("@s", WorkStatus.Provereno);
            upd.Parameters.AddWithValue("@v", row.Id);
            upd.ExecuteNonQuery();

            conn.Close();
            BallBOX.Text = "";
            CommentBOX.Text = "";
            LoadCheck();
            LoadVed();
            MessageBox.Show("Оценка сохранена");
        }

        private void VedKursCMB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadVed();
        }

        private void LoadVed()
        {
            List<VedRow> list = new List<VedRow>();
            if (VedKursCMB.SelectedItem == null) { VedGRD.ItemsSource = list; return; }
            int kursId = int.Parse(((ComboBoxItem)VedKursCMB.SelectedItem).Tag.ToString());
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            string sql = "SELECT u.[fio] AS UchenikName, z.[nazvanie] AS ZadName, o.[ball], o.[kommentariy], v.[status] FROM [23_09Vypolneniya] v INNER JOIN [23_09Ucheniki] u ON v.[uchenik_id]=u.[uchenik_id] INNER JOIN [23_09Zadaniya] z ON v.[zadanie_id]=z.[zadanie_id] LEFT JOIN [23_09Ocenki] o ON o.[vypolnenie_id]=v.[vypolnenie_id] WHERE z.[kurs_id]=@k ORDER BY u.[fio], z.[nazvanie]";
            SqlCommand cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@k", kursId);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                VedRow row = new VedRow();
                row.UchenikName = reader["UchenikName"].ToString();
                row.ZadName = reader["ZadName"].ToString();
                row.Ball = reader["ball"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["ball"]);
                row.Kommentariy = reader["kommentariy"] == DBNull.Value ? "" : reader["kommentariy"].ToString();
                row.Status = reader["status"].ToString();
                list.Add(row);
            }
            reader.Close();
            conn.Close();
            VedGRD.ItemsSource = list;
        }

        private void PrintVedBTN_Click(object sender, RoutedEventArgs e)
        {
            if (VedGRD.ItemsSource == null) { MessageBox.Show("Нет данных"); return; }
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = "Текстовый файл (*.txt)|*.txt";
            dlg.FileName = "vedomost.txt";
            if (dlg.ShowDialog() != true) return;
            try
            {
                using (System.IO.StreamWriter sw = new System.IO.StreamWriter(dlg.FileName, false, Encoding.UTF8))
                {
                    sw.WriteLine("ВЕДОМОСТЬ");
                    sw.WriteLine("Учитель: " + currentFio);
                    sw.WriteLine("Дата: " + DateTime.Now.ToString("dd.MM.yyyy"));
                    sw.WriteLine("-------------------------------------------");
                    sw.WriteLine("Ученик\tЗадание\tБалл\tСтатус\tКомментарий");
                    foreach (VedRow r in VedGRD.ItemsSource)
                    {
                        sw.WriteLine(r.UchenikName + "\t" + r.ZadName + "\t" +
                                     (r.Ball.HasValue ? r.Ball.Value.ToString() : "-") + "\t" +
                                     r.StatusRu + "\t" + r.Kommentariy);
                    }
                }
                MessageBox.Show("Ведомость сохранена: " + dlg.FileName);
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }

        private void ExitBTN_Click(object sender, RoutedEventArgs e)
        {
            MainWindow MW = new MainWindow();
            MW.Show();
            this.Close();
        }
    }

    public class MyKursRow
    {
        public int Id { get; set; }
        public string Nazvanie { get; set; }
        public string Opisanie { get; set; }
        public string DiscName { get; set; }
    }

    public class ZadRow
    {
        public int Id { get; set; }
        public string Nazvanie { get; set; }
        public string KursName { get; set; }
        public DateTime? SrokSdachi { get; set; }
        public int MaksBall { get; set; }
    }

    public class CheckRow
    {
        public int Id { get; set; }
        public string ZadName { get; set; }
        public string KursName { get; set; }
        public string UchenikName { get; set; }
        public string Otvet { get; set; }
        public DateTime? DataSdachi { get; set; }
        public string Status { get; set; }
        public int MaksBall { get; set; }
        public string StatusRu { get { return WorkStatus.ToRu(Status); } }
    }

    public class VedRow
    {
        public string UchenikName { get; set; }
        public string ZadName { get; set; }
        public int? Ball { get; set; }
        public string Kommentariy { get; set; }
        public string Status { get; set; }
        public string StatusRu { get { return WorkStatus.ToRu(Status); } }
    }
}