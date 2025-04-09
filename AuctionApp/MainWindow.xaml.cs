using System;
using System.Collections.ObjectModel; 
using System.ComponentModel; 
using System.Configuration; 
using System.Data.SqlClient; 
using System.Windows; 
using System.Windows.Controls; 
using System.Windows.Threading;

/*

namespace AuctionApp
{
    // Clasa principală a ferestrei, care implementează INotifyPropertyChanged pentru binding
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // Lista de produse afișată în UI, folosită cu binding
        private ObservableCollection<Product> products = new ObservableCollection<Product>();

        // Temporizator pentru actualizarea timpului rămas al licitațiilor
        private DispatcherTimer timer;

        // Utilizatorul curent logat (null dacă nu este logat)
        private User currentUser = null;

        // String-ul de conectare la baza de date, preluat din App.config
        private string connectionString = ConfigurationManager.ConnectionStrings["AuctionDBConnection"].ConnectionString;

        // Variabilă privată pentru starea de administrator
        private bool _isAdmin;

        // Proprietate publică pentru a verifica dacă utilizatorul este admin, cu notificare la modificare
        public bool IsAdmin
        {
            get => _isAdmin;
            set
            {
                _isAdmin = value;
                OnPropertyChanged(nameof(IsAdmin)); // Notifică UI-ul despre schimbarea stării
            }
        }

        // Constructorul ferestrei principale
        public MainWindow()
        {
            InitializeComponent(); // Inițializează componentele definite în XAML
            DataContext = this; // Setează DataContext-ul ferestrei la instanța curentă
            ProductsListView.ItemsSource = products; // Leagă lista de produse la ListView din XAML
            LoadProducts(); // Încarcă produsele inițiale din baza de date

            // Configurează temporizatorul pentru a rula la fiecare secundă
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += Timer_Tick; // Leagă evenimentul Tick la metodă
            timer.Start(); // Pornește temporizatorul

            // Oprește temporizatorul când fereastra se închide pentru a evita blocarea procesului
            Closed += (s, e) => timer.Stop();
        }

        // Metodă apelată la fiecare secundă de temporizator
        [Obsolete]
        private void Timer_Tick(object sender, EventArgs e)
        {
            foreach (var product in products)
            {
                if (product.IsActive) // Verifică doar produsele active
                {
                    var timeRemaining = product.AuctionEndTime - DateTime.Now; // Calculează timpul rămas
                    product.TimeRemaining = timeRemaining.TotalSeconds > 0 ? timeRemaining.ToString(@"mm\:ss") : "Expirat"; // Actualizează afișarea

                    if (timeRemaining.TotalSeconds <= 0) // Dacă licitația a expirat
                    {
                        product.IsActive = false; // Marchează produsul ca inactiv
                        using (var conn = new SqlConnection(connectionString))
                        {
                            conn.Open();
                            // Actualizează starea în baza de date și preia numele câștigătorului
                            var cmd = new SqlCommand(
                                "UPDATE Products SET IsActive = 0 WHERE ProductId = @ProductId;" +
                                "SELECT Username FROM Users WHERE UserId = @LastBidderId", conn);
                            cmd.Parameters.AddWithValue("@ProductId", product.ProductId);
                            cmd.Parameters.AddWithValue("@LastBidderId", product.LastBidderId ?? (object)DBNull.Value);
                            string winner = null;
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read() && !reader.IsDBNull(0))
                                {
                                    winner = reader.GetString(0); // Preia numele câștigătorului
                                }
                            }
                            if (winner != null)
                            {
                                // Afișează mesajul cu câștigătorul pe thread-ul UI
                                Dispatcher.Invoke(() => MessageBox.Show($"Licitația pentru {product.Name} s-a încheiat! Câștigător: {winner}"));
                            }
                        }
                    }
                }
            }
        }

        // Încarcă produsele din baza de date și le adaugă în lista products
        [Obsolete]
        private void LoadProducts()
        {
            products.Clear(); // Golește lista existentă
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                // Interogare SQL pentru a prelua toate produsele și ultimul ofertant
                var cmd = new SqlCommand(
                    "SELECT p.ProductId, p.Name, p.InitialPrice, p.CurrentPrice, p.AuctionStartTime, p.AuctionEndTime, p.LastBidderId, p.IsActive, u.Username " +
                    "FROM Products p LEFT JOIN Users u ON p.LastBidderId = u.UserId", conn);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var product = new Product
                        {
                            ProductId = reader.GetInt32(0), // ID-ul produsului
                            Name = reader.GetString(1), // Numele produsului
                            InitialPrice = reader.GetDecimal(2), // Prețul inițial
                            CurrentPrice = reader.GetDecimal(3), // Prețul curent
                            AuctionStartTime = reader.GetDateTime(4), // Data de start a licitației
                            AuctionEndTime = reader.GetDateTime(5), // Data de sfârșit a licitației
                            LastBidderId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6), // ID-ul ultimului ofertant (dacă există)
                            IsActive = reader.GetBoolean(7), // Starea licitației (activă/inactivă)
                            LastBidderUsername = reader.IsDBNull(8) ? "N/A" : reader.GetString(8), // Numele ultimului ofertant
                            CanBid = currentUser != null && currentUser.Role == "User" && reader.GetBoolean(7) // Poate oferta doar utilizatorul obișnuit logat
                        };
                        var timeRemaining = product.AuctionEndTime - DateTime.Now;
                        product.TimeRemaining = timeRemaining.TotalSeconds > 0 ? timeRemaining.ToString(@"mm\:ss") : "Expirat"; // Setează timpul rămas
                        products.Add(product); // Adaugă produsul în listă
                    }
                }
            }
        }

        // Eveniment pentru butonul de login
        [Obsolete]
        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                // Verifică autentificarea utilizatorului
                var cmd = new SqlCommand("SELECT UserId, Role, Username FROM Users WHERE Username = @Username AND Password = @Password", conn);
                cmd.Parameters.AddWithValue("@Username", UsernameTextBox.Text);
                cmd.Parameters.AddWithValue("@Password", PasswordTextBox.Text);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        currentUser = new User
                        {
                            UserId = reader.GetInt32(0), // ID-ul utilizatorului
                            Role = reader.GetString(1), // Rolul (Admin/User)
                            Username = reader.GetString(2) // Numele utilizatorului
                        };
                        UserStatusTextBlock.Text = $"Logat ca: {UsernameTextBox.Text} ({currentUser.Role})"; // Actualizează starea în UI
                        IsAdmin = currentUser.Role == "Admin"; // Setează starea de admin
                        LoadProducts(); // Reîncarcă produsele cu permisiuni actualizate
                    }
                    else
                    {
                        MessageBox.Show("Autentificare eșuată!"); // Mesaj de eroare dacă autentificarea eșuează
                    }
                }
            }
        }

        // Eveniment pentru butonul de logout
        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            currentUser = null; // Resetează utilizatorul curent
            UserStatusTextBlock.Text = "Neautentificat"; // Actualizează starea în UI
            IsAdmin = false; // Resetează starea de admin
            LoadProducts(); // Reîncarcă produsele fără permisiuni de utilizator
        }

        // Eveniment pentru butonul de plasare a unei oferte
        [Obsolete]
        private void PlaceBidButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser == null || currentUser.Role != "User") return; // Verifică dacă utilizatorul este logat și obișnuit

            var button = (Button)sender;
            var product = (Product)button.DataContext; // Preia produsul asociat butonului

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                // Verifică dacă licitația este activă
                var checkCmd = new SqlCommand("SELECT IsActive, AuctionEndTime FROM Products WHERE ProductId = @ProductId", conn);
                checkCmd.Parameters.AddWithValue("@ProductId", product.ProductId);
                using (var reader = checkCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        bool isActive = reader.GetBoolean(0);
                        DateTime endTime = reader.GetDateTime(1);
                        if (!isActive || endTime < DateTime.Now)
                        {
                            MessageBox.Show("Licitația s-a încheiat!"); // Mesaj dacă licitația nu mai este valabilă
                            LoadProducts();
                            return;
                        }
                    }
                }

                // Actualizează prețul, ultimul ofertant și timpul rămas, apoi inserează oferta în tabelul Bids
                var cmd = new SqlCommand(
                    "UPDATE Products SET CurrentPrice = CurrentPrice + 1, LastBidderId = @UserId, AuctionEndTime = @NewEndTime WHERE ProductId = @ProductId;" +
                    "INSERT INTO Bids (ProductId, UserId, BidAmount, BidTime) VALUES (@ProductId, @UserId, @BidAmount, @BidTime);", conn);
                cmd.Parameters.AddWithValue("@UserId", currentUser.UserId);
                cmd.Parameters.AddWithValue("@ProductId", product.ProductId);
                cmd.Parameters.AddWithValue("@NewEndTime", DateTime.Now.AddMinutes(2)); // Resetează timpul la 2 minute
                cmd.Parameters.AddWithValue("@BidAmount", product.CurrentPrice + 1); // Crește prețul cu 1
                cmd.Parameters.AddWithValue("@BidTime", DateTime.Now); // Data ofertei
                cmd.ExecuteNonQuery();

                product.LastBidderUsername = currentUser.Username; // Actualizează local ultimul ofertant
            }
            LoadProducts(); // Reîncarcă lista pentru a reflecta modificările
        }

        // Eveniment pentru butonul de adăugare a unui produs
        [Obsolete]
        private void AddProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser == null || currentUser.Role != "Admin") return; // Verifică dacă utilizatorul este admin

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                // Inserează un nou produs în baza de date
                var cmd = new SqlCommand(
                    "INSERT INTO Products (Name, InitialPrice, CurrentPrice, AuctionStartTime, AuctionEndTime) " +
                    "VALUES (@Name, @Price, @Price, @StartTime, @EndTime)", conn);
                cmd.Parameters.AddWithValue("@Name", NewProductNameTextBox.Text); // Numele introdus de admin
                cmd.Parameters.AddWithValue("@Price", decimal.Parse(NewProductPriceTextBox.Text)); // Prețul introdus
                cmd.Parameters.AddWithValue("@StartTime", DateTime.Now); // Data curentă ca start
                cmd.Parameters.AddWithValue("@EndTime", DateTime.Now.AddMinutes(2)); // Sfârșit peste 2 minute
                cmd.ExecuteNonQuery();
            }
            LoadProducts(); // Reîncarcă lista cu noul produs
        }

        // Eveniment pentru butonul de ștergere a unui produs
        [Obsolete]
        private void DeleteProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser == null || currentUser.Role != "Admin") return; // Verifică dacă utilizatorul este admin

            var selectedProduct = (Product)ProductsListView.SelectedItem; // Preia produsul selectat din ListView
            if (selectedProduct == null)
            {
                MessageBox.Show("Selectează un produs pentru a-l șterge!"); // Mesaj dacă nu este selectat niciun produs
                return;
            }

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                // Șterge mai întâi ofertele asociate produsului pentru a respecta integritatea referențială
                var deleteBidsCmd = new SqlCommand("DELETE FROM Bids WHERE ProductId = @ProductId", conn);
                deleteBidsCmd.Parameters.AddWithValue("@ProductId", selectedProduct.ProductId);
                deleteBidsCmd.ExecuteNonQuery();

                // Șterge produsul din tabelul Products
                var deleteProductCmd = new SqlCommand("DELETE FROM Products WHERE ProductId = @ProductId", conn);
                deleteProductCmd.Parameters.AddWithValue("@ProductId", selectedProduct.ProductId);
                deleteProductCmd.ExecuteNonQuery();
            }
            LoadProducts(); // Reîncarcă lista fără produsul șters
        }

        // Eveniment pentru notificarea schimbărilor de proprietăți către UI
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Clasa care definește un produs, cu suport pentru notificări de modificare
    public class Product : INotifyPropertyChanged
    {
        public int ProductId { get; set; } // ID-ul unic al produsului
        public string Name { get; set; } // Numele produsului
        public decimal InitialPrice { get; set; } // Prețul inițial al produsului
        public decimal CurrentPrice { get; set; } // Prețul curent al licitației
        public DateTime AuctionStartTime { get; set; } // Data de start a licitației
        public DateTime AuctionEndTime { get; set; } // Data de sfârșit a licitației
        public int? LastBidderId { get; set; } // ID-ul ultimului ofertant (null dacă nu există)

        private string _lastBidderUsername; // Numele ultimului ofertant
        public string LastBidderUsername
        {
            get => _lastBidderUsername;
            set
            {
                _lastBidderUsername = value;
                OnPropertyChanged(nameof(LastBidderUsername)); // Notifică UI-ul
            }
        }

        private string _timeRemaining; // Timpul rămas până la expirarea licitației
        public string TimeRemaining
        {
            get => _timeRemaining;
            set
            {
                _timeRemaining = value;
                OnPropertyChanged(nameof(TimeRemaining)); // Notifică UI-ul
            }
        }

        private bool _isActive; // Starea licitației (activă sau inactivă)
        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                OnPropertyChanged(nameof(IsActive)); // Notifică UI-ul
                OnPropertyChanged(nameof(CanBid)); // Actualizează starea butonului de ofertă
                OnPropertyChanged(nameof(AuctionStatus)); // Actualizează starea afișată
            }
        }

        public bool CanBid { get; set; } // Indică dacă utilizatorul poate plasa o ofertă

        // Proprietate calculată care afișează starea licitației
        public string AuctionStatus => IsActive ? "Activă" : (LastBidderId.HasValue ? $"Câștigată de {LastBidderUsername}" : "Expirată fără ofertă");

        // Eveniment pentru notificarea schimbărilor de proprietăți
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Clasa care definește un utilizator
    public class User
    {
        public int UserId { get; set; } // ID-ul unic al utilizatorului
        public string Role { get; set; } // Rolul utilizatorului (Admin/User)
        public string Username { get; set; } // Numele utilizatorului
    }
}

*/