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
    public partial class StudentWindow : Window
    {
        private int currentUserId;
        private string currentFio;
        private int uchenikId = 0;

        public StudentWindow(int id, string fio)
        {
            InitializeComponent();
            currentUserId = id;
            currentFio = fio;
            InfoLBL.Text = "Ученик: " + fio;
            LoadUchenikId();
            LoadAllKurs();
            LoadMyKurs();
            LoadOcenki();
        }

        private void LoadUchenikId()
        {
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand cmd = new SqlCommand("SELECT [uchenik_id] FROM [23_09Ucheniki] WHERE [polzovatel_id]=@p", conn);
            cmd.Parameters.AddWithValue("@p", currentUserId);
            object res = cmd.ExecuteScalar();
            if (res != null) uchenikId = Convert.ToInt32(res);
            conn.Close();
        }

        private void LoadAllKurs()
        {
            List<KursStudentRow> list = new List<KursStudentRow>();
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            string sql = "SELECT k.[kurs_id], k.[nazvanie], k.[opisanie], d.[nazvanie] AS DiscName, p.[fio] AS PrepName FROM [23_09Kursy] k LEFT JOIN [23_09Discipliny] d ON k.[disciplina_id]=d.[disciplina_id] LEFT JOIN [23_09Prepodavateli] p ON k.[prepodavatel_id]=p.[prepodavatel_id] ORDER BY k.[kurs_id] DESC";
            SqlCommand cmd = new SqlCommand(sql, conn);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                KursStudentRow row = new KursStudentRow();
                row.Id = Convert.ToInt32(reader["kurs_id"]);
                row.Nazvanie = reader["nazvanie"].ToString();
                row.Opisanie = reader["opisanie"] == DBNull.Value ? "" : reader["opisanie"].ToString();
                row.DiscName = reader["DiscName"] == DBNull.Value ? "" : reader["DiscName"].ToString();
                row.PrepName = reader["PrepName"] == DBNull.Value ? "" : reader["PrepName"].ToString();
                list.Add(row);
            }
            reader.Close();
            conn.Close();
            KursGRD.ItemsSource = list;
        }

        private void LoadMyKurs()
        {
            List<KursStudentRow> list = new List<KursStudentRow>();
            if (uchenikId == 0) { MyKursGRD.ItemsSource = list; return; }
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            string sql = "SELECT k.[kurs_id], k.[nazvanie], k.[opisanie], k.[put_k_izobrazheniyu], d.[nazvanie] AS DiscName, p.[fio] AS PrepName FROM [23_09ZapisNaKursy] z INNER JOIN [23_09Kursy] k ON z.[kurs_id]=k.[kurs_id] LEFT JOIN [23_09Discipliny] d ON k.[disciplina_id]=d.[disciplina_id] LEFT JOIN [23_09Prepodavateli] p ON k.[prepodavatel_id]=p.[prepodavatel_id] WHERE z.[uchenik_id]=@u ORDER BY k.[kurs_id] DESC";
            SqlCommand cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@u", uchenikId);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                KursStudentRow row = new KursStudentRow();
                row.Id = Convert.ToInt32(reader["kurs_id"]);
                row.Nazvanie = reader["nazvanie"].ToString();
                row.Opisanie = reader["opisanie"] == DBNull.Value ? "" : reader["opisanie"].ToString();
                row.ImagePath = reader["put_k_izobrazheniyu"] == DBNull.Value ? "" : reader["put_k_izobrazheniyu"].ToString();
                row.DiscName = reader["DiscName"] == DBNull.Value ? "" : reader["DiscName"].ToString();
                row.PrepName = reader["PrepName"] == DBNull.Value ? "" : reader["PrepName"].ToString();
                list.Add(row);
            }
            reader.Close();
            conn.Close();
            MyKursGRD.ItemsSource = list;
        }

        private void KursGRD_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void ZapisBTN_Click(object sender, RoutedEventArgs e)
        {
            if (uchenikId == 0) { MessageBox.Show("Вы не привязаны к группе"); return; }
            KursStudentRow row = KursGRD.SelectedItem as KursStudentRow;
            if (row == null) { MessageBox.Show("Выберите курс"); return; }
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand check = new SqlCommand("SELECT COUNT(*) FROM [23_09ZapisNaKursy] WHERE [kurs_id]=@k AND [uchenik_id]=@u", conn);
            check.Parameters.AddWithValue("@k", row.Id);
            check.Parameters.AddWithValue("@u", uchenikId);
            if (Convert.ToInt32(check.ExecuteScalar()) > 0)
            {
                conn.Close();
                MessageBox.Show("Вы уже записаны на этот курс");
                return;
            }
            SqlCommand cmd = new SqlCommand("INSERT INTO [23_09ZapisNaKursy] ([kurs_id], [uchenik_id]) VALUES (@k, @u)", conn);
            cmd.Parameters.AddWithValue("@k", row.Id);
            cmd.Parameters.AddWithValue("@u", uchenikId);
            cmd.ExecuteNonQuery();
            conn.Close();
            MessageBox.Show("Вы записаны на курс");
            LoadMyKurs();
        }

        private void MyKursGRD_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            KursStudentRow row = MyKursGRD.SelectedItem as KursStudentRow;
            if (row == null) { ZadGRD.ItemsSource = null; KursImage.Source = null; return; }

            if (!string.IsNullOrEmpty(row.ImagePath) && System.IO.File.Exists(row.ImagePath))
            {
                try
                {
                    BitmapImage bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(row.ImagePath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    KursImage.Source = bmp;
                }
                catch { KursImage.Source = null; }
            }
            else { KursImage.Source = null; }

            List<ZadanieRow> list = new List<ZadanieRow>();
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            SqlCommand cmd = new SqlCommand("SELECT [zadanie_id], [nazvanie], [opisanie], [srok_sdachi], [maks_ball] FROM [23_09Zadaniya] WHERE [kurs_id]=@k ORDER BY [srok_sdachi]", conn);
            cmd.Parameters.AddWithValue("@k", row.Id);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ZadanieRow z = new ZadanieRow();
                z.Id = Convert.ToInt32(reader["zadanie_id"]);
                z.Nazvanie = reader["nazvanie"].ToString();
                z.Opisanie = reader["opisanie"] == DBNull.Value ? "" : reader["opisanie"].ToString();
                z.SrokSdachi = reader["srok_sdachi"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(reader["srok_sdachi"]);
                z.MaksBall = reader["maks_ball"] == DBNull.Value ? 0 : Convert.ToInt32(reader["maks_ball"]);
                list.Add(z);
            }
            reader.Close();
            conn.Close();
            ZadGRD.ItemsSource = list;
        }

        private void SendOtvetBTN_Click(object sender, RoutedEventArgs e)
        {
            if (uchenikId == 0) { MessageBox.Show("Вы не привязаны к группе"); return; }
            ZadanieRow zad = ZadGRD.SelectedItem as ZadanieRow;
            if (zad == null) { MessageBox.Show("Выберите задание"); return; }
            string otvet = OtvetBOX.Text.Trim();
            if (otvet == "") { MessageBox.Show("Введите ответ"); return; }

            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();

            SqlCommand check = new SqlCommand("SELECT COUNT(*) FROM [23_09Vypolneniya] WHERE [zadanie_id]=@z AND [uchenik_id]=@u", conn);
            check.Parameters.AddWithValue("@z", zad.Id);
            check.Parameters.AddWithValue("@u", uchenikId);
            if (Convert.ToInt32(check.ExecuteScalar()) > 0)
            {
                conn.Close();
                MessageBox.Show("Вы уже отправляли ответ на это задание");
                return;
            }

            SqlCommand cmd = new SqlCommand("INSERT INTO [23_09Vypolneniya] ([zadanie_id], [uchenik_id], [otvet], [data_sdachi], [status]) VALUES (@z, @u, @o, GETDATE(), @s)", conn);
            cmd.Parameters.AddWithValue("@z", zad.Id);
            cmd.Parameters.AddWithValue("@u", uchenikId);
            cmd.Parameters.AddWithValue("@o", otvet);
            cmd.Parameters.AddWithValue("@s", WorkStatus.Otpravleno);
            cmd.ExecuteNonQuery();
            conn.Close();
            OtvetBOX.Text = "";
            MessageBox.Show("Ответ отправлен на проверку");
            LoadOcenki();
        }

        private void LoadOcenki()
        {
            List<OcenkaRow> list = new List<OcenkaRow>();
            if (uchenikId == 0) { OcenkiGRD.ItemsSource = list; return; }
            SqlConnection conn = new SqlConnection(MainWindow.ConnectionString);
            conn.Open();
            string sql = "SELECT v.[vypolnenie_id], v.[status], k.[nazvanie] AS KursName, z.[nazvanie] AS ZadName, o.[ball] FROM [23_09Vypolneniya] v INNER JOIN [23_09Zadaniya] z ON v.[zadanie_id]=z.[zadanie_id] INNER JOIN [23_09Kursy] k ON z.[kurs_id]=k.[kurs_id] LEFT JOIN [23_09Ocenki] o ON o.[vypolnenie_id]=v.[vypolnenie_id] WHERE v.[uchenik_id]=@u ORDER BY v.[vypolnenie_id] DESC";
            SqlCommand cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@u", uchenikId);
            SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                OcenkaRow row = new OcenkaRow();
                row.Id = Convert.ToInt32(reader["vypolnenie_id"]);
                row.Status = reader["status"].ToString();
                row.KursName = reader["KursName"].ToString();
                row.ZadName = reader["ZadName"].ToString();
                row.Ball = reader["ball"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["ball"]);
                list.Add(row);
            }
            reader.Close();
            conn.Close();
            OcenkiGRD.ItemsSource = list;
        }

        private void ExitBTN_Click(object sender, RoutedEventArgs e)
        {
            MainWindow MW = new MainWindow();
            MW.Show();
            this.Close();
        }
    }

    public class KursStudentRow
    {
        public int Id { get; set; }
        public string Nazvanie { get; set; }
        public string Opisanie { get; set; }
        public string ImagePath { get; set; }
        public string DiscName { get; set; }
        public string PrepName { get; set; }
    }

    public class ZadanieRow
    {
        public int Id { get; set; }
        public string Nazvanie { get; set; }
        public string Opisanie { get; set; }
        public DateTime? SrokSdachi { get; set; }
        public int MaksBall { get; set; }
    }

    public class OcenkaRow
    {
        public int Id { get; set; }
        public string KursName { get; set; }
        public string ZadName { get; set; }
        public int? Ball { get; set; }
        public string Status { get; set; }
        public string StatusRu { get { return WorkStatus.ToRu(Status); } }
    }
}