using System;
using System.Collections.ObjectModel; 
using System.ComponentModel; 
using System.Configuration; 
using System.Data.SqlClient; 
using System.Windows; 
using System.Windows.Controls; 
using System.Windows.Threading;

namespace AuctionApp
{
    // Main window class that implements INotifyPropertyChanged for data binding
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // List of products displayed in the UI, used with binding
        private ObservableCollection<Product> products = new ObservableCollection<Product>();

        // Timer for updating the remaining time of auctions
        private DispatcherTimer timer;

        // Currently logged-in user (null if not logged in)
        private User currentUser = null;

        // Database connection string, retrieved from App.config
        private string connectionString = ConfigurationManager.ConnectionStrings["AuctionDBConnection"].ConnectionString;

        // Private variable for admin status
        private bool _isAdmin;

        // Public property to check if the user is an admin, with change notification
        public bool IsAdmin
        {
            get => _isAdmin;
            set
            {
                _isAdmin = value;
                OnPropertyChanged(nameof(IsAdmin)); // Notify UI about the change
            }
        }

        // Constructor for the main window
        public MainWindow()
        {
            InitializeComponent(); // Initialize components defined in XAML
            DataContext = this; // Set the window's DataContext to this instance
            ProductsListView.ItemsSource = products; // Bind the product list to the ListView in XAML
            LoadProducts(); // Load initial products from the database

            // Configure the timer to run every second
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += Timer_Tick; // Bind Tick event to method
            timer.Start(); // Start the timer

            // Stop the timer when the window closes to avoid process hang
            Closed += (s, e) => timer.Stop();
        }

        // Method called every second by the timer
        [Obsolete]
        private void Timer_Tick(object sender, EventArgs e)
        {
            foreach (var product in products)
            {
                if (product.IsActive) // Only check active products
                {
                    var timeRemaining = product.AuctionEndTime - DateTime.Now; // Calculate remaining time
                    product.TimeRemaining = timeRemaining.TotalSeconds > 0 ? timeRemaining.ToString(@"mm\:ss") : "Expired"; // Update display

                    if (timeRemaining.TotalSeconds <= 0) // If auction expired
                    {
                        product.IsActive = false; // Mark product as inactive
                        using (var conn = new SqlConnection(connectionString))
                        {
                            conn.Open();
                            // Update status in DB and retrieve winner's name
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
                                    winner = reader.GetString(0); // Get winner's name
                                }
                            }
                            if (winner != null)
                            {
                                // Show winner message on UI thread
                                Dispatcher.Invoke(() => MessageBox.Show($"Auction for {product.Name} has ended! Winner: {winner}"));
                            }
                        }
                    }
                }
            }
        }

        // Load products from the database and add them to the 'products' list
        [Obsolete]
        private void LoadProducts()
        {
            products.Clear(); // Clear the existing list
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                // SQL query to fetch all products and last bidder info
                var cmd = new SqlCommand(
                    "SELECT p.ProductId, p.Name, p.InitialPrice, p.CurrentPrice, p.AuctionStartTime, p.AuctionEndTime, p.LastBidderId, p.IsActive, u.Username " +
                    "FROM Products p LEFT JOIN Users u ON p.LastBidderId = u.UserId", conn);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var product = new Product
                        {
                            ProductId = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            InitialPrice = reader.GetDecimal(2),
                            CurrentPrice = reader.GetDecimal(3),
                            AuctionStartTime = reader.GetDateTime(4),
                            AuctionEndTime = reader.GetDateTime(5),
                            LastBidderId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                            IsActive = reader.GetBoolean(7),
                            LastBidderUsername = reader.IsDBNull(8) ? "N/A" : reader.GetString(8),
                            CanBid = currentUser != null && currentUser.Role == "User" && reader.GetBoolean(7)
                        };
                        var timeRemaining = product.AuctionEndTime - DateTime.Now;
                        product.TimeRemaining = timeRemaining.TotalSeconds > 0 ? timeRemaining.ToString(@"mm\:ss") : "Expired";
                        products.Add(product);
                    }
                }
            }
        }

        // Event for login button
        [Obsolete]
        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                // Authenticate user
                var cmd = new SqlCommand("SELECT UserId, Role, Username FROM Users WHERE Username = @Username AND Password = @Password", conn);
                cmd.Parameters.AddWithValue("@Username", UsernameTextBox.Text);
                cmd.Parameters.AddWithValue("@Password", PasswordTextBox.Text);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        currentUser = new User
                        {
                            UserId = reader.GetInt32(0),
                            Role = reader.GetString(1),
                            Username = reader.GetString(2)
                        };
                        UserStatusTextBlock.Text = $"Logged in as: {UsernameTextBox.Text} ({currentUser.Role})";
                        IsAdmin = currentUser.Role == "Admin";
                        LoadProducts();
                    }
                    else
                    {
                        MessageBox.Show("Login failed!");
                    }
                }
            }
        }

        // Event for logout button
        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            currentUser = null;
            UserStatusTextBlock.Text = "Not logged in";
            IsAdmin = false;
            LoadProducts();
        }

        // Event for placing a bid
        [Obsolete]
        private void PlaceBidButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser == null || currentUser.Role != "User") return;

            var button = (Button)sender;
            var product = (Product)button.DataContext;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Check if auction is still active
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
                            MessageBox.Show("The auction has ended!");
                            LoadProducts();
                            return;
                        }
                    }
                }

                // Update product and insert bid
                var cmd = new SqlCommand(
                    "UPDATE Products SET CurrentPrice = CurrentPrice + 1, LastBidderId = @UserId, AuctionEndTime = @NewEndTime WHERE ProductId = @ProductId;" +
                    "INSERT INTO Bids (ProductId, UserId, BidAmount, BidTime) VALUES (@ProductId, @UserId, @BidAmount, @BidTime);", conn);
                cmd.Parameters.AddWithValue("@UserId", currentUser.UserId);
                cmd.Parameters.AddWithValue("@ProductId", product.ProductId);
                cmd.Parameters.AddWithValue("@NewEndTime", DateTime.Now.AddMinutes(2));
                cmd.Parameters.AddWithValue("@BidAmount", product.CurrentPrice + 1);
                cmd.Parameters.AddWithValue("@BidTime", DateTime.Now);
                cmd.ExecuteNonQuery();

                product.LastBidderUsername = currentUser.Username;
            }
            LoadProducts();
        }

        // Event for adding a product
        [Obsolete]
        private void AddProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser == null || currentUser.Role != "Admin") return;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(
                    "INSERT INTO Products (Name, InitialPrice, CurrentPrice, AuctionStartTime, AuctionEndTime) " +
                    "VALUES (@Name, @Price, @Price, @StartTime, @EndTime)", conn);
                cmd.Parameters.AddWithValue("@Name", NewProductNameTextBox.Text);
                cmd.Parameters.AddWithValue("@Price", decimal.Parse(NewProductPriceTextBox.Text));
                cmd.Parameters.AddWithValue("@StartTime", DateTime.Now);
                cmd.Parameters.AddWithValue("@EndTime", DateTime.Now.AddMinutes(2));
                cmd.ExecuteNonQuery();
            }
            LoadProducts();
        }

        // Event for deleting a product
        [Obsolete]
        private void DeleteProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser == null || currentUser.Role != "Admin") return;

            var selectedProduct = (Product)ProductsListView.SelectedItem;
            if (selectedProduct == null)
            {
                MessageBox.Show("Please select a product to delete!");
                return;
            }

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                var deleteBidsCmd = new SqlCommand("DELETE FROM Bids WHERE ProductId = @ProductId", conn);
                deleteBidsCmd.Parameters.AddWithValue("@ProductId", selectedProduct.ProductId);
                deleteBidsCmd.ExecuteNonQuery();

                var deleteProductCmd = new SqlCommand("DELETE FROM Products WHERE ProductId = @ProductId", conn);
                deleteProductCmd.Parameters.AddWithValue("@ProductId", selectedProduct.ProductId);
                deleteProductCmd.ExecuteNonQuery();
            }
            LoadProducts();
        }

        // Event for notifying UI about property changes
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Class defining a product with property change notification support
    public class Product : INotifyPropertyChanged
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public decimal InitialPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public DateTime AuctionStartTime { get; set; }
        public DateTime AuctionEndTime { get; set; }
        public int? LastBidderId { get; set; }

        private string _lastBidderUsername;
        public string LastBidderUsername
        {
            get => _lastBidderUsername;
            set
            {
                _lastBidderUsername = value;
                OnPropertyChanged(nameof(LastBidderUsername));
            }
        }

        private string _timeRemaining;
        public string TimeRemaining
        {
            get => _timeRemaining;
            set
            {
                _timeRemaining = value;
                OnPropertyChanged(nameof(TimeRemaining));
            }
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(CanBid));
                OnPropertyChanged(nameof(AuctionStatus));
            }
        }

        public bool CanBid { get; set; }

        // Computed property displaying auction status
        public string AuctionStatus => IsActive ? "Active" : (LastBidderId.HasValue ? $"Won by {LastBidderUsername}" : "Expired without bid");

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Class defining a user
    public class User
    {
        public int UserId { get; set; }
        public string Role { get; set; }
        public string Username { get; set; }
    }
}
